import { useEffect, useMemo, useRef, useState } from "react";
import { Button, Icon, Mark, Modal } from "./ui";
import Versions, { formatDate, useReleases } from "./Versions";
import {
  BUILD,
  downloadInstaller,
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
  onDownloaded: (record: {
    version: string;
    checksum: string;
    filename: string;
    url: string;
  }) => void;
  onLogin: () => void;
  onSignup: () => void;
};

/**
 * Escolha da versão: a mais recente já vem selecionada e o botão grande baixa
 * ela. Abaixo, “quero uma versão anterior” abre a lista completa — que sai
 * direto das releases do GitHub, sem ninguém precisar sair do site.
 */
export default function DownloadModal({
  open,
  onClose,
  user,
  onDownloaded,
  onLogin,
  onSignup,
}: Props) {
  const { releases, loading } = useReleases();
  const [selectedTag, setSelectedTag] = useState<string | null>(null);
  const [showOlder, setShowOlder] = useState(false);
  const [phase, setPhase] = useState<Phase>("idle");
  const [progress, setProgress] = useState(0);
  const [outcome, setOutcome] = useState<DownloadOutcome | null>(null);
  const [error, setError] = useState("");
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
      setShowOlder(false);
      return;
    }
    // volta sempre para a mais recente ao abrir
    setSelectedTag(null);
  }, [open]);

  const latest = releases[0] ?? null;
  const selected = useMemo(
    () => releases.find((release) => release.tag === selectedTag) ?? latest,
    [releases, selectedTag, latest],
  );

  const installed = Boolean(selected?.installer);
  const isLatest = !selectedTag || selectedTag === latest?.tag;
  const size = selected?.installer ? humanSize(selected.installer.size) : BUILD.sizeHint;
  const version = selected?.version ?? BUILD.version;

  const pick = (release: Release) => {
    setSelectedTag(release.tag);
    setPhase("idle");
    setOutcome(null);
    setError("");
    setShowOlder(false);
  };

  const start = async () => {
    if (phase === "preparing") return;
    setPhase("preparing");
    setProgress(3);
    setError("");
    try {
      const result = await downloadInstaller(
        user,
        (value) => {
          if (alive.current) setProgress(value);
        },
        selected,
      );
      if (!alive.current) return;
      setOutcome(result);
      setProgress(100);
      setPhase(result.kind === "installer" ? "ready" : "fallback");
      onDownloaded({
        version: result.version,
        checksum: result.checksum,
        filename: result.filename,
        url: result.kind === "installer" ? result.url : "",
      });
    } catch (caught) {
      if (!alive.current) return;
      setPhase("error");
      setError(caught instanceof Error ? caught.message : "Não foi possível baixar agora.");
    }
  };

  return (
    <Modal
      open={open}
      onClose={onClose}
      wide
      title="Baixar o Orbi"
      description={`Instalador para Windows · ${size} · o app baixa na hora.`}
    >
      <div className="space-y-5">
        {/* versão escolhida */}
        <div className="border border-rule bg-[#fbf9f3]">
          <div className="flex items-center gap-4 p-4">
            <span className="grid h-12 w-12 shrink-0 place-items-center bg-ink text-paper">
              <Mark className="h-6 w-6" />
            </span>
            <div className="min-w-0 flex-1">
              <p className="flex flex-wrap items-baseline gap-2 text-[15px] font-semibold text-ink">
                Orbi {version}
                {isLatest && latest && (
                  <span className="label border border-signal/40 bg-signal-soft/60 px-1.5 py-0.5 text-[9px] text-signal-dark">
                    mais recente
                  </span>
                )}
              </p>
              <p className="mt-0.5 font-mono text-[11px] text-warm">
                Windows 10/11 · x64 · {size}
                {selected?.publishedAt ? ` · ${formatDate(selected.publishedAt)}` : ""}
              </p>
              {user && (
                <p className="mt-1 truncate font-mono text-[10.5px] text-moss">
                  seu download fica registrado em {user.email}
                </p>
              )}
            </div>
          </div>

          {/* lista completa: só quando a pessoa quer uma versão antiga */}
          <div className="border-t border-rule px-4 py-3">
            <button
              type="button"
              onClick={() => setShowOlder((value) => !value)}
              aria-expanded={showOlder}
              className="flex w-full items-center justify-between gap-3 text-left"
            >
              <span className="text-[13px] font-medium text-ink">
                {showOlder ? "Escolher outra versão" : "Quero uma versão anterior"}
              </span>
              <span className="flex items-center gap-2 font-mono text-[10.5px] text-warm">
                {loading ? "carregando…" : `${releases.length} versões`}
                <Icon
                  name="chevron-down"
                  className={`h-3.5 w-3.5 transition-transform ${showOlder ? "rotate-180" : ""}`}
                />
              </span>
            </button>

            {showOlder && (
              <div className="mt-3">
                <Versions
                  releases={releases}
                  loading={loading}
                  selected={selected?.tag}
                  onSelect={pick}
                />
                <p className="mt-2 font-mono text-[10px] leading-relaxed text-warm">
                  Todos os instaladores vêm das releases do GitHub. Versões antigas não recebem
                  correções — use só se precisar.
                </p>
              </div>
            )}
          </div>
        </div>

        {!loading && !installed && (
          <div className="flex items-start gap-3 border border-[#9a6a1c]/35 bg-[#f7eed8] p-3.5">
            <Icon name="info" className="mt-0.5 h-4 w-4 shrink-0 text-[#9a6a1c]" />
            <div className="text-[12.5px] leading-snug text-[#6b4a10]">
              <p className="font-medium">Essa versão não tem instalador publicado.</p>
              <p className="mt-0.5">
                Escolha outra na lista acima. Se o GitHub não responder, o botão entrega um pacote
                com as instruções e o link direto.
              </p>
            </div>
          </div>
        )}

        {(phase === "preparing" || phase === "ready") && (
          <div>
            <div className="flex items-baseline justify-between font-mono text-[10.5px] uppercase tracking-[0.12em] text-warm">
              <span>{phase === "ready" ? "download concluído" : `baixando ${version}`}</span>
              <span className="tnum text-ink">{Math.min(100, Math.round(progress))}%</span>
            </div>
            <div className="mt-1.5 h-2 w-full overflow-hidden border border-rule bg-paper-2">
              <div
                className={
                  phase === "preparing"
                    ? "progress-stripes h-full transition-[width]"
                    : "h-full bg-moss transition-[width]"
                }
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
              <dd className="truncate font-mono text-[11.5px] text-ink" title={outcome.checksum}>
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
              <li>1. Abra o arquivo e confirme o SmartScreen (“Mais informações ▸ Executar assim mesmo”).</li>
              <li>2. O setup remove as versões antigas, instala e cria o atalho.</li>
              <li>3. Ao abrir, aperte <kbd className="font-mono">Alt+Shift+O</kbd> e peça.</li>
            </ol>
          </div>
        )}

        {phase === "fallback" && (
          <div className="border border-rule bg-paper-2/50 p-3.5">
            <p className="text-[13px] font-medium text-ink">Pacote de instruções baixado.</p>
            <p className="mt-1 text-[12.5px] leading-snug text-warm">
              Ele traz o link direto do instalador e o passo a passo. Tente de novo em alguns
              minutos — o GitHub costuma voltar rápido.
            </p>
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

        <div className="flex flex-wrap items-center gap-2.5">
          <Button
            size="lg"
            onClick={start}
            loading={phase === "preparing"}
            className="min-w-[240px]"
            disabled={!loading && !installed}
          >
            <Icon name="download" className="h-4 w-4" />
            {phase === "ready" || phase === "fallback"
              ? `Baixar de novo (${version})`
              : `Baixar Orbi ${version}`}
          </Button>
          <a
            href="https://github.com/Arthurowgg/TheOrbiApp/releases"
            target="_blank"
            rel="noreferrer"
            className="label inline-flex h-11 items-center gap-1.5 border border-rule px-3.5 text-ink-2 transition-colors hover:border-ink hover:text-ink"
          >
            ver no GitHub
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
            para a memória acompanhar você.
          </p>
        )}

        <p className="font-mono text-[10px] leading-relaxed text-warm">
          Requisitos: {BUILD.minimum}. Instalação por usuário, sem privilégio de administrador.
        </p>
      </div>
    </Modal>
  );
}
