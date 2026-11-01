/* ------------------------------------------------------------------ *
 * Contas Orbi — Firebase Authentication + Cloud Firestore
 *
 * Antes: tudo vivia no localStorage do navegador. Agora:
 *   • quem entra e sai é o Firebase Authentication (senha com hash no
 *     servidor, verificação de e-mail, redefinição por link, sessão
 *     persistente, provedor Google);
 *   • perfil, aparelhos, downloads e atividade ficam em
 *     `users/{uid}` no Firestore, protegidos por Security Rules —
 *     cada pessoa só lê e escreve o próprio documento;
 *   • a interface continua falando só com este módulo.
 * ------------------------------------------------------------------ */
import {
  GoogleAuthProvider,
  createUserWithEmailAndPassword,
  deleteUser,
  EmailAuthProvider,
  reauthenticateWithCredential,
  reload as reloadUser,
  sendEmailVerification,
  sendPasswordResetEmail,
  signInWithEmailAndPassword,
  signInWithPopup,
  signOut as fbSignOut,
  updateEmail,
  updateProfile as updateAuthProfile,
  type User,
} from "firebase/auth";
import {
  doc,
  getDoc,
  serverTimestamp,
  setDoc,
  updateDoc,
  deleteDoc,
  type DocumentData,
} from "firebase/firestore";
import { auth, db, configured } from "./firebaseClient";

export type ActivityType = "conta" | "login" | "download" | "dispositivo" | "perfil" | "seguranca";

export type SyncPrefs = {
  preferencias: boolean;
  apelidos: boolean;
  rotinas: boolean;
  atalhos: boolean;
};

export type Device = {
  id: string;
  name: string;
  os: string;
  linkedAt: string;
  code: string;
};

export type DownloadRecord = {
  id: string;
  version: string;
  at: string;
  checksum: string;
  filename: string;
  /** Link direto do instalador no GitHub (algumas versões são anteriores a isso). */
  url?: string;
};

export type Activity = {
  id: string;
  at: string;
  type: ActivityType;
  text: string;
};

export type Alias = { alias: string; target: string };
export type Shortcut = { trigger: string; expansion: string };
export type Routine = { name: string; commands: string[] };
export type AppPrefs = {
  startWithWindows: boolean;
  volume: number;
  lockEnabled: boolean;
  lockAfterMinutes: number;
  geminiModel: string;
};

export type Account = {
  id: string;
  name: string;
  email: string;
  createdAt: string;
  color: string;
  emailVerified: boolean;
  provider: string;
  sync: SyncPrefs;
  aliases: Alias[];
  shortcuts: Shortcut[];
  routines: Routine[];
  appPrefs: AppPrefs;
  devices: Device[];
  downloads: DownloadRecord[];
  activity: Activity[];
};

export const AVATAR_COLORS = ["#17120e", "#c7452a", "#2f5d62", "#6b4e9b", "#9a6a1c", "#3b6e3f"] as const;

export const DEFAULT_SYNC: SyncPrefs = {
  preferencias: true,
  apelidos: true,
  rotinas: true,
  atalhos: true,
};

export const DEFAULT_PREFS: AppPrefs = {
  startWithWindows: true,
  volume: 50,
  lockEnabled: false,
  lockAfterMinutes: 5,
  geminiModel: "gemini-2.0-flash",
};

export const MIN_PASSWORD = 8;

export type AuthErrorCode =
  | "not_found"
  | "wrong_password"
  | "email_taken"
  | "invalid_input"
  | "invalid_email"
  | "weak_password"
  | "too_many"
  | "network"
  | "not_configured"
  | "invalid_code"
  | "device_exists"
  | "requires_recent_login"
  | "storage";

export class AuthError extends Error {
  readonly code: AuthErrorCode;
  constructor(code: AuthErrorCode, message: string) {
    super(message);
    this.name = "AuthError";
    this.code = code;
  }
}

/* ------------------------------------------------------------------ */
/* Utilidades                                                         */
/* ------------------------------------------------------------------ */
// Sem 0, 1, I, L e O para não confundir quando alguém digita o código.
export const ALPHABET = "23456789ABCDEFGHJKMNPQRSTUVWXYZ";

