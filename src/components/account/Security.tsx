import { useState, type FormEvent } from "react";
import {
  Badge,
  Button,
  CopyButton,
  Icon,
  Modal,
  PasswordField,
  Panel,
  Spinner,
} from "../ui";
import { PageHeader, type PageProps } from "./AccountShell";
import {
  AuthError,
  MIN_PASSWORD,
  changePassword,
  deleteAccount,
  exportAccount,
  formatDate,
  regenerateRecovery,
  sessionMode,
} from "../../lib/accounts";
import { saveBlob } from "../../lib/download";

export default function Security({ account, reload, notify, onLogout, go }: PageProps) {
  const [current, setCurrent] = useState("");
  const [next, setNext] = useState("");
  const [errors, setErrors] = useState<{ current?: string; next?: string }>({});
  const [busy, setBusy] = useState(false);

  const [recoveryOpen, setRecoveryOpen] = useState(false);
  const [recoveryPassword, setRecoveryPassword] = useState("");
  const [recoveryError, setRecoveryError] = useState("");
  const [recoveryCode, setRecoveryCode] = useState<string | null>(null);

  const [deleteOpen, setDeleteOpen] = useState(false);
  const [deletePassword, setDeletePassword] = useState("");
  const [deleteError, setDeleteError] = useState("");

  const submitPassword = async (event: FormEvent) => {
    event.preventDefault();
    const nextErrors: typeof errors = {};
    if (!current) nextErrors.current = "Digite a senha atual.";
    if (next.length < MIN_PASSWORD) nextErrors.next = `Use pelo menos ${MIN_PASSWORD} caracteres.`;
    setErrors(nextErrors);
    if (Object.keys(nextErrors).length) return;

    setBusy(true);
    try {
      await changePassword(account.id, current, next);
      reload();
      setCurrent("");
      setNext("");
      notify("Senha alterada.");
    } catch (error) {
      if (error instanceof AuthError && error.code === "wrong_password") {
        setErrors({ current: error.message });
      } else {
        notify(error instanceof Error ? error.message : "Não foi possível alterar a senha.");
      }
    } finally {
      setBusy(false);
    }
  };

  const generateRecovery = async (event: FormEvent) => {
    event.preventDefault();
    setRecoveryError("");
    setBusy(true);
    try {
      const result = await regenerateRecovery(account.id, recoveryPassword);
      setRecoveryCode(result.recoveryCode);
      setRecoveryPassword("");
      reload();
    } catch (error) {
      setRecoveryError(error instanceof Error ? error.message : "Não foi possível gerar o código.");
    } finally {
      setBusy(false);
    }
  };

  const closeRecovery = () => {
    setRecoveryOpen(false);
    setRecoveryCode(null);
    setRecoveryPassword("");
    setRecoveryError("");
  };

  const exportData = () => {
    saveBlob(
      new Blob([exportAccount(account)], { type: "application/json" }),
      `NOVA-dados-${account.id}.json`,
    );
    notify("Seus dados foram exportados.");
  };

  const confirmDelete = async (event: FormEvent) => {
    event.preventDefault();
    setDeleteError("");
    setBusy(true);
    try {
      await deleteAccount(account.id, deletePassword);
      setDeleteOpen(false);
      go("/");
      window.setTimeout(() => window.location.reload(), 60);
    } catch (error) {
      setDeleteError(error instanceof Error ? error.message : "Não foi possível excluir a conta.");
    } finally {
      setBusy(false);
    }
  };

  return (
    <>
      <PageHeader title="Segurança" description="Senha, recuperação de acesso e os dados da sua conta." />

      <div className="grid gap-5 xl:grid-cols-[1.1fr_1fr]">
        <div className="space-y-5">
          <Panel
            title="Alterar senha"
            description={`Última alteração em ${formatDate(account.passwordChangedAt)}.`}
          >
            <form onSubmit={submitPassword} className="space-y-4" noValidate>
              <PasswordField
                id="current-password"
                label="Senha atual"
                autoComplete="current-password"
                value={current}
                onChange={(event) => setCurrent(event.target.value)}
                error={errors.current}
              />
              <PasswordField
                id="new-password"
                label="Nova senha"
                autoComplete="new-password"
                value={next}
                onChange={(event) => setNext(event.target.value)}
                error={errors.next}
                showStrength
              />
              <Button type="submit" disabled={busy}>
                {busy ? <Spinner className="h-3.5 w-3.5" /> : <Icon name="key" className="h-3.5 w-3.5" />}
                Atualizar senha
              </Button>
            </form>
          </Panel>

          <Panel
            title="Código de recuperação"
            description="Permite redefinir a senha se você esquecer. É de uso único."
            action={<Badge tone="ok">Ativo</Badge>}
          >
            <p className="text-[13px] leading-[1.6] text-ink-2">
              Gerado em {formatDate(account.recoveryCreatedAt)}. Por segurança, o código não pode ser
              exibido de novo — se você o perdeu, gere um novo. O anterior deixa de valer.
            </p>
            <Button variant="secondary" className="mt-4" onClick={() => setRecoveryOpen(true)}>
              <Icon name="key" className="h-3.5 w-3.5" />
              Gerar novo código
            </Button>
          </Panel>
        </div>

        <div className="space-y-5">
          <Panel title="Esta sessão">
            <dl className="space-y-3 text-[13px]">
              <div className="flex items-center justify-between gap-3">
                <dt className="text-warm">Dispositivo</dt>
                <dd className="text-ink">Este navegador</dd>
              </div>
              <div className="flex items-center justify-between gap-3">
                <dt className="text-warm">Sessão</dt>
                <dd className="text-ink">{sessionMode() === "persistente" ? "Mantida conectada" : "Até fechar a aba"}</dd>
              </div>
            </dl>
            <Button variant="secondary" className="mt-4" onClick={onLogout}>
              <Icon name="logout" className="h-3.5 w-3.5" />
              Sair desta sessão
            </Button>
          </Panel>

          <Panel title="Seus dados" description="Leve uma cópia do que a NOVA guarda sobre a sua conta.">
            <p className="text-[13px] leading-[1.6] text-ink-2">
              O arquivo inclui perfil, aparelhos, downloads e atividade. Senha e código de recuperação
              nunca são incluídos.
            </p>
            <Button variant="secondary" className="mt-4" onClick={exportData}>
              <Icon name="download" className="h-3.5 w-3.5" />
              Exportar dados
            </Button>
          </Panel>

          <section className="border border-signal/40 bg-signal-soft/30 p-5">
            <h2 className="text-[14px] font-semibold text-signal-dark">Excluir conta</h2>
            <p className="mt-1.5 text-[13px] leading-[1.6] text-ink-2">
              Remove a conta, os aparelhos vinculados e o histórico deste navegador. O app instalado no
              PC continua funcionando, sem sincronização.
            </p>
            <Button variant="danger" className="mt-4" onClick={() => setDeleteOpen(true)}>
              <Icon name="trash" className="h-3.5 w-3.5" />
              Excluir minha conta
            </Button>
          </section>
        </div>
      </div>

      {/* modal: novo código de recuperação */}
      <Modal open={recoveryOpen} onClose={closeRecovery} labelledBy="recovery-title" width="max-w-[460px]">
        <div className="p-6">
          {recoveryCode ? (
            <>
              <h2 id="recovery-title" className="text-[18px] font-semibold text-ink">
                Novo código gerado
              </h2>
              <p className="mt-2 text-[13.5px] leading-[1.6] text-ink-2">
                Guarde agora em um lugar seguro. Ele não aparece de novo.
              </p>
              <p className="mt-4 break-all border border-ink bg-[#fbf9f3] p-4 text-center font-mono text-[22px] font-medium tracking-[0.08em] text-ink">
                {recoveryCode}
              </p>
              <div className="mt-5 flex items-center justify-between gap-3">
                <CopyButton value={recoveryCode} />
                <Button onClick={closeRecovery}>Já guardei</Button>
              </div>
            </>
          ) : (
            <form onSubmit={generateRecovery} noValidate>
              <h2 id="recovery-title" className="text-[18px] font-semibold text-ink">
                Gerar novo código
              </h2>
              <p className="mt-2 text-[13.5px] leading-[1.6] text-ink-2">
                O código atual deixará de funcionar. Confirme com a sua senha.
              </p>
              <PasswordField
                id="recovery-password"
                label="Senha"
                wrapperClassName="mt-4"
                autoComplete="current-password"
                autoFocus
                value={recoveryPassword}
                onChange={(event) => setRecoveryPassword(event.target.value)}
                error={recoveryError}
              />
              <div className="mt-5 flex justify-end gap-2.5">
                <Button variant="ghost" onClick={closeRecovery}>
                  Cancelar
                </Button>
                <Button type="submit" disabled={busy || !recoveryPassword}>
                  {busy && <Spinner className="h-3.5 w-3.5" />}
                  Gerar código
                </Button>
              </div>
            </form>
          )}
        </div>
      </Modal>

      {/* modal: excluir conta */}
      <Modal open={deleteOpen} onClose={() => setDeleteOpen(false)} labelledBy="delete-title" width="max-w-[460px]">
        <form onSubmit={confirmDelete} className="p-6" noValidate>
          <h2 id="delete-title" className="text-[18px] font-semibold text-ink">
            Excluir a conta de verdade?
          </h2>
          <p className="mt-2 text-[13.5px] leading-[1.6] text-ink-2">
            Isso não pode ser desfeito. Confirme com a sua senha para continuar.
          </p>
          <PasswordField
            id="delete-password"
            label="Senha"
            wrapperClassName="mt-4"
            autoComplete="current-password"
            autoFocus
            value={deletePassword}
            onChange={(event) => setDeletePassword(event.target.value)}
            error={deleteError}
          />
          <div className="mt-5 flex justify-end gap-2.5">
            <Button variant="ghost" onClick={() => setDeleteOpen(false)}>
              Cancelar
            </Button>
            <Button type="submit" variant="danger" disabled={busy || !deletePassword}>
              {busy && <Spinner className="h-3.5 w-3.5" />}
              Excluir conta
            </Button>
          </div>
        </form>
      </Modal>
    </>
  );
}
