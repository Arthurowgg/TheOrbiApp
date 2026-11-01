import { useEffect, useRef, useState, type FormEvent } from "react";
import { Icon, Mark, Spinner } from "./ui";
import { aiLabel, askGemini, onAiStatus, type AiStatus } from "../lib/gemini";

/* ------------------------------------------------------------------ */
/* Tipos                                                              */
/* ------------------------------------------------------------------ */
type Sinal = "local" | "ia" | "confirmar" | "privado";
type Acao = { label: string; value: string; primary?: boolean };
type Resposta = {
  text: string;
  needsAi?: boolean;
  signal?: Sinal;
  detail?: string;
  actions?: Acao[];
};
type Mensagem = Resposta & { id: number; from: "orbi" | "voce" };

type OrbiChatProps = {
  onDownload: () => void;
  onLogin?: () => void;
};

const SINAIS: Record<Sinal, { label: string; className: string; dot: string }> = {
  local: {
    label: "no seu PC",
    className: "border border-white/15 text-bone/75",
    dot: "bg-[#a79d8f]",
  },
  ia: {
    label: "com IA",
    className: "bg-signal text-white",
    dot: "bg-white",
  },
  confirmar: {
    label: "peça confirmação",
    className: "border border-signal/60 text-[#f0a38e]",
    dot: "bg-[#e9846a]",
  },
  privado: {
    label: "privado",
    className: "border border-white/15 text-bone/75",
    dot: "bg-paper",
  },
};

const APPS = [
  { names: ["bloco de notas", "notepad"], name: "Bloco de Notas", article: "o" },
  { names: ["vs code", "vscode", "visual studio code"], name: "VS Code", article: "o" },
  { names: ["whatsapp", "zap", "zapzap"], name: "WhatsApp", article: "o" },
  { names: ["chrome", "google chrome"], name: "Chrome", article: "o" },
  { names: ["edge"], name: "Edge", article: "o" },
  { names: ["navegador"], name: "navegador", article: "o" },
  { names: ["spotify"], name: "Spotify", article: "o" },
  { names: ["discord"], name: "Discord", article: "o" },
  { names: ["steam"], name: "Steam", article: "o" },
  { names: ["notion"], name: "Notion", article: "o" },
  { names: ["calculadora", "calc"], name: "Calculadora", article: "a" },
  { names: ["explorer", "explorador", "arquivos"], name: "Explorador de Arquivos", article: "o" },
  { names: ["configuracoes"], name: "Configurações", article: "as" },
  { names: ["teams"], name: "Teams", article: "o" },
  { names: ["word"], name: "Word", article: "o" },
  { names: ["excel"], name: "Excel", article: "o" },
];

const EXAMPLES = ["Abre o zap", "Volume em 30", "Me lembra em 20 min", "Desliga em 1 hora"];

const normalize = (text: string) =>
  text
    .normalize("NFD")
    .replace(/[\u0300-\u036f]/g, "")
    .toLowerCase()
    .replace(/\s+/g, " ")
    .trim();

function clockIn(minutes: number) {
  const date = new Date(Date.now() + minutes * 60_000);
  return new Intl.DateTimeFormat("pt-BR", {
    hour: "2-digit",
    minute: "2-digit",
  }).format(date);
}

function durationInMinutes(text: string): number | null {
  const match = text.match(/(\d+)\s*(min|minuto|minutos|h|hora|horas)/);
  if (!match) return null;
  const amount = parseInt(match[1], 10);
  if (match[2].startsWith("min")) return amount;
  return amount * 60;
}

function humanDelay(minutes: number) {
  if (minutes < 1) return "em menos de um minuto";
  if (minutes === 1) return "em 1 minuto";
  if (minutes < 60) return `em ${minutes} minutos`;
  if (minutes === 60) return "em 1 hora";
  const hours = Math.round((minutes / 60) * 10) / 10;
  return `em ${hours} horas`;
}