export function normalizeEmail(email: string) {
  return email.trim().toLowerCase();
}

export function isEmail(email: string) {
  return /^[^\s@]+@[^\s@]+\.[^\s@]{2,}$/.test(email.trim());
}

function randomChars(size: number) {
  const out = new Uint8Array(size);
  globalThis.crypto.getRandomValues(out);
  return Array.from(out, (b) => ALPHABET[b % ALPHABET.length]).join("");
}

function randomColor() {
  const out = new Uint8Array(1);
  globalThis.crypto.getRandomValues(out);
  return AVATAR_COLORS[out[0] % AVATAR_COLORS.length];
}

/** Traduz os erros do Firebase para mensagens em português. */
export function toAuthError(error: unknown): AuthError {
  if (error instanceof AuthError) return error;
  const code = (error as { code?: string })?.code ?? "";
  switch (code) {
    case "auth/invalid-email":
      return new AuthError("invalid_email", "Digite um e-mail válido.");
    case "auth/missing-password":
    case "auth/wrong-password":
    case "auth/invalid-credential":
      return new AuthError("wrong_password", "E-mail ou senha incorretos.");
    case "auth/user-not-found":
      return new AuthError("not_found", "Não encontramos uma conta com este e-mail.");
    case "auth/email-already-in-use":
      return new AuthError("email_taken", "Já existe uma conta com este e-mail.");
    case "auth/weak-password":
      return new AuthError("weak_password", `A senha precisa de pelo menos ${MIN_PASSWORD} caracteres.`);
    case "auth/too-many-requests":
      return new AuthError("too_many", "Muitas tentativas. Espere um minuto e tente de novo.");
    case "auth/network-request-failed":
      return new AuthError("network", "Sem conexão com o Firebase. Confira sua internet.");
    case "auth/popup-closed-by-user":
    case "auth/cancelled-popup-request":
      return new AuthError("invalid_input", "A janela do Google foi fechada.");
    case "auth/popup-blocked":
      return new AuthError("invalid_input", "O navegador bloqueou a janela do Google. Libere pop-ups e tente de novo.");
    case "auth/operation-not-allowed":
      return new AuthError(
        "storage",
        "Este método de login ainda não está ligado no console do Firebase.",
      );
    case "auth/requires-recent-login":
      return new AuthError("requires_recent_login", "Por segurança, entre de novo antes de fazer isso.");
    case "auth/account-exists-with-different-credential":
      return new AuthError("email_taken", "Esta conta usa outro método de login (Google ou e-mail).");
    case "permission-denied":
    case "firestore/permission-denied":
      return new AuthError("storage", "O Firestore recusou a operação. Confira as Security Rules publicadas.");
    default:
      break;
  }
  if (error instanceof Error && error.message) {
    if (/offline|unavailable/i.test(error.message)) {
      return new AuthError("network", "Sem conexão com o Firestore. Confira sua internet.");
    }
    return new AuthError("storage", error.message);
  }
  return new AuthError("storage", "Algo deu errado. Tente de novo.");
}

function ensureConfigured() {
  if (!configured) {
    throw new AuthError(
      "not_configured",
      "Firebase não configurado. Preencha o arquivo .env com as chaves do projeto.",
    );
  }
}

/* ------------------------------------------------------------------ */
/* Documento do usuário                                               */
/* ------------------------------------------------------------------ */
function toAccount(user: User, data: DocumentData | undefined): Account {
  const raw = data ?? {};
  const created = raw.createdAt?.toDate?.() ?? new Date(user.metadata.creationTime ?? Date.now());
  return {
    id: user.uid,
    name: (raw.name as string) ?? user.displayName ?? (user.email?.split("@")[0] ?? "Você"),
    email: (raw.email as string) ?? user.email ?? "",
    createdAt: created.toISOString(),
    color: (raw.color as string) ?? AVATAR_COLORS[0],
    emailVerified: user.emailVerified,
    provider: user.providerData[0]?.providerId ?? "password",
    sync: { ...DEFAULT_SYNC, ...(raw.sync ?? {}) },
    aliases: Array.isArray(raw.aliases) ? (raw.aliases as Alias[]) : [],
    shortcuts: Array.isArray(raw.shortcuts) ? (raw.shortcuts as Shortcut[]) : [],
    routines: Array.isArray(raw.routines) ? (raw.routines as Routine[]) : [],
    appPrefs: { ...DEFAULT_PREFS, ...(raw.appPrefs ?? {}) },
    devices: Array.isArray(raw.devices) ? (raw.devices as Device[]) : [],
    downloads: Array.isArray(raw.downloads) ? (raw.downloads as DownloadRecord[]) : [],
    activity: Array.isArray(raw.activity) ? (raw.activity as Activity[]) : [],
  };
}

