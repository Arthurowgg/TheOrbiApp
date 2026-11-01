# Publicar o site em orbi-app-auth.web.app

O deploy automático precisa de **uma** coisa que só o dono do repositório pode criar:
o secret com a conta de serviço. Leva 40 segundos e o site vai ao ar sozinho depois.

## 1. Criar o secret (uma vez)

1. Abra https://console.firebase.google.com/project/orbi-app-auth/settings/serviceaccounts/adminsdk
2. **Gerar nova chave privada** → baixa um `.json`
3. Abra https://github.com/Arthurowgg/TheOrbiApp/settings/secrets/actions
4. **New repository secret**
   - Name: `FIREBASE_SERVICE_ACCOUNT`
   - Secret: cole **o arquivo JSON inteiro** (começa com `{` e termina com `}`)
5. **Add secret**

> A chave que você colou no chat foi marcada como exposta. Gere uma **nova** e revogue a antiga
> (mesmo botão da tela, aba *Chaves*). O GitHub também bloqueia push que contenha essa chave —
> é a proteção contra segredos funcionando.

## 2. Publicar

Depois do secret, qualquer push em `main` ou neste branch publica o site (job
do `release.yml`). Para publicar na hora, sem esperar push:

- https://github.com/Arthurowgg/TheOrbiApp/actions/workflows/publish-hosting.yml → **Run workflow**

O mesmo comando também publica as **regras do Firestore** (`firestore.rules`), então
nada fica desatualizado.

## 3. Conferir

```bash
curl -I https://orbi-app-auth.web.app/          # 200
curl -s https://orbi-app-auth.web.app/ | grep -o '<title>.*</title>'
```

O workflow `verificar-firebase.yml` já faz essa checagem no fim do deploy.

## Deploy manual (do seu PC, sem secret)

```bash
npm ci
npm run build
npx firebase-tools login          # abre o navegador
npx firebase-tools deploy --only hosting,firestore:rules --project orbi-app-auth
```
