import { useEffect, useState } from "react";
import OrbiChat from "./OrbiChat";
import AppMock from "./AppMock";
import AppShowcase from "./AppShowcase";
import { Button, Eyebrow, Icon, Keycap, Reveal } from "./ui";
import {
  CAPABILITIES,
  DOWNLOAD_STEPS,
  FAQ,
  FEATURES,
  LOCAL_ONLY,
  LOCK_EXCEPTIONS,
  LOCK_TIMELINE,
  RELEASE_NOTES,
  STATS,
} from "../data/site";
import { BUILD, fetchLatestRelease, humanSize, shortChecksum, type Release } from "../lib/download";
import { scrollToSection } from "../lib/router";
import type { Account } from "../lib/accounts";

/** Caminho de asset respeitando a base do build (Firebase `/` ou GitHub Pages `/TheOrbiApp/`). */
const asset = (file: string) => `${import.meta.env.BASE_URL}${file}`;

type HomeProps = {
  account: Account | null;
  onDownload: () => void;
  onLogin: () => void;
  onSignup: () => void;
  onAccount: () => void;
};

/* ------------------------------------------------------------------ */
function useRelease() {
  const [release, setRelease] = useState<Release | null>(null);
  const [loaded, setLoaded] = useState(false);
  useEffect(() => {
    let alive = true;
    void fetchLatestRelease().then((value) => {
      if (!alive) return;
      setRelease(value);
      setLoaded(true);
    });
    return () => {
      alive = false;
    };
  }, []);
  return { release, loaded };
}

