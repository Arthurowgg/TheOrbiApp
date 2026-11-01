import { useCallback, useEffect, useRef, useState } from "react";
import Home from "./components/Home";
import Login from "./components/Login";
import DownloadModal from "./components/DownloadModal";
import AccountShell, { type PageProps } from "./components/account/AccountShell";
import Overview from "./components/account/Overview";
import Profile from "./components/account/Profile";
import Devices from "./components/account/Devices";
import Downloads from "./components/account/Downloads";
import Security from "./components/account/Security";
import { Avatar, Brand, Button, Icon, Mark } from "./components/ui";
import { NAV } from "./data/site";
import {
  addDownload,
  endSession,
  firstName,
  getCurrentAccount,
  type Account,
} from "./lib/accounts";
import { BUILD } from "./lib/download";
import {
  modePath,
  navigate,
  parseRoute,
  routeKey,
  scrollToSection,
  scrollToTop,
  type LoginMode,
  type Route,
} from "./lib/router";

type Toast = { id: number; text: string };

/* ------------------------------------------------------------------ */
/* Cabeçalho do site                                                  */
/* ------------------------------------------------------------------ */
function SiteHeader({
  account,
  mobileOpen,
  onToggleMobile,
  onHome,
  onSection,
  onLogin,
  onSignup,
  onAccount,
  onDownload,
}: {
  account: Account | null;
  mobileOpen: boolean;
  onToggleMobile: () => void;
  onHome: () => void;
  onSection: (id: string) => void;
  onLogin: () => void;
  onSignup: () => void;
  onAccount: () => void;
  onDownload: () => void;
}) {
  return (
    <header className="fixed inset-x-0 top-0 z-50 border-b border-rule bg-paper/90 backdrop-blur-md">
      <div className="mx-auto flex h-16 max-w-[1180px] items-center justify-between gap-4 px-5 sm:px-8">
        <div className="flex items-center gap-10">
          <Brand onClick={onHome} />
          <nav aria-label="Navegação principal" className="hidden items-center gap-7 lg:flex">
            {NAV.map((item) => (
              <button
                key={item.section}
                type="button"
                onClick={() => onSection(item.section)}
                className="font-mono text-[10.5px] uppercase tracking-[0.13em] text-ink-2 transition-colors hover:text-signal"
              >
                {item.label}
              </button>
            ))}
          </nav>
        </div>

        <div className="flex items-center gap-2">
          {account ? (
            <button
              type="button"
              onClick={onAccount}
              className="flex items-center gap-2.5 border border-rule py-1 pl-1 pr-3 transition-colors hover:border-ink"
              aria-label="Minha conta"
            >
              <Avatar name={account.name} color={account.color} size="sm" />
              <span className="hidden text-[12.5px] font-medium text-ink sm:block">
                {firstName(account.name)}
              </span>
            </button>
          ) : (
            <>
              <button
                type="button"
                onClick={onLogin}
                className="label hidden px-3 py-2 text-ink-2 transition-colors hover:text-signal sm:block"
              >
                Entrar
              </button>
              <Button size="sm" variant="secondary" onClick={onSignup} className="hidden md:inline-flex">
                Criar conta
              </Button>
            </>
          )}

          <Button size="sm" onClick={onDownload} className="whitespace-nowrap">
            <Icon name="download" className="h-3.5 w-3.5" />
            Baixar
          </Button>

          <button
            type="button"
            onClick={onToggleMobile}
            aria-expanded={mobileOpen}
            aria-label={mobileOpen ? "Fechar menu" : "Abrir menu"}
            className="grid h-9 w-9 place-items-center text-ink transition-colors hover:bg-ink/[0.06] lg:hidden"
          >
            <Icon name={mobileOpen ? "close" : "menu"} className="h-5 w-5" />
          </button>
        </div>
      </div>

      {mobileOpen && (
        <div className="lg:hidden">
          <button
            type="button"
            className="fixed inset-x-0 bottom-0 top-16 z-40 cursor-default bg-ink/25 backdrop-blur-[1px]"
            onClick={onToggleMobile}
            aria-label="Fechar menu"
          />
          <div className="relative z-50 border-b border-rule bg-paper px-5 pb-5 pt-2 shadow-[0_18px_40px_-24px_rgba(23,18,14,0.4)] sm:px-8">
            <nav aria-label="Navegação mobile">
              {NAV.map((item) => (
                <button
                  key={item.section}
                  type="button"
                  onClick={() => onSection(item.section)}
                  className="flex w-full items-center justify-between border-b border-rule py-3.5 text-left font-cond text-[19px] font-semibold uppercase tracking-[0.02em] text-ink"
                >
                  {item.label}
                  <Icon name="arrow-right" className="h-4 w-4 text-signal" />
                </button>
              ))}
            </nav>
            <div className="mt-4 grid grid-cols-2 gap-2">
              {account ? (
                <Button variant="secondary" onClick={onAccount} className="col-span-2">
                  Minha conta
                </Button>
              ) : (
                <>
                  <Button variant="secondary" onClick={onLogin}>
                    Entrar
                  </Button>
                  <Button variant="secondary" onClick={onSignup}>
                    Criar conta
                  </Button>
                </>
              )}
            </div>
          </div>
        </div>
      )}
    </header>
  );
}

