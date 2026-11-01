export const NAV = [
  { label: "O que faz", section: "faz" },
  { label: "Ferramentas", section: "ferramentas" },
  { label: "O app", section: "app" },
  { label: "Sua conta", section: "conta" },
  { label: "Baixar", section: "download" },
] as const;

/** Frases que a pessoa realmente digita — é isso que a página vende. */
export const EXAMPLES = [
  {
    saida: "volume em 30",
    efeito: "o som do sistema desce na hora, sem abrir o mixer.",
  },
  {
    saida: "/captura de tela",
    efeito: "salva em Imagens\\Orbi e copia para a área de transferência.",
  },
  {
    saida: "limpa o dns",
    efeito: "aquela internet estranha costuma voltar com isso.",
  },
  {
    saida: "fixa essa janela à esquerda",
    efeito: "a janela ativa vai para a metade esquerda da tela.",
  },
  {
    saida: "lembra de pagar a conta de luz amanhã às 9",
    efeito: "aviso na bandeja na hora certa, mesmo com a barra fechada.",
  },
  {
    saida: "o que eu tava fazendo ontem à tarde?",
    efeito: "o Orbi lê a própria memória e responde.",
  },
];

export const STEPS = [
  {
    title: "Baixe e instale",
    body: "Um .exe. Ele instala por usuário, cria o atalho e abre a barra — sem terminal, sem administrador.",
  },
  {
    title: "Aperte Alt+Shift+O",
    body: "A barra aparece sobre qualquer programa. Enter executa; / abre o catálogo de ferramentas.",
  },
  {
    title: "Entre na conta quando quiser",
    body: "É a mesma conta do site: lembretes, notas, rotinas e memória passam a acompanhar você.",
  },
];

export const ACCOUNT_ITEMS = [
  { label: "Memória", body: "o que você pediu, o que o Orbi fez, o que você anotou." },
  { label: "Rotinas", body: "sequências com nome, executadas no PC em um comando." },
  { label: "Lembretes e notas", body: "criados no site ou no app — é a mesma lista." },
  { label: "Preferências", body: "atalho global, bloqueio por inatividade, modo Cloud." },
];

export const LOCAL_ONLY = ["Senhas", "Área de transferência", "Chave da IA", "Histórico de teclado"];

export const FAQ = [
  {
    q: "Preciso pagar para usar?",
    a: "Não. O app, as ferramentas e a conta são gratuitos. Se você quiser conversa livre com IA, pode colar uma chave gratuita do Google AI Studio (ou entrar na conta e usar a do Orbi).",
  },
  {
    q: "Preciso de conta para instalar?",
    a: "Para instalar, não. Todas as ferramentas rodam sem conta. A conta existe para levar lembretes, notas, rotinas e memória de um PC para outro.",
  },
  {
    q: "O Orbi mexe no sistema sem eu mandar?",
    a: "Não. Nada acontece por conta própria: cada ação parte de um comando seu. A única exceção é o bloqueio por inatividade, que você liga no ajuste e avisa 15 segundos antes.",
  },
  {
    q: "Funciona sem internet?",
    a: "Sim. O catálogo de ferramentas, os lembretes, as notas e as rotinas funcionam offline. Só a conversa livre com a IA precisa de rede.",
  },
  {
    q: "Meus dados vão para onde?",
    a: "Notas, histórico de cópia, o log do app e a chave da IA ficam só no seu PC. A conta guarda o que precisa estar nos dois lugares: lembretes, notas, rotinas, preferências e memória.",
  },
  {
    q: "Qual Windows eu preciso?",
    a: "Windows 11 (ou Windows 10 1809+), 64 bits. A instalação é por usuário e não pede privilégio de administrador.",
  },
];

/** Usado pela área da conta e pelo espelho do app: o que sincroniza. */
export const SYNC_ITEMS = [
  { key: "preferencias" as const, label: "Preferências", hint: "atalho, tema e o que sincronizar" },
  { key: "memoria" as const, label: "Memória", hint: "o seu dia, para perguntar depois" },
  { key: "rotinas" as const, label: "Rotinas", hint: "modo estudo, cinema…" },
  { key: "atalhos" as const, label: "Atalhos de texto", hint: "snippets que você criou" },
];

/** Notas da versão mostradas na área da conta. */
export const RELEASE_NOTES = [
  "120+ ferramentas do Windows na barra, em 11 grupos",
  "Barra compacta: Enter executa, / abre o catálogo, F1 também",
  "Modo Cloud opcional (Gemini 2.5), com chave cifrada ou pela conta",
  "Lembretes, notas, histórico de cópia e memória do dia",
  "Rotinas e preferências na conta, iguais no site e no app",
];
