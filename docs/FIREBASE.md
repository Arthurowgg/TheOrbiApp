# Checklist do Firebase — projeto `orbi-app-auth`

Faça estes passos uma vez no <https://console.firebase.google.com/project/orbi-app-auth>.
Cada item diz o que quebra se ficar de fora.

## 1. Authentication
- [ ] **Sign-in method ▸ E-mail/senha** ativado.
      Sem isso: `auth/operation-not-allowed` ao criar conta ou entrar.
- [ ] **Sign-in method ▸ Google** ativado (define o e-mail de suporte do projeto).
      Sem isso: o botão “Continuar com Google” devolve erro.
- [ ] **Settings ▸ Authorized domains** contém `localhost`, `orbi-app-auth.web.app` e
      `orbi-app-auth.firebaseapp.com`.
- [ ] **Templates ▸ Password reset** revisado em português (o link de redefinição usa esse template).

## 2. Firestore
- [ ] Banco criado (modo produção).
- [ ] Regras publicadas:
      ```bash
      firebase deploy --only firestore:rules
      ```
      Sem isso: `permission-denied` ao criar o documento `users/{uid}` — o site avisa no
      formulário de cadastro.
- [ ] Índices: nenhum necessário (as consultas são por documento).

## 3. Hosting
- [ ] Site `orbi-app-auth` existente.
- [ ] Deploy:
      ```bash
      npm run deploy
      ```
- [ ] (Opcional) Secret `FIREBASE_SERVICE_ACCOUNT` no GitHub para deploy automático no CI.

## 4. AI Logic (Gemini) — opcional
- [ ] **Build ▸ AI Logic ▸ Get started** → provedor **Gemini Developer API**
      (plano Spark, sem cartão).
- [ ] Modelo do site: variável `VITE_GEMINI_MODEL` (padrão `gemini-2.5-flash`).
      Se o modelo não estiver disponível na sua região, ajuste para outro da lista.
- [ ] Modelo do app: aba **IA** nos ajustes (padrão `gemini-2.0-flash`).

Sem o passo 4 nada quebra: o assistente do site usa o motor local e a barra do app também.

## 5. App Check (recomendado depois do lançamento)
- [ ] Registrar o site com reCAPTCHA v3 em **App Check** e ativar “Enforced” para AI Logic.
      Isso impede que outra pessoa consuma a cota do seu projeto.

## Verificação rápida

1. Abra o site → **Criar conta** com um e-mail qualquer → você deve cair no painel.
2. Confira no console: **Authentication ▸ Users** (o usuário aparece) e
   **Firestore ▸ users/{uid}** (documento com `name`, `email`, `sync`…).
3. Recarregue a página: a sessão continua (persistência local do SDK).
4. **Segurança ▸ Enviar link de redefinição** deve chegar na caixa de entrada.
5. **Baixar** deve puxar `OrbiSetup-1.0.0.exe` da release.
