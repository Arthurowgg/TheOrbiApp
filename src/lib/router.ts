export type AccountPage =
  | "painel"
  | "perfil"
  | "dispositivos"
  | "sincronizacao"
  | "downloads"
  | "seguranca";
export type LoginMode = "signin" | "signup" | "recover";

export type Route =
  | { name: "home" }
  | { name: "login"; mode: LoginMode }
  | { name: "account"; page: AccountPage };

const ACCOUNT_PAGES: Record<string, AccountPage> = {
  perfil: "perfil",
  dispositivos: "dispositivos",
  sincronizacao: "sincronizacao",
  downloads: "downloads",
  seguranca: "seguranca",
};

export function parseRoute(hash: string): Route {
  const path = hash.replace(/^#/, "").split("?")[0] || "/";
  if (path.startsWith("/entrar")) return { name: "login", mode: "signin" };
  if (path.startsWith("/criar")) return { name: "login", mode: "signup" };
  if (path.startsWith("/recuperar")) return { name: "login", mode: "recover" };
  if (path.startsWith("/conta")) {
    const sub = path.split("/")[2] ?? "";
    return { name: "account", page: ACCOUNT_PAGES[sub] ?? "painel" };
  }
  return { name: "home" };
}

export function routeKey(route: Route): string {
  if (route.name === "login") return `login:${route.mode}`;
  if (route.name === "account") return `account:${route.page}`;
  return "home";
}

export function modePath(mode: LoginMode): string {
  if (mode === "signup") return "/criar";
  if (mode === "recover") return "/recuperar";
  return "/entrar";
}

export function navigate(path: string) {
  const next = `#${path.startsWith("/") ? path : `/${path}`}`;
  if (window.location.hash === next) {
    window.dispatchEvent(new HashChangeEvent("hashchange"));
  } else {
    window.location.hash = next;
  }
}

export function scrollToTop() {
  window.scrollTo({ top: 0, behavior: "auto" });
}

export function scrollToSection(id: string) {
  const el = document.getElementById(id);
  if (el) el.scrollIntoView({ behavior: "smooth", block: "start" });
}
