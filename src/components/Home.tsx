import { useEffect, useState } from "react";
import OrbiChat from "./OrbiChat";
import NativeApp from "./NativeApp";
import ToolCatalog from "./ToolCatalog";
import { Button, Eyebrow, Icon, Keycap, Reveal } from "./ui";
import { EXAMPLES, FAQ, LOCAL_ONLY, STEPS, SYNC_ITEMS } from "../data/site";
import { GROUP_COUNT, TOOL_COUNT } from "../data/tools";
import { fetchLatestRelease, type Release } from "../lib/download";
import Versions, { formatDate, useReleases } from "./Versions";
import type { Account } from "../lib/accounts";

type HomeProps = {
  account: Account | null;
  onDownload: () => void;
  onLogin: () => void;
  onSignup: () => void;
  onAccount: () => void;
};

function useRelease() {
  const [release, setRelease] = useState<Release | null>(null);
  useEffect(() => {
    let alive = true;
    void fetchLatestRelease().then((value) => {
      if (alive) setRelease(value);
    });
    return () => {
      alive = false;
    };
  }, []);
  return release;
}

function Faq() {
  const [open, setOpen] = useState<number | null>(0);
  return (
    <div className="border-t border-rule">
      {FAQ.map((item, index) => {
        const expanded = open === index;
        return (
          <div key={item.q} className="border-b border-rule">
            <button
              type="button"
              onClick={() => setOpen(expanded ? null : index)}
              aria-expanded={expanded}
              className="flex w-full items-center justify-between gap-6 py-4 text-left"
            >
              <span className="text-[15px] font-medium text-ink">{item.q}</span>
              <Icon
                name="chevron-down"
                className={`h-4 w-4 shrink-0 text-warm transition-transform duration-300 ${
                  expanded ? "rotate-180" : ""
                }`}
              />
            </button>
            <div
              className={`grid transition-[grid-template-rows,opacity] duration-300 ${
                expanded ? "grid-rows-[1fr] opacity-100" : "grid-rows-[0fr] opacity-0"
              }`}
            >
              <div className="overflow-hidden">
                <p className="max-w-[70ch] pb-5 text-[14px] leading-[1.7] text-ink-2">{item.a}</p>
              </div>
            </div>
          </div>
        );
      })}
    </div>
  );
}

