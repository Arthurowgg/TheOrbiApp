import type { ReactNode } from "react";
import { Avatar, Brand, Button, Icon, type IconName } from "../ui";
import { lastProfileError } from "../../lib/accounts";
import type { Account } from "../../lib/accounts";
import type { AccountPage } from "../../lib/router";

export type PageProps = {
  account: Account;
  reload: () => void;
  notify: (text: string) => void;
  go: (path: string) => void;
  onDownload: () => void;
  onLogout: () => void;
};

export const ACCOUNT_NAV: { page: AccountPage; path: string; label: string; icon: IconName }[] = [
  { page: "painel", path: "/conta", label: "Painel", icon: "grid" },
  { page: "perfil", path: "/conta/perfil", label: "Perfil", icon: "user" },
  { page: "computadores", path: "/conta/computadores", label: "Computadores", icon: "laptop" },
  { page: "memoria", path: "/conta/memoria", label: "Memória", icon: "sparkle" },
  { page: "downloads", path: "/conta/downloads", label: "Downloads", icon: "download" },
  { page: "seguranca", path: "/conta/seguranca", label: "Segurança", icon: "shield" },
];

export function PageHeader({
  title,
  description,
  actions,
}: {
  title: string;
  description?: string;
  actions?: ReactNode;
}) {
  return (
    <div className="mb-6 flex flex-col justify-between gap-4 sm:flex-row sm:items-end">
      <div>
        <h1 className="text-[26px] font-semibold tracking-[-0.015em] text-ink sm:text-[28px]">{title}</h1>
        {description && <p className="mt-1 max-w-[60ch] text-[13.5px] leading-[1.55] text-warm">{description}</p>}
      </div>
      {actions && <div className="flex shrink-0 flex-wrap items-center gap-2">{actions}</div>}
    </div>
  );
}

type ShellProps = {
  account: Account;
  page: AccountPage;
  go: (path: string) => void;
  onHome: () => void;
  onDownload: () => void;
  onLogout: () => void;
  children: ReactNode;
};

/** Aviso honesto quando não dá para salvar na conta agora. */
function StorageWarning({ message }: { message: string }) {
  return (
    <div className="mb-5 flex items-start gap-3 border border-signal/30 bg-signal-soft/60 px-4 py-3">
      <span className="mt-0.5 font-mono text-[11px] uppercase tracking-[0.08em] text-signal">aviso</span>
      <p className="text-[12.5px] leading-relaxed text-ink-2">
        Você entrou na conta, mas não conseguimos salvar agora. <span className="text-warm">{message}</span>
      </p>
    </div>
  );
}

