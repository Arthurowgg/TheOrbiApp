/* ------------------------------------------------------------------ *
 * Download do Orbi
 *
 * O site não "gera um zip": ele entrega o instalador real (OrbiSetup.exe)
 * publicado na última release do GitHub. O build de CI
 * (.github/workflows/release.yml) compila o app Windows, embala em
 * Inno Setup e sobe o .exe + SHA256SUMS como release.
 *
 * Se a release ainda não existir (ou o GitHub estiver fora do ar), o
 * download cai para um pacote .zip de diagnóstico com as instruções —
 * a pessoa nunca fica com um botão morto.
 * ------------------------------------------------------------------ */
import JSZip from "jszip";
import { DOWNLOAD_BASE } from "./firebase";
import type { Account } from "./accounts";

export const BUILD = {
  product: "Orbi",
  version: "1.0.3",
  channel: "estável",
  os: "Windows 11",
  arch: "x64",
  minimum: "Windows 11 22H2 ou superior (Windows 10 1809+)",
  installer: "OrbiSetup-1.0.3.exe",
  packageName: "Orbi-1.0.3",
  sizeHint: "≈ 64 MB",
  repo: "Arthurowgg/TheOrbiApp",
};

export type ReleaseAsset = { name: string; url: string; size: number };
export type Release = {
  tag: string;
  version: string;
  publishedAt: string;
  notes: string;
  installer: ReleaseAsset | null;
  checksumsUrl: string | null;
};

const API = `https://api.github.com/repos/${BUILD.repo}/releases/latest`;

/** Lê a última release publicada. Cacheado em memória por 10 min. */
let cache: { at: number; value: Release | null } | null = null;

export async function fetchLatestRelease(force = false): Promise<Release | null> {
  if (!force && cache && Date.now() - cache.at < 600_000) return cache.value;
  try {
    const response = await fetch(API, { headers: { Accept: "application/vnd.github+json" } });
    if (!response.ok) throw new Error(String(response.status));
    const data = (await response.json()) as {
      tag_name?: string;
      name?: string;
      published_at?: string;
      body?: string;
      assets?: { name: string; browser_download_url: string; size: number }[];
    };
    const assets = data.assets ?? [];
    const installer =
      assets.find((asset) => /setup.*\.exe$/i.test(asset.name)) ??
      assets.find((asset) => /\.exe$/i.test(asset.name)) ??
      null;
    const value: Release = {
      tag: data.tag_name ?? "latest",
      version: (data.tag_name ?? "").replace(/^v/, "") || BUILD.version,
      publishedAt: data.published_at ?? "",
      notes: data.body ?? "",
      installer: installer
        ? { name: installer.name, url: installer.browser_download_url, size: installer.size }
        : null,
      checksumsUrl: assets.find((asset) => /sha256/i.test(asset.name))?.browser_download_url ?? null,
    };
    cache = { at: Date.now(), value };
    return value;
  } catch {
    cache = { at: Date.now(), value: null };
    return null;
  }
}

/** URL direta do instalador: release real ou página de releases. */
export function installerUrl(release: Release | null) {
  return release?.installer?.url ?? `${DOWNLOAD_BASE}`;
}

export function humanSize(bytes: number) {
  if (!bytes) return BUILD.sizeHint;
  const mb = bytes / 1024 / 1024;
  return `${mb.toFixed(1).replace(".", ",")} MB`;
}

/* ------------------------------------------------------------------ */
/* Fallback: pacote de diagnóstico (sem fingir que é o app)           */
/* ------------------------------------------------------------------ */
function readme(account: Pick<Account, "name" | "email"> | null, release: Release | null) {
  return `ORBI ${BUILD.version} — Windows ${BUILD.arch}
Canal ${BUILD.channel}

O instalador oficial é o arquivo ${BUILD.installer}, publicado em:
${DOWNLOAD_BASE}

Este pacote é o plano B: ele existe porque a leitura da release falhou
(provavelmente o build automático do GitHub Actions ainda estava rodando).
Abra o link acima em alguns minutos e baixe o OrbiSetup.exe.

${
  account
    ? `Conta deste download: ${account.name} <${account.email}>\nO app abre já com a sua conta vinculada quando você entra com o mesmo e-mail.`
    : `Este download não está ligado a nenhuma conta. Você pode criar uma em\n${"https://orbi-app-auth.web.app"} e entrar no app com o mesmo e-mail.`
}

REQUISITOS
- ${BUILD.minimum}
- Arquitetura x64
- 300 MB livres em disco

USO
- Alt+Space abre a barra de comando em qualquer programa.
- Esc fecha a barra.
- A conta é a mesma do site (Firebase Authentication).

VERIFICAÇÃO
- Confira o hash do instalador em SHA256SUMS.txt na mesma release.
${release ? `\nRELEASE ATUAL: ${release.tag}` : "\nRELEASE ATUAL: indisponível no momento"}
`;
}