/* ------------------------------------------------------------------ */
function CapabilityMarquee() {
  const items = [...CAPABILITIES, "Windows 11", "Sem Python", "Grátis", "Firebase Auth", "Gemini"];
  return (
    <div className="relative overflow-hidden border-y border-rule bg-paper-2/40 py-3">
      <div className="marquee-track">
        {[0, 1].map((copy) => (
          <ul key={copy} className="flex shrink-0 items-center gap-8 pr-8" aria-hidden={copy === 1}>
            {items.map((item) => (
              <li key={`${copy}-${item}`} className="label flex items-center gap-2 whitespace-nowrap text-ink-2">
                <span className="h-1.5 w-1.5 bg-signal" />
                {item}
              </li>
            ))}
          </ul>
        ))}
      </div>
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
              <span className="flex items-baseline gap-3">
                <span className="label tnum text-signal">{String(index + 1).padStart(2, "0")}</span>
                <span className="text-[14.5px] font-medium text-ink">{item.q}</span>
              </span>
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
                <p className="max-w-[70ch] pb-5 pl-[38px] text-[13.5px] leading-[1.65] text-ink-2">{item.a}</p>
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
  const { release, loaded } = useRelease();
  const version = release?.version ?? BUILD.version;
  const size = release?.installer ? humanSize(release.installer.size) : BUILD.sizeHint;

  return (
    <main id="inicio">
      {/* ------------------------------ HERO ------------------------------ */}
      <section className="relative overflow-hidden border-b border-rule">
        <div
          className="hairline-grid pointer-events-none absolute inset-0 [mask-image:linear-gradient(to_bottom,black,transparent_88%)]"
          aria-hidden
        />
        <div
          className="pointer-events-none absolute -right-40 -top-40 h-[520px] w-[520px] rounded-full bg-signal/[0.07] blur-3xl"
          aria-hidden
        />
        {/* ilustração editorial de fundo */}
        <div className="pointer-events-none absolute inset-x-0 top-16 hidden justify-end px-8 xl:flex" aria-hidden>
          <img
            src={asset("images/hero-opt.jpg")}
            alt=""
            width={1200}
            height={800}
            className="float-slow mt-6 w-[420px] opacity-[0.16] mix-blend-multiply [mask-image:radial-gradient(circle_at_50%_50%,black,transparent_72%)]"
            loading="lazy"
            decoding="async"
          />
        </div>
        <div className="relative mx-auto grid max-w-[1200px] items-center gap-12 px-5 pb-16 pt-28 sm:px-8 lg:grid-cols-[0.94fr_1.06fr] lg:gap-14 lg:pb-20 lg:pt-32">
          <div className="animate-fade-up">
            <div className="flex flex-wrap items-center gap-3">
              <Eyebrow>Assistente para Windows 11</Eyebrow>
              <span className="label border border-rule bg-paper/60 px-2 py-1 text-warm">v{version}</span>
              <span className="label inline-flex items-center gap-1.5 border border-moss/30 bg-moss/[0.07] px-2 py-1 text-moss">
                <span className="relative grid h-1.5 w-1.5 place-items-center rounded-full bg-moss">
                  <span className="pulse-ring absolute inset-0" />
                </span>
                grátis
              </span>
            </div>

            <h1 className="display mt-5 text-[clamp(2.9rem,6.6vw,5.6rem)] text-ink">
              Seu PC,
              <br />
              <span className="display-serif text-signal">em português.</span>
            </h1>

            <p className="mt-6 max-w-[46ch] text-[16px] leading-[1.7] text-ink-2 sm:text-[17px]">
              O Orbi abre seus apps, ajusta o PC, cria lembretes e protege sua sessão. Você fala do
              seu jeito; ele resolve — com Gemini quando precisa, sem internet quando não tem.
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

            <p className="mt-5 flex flex-wrap items-center gap-x-5 gap-y-1.5 font-mono text-[11px] text-warm">
              <span className="inline-flex items-center gap-1.5">
                <Icon name="windows" className="h-3.5 w-3.5" />
                Windows 11 · 64 bits
              </span>
              <span className="inline-flex items-center gap-1.5">
                <Icon name="download" className="h-3.5 w-3.5" />
                {size}
              </span>
              <span>{loaded && release?.installer ? "instalador publicado" : "sem cartão"}</span>
            </p>

            <dl className="mt-9 grid grid-cols-2 gap-x-6 gap-y-5 border-t border-rule pt-6 sm:grid-cols-4">
              {STATS.map((stat) => (
                <div key={stat.label}>
                  <dt className="sr-only">{stat.label}</dt>
                  <dd>
                    <span className="block font-cond text-[26px] font-bold leading-none text-ink">
                      {stat.value}
                    </span>
                    <span className="mt-1 block text-[11.5px] font-medium text-ink-2">{stat.label}</span>
                    <span className="mt-0.5 block font-mono text-[9.5px] leading-tight text-warm">
                      {stat.hint}
                    </span>
                  </dd>
                </div>
              ))}
            </dl>
          </div>

          <div className="animate-fade-up" style={{ animationDelay: "120ms" }}>
            <OrbiChat onDownload={onDownload} onLogin={onLogin} />
          </div>
        </div>
      </section>

      <CapabilityMarquee />

      {/* ------------------------------ RECURSOS ------------------------------ */}
      <section id="recursos" className="scroll-mt-16 py-20 lg:py-24">
        <div className="mx-auto max-w-[1200px] px-5 sm:px-8">
          <Reveal>
            <div className="flex flex-col justify-between gap-6 md:flex-row md:items-end">
              <div className="max-w-[560px]">
                <Eyebrow>O que ele faz</Eyebrow>
                <h2 className="display mt-4 text-[clamp(2.4rem,5vw,3.7rem)] text-ink">
                  Menos cliques.
                  <br />
                  <span className="display-serif">Mais dia.</span>
                </h2>
              </div>
              <p className="max-w-[38ch] text-[13.5px] leading-[1.65] text-warm md:text-right">
                Cinco famílias de comandos resolvidas no seu PC e uma IA que entra só quando o
                assunto foge do script.
              </p>
            </div>
          </Reveal>

          <div className="mt-10 grid border-l border-t border-rule sm:grid-cols-2 lg:grid-cols-3">
            {FEATURES.map((feature, index) => (
              <Reveal key={feature.title} delay={index * 40}>
                <article className="group card-lift h-full border-b border-r border-rule px-6 py-7">
                  <div className="flex items-start justify-between">
                    <span className="grid h-10 w-10 place-items-center border border-rule text-ink-2 transition-colors group-hover:border-ink group-hover:text-signal">
                      <Icon name={feature.icon} className="h-[18px] w-[18px]" />
                    </span>
                    <span
                      className={`label border px-1.5 py-0.5 text-[9px] ${
                        feature.tag === "IA"
                          ? "border-signal/40 bg-signal-soft/60 text-signal-dark"
                          : feature.tag === "privado"
                            ? "border-moss/30 text-moss"
                            : "border-rule text-warm"
                      }`}
                    >
                      {feature.tag}
                    </span>
                  </div>
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

      {/* ---------------------------- CENA EDITORIAL ---------------------------- */}
      <section className="border-y border-rule bg-paper-2/20">
        <div className="mx-auto grid max-w-[1200px] items-center gap-10 px-5 py-16 sm:px-8 lg:grid-cols-[0.9fr_1.1fr] lg:gap-16 lg:py-20">
          <Reveal>
            <figure className="relative">
              <img
                src={asset("images/hero-opt.jpg")}
                alt="Ilustração de uma barra de comando flutuando sobre janelas empilhadas"
                width={1200}
                height={800}
                className="w-full border border-rule object-cover shadow-panel"
                loading="lazy"
                decoding="async"
              />
              <figcaption className="mt-3 flex items-center justify-between gap-4 font-mono text-[10px] uppercase tracking-[0.14em] text-warm">
                <span>fig. 01 — uma barra, todas as janelas</span>
                <span className="text-signal">Orbi 1.0</span>
              </figcaption>
            </figure>
          </Reveal>

          <Reveal delay={80}>
            <Eyebrow>Como funciona</Eyebrow>
            <h2 className="display mt-4 text-[clamp(2.2rem,4.4vw,3.2rem)] text-ink">
              Você escreve.
              <br />
              <span className="display-serif">O Windows obedece.</span>
            </h2>
            <p className="mt-5 max-w-[46ch] text-[14px] leading-[1.7] text-ink-2">
              A Orbi lê o seu comando, monta um plano e mostra o que vai fazer antes de fazer.
              O que é do Windows acontece no Windows, com as APIs nativas — sem navegador escondido,
              sem automação frágil de cliques.
            </p>

            <ol className="mt-7 grid gap-4 sm:grid-cols-2">
              {[
                ["Interpreta", "Português do dia a dia, apelidos e erros de digitação."],
                ["Confirma", "Ações destrutivas pedem um “sim” antes."],
                ["Executa", "APIs do Windows: áudio, energia, apps, captura."],
                ["Aprende", "Apelidos e rotinas ficam na sua conta."],
              ].map(([title, body], index) => (
                <li key={title} className="border-t border-rule pt-3">
                  <p className="flex items-baseline gap-2">
                    <span className="label tnum text-signal">0{index + 1}</span>
                    <span className="font-cond text-[17px] font-semibold uppercase tracking-[0.02em] text-ink">
                      {title}
                    </span>
                  </p>
                  <p className="mt-1 text-[12.5px] leading-snug text-warm">{body}</p>
                </li>
              ))}
            </ol>
          </Reveal>
        </div>
      </section>

      {/* -------------------------------- O APP -------------------------------- */}
      <section id="app" className="scroll-mt-16 border-y border-rule bg-paper-2/30 py-20 lg:py-24">
        <div className="mx-auto max-w-[1200px] px-5 sm:px-8">
          <Reveal>
            <div className="flex flex-col justify-between gap-6 md:flex-row md:items-end">
              <div className="max-w-[560px]">
                <Eyebrow>Por dentro</Eyebrow>
                <h2 className="display mt-4 text-[clamp(2.4rem,5vw,3.7rem)] text-ink">
                  Um app de
                  <br />
                  <span className="display-serif">verdade.</span>
                </h2>
              </div>
              <p className="max-w-[40ch] text-[13.5px] leading-[1.65] text-warm md:text-right">
                Nativo, sem Electron, sem Python. Instalador Inno Setup, atalho no Menu Iniciar,
                ícone na bandeja e atualização pela release do GitHub.
              </p>
            </div>
          </Reveal>

          <div className="mt-10">
            <AppShowcase />
          </div>

          <div className="mt-12 grid gap-6 border-t border-rule pt-8 md:grid-cols-3">
            <Reveal>
              <h3 className="font-cond text-[19px] font-semibold uppercase tracking-[0.02em] text-ink">
                O que vem na caixa
              </h3>
              <ul className="mt-3 space-y-2">
                {[
                  "OrbiSetup.exe assinado pelo build de CI",
                  "Atalho no Menu Iniciar e na bandeja",
                  "Barra de comando global com Alt+Space",
                  "Banco local para notas e lembretes",
                ].map((item) => (
                  <li key={item} className="flex items-start gap-2.5 text-[13px] leading-snug text-ink-2">
                    <Icon name="check" className="mt-[2px] h-3.5 w-3.5 shrink-0 text-signal" />
                    {item}
                  </li>
                ))}
              </ul>
            </Reveal>

            <Reveal delay={60}>
              <h3 className="font-cond text-[19px] font-semibold uppercase tracking-[0.02em] text-ink">
                Nesta versão
              </h3>
              <ul className="mt-3 space-y-1.5">
                {RELEASE_NOTES.map((note) => (
                  <li key={note} className="flex items-start gap-2.5 text-[12.5px] leading-snug text-warm">
                    <span className="mt-[7px] h-1 w-1 shrink-0 bg-rule" />
                    {note}
                  </li>
                ))}
              </ul>
            </Reveal>

            <Reveal delay={120}>
              <div className="border border-rule bg-[#fbf9f3] p-4">
                <p className="label text-warm">Atalhos que valem decorar</p>
                <ul className="mt-3 space-y-2.5">
                  {[
                    { keys: ["Alt", "Space"], label: "abre a barra" },
                    { keys: ["Esc"], label: "fecha sem executar" },
                    { keys: ["Ctrl", "Shift", "O"], label: "abre os ajustes" },
                    { keys: ["Alt", "Space", "↑"], label: "repete o último comando" },
                  ].map((item) => (
                    <li key={item.label} className="flex items-center justify-between gap-3">
                      <span className="flex items-center gap-1">
                        {item.keys.map((key, keyIndex) => (
                          <span key={key} className="flex items-center gap-1">
                            {keyIndex > 0 && <span className="font-mono text-[10px] text-warm">+</span>}
                            <Keycap>{key}</Keycap>
                          </span>
                        ))}
                      </span>
                      <span className="shrink-0 font-mono text-[10.5px] text-warm">{item.label}</span>
                    </li>
                  ))}
                </ul>
                <p className="mt-3 border-t border-rule pt-2.5 font-mono text-[9.5px] leading-snug text-warm">
                  Todos configuráveis em Ajustes ▸ Atalhos.
                </p>
              </div>
            </Reveal>
          </div>
        </div>
      </section>

      {/* ----------------------------- CONTA E APP ----------------------------- */}
      <section id="conta" className="relative overflow-hidden bg-film text-bone">
        <div className="hairline-grid-dark pointer-events-none absolute inset-0" aria-hidden />
        <div className="relative mx-auto grid max-w-[1200px] items-center gap-12 px-5 py-20 sm:px-8 lg:grid-cols-[0.95fr_1.05fr] lg:gap-16 lg:py-24">
          <Reveal>
            <Eyebrow tone="paper">Uma conta, dois lugares</Eyebrow>
            <h2 className="display mt-4 text-[clamp(2.5rem,5vw,3.9rem)] text-bone">
              Site e app,
              <br />
              <span className="text-signal">a mesma conta.</span>
            </h2>
            <p className="mt-5 max-w-[46ch] text-[15px] leading-[1.7] text-bone/60">
              Login com Firebase Authentication: e-mail e senha com hash no servidor, verificação de
              e-mail, redefinição por link e entrada com Google. Crie a conta aqui, instale o Orbi e
              vincule com um código.
            </p>

            <ol className="mt-8 space-y-4 border-t border-white/15 pt-6">
              {[
                ["Abra Configurações ▸ Conta", "O app mostra um código ORBI-XXXX-XXXX."],
                ["Digite no site", "Conta ▸ Dispositivos, com um nome para o seu PC."],
                ["Pronto", "Preferências, apelidos e rotinas ficam em dia nos dois."],
              ].map(([title, body], index) => (
                <li key={title} className="flex gap-4">
                  <span className="label tnum mt-[3px] text-ember">0{index + 1}</span>
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
            <div className="relative">
              <div className="glow-signal pointer-events-none absolute -inset-10 -z-10" aria-hidden />
              <AppMock
                name={account?.name ?? "Ana Souza"}
                email={account?.email ?? "ana@exemplo.com"}
                color={account?.color}
              />
            </div>
          </Reveal>
        </div>
      </section>

      {/* ------------------------------ SEGURANÇA ------------------------------ */}
      <section id="seguranca" className="scroll-mt-16 border-b border-rule py-20 lg:py-24">
        <div className="mx-auto grid max-w-[1200px] items-center gap-12 px-5 sm:px-8 lg:grid-cols-[1.05fr_0.95fr] lg:gap-16">
          <Reveal className="order-2 lg:order-1">
            <div className="border border-rule bg-[#fbf9f3] shadow-panel">
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
                      <p className="mt-0.5 text-[12.5px] leading-snug text-warm">{step.body}</p>
                    </div>
                  </li>
                ))}
              </ol>

              <div className="border-t border-rule px-5 py-4">
                <p className="label text-warm">O app entende quando não é hora</p>
                <ul className="mt-2.5 flex flex-wrap gap-1.5">
                  {LOCK_EXCEPTIONS.map((item) => (
                    <li key={item} className="label border border-rule bg-paper px-2 py-1 text-[9.5px] text-ink-2">
                      {item}
                    </li>
                  ))}
                </ul>
              </div>
            </div>
          </Reveal>

          <Reveal delay={80} className="order-1 lg:order-2">
            <figure className="mb-7 overflow-hidden border border-rule">
              <img
                src={asset("images/security-opt.jpg")}
                alt="Cadeado sobre o desenho técnico de um notebook aberto"
                width={1024}
                height={1536}
                className="h-[190px] w-full object-cover object-center"
                loading="lazy"
                decoding="async"
              />
            </figure>
            <Eyebrow>Segurança</Eyebrow>
            <h2 className="display mt-4 text-[clamp(2.3rem,4.6vw,3.4rem)] text-ink">
              Nada sensível
              <br />
              <span className="display-serif">sai do PC.</span>
            </h2>
            <p className="mt-5 max-w-[46ch] text-[14.5px] leading-[1.7] text-ink-2">
              Depois do tempo que você escolher, o Orbi bloqueia o Windows. Avisa antes e respeita o
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
                "No Firestore ficam só perfil, aparelhos e preferências — cada documento visível apenas para o dono.",
              ].map((item) => (
                <li key={item} className="flex items-start gap-3 text-[13.5px] leading-snug text-ink-2">
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
        <div className="mx-auto max-w-[1200px] px-5 sm:px-8">
          <Reveal>
            <div className="grid border border-ink/25 bg-[#fbf9f3] lg:grid-cols-[1.08fr_0.92fr]">
              <div className="p-6 sm:p-9 lg:p-11">
                <Eyebrow>Download</Eyebrow>
                <h2 className="display mt-4 text-[clamp(2.4rem,5vw,3.7rem)] text-ink">
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
                    Baixar Orbi {version}
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

                <figure className="mt-7 overflow-hidden border border-rule/70">
                  <img
                    src={asset("images/download-opt.jpg")}
                    alt="Caixa de papel com faixa vermelha e um pen drive ao lado"
                    width={1024}
                    height={1536}
                    className="h-[168px] w-full object-cover object-center"
                    loading="lazy"
                    decoding="async"
                  />
                  <figcaption className="border-t border-rule/70 bg-paper-2/40 px-3 py-2 font-mono text-[9.5px] uppercase tracking-[0.12em] text-warm">
                    um arquivo · um atalho · zero configuração
                  </figcaption>
                </figure>

                <p className="mt-4 font-mono text-[10px] leading-relaxed text-warm">
                  {loaded && release?.installer
                    ? `release ${release.tag} · ${release.installer.name} · ${size}`
                    : "o build automático publica o instalador na aba de releases do GitHub"}
                </p>
              </div>

              <aside className="border-t border-ink/25 bg-film p-6 text-bone sm:p-9 lg:border-l lg:border-t-0 lg:p-11">
                <div className="flex items-center justify-between">
                  <p className="label text-bone/50">Versão {version}</p>
                  <span className="label border border-white/15 px-2 py-1 text-[9px] text-bone/50">
                    {BUILD.channel}
                  </span>
                </div>

                <dl className="mt-5 divide-y divide-white/10 border-y border-white/10">
                  {[
                    ["Sistema", "Windows 11 (22H2+) · Windows 10 1809+"],
                    ["Arquitetura", "64 bits (x64)"],
                    ["Tamanho", size],
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

                <div className="mt-5 border border-white/10 bg-white/[0.03] p-3">
                  <p className="font-mono text-[9.5px] uppercase tracking-[0.14em] text-bone/40">SHA-256</p>
                  <p className="mt-1 break-all font-mono text-[11px] text-bone/80">
                    {release?.installer
                      ? `disponível em ${release.tag}/SHA256SUMS.txt`
                      : shortChecksum("aguardando a primeira release")}
                  </p>
                </div>

                <button
                  type="button"
                  onClick={() => scrollToSection("seguranca")}
                  className="label mt-5 inline-flex items-center gap-2 text-bone/60 transition-colors hover:text-bone"
                >
                  Como o Orbi protege seus dados
                  <Icon name="arrow-right" className="h-3 w-3" />
                </button>
              </aside>
            </div>
          </Reveal>
        </div>
      </section>

      {/* -------------------------------- FAQ -------------------------------- */}
      <section className="border-t border-rule bg-paper-2/25 py-20 lg:py-24">
        <div className="mx-auto grid max-w-[1200px] gap-12 px-5 sm:px-8 lg:grid-cols-[0.8fr_1.2fr] lg:gap-16">
          <Reveal>
            <Eyebrow>Perguntas</Eyebrow>
            <h2 className="display mt-4 text-[clamp(2.2rem,4.4vw,3.2rem)] text-ink">
              Antes de
              <br />
              <span className="display-serif">baixar.</span>
            </h2>
            <p className="mt-5 max-w-[34ch] text-[13.5px] leading-[1.7] text-warm">
              Se ficou alguma dúvida, o assistente no topo da página responde — ele é o mesmo motor,
              rodando aqui.
            </p>
          </Reveal>
          <Reveal delay={60}>
            <Faq />
          </Reveal>
        </div>
      </section>

      {/* ------------------------------ CTA FINAL ------------------------------ */}
      <section className="relative overflow-hidden bg-film py-16 text-bone sm:py-20">
        <div className="hairline-grid-dark pointer-events-none absolute inset-0" aria-hidden />
        <div className="relative mx-auto flex max-w-[1200px] flex-col items-start justify-between gap-8 px-5 sm:px-8 md:flex-row md:items-center">
          <div>
            <p className="label text-bone/40">Comece agora</p>
            <h2 className="display mt-3 text-[clamp(2.2rem,4.6vw,3.4rem)] text-bone">
              Um comando hoje,
              <br />
              <span className="text-signal">o PC na mão.</span>
            </h2>
            <p className="mt-4 max-w-[44ch] text-[13.5px] leading-relaxed text-bone/55">
              Baixe o instalador, aperte Alt+Space e escreva do seu jeito. Sem assinatura, sem
              cartão e sem conta obrigatória.
            </p>
          </div>
          <div className="flex flex-wrap items-center gap-3">
            <Button size="lg" variant="inverse" onClick={onDownload}>
              <Icon name="download" className="h-4 w-4" />
              Baixar Orbi {version}
            </Button>
            {!account && (
              <Button size="lg" variant="outline" onClick={onSignup}>
                Criar conta
              </Button>
            )}
          </div>
        </div>
      </section>
    </main>
  );
}
