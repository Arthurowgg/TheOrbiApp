#!/usr/bin/env node
/**
 * Uma versão, um lugar.
 *
 * A fonte da verdade é `<OrbiVersion>` em `Directory.Build.props` (o app e o
 * instalador leem de lá). Este script leva o mesmo número para:
 *   - `src/lib/download.ts`  (versão, nome do instalador e do pacote no site)
 *   - `.github/workflows/release.yml` (nome dos arquivos, tag e release)
 *   - `app/Orbi.Setup/Setup.cs` (endereço do payload da release)
 *
 *   node scripts/version.mjs            escreve
 *   node scripts/version.mjs --check    só confere (o CI usa isso)
 */
import { readFileSync, writeFileSync } from "node:fs";
import { fileURLToPath } from "node:url";
import { dirname, join } from "node:path";

const raiz = join(dirname(fileURLToPath(import.meta.url)), "..");
const caminho = (p) => join(raiz, p);

const props = readFileSync(caminho("Directory.Build.props"), "utf8");
const versao = /<OrbiVersion>([^<]+)<\/OrbiVersion>/.exec(props)?.[1]?.trim();
if (!versao || !/^\d+\.\d+\.\d+$/.test(versao)) {
  console.error("Directory.Build.props: <OrbiVersion> ausente ou fora de X.Y.Z");
  process.exit(1);
}

/** Troca a versão em todos os lugares derivados e devolve o que mudou. */
function aplicar(texto) {
  return texto
    // site
    .replace(/^(\s*)version: "[^"]*"/m, `$1version: "${versao}"`)
    .replace(/installer: "OrbiSetup-[^"]*\.exe"/, `installer: "OrbiSetup-${versao}.exe"`)
    .replace(/packageName: "Orbi-[^"]*"/, `packageName: "Orbi-${versao}"`)
    // workflow: arquivos, tag e título da release
    .replace(/orbi-payload-\d+\.\d+\.\d+\.zip/g, `orbi-payload-${versao}.zip`)
    .replace(/OrbiSetup-\d+\.\d+\.\d+\.exe/g, `OrbiSetup-${versao}.exe`)
    .replace(/tag_name: v\d+\.\d+\.\d+/g, `tag_name: v${versao}`)
    .replace(/name: Orbi \d+\.\d+\.\d+/g, `name: Orbi ${versao}`)
    .replace(/## Orbi \d+\.\d+\.\d+/g, `## Orbi ${versao}`)
    // instalador: de onde ele baixa o app
    .replace(
      /releases\/download\/v\d+\.\d+\.\d+\/orbi-payload-\d+\.\d+\.\d+\.zip/g,
      `releases/download/v${versao}/orbi-payload-${versao}.zip`,
    );
}

const alvos = ["src/lib/download.ts", ".github/workflows/release.yml", "app/Orbi.Setup/Setup.cs"];
const conferir = process.argv.includes("--check");
let divergente = false;

for (const alvo of alvos) {
  const antes = readFileSync(caminho(alvo), "utf8");
  const depois = aplicar(antes);
  if (antes === depois) {
    console.log(`${alvo}: ${versao} ✓`);
    continue;
  }
  if (conferir) {
    // Arquivo derivado não pode ficar para trás: mostra onde está o descompasso.
    const linha = antes
      .split("\n")
      .find((l) => /\d+\.\d+\.\d+/.test(l) && aplicar(l) !== l);
    console.error(`${alvo}: fora da versão ${versao}${linha ? " → " + linha.trim() : ""}`);
    divergente = true;
  } else {
    writeFileSync(caminho(alvo), depois);
    console.log(`${alvo}: atualizado para ${versao}`);
  }
}

if (divergente) {
  console.error(`\nRode: node scripts/version.mjs   (versão em Directory.Build.props: ${versao})`);
  process.exit(1);
}

if (conferir) console.log(`versão em dia: ${versao}`);
