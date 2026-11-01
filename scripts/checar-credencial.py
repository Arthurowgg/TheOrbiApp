#!/usr/bin/env python3
"""Valida (e conserta) o JSON da conta de serviço usado nos deploys.

O caso mais comum de falha: colar o JSON no campo de secret do GitHub, que
costuma duplicar as barras invertidas da chave privada (`\\n` em vez de `\n`).
Aqui a chave é normalizada e o arquivo reescrito no lugar.
"""

import json
import sys

REQUIRED = ("project_id", "client_email", "private_key")


def main() -> int:
    if len(sys.argv) < 2:
        print("uso: checar-credencial.py <arquivo.json>")
        return 2

    path = sys.argv[1]
    try:
        raw = open(path, encoding="utf-8").read().strip()
    except OSError as error:
        print(f"não consegui abrir {path}: {error}")
        return 1

    if not raw:
        print("secret vazio: cadastre o JSON da conta de serviço em Settings ▸ Secrets ▸ Actions")
        return 1

    try:
        data = json.loads(raw)
    except json.JSONDecodeError as error:
        print(f"o secret não é um JSON válido ({error}).")
        print("cole o conteúdo do arquivo .json inteiro, de { até }")
        return 1

    # secret cadastrado como string JSON dentro de string JSON
    if isinstance(data, str):
        try:
            data = json.loads(data)
        except json.JSONDecodeError:
            print("o secret parece uma string, não o JSON da conta de serviço")
            return 1

    missing = [key for key in REQUIRED if key not in data]
    if missing:
        print("faltam campos no JSON: " + ", ".join(missing))
        return 1

    key = data["private_key"]
    fixed = False
    if "\\n" in key and "\n" not in key:
        data["private_key"] = key.replace("\\n", "\n")
        fixed = True

    if not data["private_key"].startswith("-----BEGIN"):
        print("a chave privada não começa com -----BEGIN PRIVATE KEY-----")
        return 1

    if fixed:
        with open(path, "w", encoding="utf-8") as handle:
            json.dump(data, handle)

    if data.get("project_id") != "orbi-app-auth":
        print(f"atenção: o projeto do JSON é {data.get('project_id')}, esperado orbi-app-auth")

    print(f"conta de serviço: {data['client_email']}")
    print(f"projeto: {data['project_id']}")
    print("chave privada: " + ("normalizada (\\n corrigido)" if fixed else "ok"))
    return 0


if __name__ == "__main__":
    sys.exit(main())