export function userDoc(uid: string) {
  return doc(db, "users", uid);
}

/**
 * Perfil local derivado do Firebase Authentication.
 *
 * Usado quando o Firestore recusa a leitura (regras ainda não publicadas, banco
 * não criado, offline). A pessoa entra e usa o site; o aviso aparece na área da
 * conta dizendo que a sincronização está desligada até o banco responder.
 */
export function accountFromAuth(user: User): Account {
  return toAccount(user, {
    name: user.displayName ?? user.email?.split("@")[0] ?? "Você",
    email: user.email ?? "",
    createdAt: new Date().toISOString(),
    color: "#c7452a",
    plan: "gratis",
  });
}

/** Cria o documento `users/{uid}` na primeira entrada (idempotente). */
export async function ensureProfile(user: User, name?: string): Promise<Account> {
  ensureConfigured();
  try {
    const account = await ensureProfileDocument(user, name);
    lastProfileError = null;
    return account;
  } catch (error) {
    // Banco indisponível/regra fechada: entrar continua funcionando.
    console.warn("[orbi] perfil no Firestore indisponível:", error);
    lastProfileError = toAuthError(error).message;
    return accountFromAuth(user);
  }
}

/** Erro do último acesso ao Firestore (mostrado na área da conta). */
export let lastProfileError: string | null = null;

async function ensureProfileDocument(user: User, name?: string): Promise<Account> {
  const ref = userDoc(user.uid);
  const snapshot = await getDoc(ref);
  if (!snapshot.exists()) {
    const payload = {
      name: (name ?? user.displayName ?? user.email?.split("@")[0] ?? "Você").trim().slice(0, 80),
      email: user.email ?? "",
      createdAt: serverTimestamp(),
      updatedAt: serverTimestamp(),
      color: randomColor(),
      plan: "gratis",
      sync: { ...DEFAULT_SYNC },
      aliases: [],
      shortcuts: [],
      routines: [],
      appPrefs: { ...DEFAULT_PREFS },
      devices: [],
      downloads: [],
      activity: [
        {
          id: randomChars(8),
          at: new Date().toISOString(),
          type: "conta" as ActivityType,
          text: "Conta criada",
        },
      ],
    };
    await setDoc(ref, payload);
    return toAccount(user, payload);
  }
  return toAccount(user, snapshot.data());
}

/** Lê o documento uma vez (usado pelo `reload` das páginas). */
export async function fetchAccount(): Promise<Account | null> {
  const user = auth.currentUser;
  if (!user || !configured) return null;
  try {
    const snapshot = await getDoc(userDoc(user.uid));
    lastProfileError = null;
    return toAccount(user, snapshot.data());
  } catch (error) {
    lastProfileError = toAuthError(error).message;
    return accountFromAuth(user);
  }
}

async function patchUser(payload: Record<string, unknown>) {
  const user = auth.currentUser;
  if (!user) throw new AuthError("not_found", "Entre na sua conta primeiro.");
  await updateDoc(userDoc(user.uid), { ...payload, updatedAt: serverTimestamp() });
}

async function pushActivity(type: ActivityType, text: string) {
  const user = auth.currentUser;
  if (!user) return;
  const snapshot = await getDoc(userDoc(user.uid));
  const activity = (snapshot.data()?.activity as Activity[] | undefined) ?? [];
  const next = [{ id: randomChars(8), at: new Date().toISOString(), type, text }, ...activity].slice(0, 40);
  await updateDoc(userDoc(user.uid), { activity: next, updatedAt: serverTimestamp() });
}

