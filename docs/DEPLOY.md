# Publicar tudo (site + Pages) — 2 comandos

## Opção A: um comando (recomendado)

No seu PC, na pasta do projeto:

```bash
git clone https://github.com/Arthurowgg/TheOrbiApp && cd TheOrbiApp
bash scripts/ligar-tudo.sh
```

Ou, se já tem o repositório: `npm run ligar`

O script:

1. instala dependências e compila o site (`dist/`);
2. faz `firebase login` (abre o navegador na primeira vez);
3. publica **Hosting + regras do Firestore** em `orbi-app-auth` → https://orbi-app-auth.web.app
4. muda o **GitHub Pages** para publicar o *build* (hoje ele está configurado para publicar o
   código-fonte, e por isso a página fica em branco) e dispara o workflow do Pages;
5. mostra as URLs no final.

Só uma parte? `bash scripts/ligar-tudo.sh --hosting` ou `--pages`.

## Opção B: 100% pelo navegador (sem instalar nada)

1. **Firebase Hosting** — [Settings ▸ Secrets ▸ Actions](https://github.com/Arthurowgg/TheOrbiApp/settings/secrets/actions)
   → *New repository secret*:
   - Name: `FIREBASE_SERVICE_ACCOUNT`
   - Secret: o JSON de uma conta de serviço (Console ▸ ⚙️ ▸ Contas de serviço ▸ *Gerar nova chave*)
2. Actions ▸ **Publicar site no Firebase Hosting** ▸ *Run workflow*
3. **GitHub Pages** — [Settings ▸ Pages](https://github.com/Arthurowgg/TheOrbiApp/settings/pages)
   → *Build and deployment* → **Source: GitHub Actions**
4. Actions ▸ **Site no GitHub Pages** ▸ *Run workflow* → https://arthurowgg.github.io/TheOrbiApp/

> Nos dois casos, adicione o domínio em **Authentication ▸ Settings ▸ Authorized domains**
> (`arthurowgg.github.io`) para o login com Google funcionar por lá. E-mail/senha funciona
> em qualquer domínio.



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

