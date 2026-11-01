import { Badge, Button, Icon, Panel, type IconName } from "../ui";
import { PageHeader, type PageProps } from "./AccountShell";
import { firstName, formatDate, relativeTime, type ActivityType } from "../../lib/accounts";

const ACTIVITY_ICON: Record<ActivityType, IconName> = {
  conta: "user",
  login: "key",
  download: "download",
  dispositivo: "laptop",
  perfil: "edit",
  seguranca: "shield",
};

export default function Overview({ account, go, onDownload }: PageProps) {
  const hasDownload = account.downloads.length > 0;
  const linked = account.devices.length > 0;

  const steps = [
    {
      title: "Criar a conta",
      body: "Feito. Você já tem acesso ao painel.",
      done: true,
    },
    {
      title: "Baixar o instalador",
      body: "Um arquivo só, para Windows 11.",
      done: hasDownload,
      action: { label: "Baixar", run: onDownload },
    },
    {
      title: "Entrar no app",
      body: "Abra o Orbi e entre com esta mesma conta — o que você já salvou aparece lá.",
      done: linked,
      action: { label: "Ver o app", run: () => go("/conta/dispositivos") },
    },
  ];
  const done = steps.filter((step) => step.done).length;
  const next = steps.find((step) => !step.done);

  return (
    <>
      <PageHeader
        title={`Olá, ${firstName(account.name)}`}
        description="Sua memória, seus aparelhos e o que aconteceu na conta."
        actions={
          <>
            <Badge tone="ok">Conta ativa</Badge>
            <Badge tone={linked ? "signal" : "plain"}>{linked ? "App conectado" : "App ainda não conectado"}</Badge>
          </>
        }
      />

      {/* números */}
      <div className="grid grid-cols-3 border border-rule bg-[#fbf9f3]">
        {[
          { label: "Aparelhos", value: String(account.devices.length), onClick: () => go("/conta/dispositivos") },
          { label: "Downloads", value: String(account.downloads.length), onClick: () => go("/conta/downloads") },
          { label: "Conta desde", value: formatDate(account.createdAt), onClick: () => go("/conta/perfil") },
        ].map((item, index) => (
          <button
            key={item.label}
            type="button"
            onClick={item.onClick}
            className={`p-4 text-left transition-colors hover:bg-paper-2/60 sm:p-5 ${index > 0 ? "border-l border-rule" : ""}`}
          >
            <p className="label text-warm">{item.label}</p>
            <p className="mt-1.5 truncate text-[20px] font-semibold tracking-tight text-ink sm:text-[24px]">
              {item.value}
            </p>
          </button>
        ))}
      </div>

      <div className="mt-5 grid gap-5 lg:grid-cols-[1.35fr_1fr]">
        <div className="space-y-5">
          <Panel
            title="Primeiros passos"
            description={next ? "Faltam poucos passos para usar o Orbi com a sua conta." : "Tudo pronto. O Orbi já está ligada à sua conta."}
            action={<span className="font-mono text-[11px] text-warm">{done} de 3</span>}
          >
            <div className="h-1.5 w-full bg-rule" role="progressbar" aria-valuenow={done} aria-valuemin={0} aria-valuemax={3} aria-label="Progresso da configuração">
              <div className="h-full bg-signal transition-[width] duration-500" style={{ width: `${(done / 3) * 100}%` }} />
            </div>

            <ol className="mt-4 divide-y divide-rule border-y border-rule">
              {steps.map((step, index) => (
                <li key={step.title} className="flex items-center gap-4 py-3.5">
                  <span
                    className={`grid h-7 w-7 shrink-0 place-items-center font-mono text-[11px] ${
                      step.done ? "bg-ink text-paper" : "border border-rule text-warm"
                    }`}
                  >
                    {step.done ? <Icon name="check" className="h-3.5 w-3.5" strokeWidth={2.2} /> : index + 1}
                  </span>
                  <div className="min-w-0 flex-1">
                    <p className={`text-[14px] font-medium ${step.done ? "text-warm line-through decoration-rule" : "text-ink"}`}>
                      {step.title}
                    </p>
                    <p className="mt-0.5 text-[12.5px] leading-snug text-warm">{step.body}</p>
                  </div>
                  {step.action && !step.done && (
                    <Button size="sm" variant={step === next ? "primary" : "secondary"} onClick={step.action.run}>
                      {step.action.label}
                    </Button>
                  )}
                </li>
              ))}
            </ol>
          </Panel>

          <Panel title="Atividade recente" description="O que aconteceu na sua conta." padded={false}>
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
          <Panel
            title="Seu app"
            action={
              <button
                type="button"
                onClick={() => go("/conta/dispositivos")}
                className="text-[12.5px] text-signal hover:text-signal-dark"
              >
                Gerenciar
              </button>
            }
          >
            {linked ? (
              <ul className="space-y-3">
                {account.devices.slice(0, 3).map((device) => (
                  <li key={device.id} className="flex items-center gap-3">
                    <span className="grid h-9 w-9 shrink-0 place-items-center bg-ink text-paper">
                      <Icon name="laptop" className="h-4 w-4" />
                    </span>
                    <div className="min-w-0">
                      <p className="truncate text-[13.5px] font-medium text-ink">{device.name}</p>
                      <p className="font-mono text-[11px] text-warm">
                        {device.os} · desde {formatDate(device.linkedAt)}
                      </p>
                    </div>
                  </li>
                ))}
              </ul>
            ) : (
              <div className="text-center">
                <span className="mx-auto grid h-11 w-11 place-items-center border border-dashed border-warm text-warm">
                  <Icon name="link" className="h-5 w-5" />
                </span>
                <p className="mt-3 text-[13.5px] font-medium text-ink">Conecte o app</p>
                <p className="mx-auto mt-1 max-w-[30ch] text-[12.5px] leading-snug text-warm">
                  Entre com esta conta no Orbi para a memória, as rotinas e os lembretes acompanharem você.
                </p>
                <Button size="sm" className="mt-4" onClick={onDownload}>
                  Baixar o app
                </Button>
              </div>
            )}
          </Panel>

          <Panel
            title="Dados no Firestore"
            description="O que já está na sua conta e o app vai usar."
          >
            <ul className="-my-1 divide-y divide-rule">
              {[
                { label: "Apelidos de apps", value: account.aliases.length },
                { label: "Atalhos de texto", value: account.shortcuts.length },
                { label: "Rotinas", value: account.routines.length },
                { label: "PCs vinculados", value: account.devices.length },
              ].map((item) => (
                <li key={item.label} className="flex items-center justify-between gap-3 py-2.5">
                  <span className="text-[13px] text-ink">{item.label}</span>
                  <span className="font-mono text-[12px] text-ink-2 tnum">{item.value}</span>
                </li>
              ))}
            </ul>
            <Button
              variant="ghost"
              size="sm"
              className="mt-3"
              onClick={() => go("/conta/sincronizacao")}
            >
              Editar o que sincroniza
              <Icon name="chevron-right" className="h-3 w-3" />
            </Button>
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