/* ------------------------------------------------------------------ */
/* Cadastro, entrada e saída                                          */
/* ------------------------------------------------------------------ */
export async function signUp(input: {
  name: string;
  email: string;
  password: string;
}): Promise<{ account: Account; needsVerification: boolean }> {
  ensureConfigured();
  if (input.name.trim().length < 2) {
    throw new AuthError("invalid_input", "Digite como você quer ser chamado.");
  }
  if (!isEmail(input.email)) throw new AuthError("invalid_email", "Digite um e-mail válido.");
  if (input.password.length < MIN_PASSWORD) {
    throw new AuthError("weak_password", `A senha precisa de pelo menos ${MIN_PASSWORD} caracteres.`);
  }
  try {
    const cred = await createUserWithEmailAndPassword(auth, normalizeEmail(input.email), input.password);
    const name = input.name.trim().slice(0, 80);
    await updateAuthProfile(cred.user, { displayName: name });
    const account = await ensureProfile(cred.user, name);
    // O link de verificação é opcional: não bloqueia o uso do site.
    sendEmailVerification(cred.user).catch(() => undefined);
    return { account, needsVerification: !cred.user.emailVerified };
  } catch (error) {
    throw toAuthError(error);
  }
}

export async function signIn(input: { email: string; password: string }): Promise<Account> {
  ensureConfigured();
  if (!isEmail(input.email)) throw new AuthError("invalid_email", "Digite um e-mail válido.");
  try {
    const cred = await signInWithEmailAndPassword(auth, normalizeEmail(input.email), input.password);
    const account = await ensureProfile(cred.user);
    pushActivity("login", "Entrada no site").catch(() => undefined);
    return account;
  } catch (error) {
    throw toAuthError(error);
  }
}

export async function signInWithGoogle(): Promise<Account> {
  ensureConfigured();
  try {
    const provider = new GoogleAuthProvider();
    provider.setCustomParameters({ prompt: "select_account" });
    const cred = await signInWithPopup(auth, provider);
    const account = await ensureProfile(cred.user);
    pushActivity("login", "Entrada com Google").catch(() => undefined);
    return account;
  } catch (error) {
    throw toAuthError(error);
  }
}

export async function signOut() {
  await fbSignOut(auth);
}

/** Envia o link de redefinição de senha do Firebase para o e-mail. */
export async function sendResetEmail(email: string): Promise<void> {
  ensureConfigured();
  if (!isEmail(email)) throw new AuthError("invalid_email", "Digite um e-mail válido.");
  try {
    await sendPasswordResetEmail(auth, normalizeEmail(email), {
      url: `${window.location.origin}/#/entrar`,
      handleCodeInApp: false,
    });
  } catch (error) {
    throw toAuthError(error);
  }
}

export async function resendVerification(): Promise<void> {
  const user = auth.currentUser;
  if (!user) throw new AuthError("not_found", "Entre na sua conta primeiro.");
  try {
    await reloadUser(user);
    if (user.emailVerified) return;
    await sendEmailVerification(user);
  } catch (error) {
    throw toAuthError(error);
  }
}

/** Confere a senha atual do usuário (reautenticação). */
async function requirePassword(password: string) {
  const user = auth.currentUser;
  if (!user || !user.email) throw new AuthError("not_found", "Entre na sua conta primeiro.");
  const provider = user.providerData.some((item) => item.providerId === "google.com")
    ? await signInWithPopup(auth, new GoogleAuthProvider())
    : null;
  const current = provider?.user ?? user;
  if (!current.email) return;
  if (!current.providerData.some((item) => item.providerId === "password")) return;
  try {
    await reauthenticateWithCredential(
      current,
      EmailAuthProvider.credential(current.email, password),
    );
  } catch (error) {
    const translated = toAuthError(error);
    if (translated.code === "wrong_password" || translated.code === "not_found") {
      throw new AuthError("wrong_password", "A senha atual está incorreta.");
    }
    throw translated;
  }
}

