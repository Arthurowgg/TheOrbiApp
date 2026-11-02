import { useEffect, useState, type FormEvent } from "react";
import AppMock from "./AppMock";
import {
  Brand,
  Button,
  Icon,
  Mark,
  PasswordField,
  Spinner,
  StrengthMeter,
  TextField,
} from "./ui";
import {
  AuthError,
  MIN_PASSWORD,
  isEmail,
  sendResetEmail,
  signIn,
  signInWithGoogle,
  signUp,
  type Account,
} from "../lib/accounts";
import type { LoginMode } from "../lib/router";

type Props = {
  mode: LoginMode;
  currentAccount: Account | null;
  message?: string;
  onMode: (mode: LoginMode) => void;
  onSession: (account: Account) => void | Promise<void>;
  onFinish: () => void;
  onBack: () => void;
  onLogout: () => void;
};

type Errors = Partial<Record<"name" | "email" | "password" | "form", string>>;

const COPY: Record<LoginMode, { title: string; accent: string; lead: string }> = {
  signin: {
    title: "Entrar",
    accent: "na sua conta.",
    lead: "Acesse o painel, os seus downloads e os computadores onde o Orbi está instalado.",
  },
  signup: {
    title: "Criar",
    accent: "sua conta.",
    lead: "Grátis, sem cartão. Uma conta liga o site ao app no seu Windows.",
  },
  recover: {
    title: "Recuperar",
    accent: "o acesso.",
    lead: "Enviamos um link para o seu e-mail. A senha nunca passa por aqui.",
  },
};

