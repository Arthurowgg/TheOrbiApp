export const NAV = [
  { label: "Recursos", section: "recursos" },
  { label: "Conta e app", section: "conta" },
  { label: "Segurança", section: "seguranca" },
  { label: "Download", section: "download" },
] as const;

export const CAPABILITIES = [
  "Abre seus apps",
  "Volume e brilho",
  "Lembretes e timers",
  "Rotinas de um comando",
  "Bloqueio automático",
  "Voz sem internet",
];

export const FEATURES = [
  {
    icon: "apps" as const,
    title: "Apps do seu jeito",
    body: "“Abre o zap.” A NOVA entende apelidos, encontra o app no seu Windows e traz para frente.",
  },
  {
    icon: "sliders" as const,
    title: "O PC na sua mão",
    body: "Volume, brilho, Wi-Fi, energia e captura de tela. Sem procurar menu nem decorar atalho.",
  },
  {
    icon: "bell" as const,
    title: "Nada some do radar",
    body: "Lembretes, timers, notas e o histórico do que você copiou — tudo guardado no seu PC.",
  },
  {
    icon: "bolt" as const,
    title: "Rotinas",
    body: "Modo estudo, modo cinema, encerrar o dia. Abre, fecha e ajusta tudo com uma frase.",
  },
  {
    icon: "monitor" as const,
    title: "Olho no sistema",
    body: "CPU, memória, disco e bateria, com alerta antes de virar problema e o app que está pesando.",
  },
  {
    icon: "lock" as const,
    title: "Privada por padrão",
    body: "Senhas, área de transferência e webcam ficam no PC. A IA recebe só o texto do comando.",
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
  { title: "Baixe o instalador", body: "Um arquivo só, para Windows 11." },
  { title: "Execute o setup", body: "Ele prepara tudo e cria o atalho. Sem terminal." },
  { title: "Aperte Alt+Space", body: "A barra abre em qualquer programa." },
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
  "Barra de comando com Alt+Space, histórico e autocompletar",
  "IA gratuita com modo básico automático, sem internet",
  "Controle de volume, brilho, energia e captura de tela",
  "Bloqueio por inatividade com aviso e exceções",
  "Lembretes, notas, rotinas e voz offline",
  "Conta NOVA: vínculo entre o site e o app",
];
