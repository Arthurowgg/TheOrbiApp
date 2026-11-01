import { useEffect, useState } from "react";
import { Icon, Keycap, Mark } from "./ui";

type Scene = "comando" | "rotinas" | "sistema" | "bloqueio";

const SCENES: {
  id: Scene;
  label: string;
  title: string;
  body: string;
  accent: string;
}[] = [
  {
    id: "comando",
    label: "Barra de comando",
    title: "Alt+Shift+O em qualquer programa",
    body: "Uma barra flutua sobre a janela atual. Você escreve do seu jeito e vê a ação antes de ela acontecer.",
    accent: "local",
  },
  {
    id: "rotinas",
    label: "Rotinas",
    title: "Uma frase, cinco ajustes",
    body: "Modo estudo, cinema, reunião. A Orbi abre, fecha, ajusta o áudio e ativa o Foco em sequência.",
    accent: "local",
  },
  {
    id: "sistema",
    label: "Sistema",
    title: "O PC sob observação",
    body: "CPU, memória, disco e bateria em tempo real, com alerta antes de virar problema — e quem está pesando.",
    accent: "local",
  },
  {
    id: "bloqueio",
    label: "Bloqueio",
    title: "Ausente? O Windows se protege",
    body: "Depois do tempo escolhido, a Orbi avisa na tela e bloqueia. Vídeo em tela cheia, jogo e chamada são respeitados.",
    accent: "privado",
  },
];

function Typing({ text }: { text: string }) {
  const [shown, setShown] = useState("");
  useEffect(() => {
    setShown("");
    let index = 0;
    const timer = window.setInterval(() => {
      index += 1;
      setShown(text.slice(0, index));
      if (index >= text.length) window.clearInterval(timer);
    }, 42);
    return () => window.clearInterval(timer);
  }, [text]);
  return (
    <span>
      {shown}
      <span className="caret ml-0.5 inline-block h-[15px] w-[1.5px] translate-y-[2px] bg-signal" />
    </span>
  );
}

function SceneCommand() {
  return (
    <div className="relative h-full min-h-[330px] overflow-hidden bg-gradient-to-br from-[#1b1611] via-[#241d17] to-[#13100c] p-4 sm:p-6">
      <div className="hairline-grid-dark pointer-events-none absolute inset-0 opacity-60" aria-hidden />

      {/* desktop atrás da barra */}
      <div className="relative space-y-2 opacity-30">
        <div className="h-2 w-24 bg-bone/40" />
        <div className="grid grid-cols-3 gap-2">
          <div className="h-14 border border-bone/20 bg-bone/[0.04]" />
          <div className="h-14 border border-bone/20 bg-bone/[0.04]" />
          <div className="h-14 border border-bone/20 bg-bone/[0.04]" />
        </div>
        <div className="h-2 w-40 bg-bone/30" />
        <div className="h-2 w-28 bg-bone/20" />
      </div>

      {/* barra flutuante */}
      <div className="glass-dark absolute inset-x-4 top-1/2 -translate-y-1/2 border border-white/15 shadow-[0_30px_60px_-20px_rgba(0,0,0,0.8)] sm:inset-x-6">
        <div className="flex items-center gap-3 border-b border-white/10 px-3.5 py-3">
          <span className="grid h-6 w-6 shrink-0 place-items-center bg-signal text-white">
            <Mark className="h-3.5 w-3.5" />
          </span>
          <p className="min-w-0 flex-1 truncate font-mono text-[12.5px] text-bone">
            <Typing text="abre o zap e põe o volume em 30" />
          </p>
          <Keycap>
            <span className="text-[9px]">Esc</span>
          </Keycap>
        </div>

        <ul className="divide-y divide-white/[0.07]">
          {[
            { icon: "apps" as const, text: "Abrir WhatsApp", hint: "apelido “zap”", tag: "confirmado" },
            { icon: "sliders" as const, text: "Volume em 30%", hint: "áudio do Windows", tag: "confirmado" },
            { icon: "sparkle" as const, text: "Perguntar ao Gemini", hint: "se o comando fugir do script", tag: "IA" },
          ].map((row, index) => (
            <li
              key={row.text}
              className={`flex items-center gap-3 px-3.5 py-2.5 ${index === 0 ? "bg-white/[0.06]" : ""}`}
            >
              <Icon name={row.icon} className="h-4 w-4 shrink-0 text-[#e9846a]" />
              <span className="min-w-0 flex-1">
                <span className="block truncate text-[12.5px] text-bone">{row.text}</span>
                <span className="block truncate font-mono text-[10px] text-bone/45">{row.hint}</span>
              </span>
              <span className="label shrink-0 border border-white/15 px-1.5 py-0.5 text-[9px] text-bone/60">
                {row.tag}
              </span>
            </li>
          ))}
        </ul>

        <div className="flex items-center justify-between gap-3 border-t border-white/10 px-3.5 py-2">
          <span className="font-mono text-[9.5px] uppercase tracking-[0.12em] text-bone/40">
            ↑↓ navegar · enter executar
          </span>
          <span className="flex items-center gap-1.5 font-mono text-[9.5px] text-bone/40">
            <span className="h-1 w-1 rounded-full bg-signal" />
            sem internet
          </span>
        </div>
      </div>

      <div className="absolute bottom-4 left-1/2 flex -translate-x-1/2 items-center gap-2 font-mono text-[10px] text-bone/35">
        <Keycap>
          <span className="text-[9px]">Alt</span>
        </Keycap>
        <span>+</span>
        <Keycap>
          <span className="text-[9px]">Space</span>
        </Keycap>
      </div>
    </div>
  );
}

