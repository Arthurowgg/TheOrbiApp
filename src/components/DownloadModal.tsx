import { useEffect, useRef, useState } from "react";
import { Button, Icon, Mark, Modal } from "./ui";
import {
  BUILD,
  downloadInstaller,
  fetchLatestRelease,
  humanSize,
  shortChecksum,
  type DownloadOutcome,
  type Release,
} from "../lib/download";
import type { Account } from "../lib/accounts";

type Phase = "idle" | "preparing" | "ready" | "fallback" | "error";

type Props = {
  open: boolean;
  onClose: () => void;
  user: Account | null;
  onDownloaded: (record: { version: string; checksum: string; filename: string }) => void;
  onLogin: () => void;
  onSignup: () => void;
};

export default function DownloadModal({
  open,
  onClose,
  user,
  onDownloaded,
  onLogin,
  onSignup,
}: Props) {
  const [phase, setPhase] = useState<Phase>("idle");
  const [progress, setProgress] = useState(0);
  const [outcome, setOutcome] = useState<DownloadOutcome | null>(null);
  const [error, setError] = useState("");
  const [release, setRelease] = useState<Release | null>(null);
  const [checking, setChecking] = useState(true);
  const alive = useRef(true);

  useEffect(() => {
    alive.current = true;
    return () => {
      alive.current = false;
    };
  }, []);

  useEffect(() => {
    if (!open) {
      setPhase("idle");
      setProgress(0);
      setOutcome(null);
      setError("");
      return;
    }
    setChecking(true);
    void fetchLatestRelease().then((value) => {
      if (!alive.current) return;
      setRelease(value);
      setChecking(false);
    });
  }, [open]);

  const start = async () => {
    if (phase === "preparing") return;
    setPhase("preparing");
    setProgress(3);
    setError("");
    try {
      const result = await downloadInstaller(user, (value) => {
        if (alive.current) setProgress(value);
      });
      if (!alive.current) return;
      setOutcome(result);
      setProgress(100);
      setPhase(result.kind === "installer" ? "ready" : "fallback");
      onDownloaded({
        version: result.version,
        checksum: result.checksum,
        filename: result.filename,
      });
    } catch (caught) {
      if (!alive.current) return;
      setPhase("error");
      setError(caught instanceof Error ? caught.message : "Não foi possível baixar agora.");
    }
  };

  const size = release?.installer ? humanSize(release.installer.size) : BUILD.sizeHint;
  const version = release?.version ?? BUILD.version;

  return (
    <Modal
      open={open}
      onClose={onClose}
      wide
      title="Baixar o Orbi para Windows"
      description={`Instalador ${release?.installer?.name ?? BUILD.installer} · ${size} · sem Python, sem administrador.`}
    >
      <div className="space-y-5">
        {/* cartão do produto */}
        <div className="flex items-center gap-4 border border-rule bg-[#fbf9f3] p-4">
          <span className="grid h-12 w-12 shrink-0 place-items-center bg-ink text-paper">
            <Mark className="h-6 w-6" />
          </span>
          <div className="min-w-0 flex-1">
            <p className="flex flex-wrap items-baseline gap-2 text-[15px] font-semibold text-ink">
              Orbi {version}
              <span className="label border border-rule px-1.5 py-0.5 text-warm">{BUILD.channel}</span>
            </p>
            <p className="mt-0.5 font-mono text-[11px] text-warm">
              Windows 11 · x64 · {size}
            </p>
            {user && (
              <p className="mt-1 truncate font-mono text-[10.5px] text-moss">
                vinculado a {user.email}
              </p>
            )}
          </div>
        </div>

        {/* estado: verificando release */}
        {checking && (
          <p className="flex items-center gap-2 font-mono text-[11px] text-warm">
            <span className="h-1.5 w-1.5 animate-pulse bg-signal" />
            verificando a última release no GitHub…
          </p>
        )}

        {!checking && !release?.installer && (
          <div className="flex items-start gap-3 border border-[#9a6a1c]/35 bg-[#f7eed8] p-3.5">
            <Icon name="info" className="mt-0.5 h-4 w-4 shrink-0 text-[#9a6a1c]" />
            <div className="text-[12.5px] leading-snug text-[#6b4a10]">
              <p className="font-medium">O instalador da última versão ainda não está publicado.</p>
              <p className="mt-0.5">
                O build automático do GitHub Actions publica o <code className="font-mono">OrbiSetup.exe</code>{" "}
                na aba de releases. Se o download não estiver pronto, o botão entrega um pacote com as
                instruções e o link direto.
              </p>
            </div>
          </div>
        )}

        {/* progresso */}
        {(phase === "preparing" || phase === "ready") && (
          <div>
            <div className="flex items-baseline justify-between font-mono text-[10.5px] uppercase tracking-[0.12em] text-warm">
              <span>{phase === "ready" ? "download concluído" : "baixando o instalador"}</span>
              <span className="tnum text-ink">{Math.min(100, Math.round(progress))}%</span>
            </div>
            <div className="mt-1.5 h-2 w-full overflow-hidden border border-rule bg-paper-2">
              <div
                className={phase === "preparing" ? "progress-stripes h-full transition-[width]" : "h-full bg-moss transition-[width]"}
                style={{ width: `${Math.min(100, progress)}%` }}
              />
            </div>
          </div>
        )}

        {outcome && (
          <dl className="divide-y divide-rule border-y border-rule">
            <div className="flex items-baseline justify-between gap-4 py-2.5">
              <dt className="font-mono text-[10.5px] uppercase tracking-[0.12em] text-warm">arquivo</dt>
              <dd className="truncate font-mono text-[11.5px] text-ink">{outcome.filename}</dd>
            </div>
            <div className="flex items-baseline justify-between gap-4 py-2.5">
              <dt className="font-mono text-[10.5px] uppercase tracking-[0.12em] text-warm">SHA-256</dt>
              <dd
                className="truncate font-mono text-[11.5px] text-ink"
                title={outcome.checksum}
              >
                {shortChecksum(outcome.checksum)}
              </dd>
            </div>
          </dl>
        )}

        {phase === "ready" && (
          <div className="border border-moss/35 bg-[#eef3ea] p-3.5">
            <p className="flex items-center gap-2 text-[13px] font-medium text-[#2c5330]">
              <Icon name="check" className="h-4 w-4" />
              Instalador baixado.
            </p>
            <ol className="mt-2 space-y-1 text-[12.5px] leading-snug text-[#3d5a3f]">
              <li>1. Abra o arquivo baixado e confirme o Windows SmartScreen (“Mais informações ▸ Executar assim mesmo”).</li>
              <li>2. O setup instala em <code className="font-mono">%LOCALAPPDATA%\Orbi</code> e cria o atalho.</li>
              <li>3. Ao abrir, aperte <kbd className="font-mono">Alt+Space</kbd> e fale com a Orbi.</li>
            </ol>
          </div>
        )}

        {phase === "fallback" && (
          <div className="border border-rule bg-paper-2/50 p-3.5">
            <p className="text-[13px] font-medium text-ink">Pacote de instruções baixado.</p>
            <p className="mt-1 text-[12.5px] leading-snug text-warm">
              Ele traz o link direto da release e o passo a passo. Assim que o build automático
              publicar o <code className="font-mono">OrbiSetup.exe</code>, o botão baixa o instalador real.
            </p>
            <a
              href={`https://github.com/${BUILD.repo}/releases`}
              target="_blank"
              rel="noreferrer"
              className="label mt-2.5 inline-flex items-center gap-1.5 text-signal hover:text-signal-dark"
            >
              abrir releases no GitHub
              <Icon name="arrow-up-right" className="h-3 w-3" />
            </a>
          </div>
        )}

        {phase === "error" && (
          <div className="flex items-start gap-3 border border-signal/40 bg-signal-soft/50 p-3.5">
            <Icon name="alert" className="mt-0.5 h-4 w-4 shrink-0 text-signal" />
            <div className="text-[12.5px] leading-snug text-signal-dark">
              <p className="font-medium">Não foi possível baixar.</p>
              <p className="mt-0.5">{error}</p>
            </div>
          </div>
        )}

        {/* ações */}
        <div className="flex flex-wrap items-center gap-2.5">
          <Button size="lg" onClick={start} loading={phase === "preparing"} className="min-w-[220px]">
            <Icon name="download" className="h-4 w-4" />
            {phase === "ready" || phase === "fallback"
              ? "Baixar de novo"
              : `Baixar Orbi ${version}`}
          </Button>
          <a
            href={`https://github.com/${BUILD.repo}/releases`}
            target="_blank"
            rel="noreferrer"
            className="label inline-flex h-11 items-center gap-1.5 border border-rule px-3.5 text-ink-2 transition-colors hover:border-ink hover:text-ink"
          >
            todas as versões
            <Icon name="arrow-up-right" className="h-3 w-3" />
          </a>
        </div>

        {!user && (
          <p className="border-t border-rule pt-3 text-[12.5px] leading-snug text-warm">
            O download é livre — a conta é opcional.{" "}
            <button
              type="button"
              onClick={onLogin}
              className="font-medium text-ink underline decoration-rule underline-offset-2 hover:decoration-signal"
            >
              Entrar
            </button>{" "}
            ou{" "}
            <button
              type="button"
              onClick={onSignup}
              className="font-medium text-ink underline decoration-rule underline-offset-2 hover:decoration-signal"
            >
              criar conta
            </button>{" "}
            para o app já abrir vinculado ao seu e-mail.
          </p>
        )}

        <p className="font-mono text-[10px] leading-relaxed text-warm">
          Requisitos: {BUILD.minimum}. Instalação por usuário, sem privilégio de administrador.
          Confira o hash com{" "}
          <code className="text-ink">certutil -hashfile OrbiSetup.exe SHA256</code>.
        </p>
      </div>
    </Modal>
  );
}
