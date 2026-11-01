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
    saida: "deixa pronto pra aula",
    efeito: "abre o Word, baixa o som, ajusta o brilho e abre a pasta do material — na ordem.",
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
    body: "É a mesma conta do site: preferências e memória passam a acompanhar você.",
  },
];

export const ACCOUNT_ITEMS = [
  { label: "Memória", body: "o que você pediu, o que o Orbi fez, o que você anotou." },

  { label: "Lembretes e notas", body: "criados no site ou no app — é a mesma lista." },
  { label: "Preferências", body: "atalho global, bloqueio por inatividade, modo Cloud." },
];

export const FAQ = [
  {
    q: "Preciso pagar?",
    a: "Não. O app e a conta são gratuitos.",
  },
  {
    q: "Preciso de conta para instalar?",
    a: "Não. Todas as ferramentas funcionam sem conta. A conta serve para levar o que é seu de um PC para outro.",
  },
  {
    q: "O Orbi mexe no PC sozinho?",
    a: "Não. Cada ação parte de um comando seu. A única exceção é o bloqueio por inatividade, que você liga nos ajustes.",
  },
  {
    q: "E a inteligência artificial?",
    a: "O Orbi usa o Gemini com uma chave sua, gratuita no Google AI Studio. Sem chave ele continua obedecendo as ferramentas.",
  },
  {
    q: "Qual Windows eu preciso?",
    a: "Windows 10 (1809+) ou Windows 11, 64 bits. A instalação é por usuário e não pede administrador.",
  },
];

/** O que a conta acompanha. Sem listar dados guardados, sem detalhes de banco. */
export const SYNC_ITEMS = [
  { key: "memoria" as const, label: "Memória" },
  { key: "preferencias" as const, label: "Preferências" },
];

/** Notas da versão mostradas na área da conta. */
export const RELEASE_NOTES = [
  "384 ferramentas do Windows na barra",
  "Barra compacta: Enter executa, / abre o catálogo, F1 também",
  "IA que usa o PC: vê a tela, clica, digita e encadeia ferramentas",
  "Memória do dia e histórico de cópia, no seu PC",
  "Preferências na conta, iguais no site e no app",
];
