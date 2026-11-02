import { useEffect, useState } from "react";
import { Icon } from "./ui";
import { fetchReleases, humanSize, type Release } from "../lib/download";

/** Carrega todas as versões publicadas (a mais nova primeiro). */
export function useReleases() {
  const [releases, setReleases] = useState<Release[]>([]);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    let alive = true;
    void fetchReleases().then((value) => {
      if (!alive) return;
      setReleases(value);
      setLoading(false);
    });
    return () => {
      alive = false;
    };
  }, []);

  return { releases, loading };
}

export function formatDate(iso: string) {
  if (!iso) return "";
  const date = new Date(iso);
  if (Number.isNaN(date.getTime())) return "";
  return date.toLocaleDateString("pt-BR", { day: "2-digit", month: "short", year: "numeric" });
}

type Props = {
  releases: Release[];
  selected?: string | null;
  onSelect: (release: Release) => void;
  loading?: boolean;
  max?: number;
};

/**
 * Lista de versões do Orbi, tirada direto das releases do GitHub.
 * A mais recente vem marcada e selecionada; as antigas ficam à mão para quem
 * precisa de uma build específica.
 */
export default function Versions({ releases, selected, onSelect, loading, max }: Props) {
  const shown = typeof max === "number" ? releases.slice(0, max) : releases;

  if (loading) {
    return (
      <p className="flex items-center gap-2 py-4 font-mono text-[11px] text-warm">
        <span className="h-1.5 w-1.5 animate-pulse bg-signal" />
        buscando as versões no GitHub…
      </p>
    );
  }

  if (shown.length === 0) {
    return (
      <p className="py-4 text-[13px] leading-snug text-warm">
        Não consegui falar com o GitHub agora.{" "}
        <a
          href="https://github.com/Arthurowgg/TheOrbiApp/releases"
          target="_blank"
          rel="noreferrer"
          className="text-ink underline decoration-rule underline-offset-2 hover:decoration-signal"
        >
          Ver as versões no repositório
        </a>
        .
      </p>
    );
  }

  const latestTag = releases[0]?.tag;

  return (
    <ul className="divide-y divide-rule border-y border-rule">
      {shown.map((release) => {
        const isLatest = release.tag === latestTag;
        const isSelected = selected ? release.tag === selected : isLatest;
        const size = release.installer ? humanSize(release.installer.size) : "";

        return (
          <li key={release.tag}>
            <button
              type="button"
              onClick={() => onSelect(release)}
              aria-pressed={isSelected}
              className={`flex w-full items-center gap-3 px-3 py-3 text-left transition-colors ${
                isSelected ? "bg-ink/[0.05]" : "hover:bg-ink/[0.03]"
              }`}
            >
              <span
                className={`grid h-4 w-4 shrink-0 place-items-center rounded-full border ${
                  isSelected ? "border-signal bg-signal text-white" : "border-rule"
                }`}
              >
                {isSelected && <Icon name="check" className="h-2.5 w-2.5" strokeWidth={3} />}
              </span>

              <span className="min-w-0 flex-1">
                <span className="flex flex-wrap items-baseline gap-2">
                  <span className="text-[14px] font-medium text-ink">Orbi {release.version}</span>
                  {isLatest ? (
                    <span className="label border border-signal/40 bg-signal-soft/60 px-1.5 py-0.5 text-[9px] text-signal-dark">
                      mais recente
                    </span>
                  ) : (
                    <span className="label text-[9px] text-warm">versão anterior</span>
                  )}
                  {release.prerelease && (
                    <span className="label text-[9px] text-warm">prévia</span>
                  )}
                </span>
                <span className="mt-0.5 block font-mono text-[10.5px] text-warm">
                  {formatDate(release.publishedAt)}
                  {size ? ` · ${size}` : ""}
                  {release.installer ? "" : " · sem instalador"}
                </span>
              </span>

              <Icon name="download" className="h-3.5 w-3.5 shrink-0 text-warm" />
            </button>
          </li>
        );
      })}
    </ul>
  );
}
