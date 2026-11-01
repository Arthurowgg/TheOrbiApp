import { useState, type FormEvent } from "react";
import { Badge, Button, Icon, Modal, Panel, Switch, TextField } from "../ui";
import { PageHeader, type PageProps } from "./AccountShell";
import {
  AuthError,
  formatDate,
  formatPairingInput,
  generateSampleCode,
  linkDevice,
  removeDevice,
  renameDevice,
  setSync,
  type Device,
  type SyncPrefs,
} from "../../lib/accounts";
import { LOCAL_ONLY, SYNC_ITEMS } from "../../data/site";

export default function Devices({ account, reload, notify, onDownload }: PageProps) {
  const [code, setCode] = useState("");
  const [deviceName, setDeviceName] = useState("");
  const [codeError, setCodeError] = useState("");
  const [editing, setEditing] = useState<{ id: string; value: string } | null>(null);
  const [removing, setRemoving] = useState<Device | null>(null);

  const link = async (event: FormEvent) => {
    event.preventDefault();
    setCodeError("");
    try {
      await linkDevice(`ORBI-${code}`, deviceName);
      await reload();
      setCode("");
      setDeviceName("");
      notify("App vinculado à sua conta.");
    } catch (error) {
      setCodeError(error instanceof AuthError ? error.message : "Não foi possível vincular.");
    }
  };

  const saveName = async () => {
    if (!editing) return;
    try {
      await renameDevice(editing.id, editing.value);
      await reload();
      setEditing(null);
    } catch (error) {
      notify(error instanceof Error ? error.message : "Não foi possível renomear.");
    }
  };

  const confirmRemove = async () => {
    if (!removing) return;
    try {
      await removeDevice(removing.id);
      await reload();
      notify(`${removing.name} foi desvinculado.`);
    } catch (error) {
      notify(error instanceof Error ? error.message : "Não foi possível desvincular.");
    }
    setRemoving(null);
  };

  const toggleSync = async (key: keyof SyncPrefs, value: boolean) => {
    try {
      await setSync({ ...account.sync, [key]: value });
      await reload();
    } catch (error) {
      notify(error instanceof Error ? error.message : "Não foi possível salvar.");
    }
  };

  return (
    <>
      <PageHeader
        title="Dispositivos"
        description="Vincule o app instalado no Windows e escolha o que acompanha a sua conta."
      />

      <div className="grid gap-5 xl:grid-cols-[1.15fr_1fr]">
        <div className="space-y-5">
          <Panel
            title="Vincular o app"
            description="Leva menos de um minuto, com o Orbi aberto no seu PC."
          >
            <ol className="mb-5 space-y-3">
              {[
                "Abra o Orbi e vá em Configurações ▸ Conta ▸ Vincular ao site.",
                "O app mostra um código no formato ORBI-XXXX-XXXX.",
                "Digite o código aqui e dê um nome ao computador.",
              ].map((text, index) => (
                <li key={text} className="flex items-start gap-3">
                  <span className="grid h-5 w-5 shrink-0 place-items-center bg-ink font-mono text-[10px] text-paper">
                    {index + 1}
                  </span>
                  <span className="text-[13.5px] leading-snug text-ink-2">{text}</span>
                </li>
              ))}
            </ol>

            <form onSubmit={link} className="space-y-4 border-t border-rule pt-5" noValidate>
              <div>
                <div className="mb-1.5 flex items-baseline justify-between gap-3">
                  <label htmlFor="pairing" className="label text-ink">
                    Código do app
                  </label>
                  <button
                    type="button"
                    onClick={() => {
                      setCode(formatPairingInput(generateSampleCode()));
                      setCodeError("");
                    }}
                    className="font-mono text-[10px] text-signal underline decoration-rule underline-offset-2 hover:decoration-signal"
                  >
                    Usar código de exemplo
                  </button>
                </div>
                <div
                  className={`flex h-12 items-stretch border bg-[#fbf9f3] transition-colors focus-within:border-ink ${
                    codeError ? "border-signal" : "border-rule"
                  }`}
                >
                  <span className="flex items-center border-r border-rule bg-paper-2/70 px-3.5 font-mono text-[14px] text-warm">
                    ORBI-
                  </span>
                  <input
                    id="pairing"
                    value={code}
                    onChange={(event) => {
                      setCode(formatPairingInput(event.target.value));
                      setCodeError("");
                    }}
                    autoComplete="off"
                    spellCheck={false}
                    placeholder="XXXX-XXXX"
                    aria-invalid={Boolean(codeError)}
                    aria-describedby={codeError ? "pairing-error" : undefined}
                    className="min-w-0 flex-1 bg-transparent px-3.5 font-mono text-[16px] tracking-[0.14em] text-ink outline-none placeholder:text-warm/40"
                  />
                </div>
                {codeError && (
                  <p id="pairing-error" role="alert" className="mt-1.5 text-[11.5px] text-signal">
                    {codeError}
                  </p>
                )}
              </div>

              <TextField
                id="device-name"
                label="Nome do computador"
                value={deviceName}
                onChange={(event) => setDeviceName(event.target.value)}
                placeholder="Ex.: PC do escritório"
                maxLength={32}
              />

              <div className="flex flex-wrap items-center gap-3">
                <Button type="submit" disabled={code.length < 9}>
                  <Icon name="link" className="h-3.5 w-3.5" />
                  Vincular
                </Button>
                <button
                  type="button"
                  onClick={onDownload}
                  className="text-[13px] text-warm underline decoration-rule underline-offset-2 hover:text-ink"
                >
                  Ainda não instalei o app
                </button>
              </div>
            </form>

            <p className="mt-5 flex items-start gap-2 border-t border-rule pt-4 text-[11.5px] leading-snug text-warm">
              <Icon name="info" className="mt-[1px] h-3.5 w-3.5 shrink-0" />
              Nesta versão do site, a conta e os vínculos ficam salvos neste navegador. O código tem um
              dígito verificador, então erros de digitação são recusados.
            </p>
          </Panel>

          <Panel title="Aparelhos vinculados" padded={false}>
            {account.devices.length === 0 ? (
              <div className="px-5 py-8 text-center">
                <span className="mx-auto grid h-11 w-11 place-items-center border border-dashed border-warm text-warm">
                  <Icon name="laptop" className="h-5 w-5" />
                </span>
                <p className="mt-3 text-[14px] font-medium text-ink">Nenhum aparelho ainda</p>
                <p className="mt-1 text-[12.5px] text-warm">Quando você vincular o app, ele aparece aqui.</p>
              </div>
            ) : (
              <ul className="divide-y divide-rule">
                {account.devices.map((device) => (
                  <li key={device.id} className="flex flex-wrap items-center gap-4 px-5 py-4">
                    <span className="grid h-10 w-10 shrink-0 place-items-center bg-ink text-paper">
                      <Icon name="laptop" className="h-[18px] w-[18px]" />
                    </span>

                    <div className="min-w-0 flex-1">
                      {editing?.id === device.id ? (
                        <form
                          onSubmit={(event) => {
                            event.preventDefault();
                            saveName();
                          }}
                          className="flex items-center gap-2"
                        >
                          <input
                            autoFocus
                            value={editing.value}
                            onChange={(event) => setEditing({ id: device.id, value: event.target.value })}
                            maxLength={32}
                            aria-label="Novo nome do aparelho"
                            className="h-9 min-w-0 flex-1 border border-ink bg-[#fbf9f3] px-3 text-[13.5px] outline-none"
                          />
                          <Button size="sm" type="submit">
                            Salvar
                          </Button>
                          <button type="button" onClick={() => setEditing(null)} className="text-[12.5px] text-warm hover:text-ink">
                            Cancelar
                          </button>
                        </form>
                      ) : (
                        <>
                          <div className="flex flex-wrap items-center gap-2">
                            <p className="truncate text-[14px] font-medium text-ink">{device.name}</p>
                            <Badge tone="ok">Vinculado</Badge>
                          </div>
                          <p className="mt-0.5 font-mono text-[11px] text-warm">
                            {device.os} · desde {formatDate(device.linkedAt)} · código …{device.code.slice(-4)}
                          </p>
                        </>
                      )}
                    </div>

                    {editing?.id !== device.id && (
                      <div className="flex items-center gap-1">
                        <button
                          type="button"
                          onClick={() => setEditing({ id: device.id, value: device.name })}
                          className="grid h-9 w-9 place-items-center text-warm transition-colors hover:bg-ink/[0.06] hover:text-ink"
                          aria-label={`Renomear ${device.name}`}
                        >
                          <Icon name="edit" className="h-4 w-4" />
                        </button>
                        <button
                          type="button"
                          onClick={() => setRemoving(device)}
                          className="grid h-9 w-9 place-items-center text-warm transition-colors hover:bg-signal-soft hover:text-signal-dark"
                          aria-label={`Desvincular ${device.name}`}
                        >
                          <Icon name="trash" className="h-4 w-4" />
                        </button>
                      </div>
                    )}
                  </li>
                ))}
              </ul>
            )}
          </Panel>
        </div>

        <div className="space-y-5">
          <Panel title="O que sincroniza" description="Vale para todos os aparelhos vinculados.">
            <ul className="divide-y divide-rule">
              {SYNC_ITEMS.map((item) => (
                <li key={item.key} className="py-3.5 first:pt-0">
                  <Switch
                    checked={account.sync[item.key]}
                    onChange={(value) => toggleSync(item.key, value)}
                    label={item.label}
                    description={item.hint}
                  />
                </li>
              ))}
            </ul>
          </Panel>

          <Panel title="Só no seu PC">
            <p className="text-[12.5px] leading-snug text-warm">
              Estes itens nunca saem do computador, mesmo com o app vinculado:
            </p>
            <ul className="mt-3 space-y-2">
              {LOCAL_ONLY.map((item) => (
                <li key={item} className="flex items-center gap-2.5 text-[13.5px] text-ink">
                  <Icon name="lock" className="h-3.5 w-3.5 text-warm" />
                  {item}
                </li>
              ))}
            </ul>
          </Panel>
        </div>
      </div>

      <Modal
        open={Boolean(removing)}
        onClose={() => setRemoving(null)}
        title={`Desvincular ${removing?.name ?? "este aparelho"}?`}
        description="O app continua funcionando no PC, mas deixa de sincronizar com a sua conta."
      >
        <div>
          <p className="text-[13.5px] leading-[1.6] text-ink-2">
            Você pode vincular de novo quando quiser, com um novo código gerado no app.
          </p>
          <div className="mt-6 flex justify-end gap-2.5">
            <Button variant="ghost" onClick={() => setRemoving(null)}>
              Cancelar
            </Button>
            <Button variant="danger" onClick={confirmRemove}>
              Desvincular
            </Button>
          </div>
        </div>
      </Modal>
    </>
  );
}