export default function Login({
  mode,
  currentAccount,
  message,
  onMode,
  onSession,
  onFinish,
  onBack,
  onLogout,
}: Props) {
  const [name, setName] = useState("");
  const [email, setEmail] = useState("");
  const [password, setPassword] = useState("");
  const [errors, setErrors] = useState<Errors>({});
  const [loading, setLoading] = useState(false);
  const [googleLoading, setGoogleLoading] = useState(false);
  const [sent, setSent] = useState(false);

  useEffect(() => {
    setErrors({});
    setSent(false);
  }, [mode]);

  const copy = COPY[mode];

  const validate = () => {
    const next: Errors = {};
    if (mode === "signup" && name.trim().length < 2) next.name = "Digite como você quer ser chamado.";
    if (!isEmail(email)) next.email = "Digite um e-mail válido.";
    if (mode !== "recover" && password.length < MIN_PASSWORD) {
      next.password = `Use pelo menos ${MIN_PASSWORD} caracteres.`;
    }
    setErrors(next);
    return Object.keys(next).length === 0;
  };

  const submit = async (event: FormEvent) => {
    event.preventDefault();
    if (loading || !validate()) return;
    setLoading(true);
    setErrors({});
    try {
      if (mode === "signup") {
        const result = await signUp({ name, email, password });
        await onSession(result.account);
        onFinish();
      } else if (mode === "signin") {
        const account = await signIn({ email, password });
        await onSession(account);
        onFinish();
      } else {
        await sendResetEmail(email);
        setSent(true);
      }
    } catch (error) {
      const authError = error instanceof AuthError ? error : null;
      if (authError?.code === "email_taken") setErrors({ email: authError.message });
      else if (authError?.code === "wrong_password") setErrors({ password: authError.message });
      else if (authError?.code === "invalid_email") setErrors({ email: authError.message });
      else if (authError?.code === "weak_password") setErrors({ password: authError.message });
      else setErrors({ form: authError?.message ?? "Não foi possível concluir. Tente de novo." });
    } finally {
      setLoading(false);
    }
  };

  const withGoogle = async () => {
    if (googleLoading) return;
    setGoogleLoading(true);
    setErrors({});
    try {
      const account = await signInWithGoogle();
      await onSession(account);
      onFinish();
    } catch (error) {
      setErrors({ form: error instanceof Error ? error.message : "Não foi possível entrar com o Google." });
    } finally {
      setGoogleLoading(false);
    }
  };

  /* ---------------------- já está dentro da conta ---------------------- */
  if (currentAccount && mode === "signin") {
    return (
      <div className="grid min-h-screen place-items-center bg-paper px-5 py-16">
        <div className="w-full max-w-[420px] border border-rule bg-[#fbf9f3] p-7 shadow-panel">
          <p className="label text-warm">sessão ativa</p>
          <h1 className="mt-3 text-[24px] font-semibold tracking-[-0.01em] text-ink">
            Você já está conectado.
          </h1>
          <p className="mt-1.5 text-[13.5px] leading-snug text-warm">
            {currentAccount.email}
          </p>
          <div className="mt-6 grid gap-2">
            <Button onClick={onFinish}>Ir para a minha conta</Button>
            <Button variant="secondary" onClick={onBack}>
              Voltar ao site
            </Button>
            <button
              type="button"
              onClick={onLogout}
              className="label mt-1 text-warm transition-colors hover:text-signal"
            >
              sair desta conta
            </button>
          </div>
        </div>
      </div>
    );
  }

  return (
    <div className="relative grid min-h-screen bg-paper lg:grid-cols-[1.02fr_0.98fr]">
      {/* ---------------------------- formulário ---------------------------- */}
      <div className="relative flex flex-col px-5 py-7 sm:px-10 lg:px-14">
        <div className="flex items-center justify-between">
          <Brand onClick={onBack} />
          <button
            type="button"
            onClick={onBack}
            className="label inline-flex items-center gap-1.5 text-warm transition-colors hover:text-ink"
          >
            <Icon name="arrow-right" className="h-3 w-3 rotate-180" />
            voltar ao site
          </button>
        </div>

        <div className="mx-auto flex w-full max-w-[430px] flex-1 flex-col justify-center py-10">
          <p className="label text-warm">
            {mode === "signup" ? "criar conta" : mode === "recover" ? "redefinir senha" : "bem-vindo de volta"}
          </p>
          <h1 className="display mt-3 text-[clamp(2.2rem,5vw,3rem)] text-ink">
            {copy.title}
            <br />
            <span className="display-serif text-signal">{copy.accent}</span>
          </h1>
          <p className="mt-4 text-[13.5px] leading-[1.65] text-ink-2">{copy.lead}</p>

          {message && (
            <p className="mt-4 flex items-start gap-2 border border-signal/35 bg-signal-soft/40 px-3 py-2 text-[12.5px] text-signal-dark">
              <Icon name="info" className="mt-[2px] h-3.5 w-3.5 shrink-0" />
              {message}
            </p>
          )}

          {/* recuperação enviada */}
          {mode === "recover" && sent ? (
            <div className="mt-7 border border-moss/35 bg-[#eff4ea] p-4">
              <p className="flex items-center gap-2 text-[14px] font-medium text-[#2c5330]">
                <Icon name="mail" className="h-4 w-4" />
                Link enviado para {email}.
              </p>
              <p className="mt-1.5 text-[12.5px] leading-relaxed text-[#3d5a3f]">
                Abra o e-mail e escolha uma nova senha. Se não chegar em alguns minutos,
                confira a caixa de spam ou tente outro endereço.
              </p>
              <div className="mt-4 flex gap-2">
                <Button size="sm" variant="secondary" onClick={() => onMode("signin")}>
                  Voltar para entrar
                </Button>
                <Button size="sm" variant="ghost" onClick={() => setSent(false)}>
                  Reenviar
                </Button>
              </div>
            </div>
          ) : (
            <form onSubmit={submit} className="mt-7 space-y-4" noValidate>
              {mode === "signup" && (
                <TextField
                  id="auth-name"
                  label="Nome"
                  icon="user"
                  autoComplete="name"
                  placeholder="Como quer ser chamado"
                  value={name}
                  onChange={(event) => setName(event.target.value)}
                  error={errors.name}
                />
              )}

              <TextField
                id="auth-email"
                label="E-mail"
                type="email"
                icon="mail"
                autoComplete="email"
                placeholder="voce@email.com"
                value={email}
                onChange={(event) => setEmail(event.target.value)}
                error={errors.email}
              />

              {mode !== "recover" && (
                <div>
                  <PasswordField
                    id="auth-password"
                    label="Senha"
                    autoComplete={mode === "signup" ? "new-password" : "current-password"}
                    placeholder={mode === "signup" ? `Pelo menos ${MIN_PASSWORD} caracteres` : "Sua senha"}
                    value={password}
                    onChange={(event) => setPassword(event.target.value)}
                    error={errors.password}
                  />
                  {mode === "signup" && <StrengthMeter password={password} />}
                  {mode === "signin" && (
                    <div className="mt-2 flex justify-end">
                      <button
                        type="button"
                        onClick={() => onMode("recover")}
                        className="font-mono text-[10.5px] text-warm underline decoration-rule underline-offset-2 transition-colors hover:text-signal"
                      >
                        esqueci minha senha
                      </button>
                    </div>
                  )}
                </div>
              )}

              {errors.form && (
                <p className="flex items-start gap-2 border border-signal/40 bg-signal-soft/40 px-3 py-2 text-[12.5px] leading-snug text-signal-dark">
                  <Icon name="alert" className="mt-[2px] h-3.5 w-3.5 shrink-0" />
                  {errors.form}
                </p>
              )}

              <Button type="submit" size="lg" loading={loading} className="w-full">
                {mode === "signup"
                  ? "Criar conta grátis"
                  : mode === "recover"
                    ? "Enviar link de redefinição"
                    : "Entrar"}
                {!loading && <Icon name="arrow-right" className="h-4 w-4" />}
              </Button>

              {mode !== "recover" && (
                <>
                  <div className="flex items-center gap-3 py-1">
                    <span className="h-px flex-1 bg-rule" />
                    <span className="label text-warm">ou</span>
                    <span className="h-px flex-1 bg-rule" />
                  </div>
                  <Button
                    type="button"
                    variant="secondary"
                    size="lg"
                    className="w-full"
                    onClick={withGoogle}
                    disabled={googleLoading}
                  >
                    {googleLoading ? <Spinner className="h-4 w-4" /> : <Icon name="google" className="h-4 w-4" />}
                    Continuar com Google
                  </Button>
                </>
              )}

              <p className="pt-1 text-center text-[12.5px] text-warm">
                {mode === "signin" ? (
                  <>
                    Ainda não tem conta?{" "}
                    <button
                      type="button"
                      onClick={() => onMode("signup")}
                      className="font-medium text-ink underline decoration-rule underline-offset-2 hover:decoration-signal"
                    >
                      Criar agora
                    </button>
                  </>
                ) : (
                  <>
                    Já tem conta?{" "}
                    <button
                      type="button"
                      onClick={() => onMode("signin")}
                      className="font-medium text-ink underline decoration-rule underline-offset-2 hover:decoration-signal"
                    >
                      Entrar
                    </button>
                  </>
                )}
              </p>
            </form>
          )}

          <p className="mt-6 flex items-start gap-2 border-t border-rule pt-4 font-mono text-[10px] leading-relaxed text-warm">
            <Icon name="shield" className="mt-[1px] h-3.5 w-3.5 shrink-0" />
            Login seguro com o Google. Sua senha é guardada com hash no
            servidor — este site nunca a vê.
          </p>
        </div>
      </div>

      {/* ---------------------------- lado visual ---------------------------- */}
      <div className="relative hidden overflow-hidden bg-film text-bone lg:block">
        <div className="hairline-grid-dark pointer-events-none absolute inset-0" aria-hidden />
        <div
          className="pointer-events-none absolute -left-24 top-1/4 h-[420px] w-[420px] rounded-full bg-signal/15 blur-3xl"
          aria-hidden
        />
        <div className="relative flex h-full flex-col justify-between p-12">
          <div>
            <span className="grid h-10 w-10 place-items-center bg-signal text-white">
              <Mark className="h-5 w-5" />
            </span>
            <h2 className="display mt-8 max-w-[15ch] text-[clamp(2.2rem,3.4vw,3rem)] text-bone">
              O PC inteiro
              <br />
              <span className="text-signal">numa frase.</span>
            </h2>
            <p className="mt-4 max-w-[36ch] text-[13.5px] leading-[1.7] text-bone/55">
              A conta guarda a memória e as preferências. O app entra com ela
              e continua de onde você parou.
            </p>
          </div>

          <div className="my-8">
            <AppMock
              name={name || currentAccount?.name || "Ana Souza"}
              email={email || currentAccount?.email || "ana@exemplo.com"}
              color={currentAccount?.color}
            />
          </div>

          <ul className="space-y-2.5 border-t border-white/12 pt-6">
            {[
              "E-mail e senha nunca ficam visíveis,",
              "Verificação de e-mail e link de redefinição",
              "Só você acessa os seus dados",
            ].map((item) => (
              <li key={item} className="flex items-start gap-2.5 text-[12.5px] leading-snug text-bone/60">
                <Icon name="check" className="mt-[2px] h-3.5 w-3.5 shrink-0 text-signal" />
                {item}
              </li>
            ))}
          </ul>
        </div>
      </div>
    </div>
  );
}