async function sha256(buffer: ArrayBuffer): Promise<string> {
  const subtle = globalThis.crypto?.subtle;
  if (!subtle) return "indisponível";
  const digest = await subtle.digest("SHA-256", buffer);
  return Array.from(new Uint8Array(digest), (b) => b.toString(16).padStart(2, "0")).join("");
}

export type PackageResult = { blob: Blob; filename: string; checksum: string };

export async function buildFallbackPackage(
  account: Pick<Account, "name" | "email"> | null,
  release: Release | null,
): Promise<PackageResult> {
  const zip = new JSZip();
  zip.file("LEIA-ME.txt", readme(account, release));
  zip.file(
    "VERSAO.txt",
    `Orbi\nversão ${BUILD.version}\ncanal ${BUILD.channel}\nsistema ${BUILD.os} ${BUILD.arch}\n`,
  );
  zip.file(
    "BAIXAR-O-INSTALADOR.url",
    `[InternetShortcut]\nURL=${DOWNLOAD_BASE}\n`,
  );
  const blob = await zip.generateAsync({ type: "blob", compression: "DEFLATE" });
  const checksum = await sha256(await blob.arrayBuffer());
  return { blob, filename: `${BUILD.packageName}-instrucoes.zip`, checksum };
}

/* ------------------------------------------------------------------ */
/* Download efetivo                                                    */
/* ------------------------------------------------------------------ */
export function saveBlob(blob: Blob, filename: string) {
  const url = URL.createObjectURL(blob);
  const anchor = document.createElement("a");
  anchor.href = url;
  anchor.download = filename;
  document.body.appendChild(anchor);
  anchor.click();
  anchor.remove();
  window.setTimeout(() => URL.revokeObjectURL(url), 4000);
}

export type DownloadOutcome =
  | { kind: "installer"; filename: string; version: string; checksum: string; url: string }
  | { kind: "fallback"; filename: string; version: string; checksum: string };

/**
 * Baixa o instalador real com progresso. Se o arquivo da release existir,
 * buscamos como blob (para dar barra de progresso e calcular o SHA-256);
 * se o download direto for bloqueado por CORS/rede, abrimos a release.
 * Sem release publicada, entrega o pacote de instruções.
 */
export async function downloadInstaller(
  account: Pick<Account, "name" | "email"> | null,
  onProgress: (percent: number) => void,
): Promise<DownloadOutcome> {
  const release = await fetchLatestRelease();
  onProgress(6);

  if (release?.installer) {
    const { url, name, size } = release.installer;
    try {
      const response = await fetch(url, { redirect: "follow" });
      if (!response.ok || !response.body) throw new Error(String(response.status));
      const total = Number(response.headers.get("content-length")) || size || 0;
      const reader = response.body.getReader();
      const chunks: Uint8Array[] = [];
      let received = 0;
      for (;;) {
        const { done, value } = await reader.read();
        if (done) break;
        if (value) {
          chunks.push(value);
          received += value.byteLength;
          if (total) onProgress(6 + Math.min(90, Math.round((received / total) * 90)));
        }
      }
      const blob = new Blob(chunks as BlobPart[], { type: "application/octet-stream" });
      const checksum = await sha256(await blob.arrayBuffer());
      onProgress(100);
      saveBlob(blob, name);
      return {
        kind: "installer",
        filename: name,
        version: release.version,
        checksum,
        url,
      };
    } catch {
      // Download bloqueado pelo navegador: abre direto na release.
      onProgress(100);
      window.open(url, "_blank", "noopener");
      return {
        kind: "installer",
        filename: name,
        version: release.version,
        checksum: "veja SHA256SUMS na release",
        url,
      };
    }
  }

  const pkg = await buildFallbackPackage(account, release);
  onProgress(100);
  saveBlob(pkg.blob, pkg.filename);
  return { kind: "fallback", filename: pkg.filename, version: BUILD.version, checksum: pkg.checksum };
}

export function shortChecksum(checksum: string) {
  if (!checksum || checksum === "indisponível") return "indisponível";
  if (checksum.length < 20) return checksum;
  return `${checksum.slice(0, 10)}…${checksum.slice(-6)}`;
}
