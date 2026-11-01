import { useState } from "react";
import { Icon, Keycap, Mark } from "./ui";

type Panel = "ferramentas" | "barra" | "ajustes" | "conta";

const PANELS: { id: Panel; label: string }[] = [
  { id: "barra", label: "A barra" },
  { id: "ferramentas", label: "Ferramentas" },
  { id: "ajustes", label: "Ajustes" },
  { id: "conta", label: "Conta" },
];

const ROWS: Record<Panel, { title: string; detail: string; badge: string }[]> = {
  barra: [
    { title: "volume em 30", detail: "ajusta o som do sistema", badge: "local" },
    { title: "/captura de tela", detail: "Tela · salva em Imagens\\Orbi", badge: "ferramenta" },
    { title: "deixa pronto pra aula", detail: "Word + som baixo + pasta aberta", badge: "5 passos" },
    { title: "clique em 640,400", detail: "o Orbi usa o mouse por você", badge: "pc" },
    { title: "o que eu fiz hoje?", detail: "responde da sua memória", badge: "memória" },
  ],
  ferramentas: [
    { title: "Sistema", detail: "Gerenciador de tarefas · Serviços · Eventos…", badge: "39" },
    { title: "Painéis", detail: "Bluetooth · Wi-Fi · Impressoras · Fontes…", badge: "17" },
    { title: "Janelas", detail: "Fixar à esquerda · Nova área de trabalho…", badge: "34" },
    { title: "Energia", detail: "Dormir · Hibernar · Desligar em 1 min…", badge: "22" },
    { title: "Rede", detail: "Limpar DNS · Meu IP · Testar a internet", badge: "25" },
  ],
  ajustes: [
    { title: "Atalho global", detail: "Alt+Shift+O — troca em um clique", badge: "geral" },
    { title: "IA e chave", detail: "Gemini 2.5 · chave cifrada com DPAPI", badge: "ia" },
    { title: "Bloqueio por inatividade", detail: "avisa 15 s antes de bloquear", badge: "segurança" },
    { title: "Iniciar com o Windows", detail: "fica quieto na bandeja", badge: "geral" },
    { title: "Backup dos seus dados", detail: "%APPDATA%\\Orbi, aberto no botão", badge: "sobre" },
  ],
  conta: [
    { title: "Memória", detail: "o dia inteiro, para perguntar depois", badge: "sync" },
    { title: "Downloads", detail: "cada versão que você baixou", badge: "site" },
  ],
};

/**
 * O app, desenhado em HTML: barra fina, catálogo e painel. É a mesma coisa que
 * o .exe mostra — só que aqui, sem precisar instalar para ver.
 */
export default function NativeApp() {
  const [panel, setPanel] = useState<Panel>("barra");

  return (
    <div className="overflow-hidden border border-white/10 bg-[#13100c] text-bone shadow-[0_40px_90px_-40px_rgba(0,0,0,0.8)]">
      {/* faixa do sistema */}
      <div className="flex items-center justify-between border-b border-white/10 bg-[#0f0c09] px-4 py-2">
        <span className="flex items-center gap-2 font-mono text-[10.5px] text-bone/45">
          <span className="grid h-4 w-4 place-items-center bg-signal text-white">
            <Mark className="h-2.5 w-2.5" />
          </span>
          Orbi · barra de comando
        </span>
        <span className="flex items-center gap-2 font-mono text-[10px] text-bone/35">
          <Keycap>Alt</Keycap>
          <Keycap>Shift</Keycap>
          <Keycap>O</Keycap>
        </span>
      </div>

      {/* a barra */}
      <div className="border-b border-white/10 bg-[#1c1712] px-4 py-4">
        <div className="flex items-center gap-3">
          <span className="grid h-7 w-7 shrink-0 place-items-center bg-signal text-white">
            <Mark className="h-3.5 w-3.5" />
          </span>
          <span className="flex-1 font-mono text-[15px] text-bone/35">Digite um comando, / para as ferramentas…</span>
          <span className="border border-white/15 px-1.5 py-0.5 font-mono text-[9.5px] text-bone/45">Esc</span>
        </div>
      </div>

      {/* navegação do painel */}
      <div className="flex flex-wrap gap-x-4 border-b border-white/10 px-4 py-2">
        {PANELS.map((item) => (
          <button
            key={item.id}
            type="button"
            onClick={() => setPanel(item.id)}
            aria-pressed={panel === item.id}
            className={`border-b-2 py-1 font-mono text-[10.5px] uppercase tracking-[0.12em] transition-colors ${
              panel === item.id
                ? "border-signal text-bone"
                : "border-transparent text-bone/35 hover:text-bone/70"
            }`}
          >
            {item.label}
          </button>
        ))}
      </div>

      {/* conteúdo */}
      <ul className="divide-y divide-white/[0.07] px-4 py-1">
        {ROWS[panel].map((row) => (
          <li key={row.title} className="flex items-center justify-between gap-3 py-2.5">
            <span className="flex min-w-0 items-baseline gap-2.5">
              <Icon name="arrow-right" className="mt-[3px] h-3 w-3 shrink-0 text-signal" />
              <span className="min-w-0">
                <span className="block truncate font-mono text-[12.5px] text-bone/90">{row.title}</span>
                <span className="block truncate text-[11.5px] text-bone/40">{row.detail}</span>
              </span>
            </span>
            <span className="shrink-0 border border-white/10 px-1.5 py-0.5 font-mono text-[9px] uppercase tracking-[0.08em] text-bone/45">
              {row.badge}
            </span>
          </li>
        ))}
      </ul>

      <div className="flex items-center justify-between border-t border-white/10 px-4 py-2 font-mono text-[10px] text-bone/35">
        <span>Enter executa · ↑↓ escolhe · Esc esconde</span>
        <span className="text-[#e9846a]">modo local</span>
      </div>
    </div>
  );
}
