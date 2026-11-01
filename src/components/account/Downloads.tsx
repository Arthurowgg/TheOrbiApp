import { Badge, Button, Icon, Panel } from "../ui";
import { PageHeader, type PageProps } from "./AccountShell";
import { formatDateTime } from "../../lib/accounts";
import { BUILD, shortChecksum } from "../../lib/download";
import { RELEASE_NOTES } from "../../data/site";

export default function Downloads({ account, onDownload }: PageProps) {
  const last = account.downloads[0];

  return (
    <>
      <PageHeader
        title="Downloads"
        description="A versão atual do Orbi e o histórico dos arquivos que você baixou."
      />

      <div className="grid gap-5 lg:grid-cols-[1.25fr_1fr]">
        <Panel padded={false} className="overflow-hidden">
          <div className="flex flex-col gap-5 bg-film p-6 text-bone sm:flex-row sm:items-center sm:justify-between">
            <div>
              <div className="flex flex-wrap items-center gap-2.5">
                <p className="label text-bone/55">Versão atual</p>
                <Badge tone="signal">{BUILD.channel}</Badge>
              </div>
              <p className="display mt-3 text-[3.4rem] text-bone">{BUILD.version}</p>
              <p className="mt-2 font-mono text-[11px] text-bone/50">
                {BUILD.os} · {BUILD.arch}
              </p>
            </div>
            <div className="sm:text-right">
              <Button variant="inverse" size="lg" onClick={onDownload}>
                <Icon name="download" className="h-4 w-4" />
                {last ? "Baixar de novo" : "Baixar instalador"}
              </Button>
              <p className="mt-2.5 font-mono text-[10.5px] text-bone/45">
                {last ? `Último download: ${formatDateTime(last.at)}` : "Nenhum download ainda"}
              </p>
            </div>
          </div>

          <div className="grid gap-px bg-rule sm:grid-cols-3">
            {[
              ["Requisito", BUILD.minimum],
              ["Instalação", "Automática"],
              ["Conta no pacote", "orbi-conta.json"],
            ].map(([term, value]) => (
              <div key={term} className="bg-[#fbf9f3] p-4">
                <p className="label text-warm">{term}</p>
                <p className="mt-1 text-[13px] font-medium text-ink">{value}</p>
              </div>
            ))}
          </div>

          <div className="border-t border-rule p-5">
            <p className="text-[13px] font-semibold text-ink">Novidades da {BUILD.version}</p>
            <ul className="mt-3 space-y-2">
              {RELEASE_NOTES.map((note) => (
                <li key={note} className="flex items-start gap-2.5 text-[13px] leading-snug text-ink-2">
                  <Icon name="check" className="mt-[2px] h-3.5 w-3.5 shrink-0 text-signal" />
                  {note}
                </li>
              ))}
            </ul>
          </div>
        </Panel>

        <Panel title="Histórico" description="Seus últimos downloads nesta conta." padded={false}>
          {account.downloads.length === 0 ? (
            <div className="px-5 py-10 text-center">
              <span className="mx-auto grid h-11 w-11 place-items-center border border-dashed border-warm text-warm">
                <Icon name="history" className="h-5 w-5" />
              </span>
              <p className="mt-3 text-[14px] font-medium text-ink">Sem downloads ainda</p>
              <p className="mt-1 text-[12.5px] text-warm">Os arquivos baixados com a conta aparecem aqui.</p>
            </div>
          ) : (
            <ul className="divide-y divide-rule">
              {account.downloads.map((item) => (
                <li key={item.id} className="px-5 py-3.5">
                  <div className="flex items-baseline justify-between gap-3">
                    <p className="text-[13.5px] font-medium text-ink">Orbi {item.version}</p>
                    <span className="font-mono text-[11px] text-warm">{formatDateTime(item.at)}</span>
                  </div>
                  <p className="mt-1 truncate font-mono text-[11px] text-warm">{item.filename}</p>
                  <p className="mt-0.5 font-mono text-[10.5px] text-warm/80">SHA-256 {shortChecksum(item.checksum)}</p>
                  {item.url && (
                    <a
                      href={item.url}
                      target="_blank"
                      rel="noreferrer"
                      className="label mt-1 inline-flex items-center gap-1 text-signal hover:text-signal-dark"
                    >
                      baixar este arquivo de novo
                      <Icon name="arrow-up-right" className="h-3 w-3" />
                    </a>
                  )}
                </li>
              ))}
            </ul>
          )}
        </Panel>
      </div>
    </>
  );
}
