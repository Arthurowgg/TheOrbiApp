import NovaChat from "./NovaChat";
import AppMock from "./AppMock";
import { Button, Eyebrow, Icon, Reveal } from "./ui";
import {
  CAPABILITIES,
  DOWNLOAD_STEPS,
  FEATURES,
  LOCAL_ONLY,
  LOCK_EXCEPTIONS,
  LOCK_TIMELINE,
} from "../data/site";
import { BUILD } from "../lib/download";
import { scrollToSection } from "../lib/router";
import type { Account } from "../lib/accounts";

type HomeProps = {
  account: Account | null;
  onDownload: () => void;
  onLogin: () => void;
  onSignup: () => void;
  onAccount: () => void;
};

export default function Home({ account, onDownload, onLogin, onSignup, onAccount }: HomeProps) {
  return (
    <main id="inicio">
      {/* ------------------------------ HERO ------------------------------ */}
      <section className="relative overflow-hidden border-b border-rule">
        <div className="hairline-grid pointer-events-none absolute inset-0 [mask-image:linear-gradient(to_bottom,black,transparent_85%)]" aria-hidden />
        <div className="relative mx-auto grid max-w-[1180px] items-center gap-12 px-5 pb-16 pt-28 sm:px-8 lg:grid-cols-[0.92fr_1.08fr] lg:gap-14 lg:pb-20 lg:pt-32">
          <div className="animate-fade-up">
            <div className="flex flex-wrap items-center gap-3">
              <Eyebrow>Assistente para Windows 11</Eyebrow>
              <span className="label border border-rule px-2 py-1 text-warm">v{BUILD.version}</span>
            </div>

            <h1 className="display mt-5 text-[clamp(2.9rem,6.6vw,5.8rem)] text-ink">
              Seu PC,
              <br />
              <span className="display-serif">em português.</span>
            </h1>

            <p className="mt-6 max-w-[44ch] text-[16px] leading-[1.7] text-ink-2 sm:text-[17px]">
              Abre apps, ajusta o volume, cria lembretes e protege sua sessão. Você escreve do seu
              jeito — a NOVA resolve.
            </p>

            <div className="mt-8 flex flex-wrap items-center gap-3">
              <Button size="lg" onClick={onDownload}>
                <Icon name="arrow-down" className="h-4 w-4" />
                Baixar para Windows
              </Button>
              {account ? (
                <Button size="lg" variant="secondary" onClick={onAccount}>
                  Minha conta
                </Button>
              ) : (
                <Button size="lg" variant="secondary" onClick={onSignup}>
                  Criar conta
                </Button>
              )}
            </div>

            <p className="mt-5 flex flex-wrap items-center gap-x-4 gap-y-1.5 font-mono text-[11px] text-warm">
              <span className="inline-flex items-center gap-1.5">
                <Icon name="windows" className="h-3.5 w-3.5" />
                Windows 11 · 64 bits
              </span>
              <span>Gratuito</span>
              <span>Sem cartão</span>
            </p>
          </div>

          <div className="animate-fade-up" style={{ animationDelay: "120ms" }}>
            <NovaChat onDownload={onDownload} onLogin={onLogin} />
          </div>
        </div>
      </section>

      {/* ----------------------------- CAPACIDADES ----------------------------- */}
      <div className="border-b border-rule bg-paper-2/50">
        <ul className="mx-auto flex max-w-[1180px] flex-wrap items-center justify-between gap-x-8 gap-y-2.5 px-5 py-4 sm:px-8">
          {CAPABILITIES.map((item) => (
            <li key={item} className="label flex items-center gap-2 text-ink-2">
              <span className="h-1.5 w-1.5 bg-signal" aria-hidden />
              {item}
            </li>
          ))}
        </ul>
      </div>

      {/* ------------------------------ RECURSOS ------------------------------ */}
      <section id="recursos" className="scroll-mt-16 py-20 lg:py-24">
        <div className="mx-auto max-w-[1180px] px-5 sm:px-8">
          <Reveal>
            <div className="max-w-[560px]">
              <Eyebrow>O que ela faz</Eyebrow>
              <h2 className="display mt-4 text-[clamp(2.4rem,5vw,3.8rem)] text-ink">
                Menos cliques.
                <br />
                <span className="display-serif">Mais dia.</span>
              </h2>
            </div>
          </Reveal>

          <div className="mt-10 grid border-l border-t border-rule sm:grid-cols-2 lg:grid-cols-3">
            {FEATURES.map((feature, index) => (
              <Reveal key={feature.title} delay={index * 40}>
                <article className="group h-full border-b border-r border-rule px-6 py-7 transition-colors hover:bg-paper-2/60">
                  <span className="grid h-10 w-10 place-items-center border border-rule text-ink-2 transition-colors group-hover:border-ink group-hover:text-signal">
                    <Icon name={feature.icon} className="h-[18px] w-[18px]" />
                  </span>
                  <h3 className="mt-6 font-cond text-[21px] font-semibold uppercase leading-tight tracking-[0.01em] text-ink">
                    {feature.title}
                  </h3>
                  <p className="mt-2 text-[14px] leading-[1.6] text-warm">{feature.body}</p>
                </article>
              </Reveal>
            ))}
          </div>
        </div>
      </section>

      {/* ----------------------------- CONTA E APP ----------------------------- */}
      <section id="conta" className="scroll-mt-16 bg-film text-bone">
        <div className="mx-auto grid max-w-[1180px] items-center gap-12 px-5 py-20 sm:px-8 lg:grid-cols-[0.95fr_1.05fr] lg:gap-16 lg:py-24">
          <Reveal>
            <Eyebrow tone="paper">Uma conta, dois lugares</Eyebrow>
            <h2 className="display mt-4 text-[clamp(2.5rem,5vw,4rem)] text-bone">
              Site e app,
              <br />
              <span className="text-signal">a mesma conta.</span>
            </h2>
            <p className="mt-5 max-w-[46ch] text-[15px] leading-[1.7] text-bone/60">
              Crie a conta no site, instale a NOVA e vincule com um código. O que importa para o seu
              dia a dia acompanha você; o que é sensível nunca sai do PC.
            </p>

            <ol className="mt-8 space-y-4 border-t border-white/15 pt-6">
              {[
                ["Abra Configurações ▸ Conta", "O app mostra um código NOVA-XXXX-XXXX."],
                ["Digite no site", "Conta ▸ Dispositivos, com um nome para o seu PC."],
                ["Pronto", "Preferências, apelidos e rotinas ficam em dia."],
              ].map(([title, body], index) => (
                <li key={title} className="flex gap-4">
                  <span className="label tnum mt-[3px] text-[#e9846a]">0{index + 1}</span>
                  <div>
                    <p className="text-[14.5px] font-medium text-bone">{title}</p>
                    <p className="mt-0.5 text-[13px] leading-snug text-bone/50">{body}</p>
                  </div>
                </li>
              ))}
            </ol>

            <p className="mt-6 flex items-start gap-2.5 text-[12.5px] leading-snug text-bone/45">
              <Icon name="lock" className="mt-[2px] h-3.5 w-3.5 shrink-0" />
              Nunca sincronizam: {LOCAL_ONLY.join(", ").toLowerCase()}.
            </p>

            <div className="mt-8 flex flex-wrap items-center gap-3">
              {account ? (
                <Button variant="inverse" onClick={onAccount}>
                  Abrir minha conta
                  <Icon name="arrow-right" className="h-3.5 w-3.5" />
                </Button>
              ) : (
                <>
                  <Button variant="inverse" onClick={onSignup}>
                    Criar conta grátis
                  </Button>
                  <Button variant="outline" onClick={onLogin}>
                    Entrar
                  </Button>
                </>
              )}
            </div>
          </Reveal>

          <Reveal delay={100}>
            <AppMock
              name={account?.name ?? "Ana Souza"}
              email={account?.email ?? "ana@exemplo.com"}
              color={account?.color}
            />
          </Reveal>
        </div>
      </section>

      {/* ------------------------------ SEGURANÇA ------------------------------ */}
      <section id="seguranca" className="scroll-mt-16 border-b border-rule py-20 lg:py-24">
        <div className="mx-auto grid max-w-[1180px] items-center gap-12 px-5 sm:px-8 lg:grid-cols-[1.05fr_0.95fr] lg:gap-16">
          <Reveal className="order-2 lg:order-1">
            <div className="border border-rule bg-[#fbf9f3]">
              <div className="flex items-center justify-between border-b border-rule px-5 py-3.5">
                <p className="text-[14px] font-semibold text-ink">Bloqueio por inatividade</p>
                <span className="label inline-flex items-center gap-1.5 bg-ink px-2.5 py-1 text-paper">
                  <span className="h-1.5 w-1.5 rounded-full bg-signal" />
                  100% local
                </span>
              </div>

              <ol className="relative px-5 py-6">
                <span className="absolute bottom-10 left-[39px] top-10 w-px bg-rule" aria-hidden />
                {LOCK_TIMELINE.map((step, index) => (
                  <li key={step.time} className="relative flex gap-4 pb-6 last:pb-0">
                    <span
                      className={`relative z-10 grid h-8 w-8 shrink-0 place-items-center font-mono text-[10px] ${
                        index === LOCK_TIMELINE.length - 1
                          ? "bg-signal text-white"
                          : "border border-ink bg-[#fbf9f3] text-ink"
                      }`}
                    >
                      {index + 1}
                    </span>
                    <div className="pt-0.5">
                      <p className="flex items-baseline gap-2.5">
                        <span className="font-mono text-[11px] text-signal">{step.time}</span>
                        <span className="text-[14.5px] font-medium text-ink">{step.title}</span>
                      </p>
                      <p className="mt-1 text-[13px] leading-snug text-warm">{step.body}</p>
                    </div>
                  </li>
                ))}
              </ol>

              <div className="border-t border-rule bg-paper-2/50 px-5 py-4">
                <p className="label text-warm">Não bloqueia durante</p>
                <div className="mt-2.5 flex flex-wrap gap-2">
                  {LOCK_EXCEPTIONS.map((item) => (
                    <span key={item} className="border border-rule bg-[#fbf9f3] px-2.5 py-1 font-mono text-[11px] text-ink-2">
                      {item}
                    </span>
                  ))}
                </div>
              </div>
            </div>
          </Reveal>

          <Reveal delay={100} className="order-1 lg:order-2">
            <Eyebrow>Segurança de verdade</Eyebrow>
            <h2 className="display mt-4 text-[clamp(2.5rem,5vw,3.9rem)] text-ink">
              Você sai.
              <br />
              <span className="display-serif">O PC se fecha.</span>
            </h2>
            <p className="mt-5 max-w-[46ch] text-[15px] leading-[1.7] text-ink-2">
              Depois do tempo que você escolher, a NOVA bloqueia o Windows. Avisa antes e respeita o
              que está acontecendo na tela.
            </p>
            <p className="serif mt-7 border-l-2 border-signal pl-4 text-[18px] italic leading-[1.5] text-ink">
              A única senha envolvida é a do seu Windows.
            </p>
            <ul className="mt-7 space-y-3">
              {[
                "Senhas e área de transferência nunca vão para a nuvem.",
                "A presença por webcam é opcional e não grava imagem.",
                "A chave da IA fica no Cofre de Credenciais do Windows.",
              ].map((item) => (
                <li key={item} className="flex items-start gap-3 text-[14px] leading-snug text-ink-2">
                  <Icon name="check" className="mt-[2px] h-4 w-4 shrink-0 text-signal" />
                  {item}
                </li>
              ))}
            </ul>
          </Reveal>
        </div>
      </section>

      {/* ------------------------------ DOWNLOAD ------------------------------ */}
      <section id="download" className="scroll-mt-16 py-20 lg:py-24">
        <div className="mx-auto max-w-[1180px] px-5 sm:px-8">
          <Reveal>
            <div className="grid border border-ink/25 bg-[#fbf9f3] lg:grid-cols-[1.1fr_0.9fr]">
              <div className="p-6 sm:p-9 lg:p-11">
                <Eyebrow>Download</Eyebrow>
                <h2 className="display mt-4 text-[clamp(2.5rem,5vw,3.9rem)] text-ink">
                  Instale em
                  <br />
                  <span className="display-serif">três passos.</span>
                </h2>

                <ol className="mt-8 divide-y divide-rule border-y border-rule">
                  {DOWNLOAD_STEPS.map((step, index) => (
                    <li key={step.title} className="flex items-baseline gap-4 py-3.5">
                      <span className="label tnum text-signal">0{index + 1}</span>
                      <div>
                        <p className="text-[14.5px] font-medium text-ink">{step.title}</p>
                        <p className="mt-0.5 text-[13px] text-warm">{step.body}</p>
                      </div>
                    </li>
                  ))}
                </ol>

                <div className="mt-8 flex flex-wrap items-center gap-3">
                  <Button size="lg" onClick={onDownload}>
                    <Icon name="download" className="h-4 w-4" />
                    Baixar NOVA {BUILD.version}
                  </Button>
                  {!account && (
                    <button
                      type="button"
                      onClick={onSignup}
                      className="label inline-flex items-center gap-2 text-ink transition-colors hover:text-signal"
                    >
                      Ou crie uma conta primeiro
                      <Icon name="arrow-right" className="h-3 w-3" />
                    </button>
                  )}
                </div>
              </div>

              <aside className="border-t border-ink/25 bg-film p-6 text-bone sm:p-9 lg:border-l lg:border-t-0 lg:p-11">
                <p className="label text-bone/50">Versão {BUILD.version}</p>
                <dl className="mt-5 divide-y divide-white/10 border-y border-white/10">
                  {[
                    ["Sistema", "Windows 11 (23H2 ou superior)"],
                    ["Arquitetura", "64 bits (x64)"],
                    ["Instalação", "Automática, sem terminal"],
                    ["Administrador", "Não é necessário"],
                    ["Conta", "Opcional"],
                  ].map(([term, value]) => (
                    <div key={term} className="flex items-baseline justify-between gap-6 py-3">
                      <dt className="font-mono text-[11px] text-bone/50">{term}</dt>
                      <dd className="text-right text-[13px] text-bone">{value}</dd>
                    </div>
                  ))}
                </dl>
                <button
                  type="button"
                  onClick={() => scrollToSection("seguranca")}
                  className="label mt-6 inline-flex items-center gap-2 text-bone/60 transition-colors hover:text-bone"
                >
                  Como a NOVA protege seus dados
                  <Icon name="arrow-right" className="h-3 w-3" />
                </button>
              </aside>
            </div>
          </Reveal>
        </div>
      </section>
    </main>
  );
}