export async function changePassword(current: string, next: string): Promise<void> {
  const user = auth.currentUser;
  if (!user) throw new AuthError("not_found", "Entre na sua conta primeiro.");
  if (next.length < MIN_PASSWORD) {
    throw new AuthError("weak_password", `A nova senha precisa de pelo menos ${MIN_PASSWORD} caracteres.`);
  }
  try {
    await requirePassword(current);
    const { updatePassword } = await import("firebase/auth");
    await updatePassword(user, next);
    await pushActivity("seguranca", "Senha alterada");
  } catch (error) {
    throw toAuthError(error);
  }
}

export async function deleteAccount(password: string): Promise<void> {
  const user = auth.currentUser;
  if (!user) throw new AuthError("not_found", "Entre na sua conta primeiro.");
  try {
    await requirePassword(password);
    await deleteDoc(userDoc(user.uid)).catch(() => undefined);
    await deleteUser(user);
  } catch (error) {
    throw toAuthError(error);
  }
}

export async function updateProfile(patch: {
  name?: string;
  email?: string;
  color?: string;
}): Promise<void> {
  const user = auth.currentUser;
  if (!user) throw new AuthError("not_found", "Entre na sua conta primeiro.");
  const payload: Record<string, unknown> = {};
  const changes: string[] = [];

  if (patch.name !== undefined) {
    const name = patch.name.trim().slice(0, 80);
    if (name.length < 2) throw new AuthError("invalid_input", "Digite como você quer ser chamado.");
    payload.name = name;
    changes.push("nome");
    await updateAuthProfile(user, { displayName: name }).catch(() => undefined);
  }
  if (patch.color !== undefined) {
    payload.color = patch.color;
    changes.push("cor do avatar");
  }
  if (patch.email !== undefined) {
    const email = normalizeEmail(patch.email);
    if (!isEmail(email)) throw new AuthError("invalid_email", "Digite um e-mail válido.");
    if (email !== user.email) {
      try {
        // O Firebase exige login recente para trocar o e-mail.
        await updateEmail(user, email);
      } catch (error) {
        throw toAuthError(error);
      }
      payload.email = email;
      changes.push("e-mail");
      sendEmailVerification(user).catch(() => undefined);
    }
  }

  try {
    await patchUser(payload);
    if (changes.length) await pushActivity("perfil", `Perfil atualizado (${changes.join(", ")})`);
  } catch (error) {
    throw toAuthError(error);
  }
}

/** Salva apelidos de apps (o app Windows importa estes). */
export async function saveAliases(aliases: Alias[]): Promise<void> {
  const clean = aliases
    .map((item) => ({ alias: item.alias.trim().slice(0, 40), target: item.target.trim().slice(0, 260) }))
    .filter((item) => item.alias && item.target)
    .slice(0, 100);
  if (clean.length !== aliases.length) {
    throw new AuthError("invalid_input", "Preencha apelido e app, e mantenha até 100 itens.");
  }
  await patchUser({ aliases: clean }).catch((error) => {
    throw toAuthError(error);
  });
}

/** Salva atalhos de texto (frase que se expande no app). */
export async function saveShortcuts(shortcuts: Shortcut[]): Promise<void> {
  const clean = shortcuts
    .map((item) => ({
      trigger: item.trigger.trim().slice(0, 40),
      expansion: item.expansion.trim().slice(0, 400),
    }))
    .filter((item) => item.trigger && item.expansion)
    .slice(0, 100);
  if (clean.length !== shortcuts.length) {
    throw new AuthError("invalid_input", "Preencha gatilho e texto, e mantenha até 100 atalhos.");
  }
  await patchUser({ shortcuts: clean }).catch((error) => {
    throw toAuthError(error);
  });
}

/** Salva rotinas (sequência de comandos do app). */
export async function saveRoutines(routines: Routine[]): Promise<void> {
  const clean = routines
    .map((item) => ({
      name: item.name.trim().slice(0, 60),
      commands: item.commands.map((command) => command.trim().slice(0, 120)).filter(Boolean).slice(0, 12),
    }))
    .filter((item) => item.name && item.commands.length > 0)
    .slice(0, 60);
  if (clean.length !== routines.length) {
    throw new AuthError("invalid_input", "Cada rotina precisa de nome e pelo menos um comando.");
  }
  await patchUser({ routines: clean }).catch((error) => {
    throw toAuthError(error);
  });
}

