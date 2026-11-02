#!/usr/bin/env node
/**
 * Gera `src/data/tools.ts` a partir do catálogo real do app.
 *
 * Fonte da verdade: os `Add("id", "Nome", "Grupo", …)` em
 * `app/Orbi.App/Services/Tools.cs`, `ToolsExtra.cs` e `ToolsWeb.cs`.
 * Assim o site nunca anuncia um número diferente do que o app executa.
 *
 *   node scripts/tools-catalog.mjs          # escreve src/data/tools.ts
 *   node scripts/tools-catalog.mjs --check  # só confere (falha se divergir)
 */
import { readFileSync, writeFileSync } from "node:fs";
import { fileURLToPath } from "node:url";
import { dirname, join } from "node:path";

const raiz = join(dirname(fileURLToPath(import.meta.url)), "..");
const servicos = join(raiz, "app/Orbi.App/Services");

const DICAS = {
  "Área de trabalho": "a mesa do PC do seu jeito",
  "Janelas": "mover, encaixar e fechar janelas",
  "Teclado e mouse": "teclas e cliques sem tocar no mouse",
  "Tela e captura": "ver, capturar e gravar a tela",
  "Som e mídia": "volume, saída de som e players",
  "Arquivos e pastas": "achar, abrir e organizar arquivos",
  "Rede e internet": "IP, ping, DNS e Wi-Fi",
  "Energia e bateria": "energia, brilho e suspender",
  "Apps": "abrir e fechar programas",
  "Privacidade e segurança": "permissões, firewall e contas",
  "Acessibilidade": "lupa, legendas e contraste",
  "Sistema": "o PC inteiro sem abrir painel",
  "Desenvolvimento": "terminal, portas e versões",
  "Texto e números": "transformar o texto copiado",
  "Cálculos": "contas, conversões e porcentagem",
  "Orbi": "o próprio Orbi",
  "Áudio e vídeo": "play, pause e faixas",
  "Web e pesquisa": "abre o resultado certo, com o que você disse",
  "Texto": "transforma o texto e devolve copiado",
  "Janelas e mouse": "o mouse e as janelas por você",
  "Tela": "vê e captura a tela",
  "Configurações do Windows": "o painel certo, sem procurar",
  "Aplicativos": "abre o programa instalado",
  "Datas": "datas, idade e feriados",
  "Área de transferência": "lê e transforma o que está copiado",
  "Painéis": "os painéis do Windows",
  "Controle do PC": "vê a tela, clica e digita no seu lugar",
};

/** Grupos na ordem em que o app os registra. */
const ORDEM = [
  "Sistema", "Janelas", "Teclado e mouse", "Tela e captura", "Som e mídia",
  "Área de trabalho", "Arquivos e pastas", "Pastas", "Rede e internet",
  "Energia e bateria", "Apps", "Privacidade e segurança", "Acessibilidade",
  "Desenvolvimento", "Texto e números", "Cálculos", "Orbi", "Áudio e vídeo",
  "Painéis", "Produtividade", "Web e pesquisa", "Texto", "Janelas e mouse",
  "Tela", "Configurações do Windows", "Aplicativos", "Datas",
  "Área de transferência", "Rede e internet", "Controle do PC",
];

// O grupo vem como texto ("Sistema") ou como variável local (desktop, janelas…),
// e a chamada pode ocupar várias linhas — por isso o regex atravessa \s.
const chamada = /\b(?:Add|Media)\(\s*"([^"]+)"\s*,\s*"([^"]+)"\s*,\s*(?:"([^"]+)"|(Forms\.Keys\.\w+)|([A-Za-z_]\w*))\s*[,)]/g;
const declaracao = /^\s*var\s+([A-Za-z_]\w*)\s*=\s*"([^"]+)";/gm;

function ler(nome, cortarEm) {
  let texto = readFileSync(join(servicos, nome), "utf8");
  if (cortarEm) texto = texto.slice(0, texto.indexOf(cortarEm));
  const variaveis = new Map();
  for (const d of texto.matchAll(declaracao)) variaveis.set(d[1], d[2]);
  const itens = [];
  for (const m of texto.matchAll(chamada)) {
    // `Media(id, nome, tecla, …)` é sempre áudio e vídeo
    const grupo = m[4] ? "Áudio e vídeo" : m[3] ?? variaveis.get(m[5]) ?? m[5];
    // o helper local `Add(string id, …)` não é uma ferramenta
    if (grupo === "string") continue;
    itens.push({ id: m[1], nome: m[2], grupo });
  }
  return itens;
}

const itens = [
  // Tools.cs inteiro: inclui o bloco "Controle do PC" (ver, clicar, digitar…),
  // que o app registra depois de ToolsExtra e ToolsWeb.
  ...ler("Tools.cs"),
  ...ler("ToolsExtra.cs"),
  ...ler("ToolsWeb.cs"),
];

const vistos = new Set();
const grupos = new Map();
for (const item of itens) {
  if (vistos.has(item.id)) continue;
  vistos.add(item.id);
  if (!grupos.has(item.grupo)) grupos.set(item.grupo, []);
  grupos.get(item.grupo).push(item.nome);
}

const nomes = [...grupos.keys()].sort((a, b) => {
  const ia = ORDEM.indexOf(a);
  const ib = ORDEM.indexOf(b);
  return (ia === -1 ? 999 : ia) - (ib === -1 ? 999 : ib) || a.localeCompare(b, "pt-BR");
});

const esc = (texto) => texto.replace(/\\/g, "\\\\").replace(/"/g, '\\"');
let saida = `/**
 * Catálogo de ferramentas do app, espelhado no site.
 *
 * GERADO por \`node scripts/tools-catalog.mjs\` a partir de
 * \`app/Orbi.App/Services/Tools.cs\`, \`ToolsExtra.cs\` e \`ToolsWeb.cs\`.
 * Não edite à mão: rode o script depois de mexer no catálogo do app.
 */
export type ToolGroup = {
  name: string;
  hint: string;
  tools: string[];
};

export const TOOL_GROUPS: ToolGroup[] = [
`;

for (const nome of nomes) {
  const ferramentas = grupos.get(nome);
  saida += `  {\n    name: "${esc(nome)}",\n    hint: "${esc(DICAS[nome] ?? "ações diretas no Windows")}",\n    tools: [\n`;
  for (const f of ferramentas) saida += `      "${esc(f)}",\n`;
  saida += "    ],\n  },\n";
}

saida += `];

export const TOOL_COUNT = TOOL_GROUPS.reduce((total, group) => total + group.tools.length, 0);

export const GROUP_COUNT = TOOL_GROUPS.length;
`;

const destino = join(raiz, "src/data/tools.ts");
const anterior = readFileSync(destino, "utf8").trimEnd();
const conteudo = saida.trimEnd();

if (process.argv.includes("--check")) {
  if (anterior !== conteudo) {
    console.error("src/data/tools.ts está diferente do catálogo do app. Rode: node scripts/tools-catalog.mjs");
    process.exit(1);
  }
  console.log(`catálogo em dia: ${vistos.size} ferramentas em ${grupos.size} grupos`);
} else {
  writeFileSync(destino, conteudo + "\n");
  console.log(`src/data/tools.ts: ${vistos.size} ferramentas em ${grupos.size} grupos`);
}
