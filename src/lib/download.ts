import JSZip from "jszip";
import type { Account } from "./accounts";

export const BUILD = {
  version: "1.0.0",
  channel: "Estável",
  os: "Windows 11",
  arch: "x64",
  minimum: "Windows 11 (23H2 ou superior)",
  packageName: "NOVA-Setup-1.0.0-win11-x64",
};

const REFERENCIA_PY = `"""Vínculo de conta do NOVA (referência do lado do app).

Mostra como o app gera o código exibido em Configurações > Conta,
valida o formato (com dígito verificador, para pegar erro de digitação)
e importa o arquivo NOVA-conta.json que acompanha o download.

Nada aqui envia senha: o app guarda apenas a identificação da conta no
Cofre de Credenciais do Windows, usando a biblioteca keyring.
"""

from __future__ import annotations

import json
import secrets
from pathlib import Path
from typing import TypedDict

import keyring

# Sem 0, 1, I, L e O para não confundir na hora de digitar.
ALFABETO = "23456789ABCDEFGHJKMNPQRSTUVWXYZ"
SERVICO = "NOVA"
CHAVE_CONTA = "conta-vinculada"


class ContaVinculada(TypedDict):
    id: str
    nome: str
    email: str


def _digito_verificador(corpo: str) -> str:
    """Último caractere do código: detecta erro de digitação."""
    soma = sum((i + 1) * ALFABETO.index(c) for i, c in enumerate(corpo))
    return ALFABETO[soma % len(ALFABETO)]


def gerar_codigo_pareamento() -> str:
    """Gera o código NOVA-XXXX-XXXX mostrado na tela do app."""
    corpo = "".join(secrets.choice(ALFABETO) for _ in range(7))
    completo = corpo + _digito_verificador(corpo)
    return f"NOVA-{completo[:4]}-{completo[4:]}"


def codigo_valido(codigo: str) -> bool:
    """Confere o formato e o dígito verificador de um código."""
    limpo = "".join(c for c in codigo.upper() if c.isalnum())
    if limpo.startswith("NOVA"):
        limpo = limpo[4:]
    if len(limpo) != 8 or any(c not in ALFABETO for c in limpo):
        return False
    return _digito_verificador(limpo[:7]) == limpo[7]


def importar_arquivo_conta(caminho: Path) -> ContaVinculada:
    """Lê o NOVA-conta.json baixado do site e guarda no cofre do Windows."""
    dados = json.loads(caminho.read_text(encoding="utf-8"))
    if dados.get("formato") != "nova-conta":
        raise ValueError("Arquivo de conta inválido.")
    conta = dados["conta"]
    vinculada: ContaVinculada = {
        "id": str(conta["id"]),
        "nome": str(conta["nome"]),
        "email": str(conta["email"]),
    }
    keyring.set_password(SERVICO, CHAVE_CONTA, json.dumps(vinculada))
    return vinculada


def conta_atual() -> ContaVinculada | None:
    """Conta vinculada a este PC, ou None se ainda não houver."""
    bruto = keyring.get_password(SERVICO, CHAVE_CONTA)
    return json.loads(bruto) if bruto else None
`;

function readme(account: Pick<Account, "name" | "email"> | null) {
  return `NOVA ${BUILD.version} — ${BUILD.os} (64 bits)
Canal ${BUILD.channel}

${
  account
    ? `Conta deste download: ${account.name} <${account.email}>
O arquivo NOVA-conta.json identifica a sua conta. O instalador o lê
e o app já abre mostrando a conta vinculada.`
    : `Este download não está ligado a nenhuma conta. Para vincular o app
depois, entre no site e use Conta > Dispositivos com o código que o
app exibe em Configurações > Conta.`
}

REQUISITOS
- ${BUILD.minimum}
- Arquitetura x64

USO
- Alt+Space abre a barra de comando em qualquer programa.
- Esc fecha a barra.
- A chave gratuita do Google AI Studio é configurada dentro do app e
  guardada no Cofre de Credenciais do Windows.

VÍNCULO COM O SITE
1. No app: Configurações > Conta > Vincular ao site.
2. O app mostra um código no formato NOVA-XXXX-XXXX.
3. No site: Conta > Dispositivos, digite o código e dê um nome ao PC.
Sincronizam: preferências, apelidos de apps, rotinas e atalhos de texto.
Nunca sincronizam: notas, área de transferência, chave da API e logs de
segurança.

A pasta referencia/ traz o módulo Python que mostra como o app gera e
valida esses códigos e importa o arquivo de conta.

Este é o kit de prévia gerado pelo site. O executável auto-suficiente
é produzido pela build oficial do NOVA para Windows 11.
`;
}

export type PackageResult = {
  blob: Blob;
  filename: string;
  checksum: string;
};

async function sha256(buffer: ArrayBuffer): Promise<string> {
  const subtle = globalThis.crypto?.subtle;
  if (!subtle) return "indisponível";
  const digest = await subtle.digest("SHA-256", buffer);
  return Array.from(new Uint8Array(digest), (b) => b.toString(16).padStart(2, "0")).join("");
}

export async function buildPackage(
  account: Pick<Account, "id" | "name" | "email"> | null,
): Promise<PackageResult> {
  const zip = new JSZip();
  zip.file("LEIA-ME.txt", readme(account));
  zip.file(
    "VERSAO.txt",
    `NOVA\nversão ${BUILD.version}\ncanal ${BUILD.channel}\nsistema ${BUILD.os} ${BUILD.arch}\n`,
  );
  zip.file("referencia/vincular_conta.py", REFERENCIA_PY);
  if (account) {
    zip.file(
      "NOVA-conta.json",
      JSON.stringify(
        {
          formato: "nova-conta",
          versao: 1,
          emitidoEm: new Date().toISOString(),
          conta: { id: account.id, nome: account.name, email: account.email },
        },
        null,
        2,
      ),
    );
  }
  const blob = await zip.generateAsync({ type: "blob", compression: "DEFLATE" });
  const checksum = await sha256(await blob.arrayBuffer());
  return { blob, filename: `${BUILD.packageName}.zip`, checksum };
}

export function saveBlob(blob: Blob, filename: string) {
  const url = URL.createObjectURL(blob);
  const anchor = document.createElement("a");
  anchor.href = url;
  anchor.download = filename;
  document.body.appendChild(anchor);
  anchor.click();
  anchor.remove();
  window.setTimeout(() => URL.revokeObjectURL(url), 4000);
}

export function savePackage(result: PackageResult) {
  saveBlob(result.blob, result.filename);
}

export function shortChecksum(checksum: string) {
  if (!checksum || checksum === "indisponível") return "indisponível";
  return `${checksum.slice(0, 10)}…${checksum.slice(-6)}`;
}