/* Calculadora pequena e segura — sem eval. */
function safeMath(input: string): number | null {
  const clean = input.replace(/[^\d+\-*/().,\s]/g, "").replace(/,/g, ".").replace(/\s+/g, "");
  if (!clean || !/[+\-*/]/.test(clean) || !/^[\d+\-*/().]+$/.test(clean)) return null;
  const tokens = clean.match(/\d+(?:\.\d+)?|[()+\-*/]/g) ?? [];
  const output: (number | string)[] = [];
  const ops: string[] = [];
  const priority: Record<string, number> = { "+": 1, "-": 1, "*": 2, "/": 2 };

  for (const token of tokens) {
    if (/^\d/.test(token)) output.push(Number(token));
    else if (token === "(") ops.push(token);
    else if (token === ")") {
      while (ops.length && ops[ops.length - 1] !== "(") output.push(ops.pop()!);
      ops.pop();
    } else {
      while (
        ops.length &&
        ops[ops.length - 1] !== "(" &&
        priority[ops[ops.length - 1]] >= priority[token]
      ) {
        output.push(ops.pop()!);
      }
      ops.push(token);
    }
  }
  while (ops.length) output.push(ops.pop()!);

  const stack: number[] = [];
  for (const token of output) {
    if (typeof token === "number") stack.push(token);
    else {
      const b = stack.pop();
      const a = stack.pop();
      if (a === undefined || b === undefined) return null;
      if (token === "+") stack.push(a + b);
      else if (token === "-") stack.push(a - b);
      else if (token === "*") stack.push(a * b);
      else stack.push(b === 0 ? NaN : a / b);
    }
  }
  const result = stack[0];
  return stack.length === 1 && Number.isFinite(result) ? result : null;
}

/* ------------------------------------------------------------------ */
/* Ações dos botões                                                   */
/* ------------------------------------------------------------------ */
export function actionReply(value: string): Resposta {
  if (value === "power:cancel") {
    return {
      text: "Cancelado. Nada vai acontecer com o PC.",
      signal: "privado",
      detail: "O agendamento não foi criado.",
    };
  }
  if (value.startsWith("power:off:") || value.startsWith("power:restart:")) {
    const [, kind, rawMinutes] = value.split(":");
    const minutes = Number(rawMinutes) || 0;
    const when = minutes ? humanDelay(minutes) : "agora";
    return {
      text: `Combinado. Vou ${kind === "restart" ? "reiniciar" : "desligar"} o PC ${when}. Se quiser parar, é só dizer “cancela o desligamento”.`,
      signal: "local",
      detail: "Esta prévia não altera o seu Windows.",
    };
  }
  if (value === "close:cancel") {
    return {
      text: "Deixei aberto. Nada foi fechado.",
      signal: "privado",
    };
  }
  if (value.startsWith("close:")) {
    const app = value.slice("close:".length);
    return {
      text: `${app} fechado. Se houvesse algo não salvo, o próprio app perguntaria antes.`,
      signal: "local",
    };
  }
  if (value === "try:apps") {
    return {
      text: "Pode falar do seu jeito. “Abre o zap”, “traz o Chrome” ou “fecha o Discord”.",
      signal: "local",
      actions: [
        { label: "Abrir o WhatsApp", value: "try:whatsapp", primary: true },
        { label: "Ver o status do PC", value: "try:status" },
      ],
    };
  }
  if (value === "try:status") {
    return {
      text: "CPU em 12%, 8,4 de 16 GB de memória e 61% do disco livre. A bateria está em 84%. O Chrome é quem mais usa memória agora.",
      signal: "local",
      detail: "Exemplo de leitura em tempo real no app.",
    };
  }
  if (value === "try:whatsapp") {
    return {
      text: "Abrindo o WhatsApp.",
      signal: "local",
      detail: "Busca pelo nome no Menu Iniciar, sem sair do seu PC.",
    };
  }
  return {
    text: "Pronto.",
    signal: "local",
  };
}

