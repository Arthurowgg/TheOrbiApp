import { Button, Icon, Panel, type IconName } from "../ui";
import { PageHeader, type PageProps } from "./AccountShell";
import { firstName, formatDate, relativeTime, type ActivityType } from "../../lib/accounts";
import { TOOL_COUNT } from "../../data/tools";

const ACTIVITY_ICON: Record<ActivityType, IconName> = {
  conta: "user",
  login: "key",
  download: "download",
  dispositivo: "laptop",
  perfil: "edit",
  seguranca: "shield",
};

export default function Overview({ account, go, onDownload }: PageProps) {
  const last = account.downloads[0];

  return (
    <>
      <PageHeader title={`Olá, ${firstName(account.name)}`} />

      <div className="grid grid-cols-2 border border-rule bg-[#fbf9f3] sm:grid-cols-3">
        {[
          { label: "Computadores", value: String(account.devices.length), onClick: () => go("/conta/computadores") },
          { label: "Downloads", value: String(account.downloads.length), onClick: () => go("/conta/downloads") },
          { label: "Na conta desde", value: formatDate(account.createdAt), onClick: () => go("/conta/perfil") },
        ].map((item, index) => (
          <button
            key={item.label}
            type="button"
            onClick={item.onClick}
            className={`p-4 text-left transition-colors hover:bg-paper-2/60 sm:p-5 ${index > 0 ? "border-l border-rule" : ""}`}
          >
            <p className="label text-warm">{item.label}</p>
            <p className="mt-1.5 truncate text-[19px] font-semibold tracking-tight text-ink sm:text-[22px]">
              {item.value}
            </p>
          </button>
        ))}
      </div>

      <div className="mt-5 grid gap-5 lg:grid-cols-[1.3fr_1fr]">
        <div className="space-y-5">
          <Panel title="Seus dados">
            <ul className="divide-y divide-rule">
              {[
                { label: "Memória", value: account.memoryPrefs.enabled ? "ligada" : "desligada", path: "/conta/memoria" },
                { label: "Computadores", value: String(account.devices.length), path: "/conta/computadores" },
                { label: "Downloads", value: String(account.downloads.length), path: "/conta/downloads" },
              ].map((row) => (
                <li key={row.label}>
                  <button
                    type="button"
                    onClick={() => go(row.path)}
                    className="group flex w-full items-center justify-between gap-3 py-2.5 text-left"
                  >
                    <span className="text-[13.5px] text-ink">{row.label}</span>
                    <span className="flex items-center gap-2 font-mono text-[11.5px] text-warm">
                      {row.value}
                      <Icon name="chevron-right" className="h-3 w-3 group-hover:text-signal" />
                    </span>
                  </button>
                </li>
              ))}
            </ul>
          </Panel>

          <Panel title="Atividade" padded={false}>
            {account.activity.length === 0 ? (
              <p className="p-5 text-[13px] text-warm">Nada por aqui ainda.</p>
            ) : (
              <ul className="divide-y divide-rule">
                {account.activity.slice(0, 6).map((item) => (
                  <li key={item.id} className="flex items-center gap-3.5 px-5 py-3">
                    <span className="grid h-8 w-8 shrink-0 place-items-center border border-rule text-ink-2">
                      <Icon name={ACTIVITY_ICON[item.type]} className="h-3.5 w-3.5" />
                    </span>
                    <p className="min-w-0 flex-1 truncate text-[13.5px] text-ink">{item.text}</p>
                    <span className="shrink-0 font-mono text-[11px] text-warm">{relativeTime(item.at)}</span>
                  </li>
                ))}
              </ul>
            )}
          </Panel>
        </div>

        <div className="space-y-5">
          <Panel title="Instalador">
            <p className="text-[13px] leading-relaxed text-ink-2">
              {TOOL_COUNT} ferramentas do Windows na barra. Leve — o app é baixado na instalação.
            </p>
            <Button size="sm" className="mt-3" onClick={onDownload}>
              <Icon name="download" className="h-3.5 w-3.5" />
              Baixar
            </Button>
            {last && (
              <p className="mt-3 font-mono text-[10.5px] text-warm">
                último download em {formatDate(last.at)}
              </p>
            )}
          </Panel>

          <Panel title="Atalhos">
            <ul className="-my-1 divide-y divide-rule">
              {[
                { label: "Editar perfil", path: "/conta/perfil", icon: "edit" as const },
                { label: "Alterar senha", path: "/conta/seguranca", icon: "key" as const },
                { label: "Histórico de downloads", path: "/conta/downloads", icon: "history" as const },
              ].map((item) => (
                <li key={item.label}>
                  <button
                    type="button"
                    onClick={() => go(item.path)}
                    className="group flex w-full items-center gap-3 py-2.5 text-left text-[13.5px] text-ink"
                  >
                    <Icon name={item.icon} className="h-4 w-4 text-warm group-hover:text-signal" />
                    <span className="flex-1">{item.label}</span>
                    <Icon name="chevron-right" className="h-3.5 w-3.5 text-warm" />
                  </button>
                </li>
              ))}
            </ul>
          </Panel>
        </div>
      </div>
    </>
  );
}
