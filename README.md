# Orbi

**Seu Windows, em português.**

Orbi é um assistente para Windows 11 que entende comandos em português: abre apps, ajusta
volume e brilho, cria lembretes, protege a sessão e lê o status do PC. Este repositório tem
as três peças do produto:

| Pasta | O que é |
| --- | --- |
| `src/` | Site oficial (React + Vite + Tailwind 4) com Firebase Authentication, Firestore e Gemini |
| `app/Orbi.App/` | O app Windows de verdade (C# / .NET 8 / WPF) |
| `app/installer/` | Instalador Inno Setup que gera o `OrbiSetup.exe` |

O instalador é **compilado e publicado automaticamente** pelo GitHub Actions a cada push em
`main`: o job do Windows builda o app, gera o `.exe`, calcula o SHA-256 e cria a release.

**Baixar:** <https://github.com/Arthurowgg/TheOrbiApp/releases/latest>
**Site:** <https://orbi-app-auth.web.app>

---

## O que já funciona

### Site
- Landing com assistente interativo (motor local + Gemini), showcase do app em 4 cenas,
  seção de conta, segurança, download, FAQ e CTA.
- **Login real com Firebase Authentication**: e-mail/senha (hash no servidor), verificação de
  e-mail, redefinição por link, entrada com Google, sessão persistente.
- **Firestore** como banco: perfil, aparelhos, downloads, atividade e — o que o app usa —
  apelidos, atalhos de texto, rotinas e preferências, com regras que deixam cada documento
  visível apenas para o dono.
- Área da conta: painel, perfil, dispositivos, **sincronização** (apelidos, atalhos, rotinas e
  preferências do app), downloads, segurança (trocar senha, redefinir por e-mail, exportar
  dados, excluir conta).
- O botão **Baixar** entrega o instalador real da última release do GitHub, com barra de
  progresso, nome do arquivo, tamanho e SHA-256. Se a release não existir, entrega um pacote
  com instruções — nunca um botão morto.

### App
- **Entra na mesma conta do site** (Firebase Authentication por REST) e sincroniza pelo
  **Firestore** (`users/{uid}`): pull ao abrir e a cada 45 s, push ao editar.
- Rotinas criadas no site rodam no app: “modo estudo” abre o Word e ajusta o brilho.
 Windows (C# / WPF, nativo, sem Electron)
- Barra de comando global com **Alt+Space**, sobre qualquer janela, com sugestões e histórico.
- Ícone na bandeja, início com o Windows, instância única.
- Comandos locais (sem internet): abrir/fechar apps por apelido, volume, mudo, brilho, bloqueio,
  desligar/reiniciar com confirmação, cancelar desligamento, dormir, captura de tela, status do
  PC (CPU/memória/disco/bateria), Wi-Fi, lixeira, play/pause.
- Lembretes com notificação, notas, histórico da área de transferência, atalhos de texto,
  apelidos de apps e rotinas — tudo em `%APPDATA%\Orbi`.
- **Gemini opcional**: chave do Google AI Studio guardada com DPAPI (só o seu usuário do Windows
  lê). Sem chave, o app continua 100% funcional no modo local.
- Bloqueio por inatividade com aviso de 15 s e exceções.
- Ajustes em 9 abas: Geral, IA, Conta, Lembretes, Notas, Atalhos, Apelidos, Segurança, Sobre.

---

## Rodar o site

```bash
npm install
cp .env.example .env     # as chaves do Firebase já vêm preenchidas
npm run dev              # http://localhost:5173
npm run typecheck
npm run build
```

### Publicar no Firebase Hosting

```bash
npm install -g firebase-tools
firebase login
firebase use orbi-app-auth
npm run deploy           # build + hosting + regras do Firestore
```

O site fica em <https://orbi-app-auth.web.app>.

### Deploy automático (opcional)

O workflow `.github/workflows/release.yml` tem um job de Hosting que roda se existir o secret
`FIREBASE_SERVICE_ACCOUNT` (JSON de uma conta de serviço do projeto `orbi-app-auth` com os
papéis *Firebase Hosting Admin* e *Cloud Datastore User*):

```bash
gh secret set FIREBASE_SERVICE_ACCOUNT --repo Arthurowgg/TheOrbiApp < service-account.json
```

Sem o secret, o job é ignorado e o site continua publicável pelo `npm run deploy`.

---

## Configurar o Firebase (uma vez, no console)

1. **Authentication ▸ Sign-in method**
   - Ativar **E-mail/senha**.
   - Ativar **Google** (o provedor precisa de um e-mail de suporte).
2. **Authentication ▸ Settings ▸ Authorized domains**
   - `orbi-app-auth.web.app`, `orbi-app-auth.firebaseapp.com` e `localhost`.
3. **Firestore Database** — criar o banco em modo produção e publicar as regras:
   ```bash
   firebase deploy --only firestore:rules
   ```
4. **AI Logic** (opcional, para o assistente do site responder perguntas livres)
   - Abrir **Build ▸ AI Logic ▸ Get started** e escolher **Gemini Developer API**
     (funciona no plano Spark, sem cartão).
   - O modelo usado pelo site vem de `VITE_GEMINI_MODEL` (padrão `gemini-2.5-flash`).
   - Sem esse passo, o assistente do site continua respondendo os comandos do app pelo motor
     local e avisa que a IA ainda não está habilitada.

> A `apiKey` do Firebase no `.env` é pública por natureza — ela identifica o projeto, não
> autoriza nada. O que protege os dados são o Authentication, as Security Rules
> (`firestore.rules`) e o proxy do AI Logic.

---

## Compilar o app e o instalador

Precisa de Windows (ou de um runner `windows-latest`) com .NET 8 SDK e Inno Setup 6.

```powershell
dotnet restore app/Orbi.App/Orbi.App.csproj
dotnet build   app/Orbi.App/Orbi.App.csproj -c Release

# binário único, autocontido (não exige .NET instalado no PC do usuário)
dotnet publish app/Orbi.App/Orbi.App.csproj -c Release -r win-x64 `
  --self-contained true -p:PublishSingleFile=true -o artifacts/publish

# instalador
& "C:\Program Files (x86)\Inno Setup 6\ISCC.exe" app\installer\orbi.iss
# → artifacts/OrbiSetup-1.0.0.exe
```

Sem Windows na mão? Faça um push: o GitHub Actions compila, testa o tamanho do binário,
gera o instalador, publica a release e sobe o site.

---

## Arquitetura

```
src/
  lib/firebase.ts         configuração pública (projeto orbi-app-auth)
  lib/firebaseClient.ts   app, auth, firestore, analytics
  lib/accounts.ts         cadastro, login, perfil, aparelhos, downloads (Firebase)
  lib/gemini.ts           Firebase AI Logic + fallback local
  lib/download.ts         release do GitHub, progresso, SHA-256, fallback
  components/             Home, OrbiChat, AppShowcase, DownloadModal, Login, account/*
app/Orbi.App/
  App.xaml(.cs)           bandeja, atalho global, lembretes, bloqueio por inatividade
  CommandBar.xaml(.cs)    barra de comando flutuante
  SettingsWindow.xaml(.cs) ajustes
  Services/               Store (JSON + DPAPI), SystemActions (Win32/WMI),
                          Interpreter (regras locais), Gemini, PairingCodes
app/installer/orbi.iss    Inno Setup, instalação por usuário
```

### Modelo de dados (Firestore)

```
users/{uid}
  name, email, createdAt, updatedAt, color, plan
  sync: { preferencias, apelidos, rotinas, atalhos }
  devices:   [{ id, name, os, linkedAt, code }]
  downloads: [{ id, version, at, checksum, filename }]
  activity:  [{ id, at, type, text }]
```

Nada sensível vai para a nuvem: notas, área de transferência, chave do Gemini, lembretes e
logs ficam só no PC.

---

## Segurança, em uma olhada

- Senhas: Firebase Authentication (hash no servidor, nunca no site).
- Dados: Firestore Rules — `request.auth.uid == uid`, campos com lista fechada e limites de
  tamanho (`firestore.rules`).
- Chave do Gemini no app: DPAPI (`DataProtectionScope.CurrentUser`).
- IA: a chamada do site passa pelo proxy do Firebase AI Logic; nenhuma chave extra no código.
- Comandos destrutivos (desligar, reiniciar, fechar apps) exigem confirmação explícita.

## Licença

O app é distribuído gratuitamente sob os termos de `app/installer/license.txt`.
