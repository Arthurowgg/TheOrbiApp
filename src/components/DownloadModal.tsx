import { useEffect, useRef, useState } from "react";
import { Button, Icon, Mark, Modal, Spinner } from "./ui";
import { BUILD, buildPackage, savePackage, shortChecksum, type PackageResult } from "../lib/download";
import type { Account } from "../lib/accounts";

type Phase = "idle" | "preparing" | "ready" | "error";

type Props = {
  open: boolean;
  onClose: () => void;
  user: Account | null;
  onDownloaded: (record: { version: string; checksum: string; filename: string }) => void;
  onLogin: () => void;
};

export default function DownloadModal({
  open,
  onClose,
  user,
  onDownloaded,
  onLogin,
}: Props) {
  const [phase, setPhase] = useState<Phase>("idle");
  const [progress, setProgress] = useState(0);
  const [result, setResult] = useState<PackageResult | null>(null);
  const [error, setError] = useState("");
  const interval = useRef<number | null>(null);
  const alive = useRef(true);
  const openRef = useRef(open);
  const runId = useRef(0);

  useEffect(() => {
    alive.current = true;
    return () => {
      alive.current = false;
      runId.current += 1;
      if (interval.current) window.clearInterval(interval.current);
    };
  }, []);

  useEffect(() => {
    openRef.current = open;
    runId.current += 1;
    if (interval.current) {
      window.clearInterval(interval.current);
      interval.current = null;
    }
    if (open) {
      setPhase("idle");
      setProgress(0);
      setResult(null);
      setError("");
    }
  }, [open]);

  const prepare = async () => {
    const id = ++runId.current;
    setPhase("preparing");
    setProgress(4);
    setError("");
    setResult(null);
    const started = Date.now();
    if (interval.current) window.clearInterval(interval.current);
    interval.current = window.setInterval(() => {
      setProgress((value) => (value < 88 ? value + Math.max(1, Math.round(Math.random() * 8)) : value));
    }, 150);

    try {
      const pkg = await buildPackage(user);
      const remaining = Math.max(0, 1300 - (Date.now() - started));
      await new Promise((resolve) => window.setTimeout(resolve, remaining));
      if (!alive.current || !openRef.current || runId.current !== id) return;
      if (interval.current) window.clearInterval(interval.current);
      setProgress(100);
      savePackage(pkg);
      setResult(pkg);
      onDownloaded({
        version: BUILD.version,
        checksum: pkg.checksum,
        filename: pkg.filename,
      });
      window.setTimeout(() => {
        if (alive.current && runId.current === id) setPhase("ready");
      }, 320);
    } catch {
      if (interval.current) window.clearInterval(interval.current);
      if (!alive.current || runId.current !== id) return;
      setError("Não foi possível preparar o pacote agora. Tente novamente.");
      setPhase("error");
    }
  };

  const downloading = phase === "preparing";

  return (
    <Modal
      open={open}
      onClose={onClose}
      labelledBy="download-title"
      width="max-w-[600px]"
      hideClose
    >
      <div className="flex items-center justify-between gap-4 bg-film px-5 py-3.5 text-bone">
        <div className="flex items-center gap-2.5">
          <span className="grid h-7 w-7 place-items-center bg-signal text-white">
            <Mark className="h-3.5 w-3.5" />
          </span>
          <div>
            <p className="label text-bone">Download</p>
            <p className="mt-0.5 font-mono text-[10px] text-bone/60">
              NOVA {BUILD.version} · {BUILD.channel}
            </p>
          </div>
        </div>
        <button
          type="button"
          onClick={onClose}
          className="grid h-8 w-8 place-items-center text-bone/65 transition-colors hover:bg-white/10 hover:text-bone"
          aria-label="Fechar download"
        >
          <Icon name="close" className="h-4 w-4" />
        </button>
      </div>

      <div className="px-5 py-6 sm:px-7 sm:py-7">
        {phase === "idle" && (
          <>
            <h2 id="download-title" className="display text-[2rem] text-ink">
              Instalar no Windows 11
            </h2>
            <p className="mt-2 text-[14px] leading-[1.6] text-ink-2">
              Um instalador único, já com tudo o que a NOVA precisa. Não é
              preciso instalar Python, abrir o terminal ou executar como
              administrador.
            </p>

            <ul className="mt-5 border-t border-rule">
              {[
                "Windows 11 de 64 bits (23H2 ou superior)",
                "Instalação automática, sem terminal e sem administrador",
                "Chave da IA gratuita configurada dentro do app",
                user
                  ? "Seu arquivo de conta vai junto, para vincular o app"
                  : "Conta opcional — você pode vincular o app depois",
              ].map((item) => (
                <li
                  key={item}
                  className="flex items-start gap-2.5 border-b border-rule py-2.5 text-[13px] leading-[1.5] text-ink-2"
                >
                  <Icon
                    name="check"
                    className="mt-[2px] h-3.5 w-3.5 shrink-0 text-signal"
                    strokeWidth={2}
                  />
                  {item}
                </li>
              ))}
            </ul>

            <div className="mt-5 flex items-center justify-between gap-4">
              <span className="label text-warm">{BUILD.os} · {BUILD.arch}</span>
              <span className="label text-warm">v{BUILD.version}</span>
            </div>

            <Button size="lg" className="mt-5 w-full" onClick={prepare}>
              <Icon name="download" className="h-4 w-4" />
              Baixar instalador
            </Button>

            <p className="mt-4 text-center text-[12px] leading-relaxed text-warm">
              {user ? (
                <>O download ficará registrado em <span className="text-ink">{user.email}</span>.</>
              ) : (
                <>
                  Baixar não exige conta.{" "}
                  <button
                    type="button"
                    className="text-signal underline decoration-rule underline-offset-2 hover:decoration-signal"
                    onClick={onLogin}
                  >
                    Entrar para vincular o app
                  </button>
                  .
                </>
              )}
            </p>

            <p className="mt-5 border-t border-rule pt-3 font-mono text-[9.5px] leading-relaxed text-warm/80">
              Build de prévia: o kit de instalação é gerado localmente. O
              executável auto-suficiente é produzido pela pipeline oficial da
              NOVA.
            </p>
          </>
        )}

        {downloading && (
          <div aria-live="polite">
            <h2 id="download-title" className="display text-[2rem] text-ink">
              Preparando o instalador
            </h2>
            <p className="mt-2 flex items-center gap-2 text-[14px] text-ink-2">
              <Spinner className="h-3.5 w-3.5 text-signal" />
              Organizando os componentes da versão {BUILD.version}…
            </p>

            <div className="mt-7">
              <div className="flex items-end justify-between">
                <span className="label text-warm">Preparando pacote</span>
                <span className="font-mono text-[12px] tabular-nums text-ink">
                  {Math.min(progress, 100)}%
                </span>
              </div>
              <div className="mt-2 h-2 w-full bg-paper-2">
                <div
                  className="h-full bg-signal transition-[width] duration-200"
                  style={{ width: `${Math.min(progress, 100)}%` }}
                />
              </div>
            </div>

            <ul className="mt-7 space-y-2.5">
              {[
                { at: 4, label: "Verificando a versão do Windows" },
                { at: 30, label: "Montando o pacote de instalação" },
                { at: 64, label: "Preparando o atalho e a bandeja" },
                { at: 90, label: "Finalizando" },
              ].map((step) => (
                <li key={step.label} className="flex items-center gap-2.5">
                  <span
                    className={`grid h-4 w-4 place-items-center border ${
                      progress >= step.at
                        ? "border-signal bg-signal text-white"
                        : "border-rule text-transparent"
                    }`}
                  >
                    <Icon name="check" className="h-2.5 w-2.5" strokeWidth={2.5} />
                  </span>
                  <span
                    className={`text-[12.5px] ${
                      progress >= step.at ? "text-ink" : "text-warm"
                    }`}
                  >
                    {step.label}
                  </span>
                </li>
              ))}
            </ul>

            <p className="mt-6 border-t border-rule pt-3 font-mono text-[10px] text-warm">
              Não feche esta janela até o download começar.
            </p>
          </div>
        )}

        {phase === "ready" && result && (
          <div aria-live="polite">
            <div className="flex items-start gap-4">
              <span className="grid h-10 w-10 shrink-0 place-items-center bg-signal text-white">
                <Icon name="check" className="h-5 w-5" strokeWidth={2} />
              </span>
              <div>
                <h2 id="download-title" className="display text-[2rem] text-ink">
                  Download iniciado.
                </h2>
                <p className="mt-1 text-[13.5px] leading-[1.6] text-ink-2">
                  Confira a pasta Downloads. O arquivo é{" "}
                  <span className="font-mono text-[12px] text-ink">{result.filename}</span>.
                </p>
              </div>
            </div>

            <div className="mt-5 border border-rule bg-paper-2/50 p-3">
              <div className="flex flex-wrap items-center justify-between gap-2">
                <span className="label text-warm">SHA-256</span>
                <span className="font-mono text-[10.5px] text-ink-2">
                  {shortChecksum(result.checksum)}
                </span>
              </div>
            </div>

            <ol className="mt-5 space-y-3">
              {[
                "Abra o instalador e confirme o Windows 11.",
                "Deixe o NOVA baixar e validar os componentes.",
                "Aperte Alt+Space e comece a usar.",
              ].map((step, index) => (
                <li key={step} className="flex items-start gap-3">
                  <span className="label tnum mt-[2px] text-signal">
                    {String(index + 1).padStart(2, "0")}
                  </span>
                  <span className="text-[13px] leading-[1.55] text-ink-2">{step}</span>
                </li>
              ))}
            </ol>

            <div className="mt-6 flex flex-col gap-2.5 sm:flex-row">
              <Button className="flex-1" onClick={onClose}>
                Concluir
              </Button>
              <Button
                variant="secondary"
                className="flex-1"
                onClick={() => result && savePackage(result)}
              >
                <Icon name="download" className="h-3.5 w-3.5" />
                Baixar novamente
              </Button>
            </div>

            {!user && (
              <div className="mt-5 flex flex-col items-start justify-between gap-3 border-t border-rule pt-4 sm:flex-row sm:items-center">
                <p className="text-[12px] leading-snug text-warm">
                  Crie uma conta para vincular o app e acompanhar seus downloads.
                </p>
                <Button size="sm" variant="ghost" onClick={onLogin}>
                  Criar conta
                  <Icon name="arrow-right" className="h-3 w-3" />
                </Button>
              </div>
            )}
          </div>
        )}

        {phase === "error" && (
          <div aria-live="polite">
            <h2 id="download-title" className="display text-[2rem] text-ink">
              Algo deu errado.
            </h2>
            <p className="mt-2 text-[14px] leading-[1.6] text-ink-2">{error}</p>
            <div className="mt-6 flex gap-2.5">
              <Button onClick={prepare}>Tentar de novo</Button>
              <Button variant="ghost" onClick={onClose}>
                Fechar
              </Button>
            </div>
          </div>
        )}
      </div>
    </Modal>
  );
}
