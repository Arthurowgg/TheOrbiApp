import { useState, type FormEvent } from "react";
import { Badge, Button, Icon, Modal, Panel, PasswordField, Spinner, StrengthMeter } from "../ui";
import { PageHeader, type PageProps } from "./AccountShell";
import {
  MIN_PASSWORD,
  changePassword,
  deleteAccount,
  exportAccount,
  formatDate,
  resendVerification,
  sendResetEmail,
} from "../../lib/accounts";
import { saveBlob } from "../../lib/download";

export default function Security({ account, reload, notify, onLogout, go }: PageProps) {
  const [current, setCurrent] = useState("");
  const [next, setNext] = useState("");
  const [errors, setErrors] = useState<{ current?: string; next?: string }>({});
  const [busy, setBusy] = useState(false);

  const [deleteOpen, setDeleteOpen] = useState(false);
  const [deletePassword, setDeletePassword] = useState("");
  const [deleteError, setDeleteError] = useState("");
  const [sending, setSending] = useState(false);

  const submitPassword = async (event: FormEvent) => {
    event.preventDefault();
    const nextErrors: typeof errors = {};
    if (!current) nextErrors.current = "Digite a senha atual.";
    if (next.length < MIN_PASSWORD) nextErrors.next = `Use pelo menos ${MIN_PASSWORD} caracteres.`;
    setErrors(nextErrors);
    if (Object.keys(nextErrors).length) return;

    setBusy(true);
    try {
      await changePassword(current, next);
      await reload();
      setCurrent("");
      setNext("");
      setErrors({});
      notify("Senha alterada. Use a nova senha na próxima entrada.");
    } catch (error) {
      const message = error instanceof Error ? error.message : "Não foi possível alterar a senha.";
      if (/atual/i.test(message)) setErrors({ current: message });
      else setErrors({ next: message });
    } finally {
      setBusy(false);
    }
  };

  const sendReset = async () => {
    setSending(true);
    try {
      await sendResetEmail(account.email);
      notify(`Link de redefinição enviado para ${account.email}.`);
    } catch (error) {
      notify(error instanceof Error ? error.message : "Não foi possível enviar o link.");
    } finally {
      setSending(false);
    }
  };

  const verify = async () => {
    setSending(true);
    try {
      await resendVerification();
      notify("Enviamos um novo link de verificação para o seu e-mail.");
    } catch (error) {
      notify(error instanceof Error ? error.message : "Não foi possível enviar o link.");
    } finally {
      setSending(false);
    }
  };

  const exportData = () => {
    saveBlob(
      new Blob([exportAccount(account)], { type: "application/json" }),
      `orbi-dados-${account.id}.json`,
    );
    notify("Seus dados foram exportados.");
  };

  const confirmDelete = async (event: FormEvent) => {
    event.preventDefault();
    setDeleteError("");
    setBusy(true);
    try {
      await deleteAccount(deletePassword);
      setDeleteOpen(false);
      go("/");
      window.setTimeout(() => window.location.reload(), 80);
    } catch (error) {
      setDeleteError(error instanceof Error ? error.message : "Não foi possível excluir a conta.");
    } finally {
      setBusy(false);
    }
  };

  return (
    <>
      <PageHeader
        title="Segurança"
        description="Senha, verificação de e-mail e os dados da sua conta — tudo no Firebase."
      />

      <div className="grid gap-5 xl:grid-cols-[1.1fr_1fr]">
        <div className="space-y-5">
          <Panel
            title="Alterar senha"
            description="Pedimos a senha atual antes de qualquer troca. O Firebase exige login recente."
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
              <div>
                <PasswordField
                  id="new-password"
                  label="Nova senha"
                  autoComplete="new-password"
                  value={next}
                  onChange={(event) => setNext(event.target.value)}
                  error={errors.next}
                />
                <StrengthMeter password={next} />
              </div>
              <Button type="submit" disabled={busy}>
                {busy ? <Spinner className="h-3.5 w-3.5" /> : <Icon name="key" className="h-3.5 w-3.5" />}
                Atualizar senha
              </Button>
            </form>
          </Panel>

          <Panel
            title="Verificação de e-mail"
            description="Confirma que o endereço é seu. Não bloqueia o uso do app."
            action={
              account.emailVerified ? <Badge tone="ok">verificado</Badge> : <Badge tone="signal">pendente</Badge>
            }
          >
            <p className="text-[13px] leading-[1.6] text-ink-2">
              {account.emailVerified
                ? `O endereço ${account.email} está verificado. Você já pode receber links de redefinição de senha normalmente.`
                : `Enviamos um link de confirmação para ${account.email}. Abra o e-mail e clique em “Verificar”. Depois volte aqui e recarregue a página.`}
            </p>
            {!account.emailVerified && (
              <Button variant="secondary" className="mt-4" onClick={verify} disabled={sending}>
                {sending ? <Spinner className="h-3.5 w-3.5" /> : <Icon name="mail" className="h-3.5 w-3.5" />}
                Reenviar verificação
              </Button>
            )}
          </Panel>

          <Panel
            title="Redefinição de senha por e-mail"
            description="Sem códigos para guardar: o Firebase envia um link seguro, válido por uma hora."
          >
            <p className="text-[13px] leading-[1.6] text-ink-2">
              Se você esquecer a senha, o link resolve. É mais seguro que um código de recuperação
              porque expira sozinho e só chega na caixa de entrada de {account.email}.
            </p>
            <Button variant="secondary" className="mt-4" onClick={sendReset} disabled={sending}>
              {sending ? <Spinner className="h-3.5 w-3.5" /> : <Icon name="mail" className="h-3.5 w-3.5" />}
              Enviar link de redefinição
            </Button>
          </Panel>
        </div>

        <div className="space-y-5">
          <Panel title="Esta sessão">
            <dl className="space-y-3 text-[13px]">
              <div className="flex items-center justify-between gap-3">
                <dt className="text-warm">Método</dt>
                <dd className="flex items-center gap-1.5 text-ink">
                  <Icon name={account.provider === "google.com" ? "google" : "mail"} className="h-3.5 w-3.5" />
                  {account.provider === "google.com" ? "Google" : "E-mail e senha"}
                </dd>
              </div>
              <div className="flex items-center justify-between gap-3">
                <dt className="text-warm">Sessão</dt>
                <dd className="text-ink">Mantida conectada neste navegador</dd>
              </div>
              <div className="flex items-center justify-between gap-3">
                <dt className="text-warm">Conta desde</dt>
                <dd className="text-ink">{formatDate(account.createdAt)}</dd>
              </div>
              <div className="flex items-center justify-between gap-3">
                <dt className="text-warm">Identificador</dt>
                <dd className="truncate font-mono text-[11px] text-warm">{account.id}</dd>
              </div>
            </dl>
            <Button variant="secondary" className="mt-4" onClick={onLogout}>
              <Icon name="logout" className="h-3.5 w-3.5" />
              Sair desta sessão
            </Button>
          </Panel>

          <Panel title="Seus dados" description="Leve uma cópia do que o Orbi guarda sobre a sua conta.">
            <p className="text-[13px] leading-[1.6] text-ink-2">
              O arquivo inclui perfil, aparelhos, downloads e atividade do Firestore. Senha e tokens
              nunca são incluídos — eles nem passam por este site.
            </p>
            <Button variant="secondary" className="mt-4" onClick={exportData}>
              <Icon name="download" className="h-3.5 w-3.5" />
              Exportar dados
            </Button>
          </Panel>

          <section className="border border-signal/40 bg-signal-soft/30 p-5">
            <h2 className="text-[14px] font-semibold text-signal-dark">Excluir conta</h2>
            <p className="mt-1.5 text-[13px] leading-[1.6] text-ink-2">
              Remove a conta no Firebase Authentication, o documento no Firestore, os aparelhos
              vinculados e o histórico. O app instalado no PC continua funcionando, sem sincronização.
            </p>
            <Button variant="danger" className="mt-4" onClick={() => setDeleteOpen(true)}>
              <Icon name="trash" className="h-3.5 w-3.5" />
              Excluir minha conta
            </Button>
          </section>
        </div>
      </div>

      <Modal
        open={deleteOpen}
        onClose={() => setDeleteOpen(false)}
        title="Excluir a conta de verdade?"
        description="Isso não pode ser desfeito. Confirme com a sua senha para continuar."
      >
        <form onSubmit={confirmDelete} noValidate>
          <PasswordField
            id="delete-password"
            label="Senha"
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