export default function Home({ account, onDownload, onLogin, onSignup, onAccount }: HomeProps) {
  const release = useRelease();
  const { releases, loading: versionsLoading } = useReleases();

  return (
    <main id="inicio">
      {/* ------------------------------ HERO ------------------------------ */}
      <section className="border-b border-rule">
        <div className="mx-auto grid max-w-[1120px] items-center gap-12 px-5 pb-14 pt-24 sm:px-8 lg:grid-cols-[1fr_1.02fr] lg:gap-14 lg:pb-16 lg:pt-28">
          <div className="animate-fade-up">
            <Eyebrow>Para Windows 10 e 11</Eyebrow>

            <h1 className="display mt-5 text-[clamp(2.6rem,5.4vw,4.3rem)] leading-[0.98] text-ink">
              Uma barra.
              <br />
              <span className="display-serif text-signal">O PC inteiro.</span>
            </h1>

            <p className="mt-6 max-w-[46ch] text-[16.5px] leading-[1.65] text-ink-2">
              O Orbi é uma barra de comando que abre no meio do que você está fazendo. Você escreve
              do seu jeito e ele faz: som, brilho, janelas, painéis do Windows, lembretes, notas.
              São {TOOL_COUNT} ações prontas, todas no seu computador.
            </p>

            <div className="mt-8 flex flex-wrap items-center gap-3">
              <Button size="lg" onClick={onDownload}>
                <Icon name="download" className="h-4 w-4" />
                Baixar para Windows
              </Button>
              {account ? (
                <Button size="lg" variant="secondary" onClick={onAccount}>
                  Minha conta
                  <Icon name="arrow-right" className="h-4 w-4" />
                </Button>
              ) : (
                <Button size="lg" variant="secondary" onClick={onSignup}>
                  Criar conta
                </Button>
              )}
            </div>

            <div className="mt-8 flex flex-wrap items-center gap-x-6 gap-y-2 font-mono text-[10.5px] uppercase tracking-[0.1em] text-warm">
              <span className="flex items-center gap-1.5">
                <Keycap>Alt</Keycap>
                <Keycap>Shift</Keycap>
                <Keycap>O</Keycap>
                abre em qualquer programa
              </span>
              <span className="flex items-center gap-1.5">
                <Keycap>/</Keycap> catálogo de ferramentas
              </span>
            </div>
          </div>

          <div className="animate-fade-up" style={{ animationDelay: "120ms" }}>
            <OrbiChat onDownload={onDownload} onLogin={onLogin} />
          </div>
        </div>
      </section>

      {/* ------------------------------ O QUE FAZ ------------------------------ */}
      <section id="faz" className="scroll-mt-16 py-16 lg:py-20">
        <div className="mx-auto max-w-[1120px] px-5 sm:px-8">
          <div className="grid gap-10 lg:grid-cols-[0.72fr_1.28fr] lg:gap-16">
            <Reveal>
              <Eyebrow>O que dá para pedir</Eyebrow>
              <h2 className="display mt-4 text-[clamp(2rem,4.2vw,3rem)] text-ink">
                Coisas que você faz <span className="display-serif">todo dia.</span>
              </h2>
              <p className="mt-5 max-w-[34ch] text-[14.5px] leading-[1.7] text-ink-2">
                Não é uma lista de recursos abstratos. É o que a barra resolve quando você não quer
                largar o teclado para caçar um menu.
              </p>
              <p className="serif mt-8 border-l-2 border-signal pl-4 text-[17px] italic leading-[1.55] text-ink">
                “Ninguém deveria decorar atalho para usar o próprio computador.”
              </p>
            </Reveal>

            <Reveal delay={80}>
              <ul className="border-t border-rule">
                {EXAMPLES.map((example) => (
                  <li key={example.saida} className="border-b border-rule py-3.5">
                    <p className="font-mono text-[13px] leading-snug text-ink">
                      <span className="mr-2 select-none text-signal">›</span>
                      {example.saida}
                    </p>
                    <p className="mt-1 pl-4 text-[13px] leading-[1.6] text-warm">{example.efeito}</p>
                  </li>
                ))}
              </ul>
            </Reveal>
          </div>
        </div>
      </section>

      {/* ------------------------------ CATÁLOGO ------------------------------ */}
      <section id="ferramentas" className="scroll-mt-16 border-y border-rule bg-paper-2/30 py-16 lg:py-20">
        <div className="mx-auto max-w-[1120px] px-5 sm:px-8">
          <Reveal>
            <div className="flex flex-col justify-between gap-5 md:flex-row md:items-end">
              <div>
                <Eyebrow>{TOOL_COUNT} ferramentas</Eyebrow>
                <h2 className="display mt-4 text-[clamp(2rem,4.2vw,3rem)] text-ink">
                  O catálogo <span className="display-serif">completo.</span>
                </h2>
              </div>
              <p className="max-w-[38ch] text-[13.5px] leading-[1.7] text-warm md:text-right">
                Escolha um grupo e veja tudo o que existe hoje. Nada aqui depende da internet, de
                conta ou de permissão de administrador.
              </p>
            </div>
          </Reveal>

          <Reveal delay={60}>
            <div className="mt-9">
              <ToolCatalog />
            </div>
          </Reveal>
        </div>
      </section>

      {/* ------------------------------ O APP ------------------------------ */}
      <section id="app" className="scroll-mt-16 py-16 lg:py-20">
        <div className="mx-auto grid max-w-[1120px] gap-10 px-5 sm:px-8 lg:grid-cols-[0.95fr_1.05fr] lg:items-center lg:gap-16">
          <Reveal>
            <Eyebrow>Por dentro</Eyebrow>
            <h2 className="display mt-4 text-[clamp(2rem,4.2vw,3rem)] text-ink">
              Uma barra fina. <span className="display-serif">Nada em volta.</span>
            </h2>
            <p className="mt-5 max-w-[42ch] text-[14.5px] leading-[1.7] text-ink-2">
              Ela ocupa uma linha e volta a sumir quando você termina. Fora dela existe um painel
              só, com o catálogo, os ajustes, as notas, os lembretes e a conta — sem abas, sem
              janelas espalhadas.
            </p>

            <dl className="mt-8 divide-y divide-rule border-y border-rule">
              {[
                ["Sem instalar nada além dele", "um .exe, sem Python, sem runtime extra, sem administrador"],
                ["Fica na bandeja", "fecha a barra e o Orbi continua vivo, esperando a tecla"],
                ["Funciona offline", "o catálogo é local; só a conversa livre precisa de rede e de chave"],
                ["Leve na memória", "sem navegador embutido, sem servidor local"],
              ].map(([title, body]) => (
                <div key={title} className="flex flex-col gap-1 py-3 sm:flex-row sm:gap-5">
                  <dt className="w-[190px] shrink-0 text-[13.5px] font-medium text-ink">{title}</dt>
                  <dd className="text-[13px] leading-relaxed text-warm">{body}</dd>
                </div>
              ))}
            </dl>
          </Reveal>

          <Reveal delay={80}>
            <NativeApp />
          </Reveal>
        </div>
      </section>

      {/* ------------------------------ CONTA ------------------------------ */}
      <section id="conta" className="scroll-mt-16 border-y border-rule bg-film text-bone">
        <div className="mx-auto grid max-w-[1120px] items-center gap-12 px-5 py-16 sm:px-8 lg:grid-cols-[1fr_0.85fr] lg:gap-16 lg:py-20">
          <Reveal>
            <Eyebrow tone="paper">Uma conta, dois lugares</Eyebrow>
            <h2 className="display mt-4 text-[clamp(2rem,4.2vw,3rem)] text-bone">
              O que você cria aqui
              <br />
              <span className="text-signal">existe no app.</span>
            </h2>
            <p className="mt-5 max-w-[46ch] text-[14.5px] leading-[1.7] text-bone/60">
              Não existe “vincular o app”: é a mesma conta. Lembretes, notas, rotinas, preferências
              e a memória ficam nela — abertos no site, eles aparecem no PC, e o que você faz no PC
              aparece no site.
            </p>

            <ul className="mt-8 divide-y divide-white/10 border-t border-white/10">
              {SYNC_ITEMS.map((item) => (
                <li key={item.key} className="flex flex-wrap items-baseline gap-x-3 gap-y-1 py-3">
                  <span className="text-[14px] font-medium text-bone">{item.label}</span>
                  <span className="text-[12.5px] leading-snug text-bone/50">{item.hint}</span>
                </li>
              ))}
            </ul>

            <div className="mt-8 flex flex-wrap items-center gap-3">
              {account ? (
                <Button variant="inverse" onClick={onAccount}>
                  Abrir minha conta
                  <Icon name="arrow-right" className="h-3.5 w-3.5" />
                </Button>
              ) : (
                <>
                  <Button variant="inverse" onClick={onSignup}>
                    Criar conta
                  </Button>
                  <Button variant="outline" onClick={onLogin}>
                    Já tenho conta
                  </Button>
                </>
              )}
            </div>
          </Reveal>

          <Reveal delay={80}>
            <div className="border border-white/10 bg-white/[0.02] p-6">
              <p className="font-mono text-[10px] uppercase tracking-[0.16em] text-bone/40">
                no app, com a sua conta
              </p>
              <ul className="mt-4 space-y-3">
                {[
                  ["lembretes", "“conta de luz · amanhã 9h” — avisa na bandeja"],
                  ["notas", "a mesma lista, sem passar por e-mail"],
                  ["rotinas", "“modo estudo” criado no site, executado no PC"],
                  ["memória", "“o que eu tava fazendo ontem à tarde?”"],
                ].map(([kind, line]) => (
                  <li key={kind} className="border-l border-signal/50 pl-3">
                    <p className="font-mono text-[10px] uppercase tracking-[0.1em] text-bone/35">{kind}</p>
                    <p className="text-[13px] leading-snug text-bone/75">{line}</p>
                  </li>
                ))}
              </ul>
              <p className="mt-5 border-t border-white/10 pt-3 text-[12.5px] leading-snug text-bone/45">
                Só neste PC: {LOCAL_ONLY.join(", ").toLowerCase()}.
              </p>
            </div>
          </Reveal>
        </div>
      </section>

      {/* ------------------------------ DOWNLOAD ------------------------------ */}
      <section id="download" className="scroll-mt-16 py-16 lg:py-20">
        <div className="mx-auto max-w-[1120px] px-5 sm:px-8">
          <Reveal>
            <Eyebrow>Instalação</Eyebrow>
            <h2 className="display mt-4 text-[clamp(2rem,4.2vw,3rem)] text-ink">
              Um arquivo. <span className="display-serif">Um minuto.</span>
            </h2>
          </Reveal>

          <div className="mt-8 grid gap-10 lg:grid-cols-[1fr_0.85fr] lg:gap-16">
            <div>
              <ol className="border-t border-rule">
                {STEPS.map((step, index) => (
                  <li key={step.title} className="flex gap-5 border-b border-rule py-4">
                    <span className="label tnum mt-1 text-signal">0{index + 1}</span>
                    <div>
                      <p className="text-[15px] font-medium text-ink">{step.title}</p>
                      <p className="mt-0.5 text-[13px] leading-relaxed text-warm">{step.body}</p>
                    </div>
                  </li>
                ))}
              </ol>

              <div className="mt-7 flex flex-wrap items-center gap-4">
                <Button size="lg" onClick={onDownload}>
                  <Icon name="download" className="h-4 w-4" />
                  {release?.installer ? `Baixar o Orbi ${release.version}` : "Baixar o Orbi"}
                </Button>
                <span className="font-mono text-[11px] leading-relaxed text-warm">
                  {release?.installer ? release.installer.name : "instalador .exe"}
                  <br />
                  Windows 10/11 · 64 bits · sem administrador
                </span>
              </div>

              <p className="mt-4 max-w-[52ch] text-[12.5px] leading-snug text-warm">
                A conta não é obrigatória para instalar. Ela serve para o Orbi lembrar de você em
                qualquer PC.
              </p>
            </div>

            <div>
              <p className="label text-warm">Todas as versões</p>
              <div className="mt-2">
                <Versions
                  releases={releases}
                  loading={versionsLoading}
                  selected={release?.tag ?? null}
                  onSelect={() => onDownload()}
                />
              </div>
              <p className="mt-2.5 font-mono text-[10px] leading-relaxed text-warm">
                A mais recente vem selecionada. Os arquivos são os mesmos instaladores publicados
                no GitHub, com SHA-256 em cada versão.
              </p>
            </div>
          </div>

          {releases[0]?.publishedAt && (
            <p className="mt-10 border-t border-rule pt-4 font-mono text-[10px] uppercase tracking-[0.13em] text-warm">
              última versão publicada em {formatDate(releases[0].publishedAt)}
            </p>
          )}
        </div>
      </section>

      {/* ------------------------------ PERGUNTAS ------------------------------ */}
      <section className="border-t border-rule bg-paper-2/25 py-14 lg:py-18">
        <div className="mx-auto grid max-w-[1120px] gap-10 px-5 sm:px-8 lg:grid-cols-[0.72fr_1.28fr] lg:gap-16">
          <Reveal>
            <Eyebrow>Perguntas</Eyebrow>
            <h2 className="display mt-4 text-[clamp(1.9rem,4vw,2.7rem)] text-ink">
              Antes de <span className="display-serif">instalar.</span>
            </h2>
            <p className="mt-4 max-w-[30ch] text-[13.5px] leading-[1.7] text-warm">
              São {GROUP_COUNT} grupos de ferramentas e {TOOL_COUNT} ações. Se faltar alguma coisa
              do seu dia, é só pedir: a barra também responde texto livre.
            </p>
          </Reveal>
          <Reveal delay={60}>
            <Faq />
          </Reveal>
        </div>
      </section>

      {/* ------------------------------ CTA FINAL ------------------------------ */}
      <section className="border-t border-rule py-14 sm:py-18">
        <div className="mx-auto flex max-w-[1120px] flex-col items-start justify-between gap-8 px-5 sm:px-8 md:flex-row md:items-center">
          <div>
            <h2 className="display text-[clamp(1.9rem,4vw,2.7rem)] text-ink">
              Instale e peça a primeira coisa.
            </h2>
            <p className="mt-3 max-w-[46ch] text-[14px] leading-relaxed text-warm">
              Se não gostar, desinstalar leva dois cliques. O que você criou continua na sua conta.
            </p>
          </div>
          <div className="flex flex-wrap items-center gap-3">
            <Button size="lg" onClick={onDownload}>
              <Icon name="download" className="h-4 w-4" />
              Baixar o Orbi
            </Button>
            {!account && (
              <Button size="lg" variant="secondary" onClick={onSignup}>
                Criar conta
              </Button>
            )}
          </div>
        </div>
      </section>
    </main>
  );
}