/* ------------------------------------------------------------------ */
/* Rodapé                                                             */
/* ------------------------------------------------------------------ */
function SiteFooter({
  account,
  onHome,
  onSection,
  onLogin,
  onAccount,
}: {
  account: Account | null;
  onHome: () => void;
  onSection: (id: string) => void;
  onLogin: () => void;
  onAccount: () => void;
}) {
  const link = "text-[13px] text-bone/60 transition-colors hover:text-bone";
  return (
    <footer className="bg-film text-bone">
      <div className="mx-auto max-w-[1180px] px-5 py-12 sm:px-8">
        <div className="grid gap-10 md:grid-cols-[1.5fr_0.7fr_0.7fr]">
          <div>
            <button type="button" onClick={onHome} className="flex items-center gap-2.5" aria-label="NOVA — início">
              <span className="grid h-8 w-8 place-items-center bg-signal text-white">
                <Mark className="h-4 w-4" />
              </span>
              <span className="font-cond text-[24px] font-bold uppercase leading-none tracking-[0.01em]">Nova</span>
            </button>
            <p className="serif mt-4 max-w-[30ch] text-[17px] italic leading-[1.5] text-bone/55">
              Seu Windows, em português.
            </p>
          </div>

          <div>
            <p className="label text-bone/35">Produto</p>
            <ul className="mt-3 space-y-2">
              {NAV.map((item) => (
                <li key={item.section}>
                  <button type="button" onClick={() => onSection(item.section)} className={link}>
                    {item.label}
                  </button>
                </li>
              ))}
            </ul>
          </div>

          <div>
            <p className="label text-bone/35">Conta</p>
            <ul className="mt-3 space-y-2">
              <li>
                <button type="button" onClick={account ? onAccount : onLogin} className={link}>
                  {account ? "Minha conta" : "Entrar"}
                </button>
              </li>
              <li>
                <button type="button" onClick={() => onSection("download")} className={link}>
                  Download
                </button>
              </li>
            </ul>
          </div>
        </div>

        <div className="mt-10 flex flex-col justify-between gap-2 border-t border-paper/15 pt-4 sm:flex-row sm:items-center">
          <p className="label text-bone/35">© {new Date().getFullYear()} NOVA · v{BUILD.version}</p>
          <p className="label text-bone/35">Windows 11 é uma marca da Microsoft.</p>
        </div>
      </div>
    </footer>
  );
}

