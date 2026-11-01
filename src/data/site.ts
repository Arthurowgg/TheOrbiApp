export const NAV = [
  { label: "Recursos", section: "recursos" },
  { label: "O app", section: "app" },
  { label: "Conta", section: "conta" },
  { label: "Segurança", section: "seguranca" },
  { label: "Download", section: "download" },
] as const;

export const CAPABILITIES = [
  "Abre seus apps",
  "Volume e brilho",
  "Lembretes e timers",
  "Rotinas de um comando",
  "Bloqueio automático",
  "Gemini quando precisa",
];

export const FEATURES = [
  {
    icon: "apps" as const,
    title: "Apps do seu jeito",
    body: "“Abre o zap.” O Orbi entende apelidos, encontra o app no seu Windows e traz para frente — sem menu, sem atalho decorado.",
    tag: "local",
  },
  {
    icon: "sliders" as const,
    title: "O PC na sua mão",
    body: "Volume, brilho, Wi-Fi, energia, captura de tela. O que hoje são cinco cliques vira uma frase.",
    tag: "local",
  },
  {
    icon: "bell" as const,
    title: "Nada some do radar",
    body: "Lembretes, timers, notas e o histórico do que você copiou — guardado no seu PC, não numa conta qualquer.",
    tag: "local",
  },
  {
    icon: "bolt" as const,
    title: "Rotinas",
    body: "Modo estudo, modo cinema, encerrar o dia. Abre, fecha e ajusta tudo de uma vez, com uma frase só.",
    tag: "local",
  },
  {
    icon: "sparkle" as const,
    title: "Gemini quando ajuda",
    body: "Comando fora do script? O Orbi pergunta ao Gemini e devolve a resposta pronta para você — sem abrir o navegador.",
    tag: "IA",
  },
  {
    icon: "cloud" as const,
    title: "A mesma conta no site e no app",
    body: "Apelidos, atalhos, rotinas e preferências ficam na sua conta (Firestore) e chegam em cada PC. Notas, área de transferência e a chave da IA continuam só no computador.",
    tag: "conta",
  },
  {
    icon: "lock" as const,
    title: "Privada por padrão",
    body: "Senhas, área de transferência e webcam ficam no PC. Quando a IA é usada, vai só o texto do comando.",
    tag: "privado",
  },
];

export const SYNC_ITEMS = [
  { key: "preferencias" as const, label: "Preferências", hint: "tema, atalho e idioma" },
  { key: "apelidos" as const, label: "Apelidos de apps", hint: "“zap” = WhatsApp" },
  { key: "rotinas" as const, label: "Rotinas", hint: "modo estudo, cinema…" },
  { key: "atalhos" as const, label: "Atalhos de texto", hint: "snippets que você criou" },
];

export const LOCAL_ONLY = ["Notas", "Área de transferência", "Chave da API", "Logs de segurança"];

export const DOWNLOAD_STEPS = [
  { title: "Baixe o instalador", body: "OrbiSetup.exe, um arquivo só, para Windows 11." },
  { title: "Execute o setup", body: "Ele instala, cria o atalho e abre o app. Sem terminal." },
  { title: "Aperte Alt+Space", body: "A barra abre em qualquer programa, sobre qualquer janela." },
];

export const LOCK_TIMELINE = [
  { time: "0:00", title: "Você se afasta", body: "Teclado e mouse ficam parados." },
  {
    time: "5:00",
    title: "Aviso na tela",
    body: "“Vou bloquear em 15 s. Mexa o mouse para cancelar.”",
  },
  { time: "5:15", title: "Windows bloqueado", body: "Só a sua senha do Windows abre." },
];

export const LOCK_EXCEPTIONS = ["Vídeo em tela cheia", "Jogo", "Chamada", "Apresentação"];

export const RELEASE_NOTES = [
  "App Windows nativo: bandeja, Alt+Space e inicialização com o sistema",
  "Comandos em português para abrir apps, volume, brilho, energia e captura",
  "Lembretes, timers, notas e histórico de área de transferência",
  "Rotinas e atalhos de texto sincronizados com a conta",
  "Gemini integrado para comandos que fogem do script",
  "Bloqueio por inatividade com aviso, exceções e senha do Windows",
];

export const FAQ = [
  {
    q: "O Orbi é realmente grátis?",
    a: "Sim. O app, o site e a conta não têm assinatura nem cartão. O uso do Gemini passa pela camada gratuita do Firebase, com limites do Google — para uso pessoal, sobra.",
  },
  {
    q: "Preciso de conta para usar o app?",
    a: "Não. O app funciona sem login. A conta serve para sincronizar preferências, apelidos e rotinas entre PCs e para reencontrar seus downloads.",
  },
  {
    q: "Onde ficam meus dados?",
    a: "Notas, área de transferência, histórico e a chave da IA ficam no seu PC. Na nuvem ficam só o perfil (nome e e-mail), a lista de aparelhos e as preferências que você escolher sincronizar — no Firestore, com regras que limitam cada documento ao próprio dono.",
  },
  {
    q: "Como o login funciona?",
    a: "Firebase Authentication: e-mail e senha com hash no servidor, verificação de e-mail, redefinição por link e entrada com Google. O Orbi nunca vê sua senha.",
  },
  {
    q: "Qual Windows eu preciso?",
    a: "Windows 11 (22H2+) ou Windows 10 1809+, 64 bits. O instalador não pede privilégio de administrador para a instalação por usuário.",
  },
  {
    q: "Como sei que o download é autêntico?",
    a: "Cada release publica o SHA256SUMS.txt junto do instalador. O site mostra o hash da última versão e você pode conferir com o comando indicado no rodapé da seção de download.",
  },
];

export const STATS = [
  { value: "1", label: "instalador", hint: "sem Python, sem runtime extra" },
  { value: "~40 MB", label: "em disco", hint: "app nativo, sem Electron" },
  { value: "< 200 ms", label: "para abrir", hint: "do atalho até a barra pronta" },
  { value: "0", label: "dados sensíveis na nuvem", hint: "notas e teclado ficam no PC" },
];
