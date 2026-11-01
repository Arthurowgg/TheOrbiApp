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
        # Colou só a chave privada (PEM)? Reconstrói o JSON da conta de serviço
        # com os dados públicos do projeto — a chave é a única parte secreta.
        if "BEGIN PRIVATE KEY" in raw:
            pem = raw.replace("\\n", "\n")
            if not pem.endswith("\n"):
                pem += "\n"
            data = {
                "type": "service_account",
                "project_id": "orbi-app-auth",
                "private_key_id": "informado-no-secret",
                "private_key": pem,
                "client_email": "firebase-adminsdk-fbsvc@orbi-app-auth.iam.gserviceaccount.com",
                "client_id": "102432631538703606435",
                "auth_uri": "https://accounts.google.com/o/oauth2/auth",
                "token_uri": "https://oauth2.googleapis.com/token",
                "auth_provider_x509_cert_url": "https://www.googleapis.com/oauth2/v1/certs",
                "client_x509_cert_url": "https://www.googleapis.com/robot/v1/metadata/x509/firebase-adminsdk-fbsvc%40orbi-app-auth.iam.gserviceaccount.com",
                "universe_domain": "googleapis.com",
            }
            with open(path, "w", encoding="utf-8") as handle:
                json.dump(data, handle)
            print("secret contém só a chave privada — JSON da conta de serviço recomposto")
            print(f"conta de serviço: {data['client_email']}")
            return 0

        tamanho = len(raw)
        inicio = raw[:1] if raw else "(vazio)"
        print(f"o secret não é um JSON válido ({error}). "
              f"tamanho={tamanho} caracteres, começa com {inicio!r}.")
        print("cole o conteúdo do arquivo .json inteiro, de { até }, ou apenas a chave privada")
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