function SceneRotinas() {
  const routines = [
    { name: "Modo estudo", detail: "Word + Foco + volume 20%", on: true },
    { name: "Modo cinema", detail: "Spotify + brilho 40% + silêncio", on: true },
    { name: "Reunião", detail: "Teams + notificações off", on: false },
    { name: "Encerrar o dia", detail: "fecha apps + bloqueia + desliga em 1 h", on: true },
  ];
  return (
    <div className="h-full min-h-[330px] bg-[#16120e] p-4 sm:p-6">
      <div className="flex items-center justify-between">
        <p className="font-mono text-[10px] uppercase tracking-[0.16em] text-bone/45">rotinas</p>
        <span className="label border border-white/15 px-2 py-0.5 text-[9px] text-bone/50">4 ativas</span>
      </div>
      <ul className="mt-3 divide-y divide-white/10 border-y border-white/10">
        {routines.map((routine) => (
          <li key={routine.name} className="flex items-center gap-3 py-3">
            <span className="grid h-7 w-7 shrink-0 place-items-center border border-white/15 text-[#e9846a]">
              <Icon name="bolt" className="h-3.5 w-3.5" />
            </span>
            <span className="min-w-0 flex-1">
              <span className="block truncate text-[13px] text-bone">{routine.name}</span>
              <span className="block truncate font-mono text-[10px] text-bone/45">{routine.detail}</span>
            </span>
            <span
              className={`relative h-[18px] w-[32px] shrink-0 rounded-full ${
                routine.on ? "bg-signal" : "bg-white/15"
              }`}
            >
              <span
                className={`absolute top-[2px] h-3.5 w-3.5 rounded-full bg-white ${
                  routine.on ? "left-[16px]" : "left-[2px]"
                }`}
              />
            </span>
          </li>
        ))}
      </ul>
      <p className="mt-4 font-mono text-[10px] leading-relaxed text-bone/40">
        Diga “modo estudo” na barra e a rotina roda inteira. As rotinas acompanham a sua conta.
      </p>
    </div>
  );
}

function SceneSistema() {
  const bars = [
    { label: "CPU", value: 12, note: "8 núcleos" },
    { label: "Memória", value: 52, note: "8,4 de 16 GB" },
    { label: "Disco", value: 39, note: "61% livre" },
    { label: "Bateria", value: 84, note: "3h 20min" },
  ];
  return (
    <div className="h-full min-h-[330px] bg-[#16120e] p-4 sm:p-6">
      <div className="flex items-center justify-between">
        <p className="font-mono text-[10px] uppercase tracking-[0.16em] text-bone/45">sistema</p>
        <span className="flex items-center gap-1.5 font-mono text-[9.5px] text-[#e9846a]">
          <span className="h-1.5 w-1.5 rounded-full bg-signal" />
          ao vivo
        </span>
      </div>

      <div className="mt-4 space-y-3.5">
        {bars.map((bar) => (
          <div key={bar.label}>
            <div className="flex items-baseline justify-between font-mono text-[10.5px]">
              <span className="text-bone/70">{bar.label}</span>
              <span className="tnum text-bone/45">{bar.note}</span>
            </div>
            <div className="mt-1 h-1.5 w-full bg-white/10">
              <div
                className="h-full"
                style={{
                  width: `${bar.value}%`,
                  background: bar.value > 80 ? "#c7452a" : bar.value > 60 ? "#9a6a1c" : "#3b6e3f",
                }}
              />
            </div>
          </div>
        ))}
      </div>

      <div className="mt-5 flex items-start gap-2.5 border border-white/10 bg-white/[0.03] p-3">
        <Icon name="alert" className="mt-0.5 h-3.5 w-3.5 shrink-0 text-[#e9846a]" />
        <p className="font-mono text-[10px] leading-snug text-bone/60">
          Chrome está usando 2,1 GB. Quer fechar as abas paradas?
        </p>
      </div>
    </div>
  );
}

