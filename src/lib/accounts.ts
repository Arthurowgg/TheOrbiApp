/* ------------------------------------------------------------------ *
 * Contas NOVA
 *
 * Esta versão do site não tem servidor: contas, sessão, aparelhos e
 * histórico ficam salvos NESTE navegador. As senhas nunca são guardadas
 * em texto — só um hash PBKDF2 com sal próprio.
 *
 * Toda a interface fala com este módulo; quando existir um serviço
 * online, só ele precisa ser trocado.
 * ------------------------------------------------------------------ */

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
};

export type Activity = {
  id: string;
  at: string;
  type: ActivityType;
  text: string;
};

export type Account = {
  id: string;
  name: string;
  email: string;
  createdAt: string;
  color: string;
  passwordHash: string;
  passwordSalt: string;
  passwordChangedAt: string;
  recoveryHash: string;
  recoverySalt: string;
  recoveryCreatedAt: string;
  sync: SyncPrefs;
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

export type AuthErrorCode =
  | "not_found"
  | "wrong_password"
  | "email_taken"
  | "invalid_input"
  | "wrong_recovery"
  | "invalid_code"
  | "device_exists"
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

const DB_KEY = "nova.db.v2";
const SESSION_KEY = "nova.session.v2";
const encoder = new TextEncoder();

const toHex = (bytes: Uint8Array) =>
  Array.from(bytes, (b) => b.toString(16).padStart(2, "0")).join("");

function randomBytes(size: number): Uint8Array {
  const out = new Uint8Array(size);
  globalThis.crypto.getRandomValues(out);
  return out;
}

function randomHex(size: number) {
  return toHex(randomBytes(size));
}

function randomChars(size: number) {
  return Array.from(randomBytes(size), (b) => ALPHABET[b % ALPHABET.length]).join("");
}

export function normalizeEmail(email: string) {
  return email.trim().toLowerCase();
}

export function isEmail(email: string) {
  return /^[^\s@]+@[^\s@]+\.[^\s@]{2,}$/.test(email.trim());
}

async function hashSecret(secret: string, salt: string): Promise<string> {
  const subtle = globalThis.crypto?.subtle;
  if (subtle) {
    const key = await subtle.importKey("raw", encoder.encode(secret), "PBKDF2", false, ["deriveBits"]);
    const bits = await subtle.deriveBits(
      { name: "PBKDF2", salt: encoder.encode(salt), iterations: 120_000, hash: "SHA-256" },
      key,
      256,
    );
    return `p1:${toHex(new Uint8Array(bits))}`;
  }
  // Contexto sem WebCrypto (HTTP simples): hash simples, só para não guardar texto puro.
  let h1 = 0x811c9dc5;
  let h2 = 0x01000193;
  const input = `${salt}:${secret}`;
  for (let round = 0; round < 2000; round += 1) {
    for (let i = 0; i < input.length; i += 1) {
      h1 = Math.imul(h1 ^ input.charCodeAt(i), 16777619);
      h2 = (h2 + h1) | 0;
    }
  }
  return `f1:${(h1 >>> 0).toString(16)}${(h2 >>> 0).toString(16)}`;
}

/* ------------------------------------------------------------------ */
/* Armazenamento                                                      */
/* ------------------------------------------------------------------ */
type Db = { version: 2; accounts: Account[] };

function hydrate(raw: Partial<Account>): Account {
  const now = new Date().toISOString();
  return {
    id: raw.id ?? `NV-${randomChars(6)}`,
    name: raw.name ?? "Você",
    email: raw.email ?? "",
    createdAt: raw.createdAt ?? now,
    color: raw.color ?? AVATAR_COLORS[0],
    passwordHash: raw.passwordHash ?? "",
    passwordSalt: raw.passwordSalt ?? "",
    passwordChangedAt: raw.passwordChangedAt ?? raw.createdAt ?? now,
    recoveryHash: raw.recoveryHash ?? "",
    recoverySalt: raw.recoverySalt ?? "",
    recoveryCreatedAt: raw.recoveryCreatedAt ?? raw.createdAt ?? now,
    sync: { ...DEFAULT_SYNC, ...(raw.sync ?? {}) },
    devices: Array.isArray(raw.devices) ? raw.devices : [],
    downloads: Array.isArray(raw.downloads) ? raw.downloads : [],
    activity: Array.isArray(raw.activity) ? raw.activity : [],
  };
}

function readDb(): Db {
  try {
    const raw = window.localStorage.getItem(DB_KEY);
    if (raw) {
      const parsed = JSON.parse(raw) as { accounts?: Partial<Account>[] };
      if (parsed && Array.isArray(parsed.accounts)) {
        return { version: 2, accounts: parsed.accounts.map(hydrate) };
      }
    }
  } catch {
    // Leitura falhou: começamos vazio.
  }
  return { version: 2, accounts: [] };
}

function writeDb(db: Db) {
  try {
    window.localStorage.setItem(DB_KEY, JSON.stringify(db));
  } catch {
    throw new AuthError("storage", "Não foi possível salvar neste navegador. Verifique se o armazenamento está liberado.");
  }
}

function mutate(id: string, change: (account: Account) => void): Account {
  const db = readDb();
  const account = db.accounts.find((item) => item.id === id);
  if (!account) throw new AuthError("not_found", "Conta não encontrada.");
  change(account);
  writeDb(db);
  return account;
}

function pushActivity(account: Account, type: ActivityType, text: string) {
  account.activity.unshift({
    id: randomChars(8),
    at: new Date().toISOString(),
    type,
    text,
  });
  account.activity = account.activity.slice(0, 40);
}

/* ------------------------------------------------------------------ */
/* Sessão                                                             */
/* ------------------------------------------------------------------ */
export function startSession(id: string, remember: boolean) {
  endSession();
  try {
    (remember ? window.localStorage : window.sessionStorage).setItem(SESSION_KEY, id);
  } catch {
    // Sem armazenamento: a sessão vive só em memória nesta aba.
  }
}

export function endSession() {
  try {
    window.localStorage.removeItem(SESSION_KEY);
    window.sessionStorage.removeItem(SESSION_KEY);
  } catch {
    // Nada a fazer.
  }
}

export function sessionMode(): "persistente" | "temporária" {
  try {
    return window.sessionStorage.getItem(SESSION_KEY) ? "temporária" : "persistente";
  } catch {
    return "temporária";
  }
}

export function getCurrentAccount(): Account | null {
  try {
    const id = window.sessionStorage.getItem(SESSION_KEY) ?? window.localStorage.getItem(SESSION_KEY);
    if (!id) return null;
    return readDb().accounts.find((account) => account.id === id) ?? null;
  } catch {
    return null;
  }
}

/* ------------------------------------------------------------------ */
/* Cadastro, entrada e recuperação                                    */
/* ------------------------------------------------------------------ */
export const MIN_PASSWORD = 8;

export function formatRecoveryCode(raw: string) {
  const clean = raw.toUpperCase().replace(/[^A-Z0-9]/g, "").slice(0, 12);
  return clean.match(/.{1,4}/g)?.join("-") ?? "";
}

function normalizeRecovery(code: string) {
  return code.toUpperCase().replace(/[^A-Z0-9]/g, "");
}

export async function signUp(input: {
  name: string;
  email: string;
  password: string;
  remember: boolean;
}): Promise<{ account: Account; recoveryCode: string }> {
  const email = normalizeEmail(input.email);
  if (input.name.trim().length < 2 || !isEmail(email) || input.password.length < MIN_PASSWORD) {
    throw new AuthError("invalid_input", "Confira os dados informados.");
  }
  const db = readDb();
  if (db.accounts.some((account) => account.email === email)) {
    throw new AuthError("email_taken", "Já existe uma conta com este e-mail.");
  }

  const now = new Date().toISOString();
  const passwordSalt = randomHex(16);
  const recoverySalt = randomHex(16);
  const recoveryCode = formatRecoveryCode(randomChars(12));

  const account: Account = {
    id: `NV-${randomChars(6)}`,
    name: input.name.trim(),
    email,
    createdAt: now,
    color: AVATAR_COLORS[randomBytes(1)[0] % AVATAR_COLORS.length],
    passwordSalt,
    passwordHash: await hashSecret(input.password, passwordSalt),
    passwordChangedAt: now,
    recoverySalt,
    recoveryHash: await hashSecret(normalizeRecovery(recoveryCode), recoverySalt),
    recoveryCreatedAt: now,
    sync: { ...DEFAULT_SYNC },
    devices: [],
    downloads: [],
    activity: [],
  };
  pushActivity(account, "conta", "Conta criada");
  db.accounts.push(account);
  writeDb(db);
  startSession(account.id, input.remember);
  return { account, recoveryCode };
}

export async function verifyPassword(account: Account, password: string) {
  return (await hashSecret(password, account.passwordSalt)) === account.passwordHash;
}

export async function signIn(input: {
  email: string;
  password: string;
  remember: boolean;
}): Promise<Account> {
  const email = normalizeEmail(input.email);
  const account = readDb().accounts.find((item) => item.email === email);
  if (!account) throw new AuthError("not_found", "Não encontramos uma conta com este e-mail.");
  if (!(await verifyPassword(account, input.password))) {
    throw new AuthError("wrong_password", "Senha incorreta.");
  }
  const updated = mutate(account.id, (item) => pushActivity(item, "login", "Entrou neste navegador"));
  startSession(updated.id, input.remember);
  return updated;
}

export async function recoverPassword(input: {
  email: string;
  code: string;
  password: string;
}): Promise<{ account: Account; recoveryCode: string }> {
  const email = normalizeEmail(input.email);
  const account = readDb().accounts.find((item) => item.email === email);
  if (!account) throw new AuthError("not_found", "Não encontramos uma conta com este e-mail.");
  if (input.password.length < MIN_PASSWORD) {
    throw new AuthError("invalid_input", "A nova senha é curta demais.");
  }
  const attempt = await hashSecret(normalizeRecovery(input.code), account.recoverySalt);
  if (attempt !== account.recoveryHash) {
    throw new AuthError("wrong_recovery", "Código de recuperação incorreto.");
  }

  // O código é de uso único: depois de usado, geramos um novo.
  const passwordSalt = randomHex(16);
  const recoverySalt = randomHex(16);
  const recoveryCode = formatRecoveryCode(randomChars(12));
  const passwordHash = await hashSecret(input.password, passwordSalt);
  const recoveryHash = await hashSecret(normalizeRecovery(recoveryCode), recoverySalt);
  const now = new Date().toISOString();

  const updated = mutate(account.id, (item) => {
    item.passwordSalt = passwordSalt;
    item.passwordHash = passwordHash;
    item.passwordChangedAt = now;
    item.recoverySalt = recoverySalt;
    item.recoveryHash = recoveryHash;
    item.recoveryCreatedAt = now;
    pushActivity(item, "seguranca", "Senha redefinida com o código de recuperação");
  });
  startSession(updated.id, true);
  return { account: updated, recoveryCode };
}

/* ------------------------------------------------------------------ */
/* Perfil e segurança                                                 */
/* ------------------------------------------------------------------ */
export function updateProfile(
  id: string,
  patch: { name?: string; email?: string; color?: string },
): Account {
  const email = patch.email !== undefined ? normalizeEmail(patch.email) : undefined;
  if (patch.name !== undefined && patch.name.trim().length < 2) {
    throw new AuthError("invalid_input", "Digite como você quer ser chamado.");
  }
  if (email !== undefined && !isEmail(email)) {
    throw new AuthError("invalid_input", "Digite um e-mail válido.");
  }
  const others = readDb().accounts.filter((account) => account.id !== id);
  if (email && others.some((account) => account.email === email)) {
    throw new AuthError("email_taken", "Este e-mail já está em uso por outra conta.");
  }
  return mutate(id, (account) => {
    const changes: string[] = [];
    if (patch.name !== undefined && patch.name.trim() !== account.name) {
      account.name = patch.name.trim();
      changes.push("nome");
    }
    if (email !== undefined && email !== account.email) {
      account.email = email;
      changes.push("e-mail");
    }
    if (patch.color !== undefined && patch.color !== account.color) {
      account.color = patch.color;
      changes.push("cor do avatar");
    }
    if (changes.length) pushActivity(account, "perfil", `Perfil atualizado (${changes.join(", ")})`);
  });
}

export async function changePassword(id: string, current: string, next: string): Promise<Account> {
  const account = readDb().accounts.find((item) => item.id === id);
  if (!account) throw new AuthError("not_found", "Conta não encontrada.");
  if (!(await verifyPassword(account, current))) {
    throw new AuthError("wrong_password", "A senha atual está incorreta.");
  }
  if (next.length < MIN_PASSWORD) {
    throw new AuthError("invalid_input", `A nova senha precisa de pelo menos ${MIN_PASSWORD} caracteres.`);
  }
  const salt = randomHex(16);
  const hash = await hashSecret(next, salt);
  return mutate(id, (item) => {
    item.passwordSalt = salt;
    item.passwordHash = hash;
    item.passwordChangedAt = new Date().toISOString();
    pushActivity(item, "seguranca", "Senha alterada");
  });
}

export async function regenerateRecovery(
  id: string,
  password: string,
): Promise<{ account: Account; recoveryCode: string }> {
  const account = readDb().accounts.find((item) => item.id === id);
  if (!account) throw new AuthError("not_found", "Conta não encontrada.");
  if (!(await verifyPassword(account, password))) {
    throw new AuthError("wrong_password", "Senha incorreta.");
  }
  const recoveryCode = formatRecoveryCode(randomChars(12));
  const salt = randomHex(16);
  const hash = await hashSecret(normalizeRecovery(recoveryCode), salt);
  const updated = mutate(id, (item) => {
    item.recoverySalt = salt;
    item.recoveryHash = hash;
    item.recoveryCreatedAt = new Date().toISOString();
    pushActivity(item, "seguranca", "Novo código de recuperação gerado");
  });
  return { account: updated, recoveryCode };
}

export async function deleteAccount(id: string, password: string) {
  const db = readDb();
  const account = db.accounts.find((item) => item.id === id);
  if (!account) throw new AuthError("not_found", "Conta não encontrada.");
  if (!(await verifyPassword(account, password))) {
    throw new AuthError("wrong_password", "Senha incorreta.");
  }
  db.accounts = db.accounts.filter((item) => item.id !== id);
  writeDb(db);
  endSession();
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

export function setSync(id: string, sync: SyncPrefs): Account {
  return mutate(id, (account) => {
    account.sync = sync;
    pushActivity(account, "perfil", "Preferências de sincronização atualizadas");
  });
}

/* ------------------------------------------------------------------ */
/* Aparelhos: o app mostra um código NOVA-XXXX-XXXX; o último caractere
 * é um dígito verificador, assim o site recusa erros de digitação.    */
/* ------------------------------------------------------------------ */
function checkChar(body: string) {
  let sum = 0;
  for (let i = 0; i < body.length; i += 1) sum += (i + 1) * ALPHABET.indexOf(body[i]);
  return ALPHABET[sum % ALPHABET.length];
}

export function normalizePairingCode(input: string): string | null {
  let clean = input.toUpperCase().replace(/[^A-Z0-9]/g, "");
  if (clean.startsWith("NOVA")) clean = clean.slice(4);
  if (clean.length !== 8) return null;
  if ([...clean].some((char) => !ALPHABET.includes(char))) return null;
  if (checkChar(clean.slice(0, 7)) !== clean[7]) return null;
  return `NOVA-${clean.slice(0, 4)}-${clean.slice(4)}`;
}

export function formatPairingInput(input: string): string {
  let clean = input.toUpperCase().replace(/[^A-Z0-9]/g, "");
  if (clean.startsWith("NOVA")) clean = clean.slice(4);
  clean = clean.slice(0, 8);
  return clean.length > 4 ? `${clean.slice(0, 4)}-${clean.slice(4)}` : clean;
}

export function generateSampleCode(): string {
  const body = randomChars(7);
  const full = body + checkChar(body);
  return `NOVA-${full.slice(0, 4)}-${full.slice(4)}`;
}

export function linkDevice(id: string, rawCode: string, name: string): Account {
  const code = normalizePairingCode(rawCode);
  if (!code) {
    throw new AuthError("invalid_code", "Código inválido. Confira os 8 caracteres exibidos no app.");
  }
  const deviceName = name.trim() || "Meu PC";
  return mutate(id, (account) => {
    if (account.devices.some((device) => device.code === code)) {
      throw new AuthError("device_exists", "Este código já foi usado para vincular um aparelho.");
    }
    account.devices.push({
      id: randomChars(8),
      name: deviceName,
      os: "Windows 11",
      linkedAt: new Date().toISOString(),
      code,
    });
    pushActivity(account, "dispositivo", `App vinculado: ${deviceName}`);
  });
}

export function renameDevice(id: string, deviceId: string, name: string): Account {
  const clean = name.trim();
  if (!clean) throw new AuthError("invalid_input", "Digite um nome para o aparelho.");
  return mutate(id, (account) => {
    const device = account.devices.find((item) => item.id === deviceId);
    if (device) device.name = clean;
  });
}

export function removeDevice(id: string, deviceId: string): Account {
  return mutate(id, (account) => {
    const device = account.devices.find((item) => item.id === deviceId);
    account.devices = account.devices.filter((item) => item.id !== deviceId);
    if (device) pushActivity(account, "dispositivo", `App desvinculado: ${device.name}`);
  });
}

/* ------------------------------------------------------------------ */
/* Downloads                                                          */
/* ------------------------------------------------------------------ */
export function addDownload(
  id: string,
  record: { version: string; checksum: string; filename: string },
): Account {
  return mutate(id, (account) => {
    account.downloads.unshift({
      id: randomChars(8),
      at: new Date().toISOString(),
      ...record,
    });
    account.downloads = account.downloads.slice(0, 20);
    pushActivity(account, "download", `Download da versão ${record.version}`);
  });
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
  return { score: password.length < MIN_PASSWORD ? 0 : clamped, label: password.length < MIN_PASSWORD ? labels[0] : labels[clamped] };
}