/* ------------------------------------------------------------------ */
/* Interpretação local da prévia                                      */
/* ------------------------------------------------------------------ */
export function interpret(input: string): Resposta {
  const text = normalize(input);
  const raw = input.trim();

  if (!text) return { text: "Me diga o que você precisa.", signal: "local" };

  if (/^(oi|ola|eai|e ai|bom dia|boa tarde|boa noite|tudo bem|opa)\b/.test(text)) {
    return {
      text: "Oi. Estou aqui. Posso abrir apps, ajustar o PC, criar lembretes e responder o que sei sem internet.",
      signal: "local",
      actions: [
        { label: "Abre o zap", value: "try:whatsapp", primary: true },
        { label: "Status do PC", value: "try:status" },
      ],
    };
  }

  if (/(baixar|download|instalar|instalador|setup|programa)/.test(text)) {
    return {
      text: "O Orbi é um instalador único para Windows 11. Ele já traz tudo o que precisa — não pede Python, terminal nem administrador.",
      signal: "privado",
      detail: "A conta é opcional.",
      actions: [{ label: "Abrir download", value: "app:download", primary: true }],
    };
  }

  if (/(entrar|login|conta|cadastro|criar conta|vincular|sincroniz)/.test(text)) {
    return {
      text: "É a mesma conta no site e no app: lembretes, notas, rotinas, preferências e memória acompanham você. Área de transferência e chave da API ficam só no PC.",
      signal: "privado",
      actions: [{ label: "Entrar ou criar conta", value: "app:login", primary: true }],
    };
  }

  if (/(privacidade|senha|clipboard|area de transferencia|copiei|nuvem|webcam)/.test(text)) {
    return {
      text: "Senhas, o que você copia, imagens da webcam e logs de segurança não saem do seu PC. A IA recebe só o texto do comando — nada mais.",
      signal: "privado",
      detail: "A leitura da área de transferência só acontece quando você pede.",
    };
  }

  if (/(windows 10|win 10|computador antigo)/.test(text)) {
    return {
      text: "A versão 1.0 é feita para Windows 11, 64 bits. Preferimos entregar bem em um sistema só a funcionar pela metade em dois.",
      signal: "privado",
    };
  }

  if (/(quanto|calcula|resultado|soma)/.test(text) || /^[\d\s+\-*/().,%]+$/.test(text)) {
    const expression = text.replace(/quanto (e|fica)|calcula|resultado de|soma/g, "").replace(/[?=]/g, "");
    const result = safeMath(expression);
    if (result !== null) {
      return {
        text: `${new Intl.NumberFormat("pt-BR", { maximumFractionDigits: 4 }).format(result)}. Feito sem chamar a IA.`,
        signal: "local",
      };
    }
  }

  if (/(status|como esta o pc|cpu|memoria|ram|disco|bateria|processador|desempenho)/.test(text)) {
    return {
      text: "CPU em 12%, 8,4 de 16 GB de memória e 61% do disco livre. A bateria está em 84%. O Chrome é quem mais usa memória agora.",
      signal: "local",
      detail: "No app, estes números vêm do seu PC em tempo real.",
    };
  }

  if (/(print|captura|screenshot|foto da tela)/.test(text)) {
    return {
      text: "Captura feita. A imagem foi salva e copiada para você colar com Ctrl+V.",
      signal: "local",
      detail: "Nada de imagem enviado para a nuvem.",
    };
  }

  if (/(anota|anote|nota|lembrar de comprar)/.test(text)) {
    const note = raw
      .replace(/^.*?(anota[r]?|anote|cria uma nota|nota)\s*/i, "")
      .replace(/^\s*(que|:|-)\s*/i, "")
      .trim();
    return {
      text: note ? `Anotado: “${note}”.` : "Claro. O que você quer anotar?",
      signal: "local",
      detail: "Suas notas ficam num banco de dados local.",
    };
  }

  if (/(cancela|cancelar|para o desligamento)/.test(text)) {
    return {
      text: "Cancelado. Seu PC segue como está.",
      signal: "privado",
    };
  }

  if (/(desliga|desligar|reinicia|reiniciar|suspende|hiberna)/.test(text)) {
    const minutes = durationInMinutes(text);
    const op = /reinici/.test(text) ? "restart" : "shutdown";
    return {
      text: `Para ${op === "restart" ? "reiniciar" : "desligar"} o PC ${
        minutes ? humanDelay(minutes) : "agora"
      }, preciso da sua confirmação. Isso fecha tudo o que estiver aberto.`,
      signal: "confirmar",
      detail: "Ação destrutiva: nada acontece sem um “sim”.",
      actions: [
        {
          label: op === "restart" ? "Sim, reiniciar" : "Sim, desligar",
          value: `power:${op === "restart" ? "restart" : "off"}:${minutes ?? 0}`,
          primary: true,
        },
        { label: "Não, cancelar", value: "power:cancel" },
      ],
    };
  }

  if (/(bloqueia|bloquear|tranca|lock)/.test(text)) {
    return {
      text: "O Windows bloqueia na hora, com a sua senha de verdade. Para voltar, nenhuma senha do Orbi: a mesma de sempre.",
      signal: "local",
      detail: "O bloqueio usa o próprio Windows, não uma tela do app.",
    };
  }

  if (/(lembra|lembrete|me avisa|alarme|aviso)/.test(text)) {
    const minutes = durationInMinutes(text);
    const task = raw
      .replace(/^.*?(lembra[r]?|lembrete|me avisa[r]?|alarme|aviso)\s*/i, "")
      .replace(/\b(em|daqui a|por|dentro de)\s+\d+\s*(min\w*|h\w*)\b/i, "")
      .replace(/^\s*(de|para|que|pra)\s+/i, "")
      .replace(/[.!?]+$/, "")
      .trim();

    if (!minutes) {
      return {
        text: "Claro. Em quanto tempo eu te aviso?",
        signal: "local",
        actions: [
          { label: "Em 10 min", value: "reminder:10", primary: true },
          { label: "Em 1 hora", value: "reminder:60" },
        ],
      };
    }
    return {
      text: `Vou te avisar ${humanDelay(minutes)}${task ? ` para ${task}` : ""}. O lembrete fica salvo neste PC e funciona sem internet.`,
      signal: "local",
      detail: `Próximo aviso às ${clockIn(minutes)}.`,
    };
  }

  if (/(pomodoro|timer|cronometro|contagem)/.test(text)) {
    return {
      text: "Pomodoro de 25 minutos começado. No fim, eu aviso e sugiro uma pausa.",
      signal: "local",
      detail: "Você pode combinar com o modo foco nas configurações.",
    };
  }

  if (/(rotina|modo estudo|modo cinema|encerrar o dia|bom dia|macro)/.test(text)) {
    return {
      text: "Monta uma rotina com uma frase. “Modo estudo: abre o Word, fecha as redes sociais, ativa o Foco e coloca o volume em 20%.”",
      signal: "ia",
      detail: "Depois é só dizer o nome da rotina.",
    };
  }

  if (/(volume|mudo|muta|silencio|som)/.test(text)) {
    if (/(mudo|muta|silencio)/.test(text)) {
      return { text: "Som silenciado.", signal: "local" };
    }
    const match = text.match(/(\d{1,3})\s*%?/);
    if (match) {
      const level = Math.min(100, Math.max(0, parseInt(match[1], 10)));
      return {
        text: `Volume em ${level}%.`,
        signal: "local",
        detail: "Ajuste direto no áudio do Windows.",
      };
    }
    return {
      text: "Em quanto você quer o volume?",
      signal: "local",
      actions: [
        { label: "30%", value: "volume:30", primary: true },
        { label: "Silenciar", value: "volume:mute" },
      ],
    };
  }

  if (/(brilho|tela mais clara|tela mais escura)/.test(text)) {
    const match = text.match(/(\d{1,3})/);
    return {
      text: match ? `Brilho em ${Math.min(100, parseInt(match[1], 10))}%.` : "Brilho ajustado. Quer mais claro ou mais escuro?",
      signal: "local",
    };
  }

  const closeIntent = /(fecha|fechar|encerra)\b/.test(text);
  const apps = APPS.filter((candidate) =>
    candidate.names.some((name) => text.includes(name)),
  );
  const app = apps[0];

  if (closeIntent && app) {
    return {
      text: `Quer mesmo fechar ${app.article} ${app.name}? Algo não salvo pode se perder.`,
      signal: "confirmar",
      actions: [
        { label: `Sim, fechar ${app.article} ${app.name}`, value: `close:${app.name}`, primary: true },
        { label: "Deixar aberto", value: "close:cancel" },
      ],
    };
  }

  if (apps.length > 1) {
    const names = apps.slice(0, 3).map((item) => `${item.article} ${item.name}`);
    const list = names.length === 2 ? names.join(" e ") : `${names.slice(0, -1).join(", ")} e ${names[names.length - 1]}`;
    return {
      text: `Abrindo ${list}.`,
      signal: "local",
      detail: "Tudo resolvido no seu Windows, sem gastar IA.",
    };
  }

  if (app) {
    return {
      text: `Abrindo ${app.article} ${app.name}.`,
      signal: "local",
      detail: "Procura no Menu Iniciar do seu Windows, sem internet.",
    };
  }

  if (/(obrigad|valeu|brigad)/.test(text)) {
    return { text: "De nada. Alt+Shift+O quando precisar.", signal: "local" };
  }

  if (/(tchau|até logo|ate logo|fecha a nova)/.test(text)) {
    return {
      text: "Até já. Continuo na bandeja do Windows.",
      signal: "local",
    };
  }

  if (/(json|codigo|código|como funciona a ia|gemini|api|chave)/.test(text)) {
    return {
      text: "Você fala em português. A IA devolve uma ação e o Orbi confere se ela está na lista permitida. Sua chave é gratuita e fica no Cofre do Windows.",
      signal: "privado",
      detail: "O código nunca aparece para você — só a resposta.",
    };
  }

  return {
    text: "",
    needsAi: true,
    signal: "ia",
  };
}

