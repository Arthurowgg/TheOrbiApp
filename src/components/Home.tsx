import { useEffect, useState } from "react";
import OrbiChat from "./OrbiChat";
import AppShowcase from "./AppShowcase";
import { Button, Eyebrow, Icon, Reveal } from "./ui";
import { ACCOUNT_ITEMS, EXAMPLES, FAQ, FEATURES, LOCAL_ONLY, STEPS } from "../data/site";
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

/* ------------------------------------------------------------------ */
function Example({ saida, efeito }: { saida: string; efeito: string }) {
  return (
    <div className="group border-b border-rule py-4">
      <p className="font-mono text-[13px] leading-snug text-ink">
        <span className="mr-2 select-none text-signal">›</span>
        {saida}
      </p>
      <p className="mt-1 pl-4 text-[13.5px] leading-[1.6] text-warm">{efeito}</p>
    </div>
  );
}

/* ------------------------------------------------------------------ */
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

/* ------------------------------------------------------------------ */
export default function Home({ account, onDownload, onLogin, onSignup, onAccount }: HomeProps) {
  const release = useRelease();
  const { releases, loading: versionsLoading } = useReleases();

  return (
    <main id="inicio">
      {/* ------------------------------ HERO ------------------------------ */}
      <section className="border-b border-rule">
        <div className="mx-auto grid max-w-[1100px] items-center gap-12 px-5 pb-16 pt-28 sm:px-8 lg:grid-cols-[1fr_0.92fr] lg:gap-16 lg:pb-20 lg:pt-32">
          <div className="animate-fade-up">
            <Eyebrow>Assistente para Windows</Eyebrow>

            <h1 className="display mt-5 text-[clamp(2.7rem,6vw,4.8rem)] text-ink">
              Você pede.
              <br />
              <span className="display-serif text-signal">O PC faz.</span>
            </h1>

            <p className="mt-6 max-w-[44ch] text-[17px] leading-[1.7] text-ink-2">
              O Orbi abre seus programas, ajusta o computador, anota, lembra e responde — do jeito
              que você fala. Uma tecla, uma frase, resolvido.
            </p>

            <div className="mt-9 flex flex-wrap items-center gap-3">
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

            <p className="mt-6 font-mono text-[11px] leading-relaxed text-warm">
              Windows 10 e 11 · 64 bits · instalação sem administrador
            </p>
          </div>

          <div className="animate-fade-up" style={{ animationDelay: "120ms" }}>
            <OrbiChat onDownload={onDownload} onLogin={onLogin} />
          </div>
        </div>
      </section>

      {/* ------------------------------ O QUE FAZ ------------------------------ */}
      <section id="faz" className="scroll-mt-16 py-20 lg:py-24">
        <div className="mx-auto max-w-[1100px] px-5 sm:px-8">
          <Reveal>
            <Eyebrow>O que ele resolve</Eyebrow>
            <h2 className="display mt-4 max-w-[20ch] text-[clamp(2.2rem,4.6vw,3.3rem)] text-ink">
              O dia a dia do PC, <span className="display-serif">sem cliques.</span>
            </h2>
          </Reveal>

          <div className="mt-10 grid gap-x-12 gap-y-8 md:grid-cols-2 lg:grid-cols-3">
            {FEATURES.map((feature, index) => (
              <Reveal key={feature.title} delay={index * 40}>
                <div>
                  <span className="text-signal">
                    <Icon name={feature.icon} className="h-5 w-5" />
                  </span>
                  <h3 className="mt-3 text-[16.5px] font-semibold text-ink">{feature.title}</h3>
                  <p className="mt-1.5 text-[14px] leading-[1.65] text-warm">{feature.body}</p>
                </div>
              </Reveal>
            ))}
          </div>
        </div>
      </section>

      {/* ------------------------------ EXEMPLOS ------------------------------ */}
      <section id="exemplos" className="scroll-mt-16 border-y border-rule bg-paper-2/25 py-20 lg:py-24">
        <div className="mx-auto grid max-w-[1100px] gap-12 px-5 sm:px-8 lg:grid-cols-[0.85fr_1.15fr] lg:gap-16">
          <Reveal>
            <Eyebrow>Como soa</Eyebrow>
            <h2 className="display mt-4 text-[clamp(2.2rem,4.6vw,3.3rem)] text-ink">
              Fale como <span className="display-serif">você fala.</span>
            </h2>
            <p className="mt-5 max-w-[38ch] text-[14.5px] leading-[1.7] text-ink-2">
              Não existe sintaxe. Se o pedido for do Windows, ele acontece na hora. Se for algo que
              o PC sozinho não resolve, o Orbi pensa e responde ali mesmo, na barra.
            </p>
            <p className="serif mt-7 border-l-2 border-signal pl-4 text-[18px] italic leading-[1.5] text-ink">
              “Ninguém deveria decorar atalho para usar o próprio computador.”
            </p>
          </Reveal>

          <Reveal delay={80}>
            <div className="border-t border-rule">
              {EXAMPLES.map((example) => (
                <Example key={example.saida} {...example} />
              ))}
            </div>
            <p className="mt-4 font-mono text-[10.5px] uppercase tracking-[0.13em] text-warm">
              exemplos reais do que a barra aceita
            </p>
          </Reveal>
        </div>
      </section>

      {/* ------------------------------ O APP ------------------------------ */}
      <section id="app" className="scroll-mt-16 py-20 lg:py-24">
        <div className="mx-auto max-w-[1100px] px-5 sm:px-8">
          <Reveal>
            <div className="flex flex-col justify-between gap-6 md:flex-row md:items-end">
              <div className="max-w-[30ch]">
                <Eyebrow>Por dentro</Eyebrow>
                <h2 className="display mt-4 text-[clamp(2.2rem,4.6vw,3.3rem)] text-ink">
                  Feito para <span className="display-serif">o Windows.</span>
                </h2>
              </div>
              <p className="max-w-[34ch] text-[14px] leading-[1.7] text-warm md:text-right">
                Um programa nativo, leve, que fica na bandeja. Sem navegador embutido, sem runtime
                extra.
              </p>
            </div>
          </Reveal>

          <div className="mt-10">
            <AppShowcase />
          </div>
        </div>
      </section>

      {/* ------------------------------ CONTA ------------------------------ */}
      <section id="conta" className="scroll-mt-16 border-y border-rule bg-film text-bone">
        <div className="mx-auto grid max-w-[1100px] items-center gap-12 px-5 py-20 sm:px-8 lg:grid-cols-[1fr_0.85fr] lg:gap-16 lg:py-24">
          <Reveal>
            <Eyebrow tone="paper">Sua conta</Eyebrow>
            <h2 className="display mt-4 text-[clamp(2.2rem,4.6vw,3.3rem)] text-bone">
              Ele lembra de você,
              <br />
              <span className="text-signal">em qualquer PC.</span>
            </h2>
            <p className="mt-5 max-w-[46ch] text-[15px] leading-[1.7] text-bone/60">
              Uma conta só, a mesma no site e no app. Tudo o que o Orbi aprende sobre o seu jeito de
              trabalhar fica salvo nela — e aparece no próximo computador que você abrir.
            </p>

            <ul className="mt-8 divide-y divide-white/10 border-t border-white/10">
              {ACCOUNT_ITEMS.map((item) => (
                <li key={item.label} className="flex flex-wrap items-baseline gap-x-3 gap-y-1 py-3">
                  <span className="text-[14.5px] font-medium text-bone">{item.label}</span>
                  <span className="text-[13px] leading-snug text-bone/50">{item.body}</span>
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
                memória do orbi
              </p>
              <ul className="mt-4 space-y-3">
                {[
                  ["ontem, 14h32", "“modo estudo” · 50 min de timer"],
                  ["hoje, 09h07", "lembrete pago: conta de luz"],
                  ["hoje, 09h15", "abriu o Excel com a planilha de março"],
                  ["hoje, 11h02", "você pediu para não ser interrompido até 12h"],
                ].map(([hora, linha]) => (
                  <li key={`${hora}-${linha}`} className="border-l border-signal/50 pl-3">
                    <p className="font-mono text-[10px] text-bone/35">{hora}</p>
                    <p className="text-[13px] leading-snug text-bone/75">{linha}</p>
                  </li>
                ))}
              </ul>
              <p className="mt-5 border-t border-white/10 pt-3 text-[12.5px] leading-snug text-bone/45">
                É isso que faz o Orbi conseguir responder “o que eu tava fazendo ontem?”.
              </p>
            </div>
          </Reveal>
        </div>
      </section>

      {/* ------------------------------ PRIVACIDADE ------------------------------ */}
      <section id="privacidade" className="scroll-mt-16 py-20 lg:py-24">
        <div className="mx-auto max-w-[1100px] px-5 sm:px-8">
          <div className="grid gap-10 lg:grid-cols-[0.9fr_1.1fr] lg:gap-16">
            <Reveal>
              <Eyebrow>Privacidade</Eyebrow>
              <h2 className="display mt-4 text-[clamp(2.2rem,4.6vw,3.3rem)] text-ink">
                O que é seu <span className="display-serif">fica seu.</span>
              </h2>
              <p className="mt-5 max-w-[42ch] text-[14.5px] leading-[1.7] text-ink-2">
                Só entra na conta aquilo que precisa estar lá para o Orbi lembrar de você. O resto
                nunca sai deste computador.
              </p>
            </Reveal>

            <Reveal delay={60}>
              <div className="grid gap-6 sm:grid-cols-2">
                <div className="border-t-2 border-ink pt-3">
                  <p className="label text-ink">Vai para a sua conta</p>
                  <ul className="mt-2 space-y-1.5">
                    {["Memória e histórico de pedidos", "Rotinas e preferências", "Lembretes e notas", "Aparelhos conectados"].map(
                      (item) => (
                        <li key={item} className="flex items-start gap-2 text-[13.5px] leading-snug text-ink-2">
                          <Icon name="check" className="mt-[2px] h-3.5 w-3.5 shrink-0 text-signal" />
                          {item}
                        </li>
                      ),
                    )}
                  </ul>
                </div>
                <div className="border-t-2 border-rule pt-3">
                  <p className="label text-warm">Fica só no PC</p>
                  <ul className="mt-2 space-y-1.5">
                    {LOCAL_ONLY.map((item) => (
                      <li key={item} className="flex items-start gap-2 text-[13.5px] leading-snug text-warm">
                        <span className="mt-[7px] h-1 w-1 shrink-0 bg-rule" />
                        {item}
                      </li>
                    ))}
                  </ul>
                  <p className="mt-4 text-[12.5px] leading-snug text-warm">
                    A chave da IA fica criptografada no próprio Windows.
                  </p>
                </div>
              </div>
            </Reveal>
          </div>
        </div>
      </section>

      {/* ------------------------------ DOWNLOAD ------------------------------ */}
      <section id="download" className="scroll-mt-16 border-t border-rule bg-paper-2/25 py-20 lg:py-24">
        <div className="mx-auto max-w-[1100px] px-5 sm:px-8">
          <Reveal>
            <Eyebrow>Download</Eyebrow>
            <h2 className="display mt-4 text-[clamp(2.2rem,4.6vw,3.3rem)] text-ink">
              Leva <span className="display-serif">um minuto.</span>
            </h2>
          </Reveal>

          <div className="mt-8 grid gap-10 lg:grid-cols-[1fr_0.9fr] lg:gap-14">
            <div>
              <ol className="border-t border-rule">
                {STEPS.map((step, index) => (
                  <li key={step.title} className="flex gap-5 border-b border-rule py-4">
                    <span className="label tnum mt-1 text-signal">0{index + 1}</span>
                    <div>
                      <p className="text-[15px] font-medium text-ink">{step.title}</p>
                      <p className="mt-0.5 text-[13.5px] leading-relaxed text-warm">{step.body}</p>
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
                  Windows 10/11 · 64 bits
                </span>
              </div>

              <p className="mt-4 text-[12.5px] leading-snug text-warm">
                A conta não é obrigatória para baixar — só para o Orbi lembrar de você.
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

          <div className="mt-12 border-t border-rule pt-6">
            {releases[0]?.publishedAt && (
              <p className="font-mono text-[10px] uppercase tracking-[0.13em] text-warm">
                última versão publicada em {formatDate(releases[0].publishedAt)}
              </p>
            )}
          </div>
        </div>
      </section>

      {/* ------------------------------ PERGUNTAS ------------------------------ */}
      <section className="border-t border-rule py-16 lg:py-20">
        <div className="mx-auto grid max-w-[1100px] gap-10 px-5 sm:px-8 lg:grid-cols-[0.8fr_1.2fr] lg:gap-16">
          <Reveal>
            <Eyebrow>Perguntas</Eyebrow>
            <h2 className="display mt-4 text-[clamp(2rem,4.2vw,3rem)] text-ink">
              Antes de <span className="display-serif">instalar.</span>
            </h2>
          </Reveal>
          <Reveal delay={60}>
            <Faq />
          </Reveal>
        </div>
      </section>

      {/* ------------------------------ CTA FINAL ------------------------------ */}
      <section className="border-t border-rule py-16 sm:py-20">
        <div className="mx-auto flex max-w-[1100px] flex-col items-start justify-between gap-8 px-5 sm:px-8 md:flex-row md:items-center">
          <div>
            <h2 className="display text-[clamp(2rem,4.2vw,3rem)] text-ink">
              Instale e peça a primeira coisa.
            </h2>
            <p className="mt-3 max-w-[44ch] text-[14px] leading-relaxed text-warm">
              Se não gostar, desinstalar leva dois cliques. Seus dados continuam na sua conta.
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