/** Preferências do app que o site consegue editar. */
export async function saveAppPrefs(prefs: AppPrefs): Promise<void> {
  const clean: AppPrefs = {
    startWithWindows: Boolean(prefs.startWithWindows),
    volume: Math.max(0, Math.min(100, Math.round(Number(prefs.volume) || 0))),
    lockEnabled: Boolean(prefs.lockEnabled),
    lockAfterMinutes: Math.max(1, Math.min(120, Math.round(Number(prefs.lockAfterMinutes) || 5))),
    geminiModel: prefs.geminiModel.slice(0, 40),
  };
  await patchUser({ appPrefs: clean }).catch((error) => {
    throw toAuthError(error);
  });
}

export function setSync(sync: SyncPrefs): Promise<void> {
  return patchUser({ sync }).catch((error) => {
    throw toAuthError(error);
  });
}

/* ------------------------------------------------------------------ */
/* Aparelhos: o app mostra um código ORBI-XXXX-XXXX; o último caractere
 * é um dígito verificador, então o site recusa erro de digitação.     */
/* ------------------------------------------------------------------ */
function checkChar(body: string) {
  let sum = 0;
  for (let i = 0; i < body.length; i += 1) sum += (i + 1) * ALPHABET.indexOf(body[i]);
  return ALPHABET[sum % ALPHABET.length];
}

export function normalizePairingCode(input: string): string | null {
  let clean = input.toUpperCase().replace(/[^A-Z0-9]/g, "");
  if (clean.startsWith("ORBI")) clean = clean.slice(4);
  if (clean.length !== 8) return null;
  if ([...clean].some((char) => !ALPHABET.includes(char))) return null;
  if (checkChar(clean.slice(0, 7)) !== clean[7]) return null;
  return `ORBI-${clean.slice(0, 4)}-${clean.slice(4)}`;
}

export function formatPairingInput(input: string): string {
  let clean = input.toUpperCase().replace(/[^A-Z0-9]/g, "");
  if (clean.startsWith("ORBI")) clean = clean.slice(4);
  clean = clean.slice(0, 8);
  return clean.length > 4 ? `${clean.slice(0, 4)}-${clean.slice(4)}` : clean;
}

export function generateSampleCode(): string {
  const body = randomChars(7);
  const full = body + checkChar(body);
  return `ORBI-${full.slice(0, 4)}-${full.slice(4)}`;
}

export async function linkDevice(rawCode: string, name: string): Promise<void> {
  const user = auth.currentUser;
  if (!user) throw new AuthError("not_found", "Entre na sua conta primeiro.");
  const code = normalizePairingCode(rawCode);
  if (!code) throw new AuthError("invalid_code", "Código inválido. Confira os 8 caracteres exibidos no app.");
  const snapshot = await getDoc(userDoc(user.uid));
  const devices = (snapshot.data()?.devices as Device[] | undefined) ?? [];
  if (devices.some((device) => device.code === code)) {
    throw new AuthError("device_exists", "Este código já foi usado para vincular um aparelho.");
  }
  if (devices.length >= 20) throw new AuthError("invalid_input", "Limite de 20 aparelhos por conta.");
  const device: Device = {
    id: randomChars(8),
    name: name.trim().slice(0, 60) || "Meu PC",
    os: "Windows 11",
    linkedAt: new Date().toISOString(),
    code,
  };
  await patchUser({ devices: [device, ...devices] });
  await pushActivity("dispositivo", `App vinculado: ${device.name}`);
}

export async function removeDevice(deviceId: string): Promise<void> {
  const user = auth.currentUser;
  if (!user) throw new AuthError("not_found", "Entre na sua conta primeiro.");
  const snapshot = await getDoc(userDoc(user.uid));
  const devices = (snapshot.data()?.devices as Device[] | undefined) ?? [];
  const device = devices.find((item) => item.id === deviceId);
  await patchUser({ devices: devices.filter((item) => item.id !== deviceId) });
  if (device) await pushActivity("dispositivo", `App desvinculado: ${device.name}`);
}

