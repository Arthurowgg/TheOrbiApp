export const NAV = [
  { label: "O que faz", section: "faz" },
  { label: "Exemplos", section: "exemplos" },
  { label: "Sua conta", section: "conta" },
  { label: "Privacidade", section: "privacidade" },
  { label: "Baixar", section: "download" },
] as const;

/** Frases que a pessoa realmente digita — é isso que a página vende. */
export const EXAMPLES = [
  {
    saida: "abre o spotify e baixa o volume",
    efeito: "Spotify em primeiro plano, volume em 20%.",
  },
  {
    saida: "modo estudo",
    efeito: "Abre a apostila, silencia as notificações e abre um timer de 50 minutos.",
  },
  {
    saida: "lembra de pagar a conta de luz amanhã às 9",
    efeito: "Aviso na bandeja na hora certa, mesmo com o app fechado.",
  },
  {
    saida: "o que eu tava fazendo ontem à tarde?",
    efeito: "O Orbi lembra e responde — é a memória dele sobre o seu dia.",
  },
  {
    saida: "tô cansado, põe algo leve pra tocar",
    efeito: "Abre a música, baixa o brilho e deixa o PC em modo silencioso.",
  },
  {
    saida: "trava o pc quando eu sair",
    efeito: "Bloqueio automático depois do tempo parado, com aviso antes.",
  },
];

export const FEATURES = [
  {
    icon: "apps" as const,
    title: "Fala como você fala",
    body: "“abre o zap”, “aumenta o som”, “isso aí que eu abri ontem”. Sem comando decorado, sem barra de menu.",
  },
  {
    icon: "bolt" as const,
    title: "Um pedido, várias coisas",
    body: "“modo cinema” abre o player, apaga a luz da tela, silencia o celular conectado e ativa o Foco.",
  },
  {
    icon: "bell" as const,
    title: "Não deixa passar nada",
    body: "Lembretes, timers e notas que aparecem na hora marcada, mesmo quando o app está na bandeja.",
  },
  {
    icon: "sparkle" as const,
    title: "Memória de verdade",
    body: "O Orbi guarda o que você fez, pediu e anotou — e usa isso para responder “o que eu tava fazendo ontem?”.",
  },
  {
    icon: "cloud" as const,
    title: "Tudo na sua conta",
    body: "Você entra uma vez. Preferências, rotinas, memória e lembretes ficam salvos na sua conta e aparecem em qualquer PC.",
  },
  {
    icon: "lock" as const,
    title: "Nada sai sem você",
    body: "Notas, área de transferência e a chave da IA nunca vão para a nuvem. Só o que você quer sincronizar sincroniza.",
  },
];

export const STEPS = [
  {
    title: "Baixe e instale",
    body: "Um arquivo. Ele instala, cria o atalho e abre o Orbi — sem terminal, sem administrador.",
  },
  {
    title: "Entre na sua conta",
    body: "E-mail e senha ou Google. É a mesma conta do site, então o que você já salvou está lá.",
  },
  {
    title: "Aperte Alt+Shift+O",
    body: "A barra aparece sobre qualquer programa. Escreva, ou peça em português mesmo.",
  },
];

export const ACCOUNT_ITEMS = [
  { label: "Memória da IA", body: "o que você pediu, o que o Orbi fez, o que você anotou." },
  { label: "Rotinas", body: "seus atalhos de várias ações, prontos em qualquer PC." },
  { label: "Lembretes e notas", body: "marcados no app, visíveis no site." },
  { label: "Preferências", body: "atalho, tema, o que sincronizar." },
];

export const LOCAL_ONLY = ["Senhas", "Área de transferência", "Chave da IA", "Histórico de teclado"];

export const FAQ = [
  {
    q: "Preciso pagar para usar?",
    a: "Não. O app e a conta são gratuitos, sem assinatura e sem cartão.",
  },
  {
    q: "Preciso de conta para instalar?",
    a: "Para instalar, não. Mas é a conta que guarda a memória, as rotinas e os lembretes — é ela que faz o Orbi lembrar de você em qualquer PC.",
  },
  {
    q: "A IA sabe tudo o que eu faço?",
    a: "Só o texto que você escreve vai para o modelo, e apenas quando o pedido foge do que o Windows resolve sozinho. Notas, senhas e área de transferência nunca entram nesse envio.",
  },
  {
    q: "Funciona sem internet?",
    a: "Sim. Abrir apps, mexer no som, lembretes e rotinas funcionam offline. A conversa com a IA é a única parte que precisa de rede.",
  },
  {
    q: "Qual Windows eu preciso?",
    a: "Windows 11 (ou Windows 10 1809+), 64 bits. A instalação é por usuário e não pede privilégio de administrador.",
  },
];

/** Usado pela área da conta e pelo espelho do app: o que sincroniza. */
export const SYNC_ITEMS = [
  { key: "preferencias" as const, label: "Preferências", hint: "atalho, tema e o que sincronizar" },
  { key: "apelidos" as const, label: "Aprendizado de apps", hint: "o Orbi reconhece o app sozinho" },
  { key: "rotinas" as const, label: "Rotinas", hint: "modo estudo, cinema…" },
  { key: "atalhos" as const, label: "Atalhos de texto", hint: "snippets que você criou" },
];

/** Notas da versão mostradas na área da conta. */
export const RELEASE_NOTES = [
  "Conversa com memória: o Orbi lembra do que você pediu e fez",
  "Comandos em português para abrir apps, volume, brilho, energia e captura",
  "Lembretes, timers, notas e histórico de área de transferência",
  "Rotinas e preferências na sua conta, em qualquer PC",
  "Bloqueio por inatividade com aviso e senha do Windows",
];