function SceneBloqueio() {
  return (
    <div className="h-full min-h-[330px] bg-[#16120e] p-4 sm:p-6">
      <div className="flex items-center justify-between">
        <p className="font-mono text-[10px] uppercase tracking-[0.16em] text-bone/45">bloqueio por inatividade</p>
        <span className="label border border-white/15 px-2 py-0.5 text-[9px] text-[#e9846a]">100% local</span>
      </div>

      <ol className="relative mt-4">
        <span className="absolute bottom-4 left-[11px] top-2 w-px bg-white/12" aria-hidden />
        {[
          { time: "0:00", title: "Sem teclado nem mouse", body: "O relógio começa sozinho." },
          { time: "5:00", title: "Aviso na tela", body: "“Vou bloquear em 15 s. Mexa o mouse.”" },
          { time: "5:15", title: "Windows bloqueado", body: "Só a sua senha do Windows abre." },
        ].map((step, index) => (
          <li key={step.time} className="relative flex gap-3.5 pb-5 last:pb-0">
            <span
              className={`relative z-10 grid h-6 w-6 shrink-0 place-items-center font-mono text-[9px] ${
                index === 2 ? "bg-signal text-white" : "border border-white/25 bg-[#16120e] text-bone/70"
              }`}
            >
              {index + 1}
            </span>
            <div className="pt-0.5">
              <p className="flex items-baseline gap-2">
                <span className="font-mono text-[10px] text-[#e9846a]">{step.time}</span>
                <span className="text-[12.5px] font-medium text-bone">{step.title}</span>
              </p>
              <p className="mt-0.5 text-[11px] leading-snug text-bone/45">{step.body}</p>
            </div>
          </li>
        ))}
      </ol>

      <div className="mt-2 flex flex-wrap gap-1.5">
        {["tela cheia", "jogo", "chamada", "apresentação"].map((item) => (
          <span key={item} className="label border border-white/12 px-1.5 py-0.5 text-[9px] text-bone/45">
            ignora: {item}
          </span>
        ))}
      </div>
    </div>
  );
}

/* ------------------------------------------------------------------ */
export default function AppShowcase() {
  const [scene, setScene] = useState<Scene>("comando");
  const active = SCENES.find((item) => item.id === scene) ?? SCENES[0];

  return (
    <div className="grid gap-8 lg:grid-cols-[0.82fr_1.18fr] lg:gap-12">
      <div>
        <div className="border-t border-rule">
          {SCENES.map((item) => {
            const selected = item.id === scene;
            return (
              <button
                key={item.id}
                type="button"
                onClick={() => setScene(item.id)}
                aria-pressed={selected}
                className={`group flex w-full items-start gap-3.5 border-b border-rule px-1 py-4 text-left transition-colors ${
                  selected ? "bg-paper-2/60" : "hover:bg-paper-2/30"
                }`}
              >
                <span
                  className={`mt-0.5 grid h-6 w-6 shrink-0 place-items-center border text-[10px] font-mono transition-colors ${
                    selected
                      ? "border-signal bg-signal text-white"
                      : "border-rule text-warm group-hover:border-ink"
                  }`}
                >
                  {String(SCENES.indexOf(item) + 1).padStart(2, "0")}
                </span>
                <span className="min-w-0">
                  <span
                    className={`block font-cond text-[16px] font-semibold uppercase tracking-[0.02em] ${
                      selected ? "text-ink" : "text-ink-2"
                    }`}
                  >
                    {item.label}
                  </span>
                  <span
                    className={`mt-0.5 block overflow-hidden text-[12.5px] leading-snug text-warm transition-[max-height,opacity] duration-300 ${
                      selected ? "max-h-24 opacity-100" : "max-h-0 opacity-0 lg:max-h-0"
                    }`}
                  >
                    {item.body}
                  </span>
                </span>
              </button>
            );
          })}
        </div>
      </div>

      <div className="relative">
        <div className="glow-signal pointer-events-none absolute -inset-8 -z-10" aria-hidden />
        <div className="overflow-hidden border border-ink/20 bg-[#14100c] shadow-[0_40px_90px_-40px_rgba(20,16,12,0.7)]">
          <div className="flex items-center justify-between border-b border-white/10 bg-[#100d09] px-3 py-2">
            <div className="flex items-center gap-2">
              <span className="grid h-4 w-4 place-items-center bg-signal text-white">
                <Mark className="h-2.5 w-2.5" />
              </span>
              <span className="font-mono text-[10px] text-bone/60">Orbi · {active.label}</span>
            </div>
            <div className="flex text-bone/40">
              {(["minus", "square", "close"] as const).map((icon) => (
                <span key={icon} className="grid h-7 w-9 place-items-center">
                  <Icon name={icon} className="h-3 w-3" strokeWidth={1.4} />
                </span>
              ))}
            </div>
          </div>
          <div key={scene} className="animate-fade-up">
            {scene === "comando" && <SceneCommand />}
            {scene === "rotinas" && <SceneRotinas />}
            {scene === "sistema" && <SceneSistema />}
            {scene === "bloqueio" && <SceneBloqueio />}
          </div>
        </div>
        <p className="mt-3 flex items-center gap-2 font-mono text-[10.5px] text-warm">
          <span className="h-1.5 w-1.5 bg-signal" aria-hidden />
          {active.title} — {active.body}
        </p>
      </div>
    </div>
  );
}