export async function renameDevice(deviceId: string, name: string): Promise<void> {
  const user = auth.currentUser;
  if (!user) throw new AuthError("not_found", "Entre na sua conta primeiro.");
  const clean = name.trim().slice(0, 60);
  if (!clean) throw new AuthError("invalid_input", "Digite um nome para o aparelho.");
  const snapshot = await getDoc(userDoc(user.uid));
  const devices = (snapshot.data()?.devices as Device[] | undefined) ?? [];
  await patchUser({ devices: devices.map((item) => (item.id === deviceId ? { ...item, name: clean } : item)) });
}

/* ------------------------------------------------------------------ */
/* Downloads                                                          */
/* ------------------------------------------------------------------ */
export async function addDownload(record: {
  version: string;
  checksum: string;
  filename: string;
  url?: string;
}): Promise<void> {
  const user = auth.currentUser;
  if (!user) return; // visitante baixa sem conta, sem erro.
  const snapshot = await getDoc(userDoc(user.uid));
  const downloads = (snapshot.data()?.downloads as DownloadRecord[] | undefined) ?? [];
  const next = [
    { id: randomChars(8), at: new Date().toISOString(), ...record },
    ...downloads,
  ].slice(0, 30);
  await patchUser({ downloads: next }).catch(() => undefined);
  await pushActivity("download", `Download do instalador ${record.version}`).catch(() => undefined);
}

export function exportAccount(account: Account): string {
  return JSON.stringify(
    {
      exportadoEm: new Date().toISOString(),
      conta: {
        id: account.id,
        nome: account.name,
        email: account.email,
        criadaEm: account.createdAt,
      },
      sincronizacao: account.sync,
      aparelhos: account.devices.map(({ name, os, linkedAt }) => ({ name, os, linkedAt })),
      downloads: account.downloads,
      atividade: account.activity,
    },
    null,
    2,
  );
}

/* ------------------------------------------------------------------ */
/* Formatação                                                         */
/* ------------------------------------------------------------------ */
export function firstName(name: string) {
  return name.trim().split(/\s+/)[0] || "você";
}

export function formatDate(iso: string) {
  try {
    return new Intl.DateTimeFormat("pt-BR", { day: "2-digit", month: "short", year: "numeric" }).format(
      new Date(iso),
    );
  } catch {
    return "";
  }
}

export function formatDateTime(iso: string) {
  try {
    return new Intl.DateTimeFormat("pt-BR", {
      day: "2-digit",
      month: "short",
      hour: "2-digit",
      minute: "2-digit",
    }).format(new Date(iso));
  } catch {
    return "";
  }
}

export function relativeTime(iso: string) {
  const diff = Date.now() - new Date(iso).getTime();
  const minutes = Math.round(diff / 60000);
  if (minutes < 1) return "agora";
  if (minutes < 60) return `há ${minutes} min`;
  const hours = Math.round(minutes / 60);
  if (hours < 24) return `há ${hours} h`;
  const days = Math.round(hours / 24);
  if (days === 1) return "ontem";
  if (days < 7) return `há ${days} dias`;
  return formatDate(iso);
}

export function passwordStrength(password: string): { score: 0 | 1 | 2 | 3 | 4; label: string } {
  if (!password) return { score: 0, label: "" };
  let score = 0;
  if (password.length >= MIN_PASSWORD) score += 1;
  if (password.length >= 12) score += 1;
  if (/[a-z]/.test(password) && /[A-Z]/.test(password)) score += 1;
  if (/\d/.test(password) && /[^A-Za-z0-9]/.test(password)) score += 1;
  const clamped = Math.min(4, score) as 0 | 1 | 2 | 3 | 4;
  const labels = ["Muito curta", "Fraca", "Razoável", "Boa", "Forte"];
  return {
    score: password.length < MIN_PASSWORD ? 0 : clamped,
    label: password.length < MIN_PASSWORD ? labels[0] : labels[clamped],
  };
}

