import { useCallback, useEffect, useRef, useState } from "react";
import { onAuthStateChanged } from "firebase/auth";
import Home from "./components/Home";
import Login from "./components/Login";
import DownloadModal from "./components/DownloadModal";
import AccountShell, { type PageProps } from "./components/account/AccountShell";
import Overview from "./components/account/Overview";
import Profile from "./components/account/Profile";
import Devices from "./components/account/Devices";
import Downloads from "./components/account/Downloads";
import Security from "./components/account/Security";
import { Avatar, Brand, Button, Icon, Mark, OrbitArt } from "./components/ui";
import { NAV } from "./data/site";
import { addDownload, ensureProfile, firstName, signOut, type Account } from "./lib/accounts";
import { auth, initAnalytics } from "./lib/firebaseClient";
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
  scrolled,
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
  scrolled: boolean;
  onToggleMobile: () => void;
  onHome: () => void;
  onSection: (id: string) => void;
  onLogin: () => void;
  onSignup: () => void;
  onAccount: () => void;
  onDownload: () => void;
}) {
  return (
    <header
      className={`fixed inset-x-0 top-0 z-50 border-b transition-[background-color,border-color,box-shadow] duration-300 ${
        scrolled
          ? "border-rule bg-paper/92 shadow-[0_10px_30px_-24px_rgba(20,16,12,0.5)] backdrop-blur-md"
          : "border-transparent bg-paper/70 backdrop-blur-sm"
      }`}
    >
      <div className="mx-auto flex h-16 max-w-[1200px] items-center justify-between gap-4 px-5 sm:px-8">
        <div className="flex items-center gap-10">
          <Brand onClick={onHome} />
          <nav aria-label="Navegação principal" className="hidden items-center gap-7 lg:flex">
            {NAV.map((item) => (
              <button
                key={item.section}
                type="button"
                onClick={() => onSection(item.section)}
                className="link-underline font-mono text-[10.5px] uppercase tracking-[0.13em] text-ink-2 transition-colors hover:text-signal"
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
          <div className="relative z-50 border-b border-rule bg-paper px-5 pb-5 pt-2 shadow-[0_18px_40px_-24px_rgba(20,16,12,0.4)] sm:px-8">
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
    <footer className="relative overflow-hidden bg-film text-bone">
      <OrbitArt className="pointer-events-none absolute -right-32 -top-40 h-[540px] w-[540px] text-white/[0.07]" />
      <div className="relative mx-auto max-w-[1200px] px-5 py-14 sm:px-8">
        <div className="grid gap-10 md:grid-cols-[1.6fr_0.7fr_0.7fr_0.7fr]">
          <div>
            <button type="button" onClick={onHome} className="flex items-center gap-2.5" aria-label="Orbi — início">
              <span className="grid h-8 w-8 place-items-center bg-signal text-white">
                <Mark className="h-[18px] w-[18px]" />
              </span>
              <span className="font-cond text-[24px] font-bold uppercase leading-none tracking-[0.02em]">Orbi</span>
            </button>
            <p className="serif mt-4 max-w-[32ch] text-[17px] italic leading-[1.5] text-bone/55">
              Seu Windows, em português — e com a sua voz no comando.
            </p>
            <p className="mt-5 font-mono text-[10.5px] uppercase tracking-[0.14em] text-bone/35">
              Windows 11 · gratuito · sem cartão
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
              <li>
                <button type="button" onClick={() => onSection("seguranca")} className={link}>
                  Privacidade
                </button>
              </li>
            </ul>
          </div>

          <div>
            <p className="label text-bone/35">Código</p>
            <ul className="mt-3 space-y-2">
              <li>
                <a
                  href="https://github.com/Arthurowgg/TheOrbiApp"
                  target="_blank"
                  rel="noreferrer"
                  className={`${link} inline-flex items-center gap-1.5`}
                >
                  <Icon name="github" className="h-3.5 w-3.5" />
                  GitHub
                </a>
              </li>
              <li>
                <a
                  href="https://github.com/Arthurowgg/TheOrbiApp/releases"
                  target="_blank"
                  rel="noreferrer"
                  className={link}
                >
                  Releases
                </a>
              </li>
              <li>
                <span className="font-mono text-[11px] text-bone/35">Firebase · Firestore</span>
              </li>
            </ul>
          </div>
        </div>

        <div className="mt-10 flex flex-col justify-between gap-2 border-t border-paper/15 pt-4 sm:flex-row sm:items-center">
          <p className="label text-bone/35">
            © {new Date().getFullYear()} Orbi · v{BUILD.version} · {BUILD.channel}
          </p>
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
  const [account, setAccount] = useState<Account | null>(null);
  const [authReady, setAuthReady] = useState(false);
  const [downloadOpen, setDownloadOpen] = useState(false);
  const [mobileOpen, setMobileOpen] = useState(false);
  const [scrolled, setScrolled] = useState(false);
  const [pendingSection, setPendingSection] = useState<string | null>(null);
  const [toast, setToast] = useState<Toast | null>(null);

  const toastTimer = useRef<number | null>(null);
  const guestDownload = useRef<{ version: string; checksum: string; filename: string } | null>(null);

  // Quem não tem conta é levado ao login ao tentar abrir a área da conta.
  const effective: Route =
    route.name === "account" && authReady && !account ? { name: "login", mode: "signin" } : route;
  const key = routeKey(effective);

  const notify = useCallback((text: string) => {
    if (toastTimer.current) window.clearTimeout(toastTimer.current);
    setToast({ id: Date.now(), text });
    toastTimer.current = window.setTimeout(() => setToast(null), 3800);
  }, []);

  /* ------------------------- sessão do Firebase ------------------------- */
  useEffect(() => {
    initAnalytics();
    const stop = onAuthStateChanged(auth, async (user) => {
      if (!user) {
        setAccount(null);
        setAuthReady(true);
        return;
      }
      try {
        const next = await ensureProfile(user);
        setAccount(next);
      } catch {
        setAccount(null);
      } finally {
        setAuthReady(true);
      }
    });
    return stop;
  }, []);

  useEffect(() => {
    const onHash = () => setRoute(parseRoute(window.location.hash));
    window.addEventListener("hashchange", onHash);
    return () => window.removeEventListener("hashchange", onHash);
  }, []);

  useEffect(() => {
    scrollToTop();
  }, [key]);

  useEffect(() => {
    const onScroll = () => setScrolled(window.scrollY > 12);
    onScroll();
    window.addEventListener("scroll", onScroll, { passive: true });
    return () => window.removeEventListener("scroll", onScroll);
  }, []);

  useEffect(() => {
    if (effective.name === "home" && pendingSection) {
      const timer = window.setTimeout(() => scrollToSection(pendingSection), 60);
      setPendingSection(null);
      return () => window.clearTimeout(timer);
    }
  }, [effective.name, pendingSection]);

  // Trava o scroll do body quando o menu mobile está aberto.
  useEffect(() => {
    document.body.style.overflow = mobileOpen ? "hidden" : "";
    return () => {
      document.body.style.overflow = "";
    };
  }, [mobileOpen]);

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

  const handleSession = async (next: Account) => {
    setAccount(next);
    const pending = guestDownload.current;
    if (pending) {
      guestDownload.current = null;
      await addDownload(pending).catch(() => undefined);
    }
  };

  const handleLogout = async () => {
    try {
      await signOut();
    } catch {
      // mesmo que falhe, limpamos a interface
    }
    setAccount(null);
    navigate("/");
    notify("Você saiu da conta.");
  };

  const refresh = useCallback(async () => {
    const user = auth.currentUser;
    if (!user) return;
    const next = await ensureProfile(user);
    setAccount(next);
  }, []);

  const handleDownloaded = (record: { version: string; checksum: string; filename: string }) => {
    if (account) {
      void addDownload(record).then(() => refresh());
    } else {
      guestDownload.current = record;
    }
  };

  const pageProps = (current: Account): PageProps => ({
    account: current,
    reload: refresh,
    notify,
    go: navigate,
    onDownload: openDownload,
    onLogout: handleLogout,
  });

  /* ----------------------------- render ----------------------------- */
  let screen;
  if (!authReady) {
    screen = (
      <div className="grid min-h-screen place-items-center bg-paper">
        <div className="flex flex-col items-center gap-4">
          <span className="grid h-12 w-12 place-items-center bg-ink text-paper">
            <Mark className="h-6 w-6" />
          </span>
          <p className="label text-warm">conectando à conta</p>
        </div>
      </div>
    );
  } else if (effective.name === "login") {
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
          scrolled={scrolled}
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
    <div className="relative z-[1]">
      {screen}

      <DownloadModal
        open={downloadOpen}
        onClose={() => setDownloadOpen(false)}
        user={account}
        onDownloaded={handleDownloaded}
        onLogin={openLogin}
        onSignup={openSignup}
      />

      {toast && (
        <div
          role="status"
          className="animate-fade-up fixed bottom-5 left-1/2 z-[90] w-[min(92vw,420px)] -translate-x-1/2 border border-white/10 bg-film px-4 py-3 text-[13px] text-bone shadow-[0_24px_60px_-24px_rgba(0,0,0,0.7)]"
        >
          <span className="flex items-start gap-2.5">
            <Icon name="check" className="mt-0.5 h-4 w-4 shrink-0 text-signal" />
            {toast.text}
          </span>
        </div>
      )}
    </div>
  );
}