/* ------------------------------------------------------------------ */
/* Componente                                                         */
/* ------------------------------------------------------------------ */
export default function OrbiChat({ onDownload, onLogin }: OrbiChatProps) {
  const [messages, setMessages] = useState<Mensagem[]>([
    {
      id: 1,
      from: "orbi",
      text: "Oi. Eu sou o Orbi. O que você precisa?",
      signal: "local",
      detail: "Experimente os exemplos ou escreva do seu jeito.",
    },
    {
      id: 2,
      from: "voce",
      text: "Me lembra em 20 min de beber água.",
    },
    {
      id: 3,
      from: "orbi",
      text: "Combinado. Te aviso em 20 minutos. O lembrete fica no seu PC e continua valendo mesmo se você reiniciar.",
      signal: "local",
      detail: "Funciona sem internet.",
    },
  ]);
  const [value, setValue] = useState("");
  const [thinking, setThinking] = useState(false);
  const [ai, setAi] = useState<AiStatus>("unknown");

  useEffect(() => onAiStatus(setAi), []);
  const seq = useRef(3);
  const timer = useRef<number | null>(null);
  const listRef = useRef<HTMLDivElement>(null);

  useEffect(() => {
    const el = listRef.current;
    if (el) el.scrollTop = el.scrollHeight;
  }, [messages, thinking]);

  useEffect(() => {
    return () => {
      if (timer.current) window.clearTimeout(timer.current);
    };
  }, []);

  const addMessage = (message: Omit<Mensagem, "id">) => {
    seq.current += 1;
    setMessages((previous) => [...previous, { ...message, id: seq.current }]);
  };

  const push = (message: Omit<Mensagem, "id">) => {
    addMessage(message);
  };

  /** Resposta local (rápida) ou Gemini quando o comando foge do script. */
  const reply = (userText: string, response: Resposta) => {
    push({ from: "voce", text: userText });
    setThinking(true);
    if (timer.current) window.clearTimeout(timer.current);

    if (!response.needsAi) {
      timer.current = window.setTimeout(() => {
        push({ from: "orbi", ...response });
        setThinking(false);
      }, 420 + Math.random() * 200);
      return;
    }

    // Precisa da IA: pergunta ao Gemini (Firebase AI Logic) com o histórico.
    const history = messages
      .filter((message) => !message.needsAi)
      .slice(-6)
      .map((message) => ({
        role: message.from === "voce" ? ("user" as const) : ("model" as const),
        text: message.text,
      }));

    void askGemini(userText, history).then((answer) => {
      setThinking(false);
      if (answer) {
        push({
          from: "orbi",
          text: answer,
          signal: "ia",
          detail: "Respondido pelo Gemini.",
        });
      } else {
        push({
          from: "orbi",
          text: "Não consegui falar com o Gemini agora. Mesmo sem ele eu abro apps, ajusto o volume, crio lembretes e mostro o status do PC — tenta um desses.",
          signal: "privado",
          detail: "Sem internet, o app continua funcionando no modo local.",
          actions: [
            { label: "Ver exemplos", value: "try:apps", primary: true },
            { label: "Baixar o Orbi", value: "app:download" },
          ],
        });
      }
    });
  };

  const send = (text: string) => {
    const clean = text.trim();
    if (!clean || thinking) return;
    setValue("");
    reply(clean, interpret(clean));
  };

  const runAction = (action: Acao) => {
    if (thinking) return;
    if (action.value === "app:download") {
      onDownload();
      addMessage({ from: "voce", text: action.label });
      setThinking(true);
      timer.current = window.setTimeout(() => {
        addMessage({
          from: "orbi",
          text: "Abri o download do Orbi para Windows 11.",
          signal: "privado",
        });
        setThinking(false);
      }, 480);
      return;
    }
    if (action.value === "app:login") {
      addMessage({ from: "voce", text: action.label });
      window.setTimeout(() => onLogin?.(), 280);
      return;
    }
    if (action.value.startsWith("volume:")) {
      const level = action.value.split(":")[1];
      reply(action.label, {
        text: level === "mute" ? "Som silenciado." : `Volume em ${level}%.`,
        signal: "local",
      });
      return;
    }
    if (action.value.startsWith("reminder:")) {
      const minutes = parseInt(action.value.split(":")[1], 10);
      reply(action.label, {
        text: `Combinado. Te aviso ${humanDelay(minutes)}.`,
        signal: "local",
        detail: `Próximo aviso às ${clockIn(minutes)}.`,
      });
      return;
    }
    reply(action.label, actionReply(action.value));
  };

  const submit = (event: FormEvent) => {
    event.preventDefault();
    send(value);
  };

  const reset = () => {
    if (timer.current) window.clearTimeout(timer.current);
    seq.current = 3;
    setThinking(false);
    setValue("");
    setMessages([
      {
        id: 1,
        from: "orbi",
        text: "Oi. Eu sou o Orbi. O que você precisa?",
        signal: "local",
        detail: "Experimente os exemplos ou escreva do seu jeito.",
      },
    ]);
  };

  return (
    <div className="relative">
      <div className="overflow-hidden border border-ink/20 bg-film shadow-[0_30px_70px_-32px_rgba(23,18,14,0.65)]">
        {/* barra da janela */}
        <div className="flex items-center justify-between gap-3 border-b border-white/10 px-4 py-3 sm:px-5">
          <div className="flex min-w-0 items-center gap-3">
            <span className="grid h-8 w-8 place-items-center bg-signal text-white">
              <Mark className="h-4 w-4" />
            </span>
            <div className="min-w-0">
              <p className="font-cond text-[16px] font-bold uppercase leading-none tracking-[0.08em] text-bone">
                Orbi
              </p>
              <p className="mt-1 truncate font-mono text-[10px] uppercase tracking-[0.12em] text-bone/65">
                prévia interativa
              </p>
            </div>
          </div>

          <div className="flex items-center gap-2">
            <span
              className={`label hidden items-center gap-1.5 rounded-full border px-2.5 py-1 sm:flex ${
                ai === "online"
                  ? "border-signal/60 bg-signal/10 text-[#f0a38e]"
                  : "border-white/15 text-bone/75"
              }`}
              title="O assistente conversa de verdade quando a IA está ativa."
            >
              <span
                className={`h-1.5 w-1.5 rounded-full ${ai === "online" ? "bg-signal" : "bg-[#d9d1c1]"}`}
                aria-hidden
              />
              {aiLabel(ai)}
            </span>
            <button
              type="button"
              onClick={reset}
              className="label rounded-full border border-white/15 px-2.5 py-1 text-bone/65 transition-colors hover:border-white/35 hover:text-bone"
            >
              recomeçar
            </button>
          </div>
        </div>

        {/* conversa */}
        <div
          ref={listRef}
          role="log"
          aria-live="polite"
          aria-relevant="additions"
          aria-busy={thinking}
          aria-label="Conversa de prévia do Orbi"
          className="thin-scrollbar min-h-[270px] max-h-[340px] space-y-4 overflow-y-auto px-3.5 py-5 sm:max-h-[360px] sm:px-5"
        >
          {messages.map((message, index) => {
            const mine = message.from === "voce";
            const signal = message.signal ? SINAIS[message.signal] : null;
            const isLast = index === messages.length - 1;
            return (
              <div
                key={message.id}
                className={`animate-fade-up flex ${mine ? "justify-end" : "justify-start"}`}
              >
                <div className={`min-w-0 ${mine ? "max-w-[85%]" : "max-w-[90%]"}`}>
                  <div
                    className={`px-3.5 py-2.5 text-[13.5px] leading-[1.55] sm:text-[14px] ${
                      mine
                        ? "bg-signal text-white"
                        : "border border-white/10 bg-film-2 text-bone"
                    }`}
                  >
                    <p className="whitespace-pre-line">{message.text}</p>
                  </div>

                  {!mine && signal && (
                    <div className="mt-1.5 flex flex-wrap items-center gap-x-2 gap-y-1 pl-0.5">
                      <span
                        className={`label inline-flex items-center gap-1.5 rounded-full px-2 py-0.5 ${signal.className}`}
                      >
                        <span className={`h-1.5 w-1.5 rounded-full ${signal.dot}`} aria-hidden />
                        {signal.label}
                      </span>
                      {message.detail && (
                        <span className="font-mono text-[10px] leading-snug text-bone/55">
                          {message.detail}
                        </span>
                      )}
                    </div>
                  )}

                  {!mine && isLast && message.actions && message.actions.length > 0 && (
                    <div className="mt-2.5 flex flex-wrap gap-2">
                      {message.actions.map((action) => (
                        <button
                          key={action.value + action.label}
                          type="button"
                          disabled={thinking}
                          onClick={() => runAction(action)}
                          className={`rounded-full px-3 py-1.5 font-mono text-[11px] leading-none transition-colors disabled:cursor-not-allowed disabled:opacity-50 ${
                            action.primary
                              ? "bg-signal text-white hover:bg-[#d9563a]"
                              : "border border-white/20 text-bone/75 hover:border-white/45 hover:text-bone"
                          }`}
                        >
                          {action.label}
                        </button>
                      ))}
                    </div>
                  )}
                </div>
              </div>
            );
          })}

          {thinking && (
            <div className="flex items-center gap-2.5" aria-live="polite">
              <span className="flex items-center gap-1.5 border border-white/10 bg-film-2 px-3 py-2.5">
                {[0, 1, 2].map((dot) => (
                  <span
                    key={dot}
                    className="thinking-dot h-1.5 w-1.5 rounded-full bg-signal"
                    style={{ animationDelay: `${dot * 140}ms` }}
                  />
                ))}
                <span className="label ml-1.5 text-bone/60">
                  {thinking ? "pensando" : ""}
                </span>
              </span>
            </div>
          )}
        </div>

        {/* entrada */}
        <div className="border-t border-white/10 px-3.5 pb-3.5 pt-3 sm:px-5">
          <div className="no-scrollbar mb-3 flex items-center gap-2 overflow-x-auto">
            {EXAMPLES.map((example) => (
              <button
                key={example}
                type="button"
                disabled={thinking}
                onClick={() => send(example)}
                className="shrink-0 rounded-full border border-white/15 px-2.5 py-1 font-mono text-[10.5px] leading-none text-bone/75 transition-colors hover:border-signal hover:text-bone disabled:opacity-50"
              >
                {example}
              </button>
            ))}
          </div>

          <form onSubmit={submit} className="flex items-center gap-2">
            <label htmlFor="orbi-chat-input" className="sr-only">
              Mensagem para o Orbi
            </label>
            <input
              id="orbi-chat-input"
              type="text"
              value={value}
              onChange={(event) => setValue(event.target.value)}
              maxLength={240}
              autoComplete="off"
              placeholder="Peça alguma coisa…"
              className="h-11 min-w-0 flex-1 rounded-[2px] border border-white/15 bg-[#0f0c09] px-3.5 font-mono text-[12.5px] text-bone outline-none transition-colors placeholder:text-bone/45 focus:border-signal"
            />
            <button
              type="submit"
              disabled={thinking || !value.trim()}
              aria-label="Enviar mensagem"
              className="grid h-11 w-11 shrink-0 place-items-center bg-signal text-white transition-colors hover:bg-[#d9563a] disabled:cursor-not-allowed disabled:bg-white/10 disabled:text-bone/40"
            >
              {thinking ? (
                <Spinner className="h-3.5 w-3.5" />
              ) : (
                <Icon name="arrow-right" className="h-4 w-4" />
              )}
            </button>
          </form>

          <div className="mt-2.5 flex items-center justify-between gap-3">
            <span className="label text-bone/45">prévia · sem alterar o seu PC</span>
            <span className="hidden font-mono text-[10px] text-bone/45 sm:block">
              Enter envia
            </span>
          </div>
        </div>
      </div>

      <p className="mt-3 flex items-center gap-2 font-mono text-[10.5px] leading-relaxed text-warm">
        <span className="h-1.5 w-1.5 shrink-0 bg-signal" aria-hidden />
        Este assistente roda de verdade no navegador: regras locais para os comandos do app e Gemini
        para o resto. No Windows, ele age no seu PC.
      </p>
    </div>
  );
}