export default function AccountShell({
  account,
  page,
  go,
  onHome,
  onDownload,
  onLogout,
  children,
}: ShellProps) {
  const device = account.devices[0];

  return (
    <div className="min-h-screen bg-[#e8e2d5]">
      {/* barra superior */}
      <header className="sticky top-0 z-40 border-b border-rule bg-paper">
        <div className="mx-auto flex h-14 max-w-[1280px] items-center justify-between gap-4 px-4 sm:px-6">
          <div className="flex items-center gap-5">
            <Brand onClick={onHome} />
            <span className="hidden h-5 w-px bg-rule sm:block" aria-hidden />
            <span className="label hidden text-warm sm:block">Minha conta</span>
          </div>

          <div className="flex items-center gap-1.5 sm:gap-3">
            <button
              type="button"
              onClick={onHome}
              className="label hidden items-center gap-1.5 px-2 py-2 text-warm transition-colors hover:text-ink sm:inline-flex"
            >
              <Icon name="home" className="h-3.5 w-3.5" />
              Ver site
            </button>
            <button
              type="button"
              onClick={() => go("/conta/perfil")}
              className="flex items-center gap-2.5 border border-rule py-1 pl-1 pr-3 transition-colors hover:border-ink"
              aria-label="Abrir meu perfil"
            >
              <Avatar name={account.name} color={account.color} size="sm" />
              <span className="hidden max-w-[150px] truncate text-[12.5px] font-medium text-ink sm:block">
                {account.name}
              </span>
            </button>
            <button
              type="button"
              onClick={onLogout}
              className="grid h-9 w-9 place-items-center text-warm transition-colors hover:bg-ink/[0.06] hover:text-ink"
              aria-label="Sair da conta"
              title="Sair"
            >
              <Icon name="logout" className="h-4 w-4" />
            </button>
          </div>
        </div>

        {/* abas no mobile */}
        <nav aria-label="Seções da conta" className="no-scrollbar flex gap-1 overflow-x-auto border-t border-rule px-3 py-1.5 lg:hidden">
          {ACCOUNT_NAV.map((item) => (
            <button
              key={item.page}
              type="button"
              onClick={() => go(item.path)}
              aria-current={page === item.page ? "page" : undefined}
              className={`flex shrink-0 items-center gap-2 px-3 py-2 font-mono text-[10.5px] uppercase tracking-[0.1em] transition-colors ${
                page === item.page ? "bg-ink text-paper" : "text-ink-2 hover:bg-ink/[0.06]"
              }`}
            >
              <Icon name={item.icon} className="h-3.5 w-3.5" />
              {item.label}
            </button>
          ))}
        </nav>
      </header>

      <div className="mx-auto flex max-w-[1280px] gap-0 lg:gap-8 lg:px-6">
        {/* lateral */}
        <aside className="sticky top-14 hidden h-[calc(100vh-56px)] w-[236px] shrink-0 flex-col justify-between py-7 lg:flex">
          <nav aria-label="Seções da conta" className="space-y-1">
            {ACCOUNT_NAV.map((item) => (
              <button
                key={item.page}
                type="button"
                onClick={() => go(item.path)}
                aria-current={page === item.page ? "page" : undefined}
                className={`flex w-full items-center gap-3 px-3.5 py-2.5 text-left text-[13.5px] font-medium transition-colors ${
                  page === item.page ? "bg-ink text-bone" : "text-ink-2 hover:bg-ink/[0.07] hover:text-ink"
                }`}
              >
                <Icon name={item.icon} className="h-4 w-4" />
                {item.label}
              </button>
            ))}
          </nav>

          <div className="space-y-3">
            <div className="border border-rule bg-[#fbf9f3] p-4">
              <p className="label text-warm">App no Windows</p>
              {device ? (
                <>
                  <p className="mt-2 flex items-center gap-2 text-[13px] font-medium text-ink">
                    <span className="h-2 w-2 rounded-full bg-signal" aria-hidden />
                    Na conta
                  </p>
                  <p className="mt-0.5 truncate font-mono text-[11px] text-warm">
                    {device.name}
                    {account.devices.length > 1 ? ` +${account.devices.length - 1}` : ""}
                  </p>
                </>
              ) : (
                <>
                  <p className="mt-2 text-[13px] font-medium text-ink">Nenhum PC entrou ainda</p>
                  <button
                    type="button"
                    onClick={() => go("/conta/dispositivos")}
                    className="mt-1.5 inline-flex items-center gap-1.5 text-[12.5px] text-signal hover:text-signal-dark"
                  >
                    Como entrar no app
                    <Icon name="arrow-right" className="h-3 w-3" />
                  </button>
                </>
              )}
            </div>
            <Button className="w-full" size="sm" onClick={onDownload}>
              <Icon name="download" className="h-3.5 w-3.5" />
              Baixar Orbi
            </Button>
          </div>
        </aside>

        <main className="min-w-0 flex-1 px-4 py-6 sm:px-6 lg:px-0 lg:py-8">
          <div key={page} className="animate-fade-up">
            {lastProfileError && <StorageWarning message={lastProfileError} />}
            {children}
          </div>
        </main>
      </div>
    </div>
  );
}