/* ------------------------------------------------------------------ */
/* App                                                                */
/* ------------------------------------------------------------------ */
export default function App() {
  const [route, setRoute] = useState<Route>(() => parseRoute(window.location.hash));
  const [account, setAccount] = useState<Account | null>(() => getCurrentAccount());
  const [downloadOpen, setDownloadOpen] = useState(false);
  const [mobileOpen, setMobileOpen] = useState(false);
  const [pendingSection, setPendingSection] = useState<string | null>(null);
  const [toast, setToast] = useState<Toast | null>(null);

  const toastTimer = useRef<number | null>(null);
  const guestDownload = useRef<{ version: string; checksum: string; filename: string } | null>(null);

  // Quem não tem conta é levado ao login ao tentar abrir a área da conta.
  const effective: Route =
    route.name === "account" && !account ? { name: "login", mode: "signin" } : route;
  const key = routeKey(effective);

  const notify = useCallback((text: string) => {
    if (toastTimer.current) window.clearTimeout(toastTimer.current);
    setToast({ id: Date.now(), text });
    toastTimer.current = window.setTimeout(() => setToast(null), 3800);
  }, []);

  const reload = useCallback(() => setAccount(getCurrentAccount()), []);

  useEffect(() => {
    const onHash = () => setRoute(parseRoute(window.location.hash));
    window.addEventListener("hashchange", onHash);
    return () => window.removeEventListener("hashchange", onHash);
  }, []);

  useEffect(() => {
    scrollToTop();
    setMobileOpen(false);
    const titles: Record<string, string> = {
      home: "NOVA — Seu Windows, em português",
      "login:signin": "Entrar — NOVA",
      "login:signup": "Criar conta — NOVA",
      "login:recover": "Recuperar acesso — NOVA",
      "account:painel": "Painel — Minha conta NOVA",
      "account:perfil": "Perfil — Minha conta NOVA",
      "account:dispositivos": "Dispositivos — Minha conta NOVA",
      "account:downloads": "Downloads — Minha conta NOVA",
      "account:seguranca": "Segurança — Minha conta NOVA",
    };
    document.title = titles[key] ?? "NOVA";
  }, [key]);

  useEffect(
    () => () => {
      if (toastTimer.current) window.clearTimeout(toastTimer.current);
    },
    [],
  );

  useEffect(() => {
    if (effective.name !== "home" || !pendingSection) return;
    const id = pendingSection;
    setPendingSection(null);
    const timer = window.setTimeout(() => scrollToSection(id), 60);
    return () => window.clearTimeout(timer);
  }, [effective.name, pendingSection]);

  /* ----------------------------- ações ----------------------------- */
  const goHome = () => {
    setMobileOpen(false);
    if (effective.name === "home") scrollToTop();
    else navigate("/");
  };

  const goSection = (id: string) => {
    setMobileOpen(false);
    if (effective.name !== "home") {
      setPendingSection(id);
      navigate("/");
    } else {
      scrollToSection(id);
    }
  };

  const openDownload = () => {
    setMobileOpen(false);
    setDownloadOpen(true);
  };

  const openLogin = () => {
    setDownloadOpen(false);
    navigate("/entrar");
  };

  const openSignup = () => {
    setDownloadOpen(false);
    navigate("/criar");
  };

  const handleSession = (next: Account) => {
    let current = next;
    const pending = guestDownload.current;
    if (pending) {
      current = addDownload(next.id, pending);
      guestDownload.current = null;
    }
    setAccount(current);
  };

  const handleLogout = () => {
    endSession();
    setAccount(null);
    navigate("/");
    notify("Você saiu da conta.");
  };

  const handleDownloaded = (record: { version: string; checksum: string; filename: string }) => {
    if (account) {
      setAccount(addDownload(account.id, record));
    } else {
      guestDownload.current = record;
    }
  };

  const pageProps = (current: Account): PageProps => ({
    account: current,
    reload,
    notify,
    go: navigate,
    onDownload: openDownload,
    onLogout: handleLogout,
  });

  /* ----------------------------- render ----------------------------- */
  let screen;
  if (effective.name === "login") {
    const mode: LoginMode = effective.mode;
    screen = (
      <Login
        mode={mode}
        currentAccount={account}
        message={route.name === "account" ? "Entre para acessar a sua conta." : undefined}
        onMode={(next) => navigate(modePath(next))}
        onSession={handleSession}
        onFinish={() => {
          notify(account ? `Bem-vindo(a), ${firstName(account.name)}.` : "Conta pronta.");
          navigate("/conta");
        }}
        onBack={goHome}
        onLogout={handleLogout}
      />
    );
  } else if (effective.name === "account" && account) {
    const props = pageProps(account);
    const page = effective.page;
    screen = (
      <AccountShell
        account={account}
        page={page}
        go={navigate}
        onHome={goHome}
        onDownload={openDownload}
        onLogout={handleLogout}
      >
        {page === "painel" && <Overview {...props} />}
        {page === "perfil" && <Profile {...props} />}
        {page === "dispositivos" && <Devices {...props} />}
        {page === "downloads" && <Downloads {...props} />}
        {page === "seguranca" && <Security {...props} />}
      </AccountShell>
    );
  } else {
    screen = (
      <>
        <SiteHeader
          account={account}
          mobileOpen={mobileOpen}
          onToggleMobile={() => setMobileOpen((open) => !open)}
          onHome={goHome}
          onSection={goSection}
          onLogin={openLogin}
          onSignup={openSignup}
          onAccount={() => navigate("/conta")}
          onDownload={openDownload}
        />
        <Home
          account={account}
          onDownload={openDownload}
          onLogin={openLogin}
          onSignup={openSignup}
          onAccount={() => navigate("/conta")}
        />
        <SiteFooter
          account={account}
          onHome={goHome}
          onSection={goSection}
          onLogin={openLogin}
          onAccount={() => navigate("/conta")}
        />
      </>
    );
  }

  return (
    <div className="relative min-h-screen">
      {screen}

      <DownloadModal
        open={downloadOpen}
        onClose={() => setDownloadOpen(false)}
        user={account}
        onDownloaded={handleDownloaded}
        onLogin={openLogin}
      />

      {toast && (
        <div
          role="status"
          className="animate-fade-up fixed bottom-5 left-1/2 z-[90] flex w-[calc(100%-2rem)] max-w-[380px] -translate-x-1/2 items-center justify-between gap-3 border border-paper/15 bg-film px-4 py-3 text-bone shadow-[0_20px_50px_-20px_rgba(23,18,14,0.7)]"
        >
          <span className="flex items-center gap-2.5 text-[12.5px] leading-snug">
            <span className="grid h-5 w-5 shrink-0 place-items-center bg-signal text-white">
              <Icon name="check" className="h-3 w-3" strokeWidth={2.4} />
            </span>
            {toast.text}
          </span>
          <button
            type="button"
            onClick={() => setToast(null)}
            aria-label="Fechar aviso"
            className="shrink-0 text-bone/50 transition-colors hover:text-bone"
          >
            <Icon name="close" className="h-3.5 w-3.5" />
          </button>
        </div>
      )}
    </div>
  );
}
