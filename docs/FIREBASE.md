# Orbi — Firebase (contas + banco de dados)

O site e o app Windows usam **o mesmo projeto Firebase**, `orbi-app-auth`.
Não existe servidor próprio: o Firebase Authentication guarda as contas e o
Firestore guarda os dados. Se os dois clientes falam com o mesmo projeto, eles
compartilham dados por definição.

| Peça | Onde | Para quê |
| --- | --- | --- |
| Authentication | Firebase Console ▸ Authentication | contas de e-mail/senha + Google |
| Firestore | Firebase Console ▸ Firestore Database | `users/{uid}` — perfil, apelidos, atalhos, rotinas, preferências |
| Hosting | Firebase Console ▸ Hosting | site estático (`dist/`) em `orbi-app-auth.web.app` |
| IA (Gemini) | Firebase AI Logic ▸ Gemini Developer API | chat do site, plano Spark (grátis) |

## Configuração pública (pode circular)

```js
{
  apiKey: "AIzaSyDCpoYpdBiu_ENT8xFbvz0y1gl0W1CufFw",
  authDomain: "orbi-app-auth.firebaseapp.com",
  projectId: "orbi-app-auth",
  storageBucket: "orbi-app-auth.firebasestorage.app",
  messagingSenderId: "96738360575",
  appId: "1:96738360575:web:2bb25abca7d0bdde9220ca",
  measurementId: "G-7CT3TDG2L7"
}
```

Estes valores são identificadores, não segredos (é assim que todo app web do
Firebase funciona). O que protege os dados são as **regras** e o
**Authentication**. Eles vivem em:

- site: `.env` (`VITE_FIREBASE_*`) e, como reserva, `src/lib/firebase.ts`;
- app: constantes em `app/Orbi.App/Services/Firebase.cs`.

## 3 passos no console (uma vez só)

1. **Authentication ▸ Sign-in method**: ative **E-mail/senha** e **Google**.
2. **Authentication ▸ Settings ▸ Authorized domains**: inclua
   `orbi-app-auth.web.app`, `orbi-app-auth.firebaseapp.com` e `localhost`.
3. **Firestore Database**: crie o banco (modo produção, região `southamerica-east1`
   ou `us-east1`) e **publique as regras** do arquivo [`firestore.rules`](../firestore.rules)
   em *Firestore Database ▸ Regras*.

Sem o passo 3 o login funciona e o app mostra `permission-denied` ao sincronizar.

> Se o Firestore já foi criado em outra região, tudo bem — o app e o site não
> ligam para a região.

## Modelo de dados

Documento único por pessoa: `users/{uid}` (o `uid` é do Authentication).

```jsonc
{
  "name": "Ana Souza",
  "email": "ana@exemplo.com",
  "createdAt": "<timestamp>",
  "updatedAt": "<timestamp>",
  "color": "#c7452a",
  "plan": "free",

  "sync":      { "preferencias": true, "apelidos": true, "rotinas": true, "atalhos": true },
  "aliases":   [ { "alias": "zap", "target": "WhatsApp.exe" } ],   // máx 100
  "shortcuts": [ { "trigger": "assinatura", "expansion": "Ana Souza · Orbi" } ], // máx 100
  "routines":  [ { "name": "modo estudo", "commands": ["abre o word", "brilho 40"] } ], // máx 60
  "appPrefs":  { "startWithWindows": true, "volume": 50,
                 "lockEnabled": false, "lockAfterMinutes": 5,
                 "geminiModel": "gemini-2.0-flash" },
  "appVersion": "1.0.0",
  "lastSyncAt": "<timestamp>",

  "devices":   [ { "id": "…", "name": "PC da sala", "os": "Windows 11", "lastSeen": "<ts>" } ], // máx 20
  "downloads": [ … ],   // máx 30
  "activity":  [ … ]    // máx 40
}
```

Regras de convivência dos dois clientes:

- cada um grava **só os campos que possui**, sempre com `updateMask`
  (`updateMask.fieldPaths=…`), então nunca sobrescreve o outro;
- o site escreve `aliases`, `shortcuts`, `routines`, `appPrefs`, `sync`, `devices`;
- o app escreve `name`, `aliases`, `shortcuts`, `routines`, `appPrefs`, `lastSyncAt`;
- o app faz *pull* ao abrir, a cada 45 s e ao clicar em **Sincronizar agora**;
- o app faz *push* quando você adiciona/remove apelido, atalho ou muda preferências.

### O que **não** vai para o Firestore

Notas, área de transferência, lembretes, histórico de comandos e a **chave do
Gemini**. Eles ficam em `%APPDATA%\Orbi\orbi.json` e `gemini.bin`, cifrados com
DPAPI (só aquele usuário do Windows abre).

## Como o app fala com o Firebase

Sem SDK: o app .NET usa a **API REST** (`Services/Firebase.cs`).

| Operação | Endpoint |
| --- | --- |
| entrar | `POST identitytoolkit.googleapis.com/v1/accounts:signInWithPassword?key=API_KEY` |
| criar conta | `POST …/accounts:signUp` |
| renovar sessão | `POST securetoken.googleapis.com/v1/token` (refresh token) |
| ler dados | `GET firestore.googleapis.com/v1/projects/{p}/databases/(default)/documents/users/{uid}` |
| gravar parcial | `PATCH …/users/{uid}?updateMask.fieldPaths=a&updateMask.fieldPaths=b` |

O `idToken` vai no header `Authorization: Bearer …`. O refresh token é salvo em
`%APPDATA%\Orbi\session.bin` com DPAPI — o app não pede senha de novo.

## Deploy do site (opcional)

```bash
npm run build
npx firebase-tools deploy --only hosting,firestore:rules --project orbi-app-auth
```

O CI já publica o Hosting sozinho **se** existir o secret
`FIREBASE_SERVICE_ACCOUNT` (conteúdo JSON de uma conta de serviço) no repositório.
Nunca comite esse JSON.

## Conta de serviço — cuidado

O "segredo" que aparece no console (o JSON `firebase-adminsdk@…`) é uma
**credencial administrativa**: quem tem esse arquivo ignora todas as regras,
lê e apaga qualquer documento e cria usuários. Ela **não** deve ser colada em
chat, issue, commit ou variável pública. Se vazar:

1. Firebase Console ▸ ⚙️ **Configurações do projeto** ▸ **Contas de serviço**;
2. aba **Contas de serviço** (Google Cloud) ▸ encontre
   `firebase-adminsdk-fbsvc@orbi-app-auth…` ▸ **Chaves** ▸
   **Excluir** a chave `e1ce455b…`;
3. **Criar nova chave** só se precisar (por exemplo, para o CI);
4. no CI, guarde o JSON em **Settings ▸ Secrets and variables ▸ Actions** com o
   nome `FIREBASE_SERVICE_ACCOUNT`.

Para o dia a dia, o app **não** precisa dessa chave: ele entra com e-mail e senha
como qualquer usuário e obedece às regras.
