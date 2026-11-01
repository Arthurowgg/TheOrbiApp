import { useState } from "react";
import { Badge, Button, Icon, Modal, Panel, Switch } from "../ui";
import { PageHeader, type PageProps } from "./AccountShell";
import { formatDate, removeDevice, renameDevice, setSync, type Device, type SyncPrefs } from "../../lib/accounts";
import { LOCAL_ONLY, SYNC_ITEMS } from "../../data/site";

/**
 * Computadores que entraram na conta. Não existe código de pareamento nem
 * "vincular": o app entra com o mesmo e-mail e senha deste site, e aparece
 * aqui sozinho na primeira sincronização.
 */
export default function Devices({ account, reload, notify, onDownload }: PageProps) {
  const [editing, setEditing] = useState<{ id: string; value: string } | null>(null);
  const [removing, setRemoving] = useState<Device | null>(null);

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
      notify(`${removing.name} saiu da lista.`);
    } catch (error) {
      notify(error instanceof Error ? error.message : "Não foi possível remover.");
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
        title="Computadores"
        description="Cada PC onde você entra com esta conta aparece aqui. É a mesma conta do site — sem código, sem vínculo."
        actions={
          <Button size="sm" variant="secondary" onClick={onDownload}>
            <Icon name="download" className="h-3.5 w-3.5" />
            Instalar em outro PC
          </Button>
        }
      />

      <div className="grid gap-5 xl:grid-cols-[1.15fr_1fr]">
        <div className="space-y-5">
          <Panel title="Como um PC entra" padded={false}>
            <ol className="divide-y divide-rule">
              {[
                {
                  title: "Instale o Orbi naquele computador",
                  body: "O mesmo instalador, sem administrador. A barra abre e o app fica na bandeja.",
                },
                {
                  title: "Entre com este e-mail e senha",
                  body: "No app: Ajustes ▸ Conta ▸ Entrar. Nada de código para copiar de uma tela para outra.",
                },
                {
                  title: "A conta se atualiza sozinha",
                  body: "O computador entra nesta lista na primeira sincronização — e os seus dados chegam junto.",
                },
              ].map((step, index) => (
                <li key={step.title} className="flex items-start gap-4 px-5 py-4">
                  <span className="mt-0.5 grid h-6 w-6 shrink-0 place-items-center bg-ink font-mono text-[10.5px] text-paper">
                    {index + 1}
                  </span>
                  <div className="min-w-0">
                    <p className="text-[14px] font-medium text-ink">{step.title}</p>
                    <p className="mt-0.5 text-[12.5px] leading-snug text-warm">{step.body}</p>
                  </div>
                </li>
              ))}
            </ol>
          </Panel>

          <Panel title="Na sua conta" padded={false}>
            {account.devices.length === 0 ? (
              <div className="px-5 py-9 text-center">
                <span className="mx-auto grid h-11 w-11 place-items-center border border-dashed border-warm text-warm">
                  <Icon name="laptop" className="h-5 w-5" />
                </span>
                <p className="mt-3 text-[14px] font-medium text-ink">Nenhum computador ainda</p>
                <p className="mx-auto mt-1 max-w-[38ch] text-[12.5px] leading-snug text-warm">
                  Quando o app entrar com esta conta, ele aparece aqui com o nome do PC e a data da
                  primeira sincronização.
                </p>
                <Button size="sm" className="mt-4" onClick={onDownload}>
                  Baixar o instalador
                </Button>
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
                            aria-label="Novo nome do computador"
                            className="h-9 min-w-0 flex-1 border border-ink bg-[#fbf9f3] px-3 text-[13.5px] outline-none"
                          />
                          <Button size="sm" type="submit">
                            Salvar
                          </Button>
                          <button
                            type="button"
                            onClick={() => setEditing(null)}
                            className="text-[12.5px] text-warm hover:text-ink"
                          >
                            Cancelar
                          </button>
                        </form>
                      ) : (
                        <>
                          <div className="flex flex-wrap items-center gap-2">
                            <p className="truncate text-[14px] font-medium text-ink">{device.name}</p>
                            <Badge tone="ok">Na conta</Badge>
                          </div>
                          <p className="mt-0.5 font-mono text-[11px] text-warm">
                            {device.os} · desde {formatDate(device.linkedAt)}
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
                          aria-label={`Remover ${device.name}`}
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
          <Panel title="O que sincroniza" description="Vale para todos os computadores da conta.">
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

          <Panel title="O que fica só no PC">
            <p className="text-[12.5px] leading-snug text-warm">
              Estes itens nunca saem do computador, nem com a conta aberta:
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
        title={`Remover ${removing?.name ?? "este computador"}?`}
        description="O Orbi continua instalado e funcionando no PC; ele só deixa de sincronizar com a sua conta."
      >
        <div>
          <p className="text-[13.5px] leading-[1.6] text-ink-2">
            Ele volta para a lista quando você entrar na conta de novo por lá.
          </p>
          <div className="mt-6 flex justify-end gap-2.5">
            <Button variant="ghost" onClick={() => setRemoving(null)}>
              Cancelar
            </Button>
            <Button variant="danger" onClick={confirmRemove}>
              Remover
            </Button>
          </div>
        </div>
      </Modal>
    </>
  );
}
