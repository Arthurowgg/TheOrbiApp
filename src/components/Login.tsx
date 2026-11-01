import { useEffect, useRef, useState, type FormEvent } from "react";
import AppMock from "./AppMock";
import {
  Avatar,
  Brand,
  Button,
  CopyButton,
  Eyebrow,
  Icon,
  PasswordField,
  Spinner,
  TextField,
} from "./ui";
import {
  AuthError,
  MIN_PASSWORD,
  firstName,
  formatRecoveryCode,
  isEmail,
  recoverPassword,
  signIn,
  signUp,
  type Account,
} from "../lib/accounts";
import { saveBlob } from "../lib/download";
import type { LoginMode } from "../lib/router";

type Props = {
  mode: LoginMode;
  currentAccount: Account | null;
  message?: string;
  onMode: (mode: LoginMode) => void;
  onSession: (account: Account) => void;
  onFinish: () => void;
  onBack: () => void;
  onLogout: () => void;
};

type Errors = Partial<Record<"name" | "email" | "password" | "code" | "form", string>>;

const COPY: Record<LoginMode, { title: string; accent: string; lead: string }> = {
  signin: {
    title: "Entrar",
    accent: "na sua conta.",
    lead: "Acesse seu painel, seus downloads e os aparelhos vinculados ao NOVA.",
  },
  signup: {
    title: "Criar",
    accent: "sua conta.",
    lead: "Grátis, sem cartão. Uma conta liga o site ao app no seu Windows.",
  },
  recover: {
    title: "Recuperar",
    accent: "o acesso.",
    lead: "Use o código de recuperação que você recebeu ao criar a conta.",
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
  const [code, setCode] = useState("");
  const [remember, setRemember] = useState(true);
  const [errors, setErrors] = useState<Errors>({});
  const [loading, setLoading] = useState(false);
  const [saved, setSaved] = useState<{ account: Account; code: string; kind: "new" | "reset" } | null>(null);
  const [confirmedSave, setConfirmedSave] = useState(false);
  const attempts = useRef(0);
  const lockedUntil = useRef(0);

  useEffect(() => {
    setErrors({});
    setPassword("");
    setCode("");
  }, [mode]);

  const copy = COPY[mode];

  const validate = () => {
    const next: Errors = {};
    if (mode === "signup" && name.trim().length < 2) next.name = "Digite como você quer ser chamado.";
    if (!isEmail(email)) next.email = "Digite um e-mail válido.";
    if (mode === "signin" && !password) next.password = "Digite sua senha.";
    if (mode !== "signin" && password.length < MIN_PASSWORD) {
      next.password = `Use pelo menos ${MIN_PASSWORD} caracteres.`;
    }
    if (mode === "recover" && formatRecoveryCode(code).length < 14) {
      next.code = "O código tem 12 caracteres, no formato XXXX-XXXX-XXXX.";
    }
    setErrors(next);
    return Object.keys(next).length === 0;
  };

  const submit = async (event: FormEvent) => {
    event.preventDefault();
    if (loading || !validate()) return;

    const wait = Math.ceil((lockedUntil.current - Date.now()) / 1000);
    if (mode === "signin" && wait > 0) {
      setErrors({ form: `Muitas tentativas. Aguarde ${wait} s e tente de novo.` });
      return;
    }

    setLoading(true);
    setErrors({});
    try {
      if (mode === "signup") {
        const result = await signUp({ name, email, password, remember });
        onSession(result.account);
        setSaved({ account: result.account, code: result.recoveryCode, kind: "new" });
      } else if (mode === "recover") {
        const result = await recoverPassword({ email, code, password });
        onSession(result.account);
        setSaved({ account: result.account, code: result.recoveryCode, kind: "reset" });
      } else {
        const account = await signIn({ email, password, remember });
        attempts.current = 0;
        onSession(account);
        onFinish();
      }
    } catch (error) {
      if (error instanceof AuthError) {
        if (error.code === "wrong_password") {
          attempts.current += 1;
          if (attempts.current >= 5) {
            lockedUntil.current = Date.now() + 30_000;
            attempts.current = 0;
            setErrors({ form: "Muitas tentativas. Aguarde 30 s e tente de novo." });
          } else {
            setErrors({ password: error.message });
          }
        } else if (error.code === "wrong_recovery") {
          setErrors({ code: error.message });
        } else if (error.code === "email_taken" || error.code === "not_found") {
          setErrors({ email: error.message });
        } else {
          setErrors({ form: error.message });
        }
      } else {
        setErrors({ form: "Algo deu errado. Tente novamente." });
      }
    } finally {
      setLoading(false);
    }
  };

  const downloadCode = () => {
    if (!saved) return;
    const text = `NOVA — código de recuperação\nConta: ${saved.account.email}\nCódigo: ${saved.code}\n\nGuarde em um lugar seguro. Quem tiver este código consegue redefinir a sua senha.\n`;
    saveBlob(new Blob([text], { type: "text/plain" }), "NOVA-codigo-de-recuperacao.txt");
  };

  const previewName = saved?.account.name ?? currentAccount?.name ?? name;
  const previewEmail = saved?.account.email ?? currentAccount?.email ?? email;
  const previewColor = saved?.account.color ?? currentAccount?.color;

  return (
    <div className="min-h-screen bg-paper lg:grid lg:grid-cols-[minmax(0,1fr)_minmax(0,1.04fr)]">
      {/* ------------------------------ formulário ------------------------------ */}
      <div className="flex min-h-screen flex-col px-5 py-5 sm:px-10 sm:py-7">
        <div className="flex items-center justify-between gap-4">
          <Brand onClick={onBack} />
          <button
            type="button"
            onClick={onBack}
            className="label inline-flex items-center gap-2 text-warm transition-colors hover:text-ink"
          >
            <Icon name="arrow-right" className="h-3.5 w-3.5 rotate-180" />
            Voltar ao site
          </button>
        </div>

        <div className="mx-auto flex w-full max-w-[410px] flex-1 flex-col justify-center py-10">
          {saved ? (
            /* ----------- código de recuperação ----------- */
            <div className="animate-fade-up">
              <Eyebrow>{saved.kind === "new" ? "Conta criada" : "Senha redefinida"}</Eyebrow>
              <h1 className="display mt-4 text-[clamp(2.4rem,5vw,3.2rem)] text-ink">
                Guarde este
                <br />
                <span className="display-serif">código.</span>
              </h1>
              <p className="mt-4 text-[14.5px] leading-[1.65] text-ink-2">
                {saved.kind === "new"
                  ? "É a única forma de recuperar a conta se você esquecer a senha. Ele não aparece de novo."
                  : "Seu código antigo foi usado e deixou de valer. Este é o novo — guarde-o."}
              </p>

              <div className="mt-6 border border-ink bg-[#fbf9f3] p-4">
                <p className="label text-warm">Código de recuperação</p>
                <p className="mt-2 break-all font-mono text-[22px] font-medium tracking-[0.08em] text-ink sm:text-[26px]">
                  {saved.code}
                </p>
                <div className="mt-4 flex flex-wrap gap-2">
                  <CopyButton value={saved.code} />
                  <button
                    type="button"
                    onClick={downloadCode}
                    className="label inline-flex items-center gap-1.5 border border-rule px-2.5 py-1.5 text-ink transition-colors hover:border-ink"
                  >
                    <Icon name="download" className="h-3 w-3" />
                    Baixar .txt
                  </button>
                </div>
              </div>

              <label className="mt-5 flex cursor-pointer items-start gap-3">
                <input
                  type="checkbox"
                  checked={confirmedSave}
                  onChange={(event) => setConfirmedSave(event.target.checked)}
                  className="mt-[3px] h-4 w-4 shrink-0 accent-[#c7452a]"
                />
                <span className="text-[13px] leading-[1.55] text-ink-2">
                  Guardei o código em um lugar seguro.
                </span>
              </label>

              <Button size="lg" className="mt-5 w-full" disabled={!confirmedSave} onClick={onFinish}>
                Ir para minha conta
                <Icon name="arrow-right" className="h-3.5 w-3.5" />
              </Button>
            </div>
          ) : currentAccount ? (
            /* ----------- já conectado ----------- */
            <div className="animate-fade-up">
              <Eyebrow>Sessão ativa</Eyebrow>
              <h1 className="display mt-4 text-[clamp(2.4rem,5vw,3.2rem)] text-ink">
                Olá, <span className="display-serif">{firstName(currentAccount.name)}.</span>
              </h1>
              <p className="mt-4 text-[14.5px] leading-[1.65] text-ink-2">
                Você já está conectado neste navegador.
              </p>
              <div className="mt-6 flex items-center gap-3 border border-rule bg-[#fbf9f3] p-4">
                <Avatar name={currentAccount.name} color={currentAccount.color} size="lg" />
                <div className="min-w-0">
                  <p className="truncate text-[14px] font-medium text-ink">{currentAccount.name}</p>
                  <p className="truncate font-mono text-[11px] text-warm">{currentAccount.email}</p>
                </div>
              </div>
              <div className="mt-5 space-y-2.5">
                <Button className="w-full" onClick={onFinish}>
                  Abrir meu painel
                  <Icon name="arrow-right" className="h-3.5 w-3.5" />
                </Button>
                <Button variant="secondary" className="w-full" onClick={onLogout}>
                  <Icon name="logout" className="h-3.5 w-3.5" />
                  Trocar de conta
                </Button>
              </div>
            </div>
          ) : (
            /* ----------- formulário principal ----------- */
            <div className="animate-fade-up" key={mode}>
              <Eyebrow>{mode === "signup" ? "Nova conta" : mode === "recover" ? "Recuperação" : "Sua conta NOVA"}</Eyebrow>
              <h1 className="display mt-4 text-[clamp(2.6rem,5.2vw,3.6rem)] text-ink">
                {copy.title}
                <br />
                <span className="display-serif">{copy.accent}</span>
              </h1>
              <p className="mt-4 text-[14.5px] leading-[1.65] text-ink-2">{message ?? copy.lead}</p>

              {mode !== "recover" && (
                <div className="mt-6 grid grid-cols-2 border border-rule p-0.5">
                  {(["signin", "signup"] as const).map((item) => (
                    <button
                      key={item}
                      type="button"
                      aria-pressed={mode === item}
                      onClick={() => onMode(item)}
                      className={`h-9 font-mono text-[10.5px] uppercase tracking-[0.13em] transition-colors ${
                        mode === item ? "bg-ink text-paper" : "text-warm hover:text-ink"
                      }`}
                    >
                      {item === "signin" ? "Entrar" : "Criar conta"}
                    </button>
                  ))}
                </div>
              )}

              <form onSubmit={submit} className="mt-5 space-y-4" noValidate>
                {errors.form && (
                  <p
                    role="alert"
                    className="flex items-start gap-2 border border-signal/40 bg-signal-soft/60 px-3 py-2.5 text-[12.5px] leading-snug text-signal-dark"
                  >
                    <Icon name="alert" className="mt-[1px] h-3.5 w-3.5 shrink-0" />
                    {errors.form}
                  </p>
                )}

                {mode === "signup" && (
                  <TextField
                    id="name"
                    label="Seu nome"
                    autoComplete="name"
                    value={name}
                    onChange={(event) => setName(event.target.value)}
                    placeholder="Como quer ser chamado"
                    error={errors.name}
                  />
                )}

                <TextField
                  id="email"
                  label="E-mail"
                  type="email"
                  autoComplete="email"
                  autoFocus
                  value={email}
                  onChange={(event) => setEmail(event.target.value)}
                  placeholder="voce@email.com"
                  error={errors.email}
                />

                {mode === "recover" && (
                  <TextField
                    id="code"
                    label="Código de recuperação"
                    autoComplete="off"
                    spellCheck={false}
                    value={code}
                    onChange={(event) => setCode(formatRecoveryCode(event.target.value))}
                    placeholder="XXXX-XXXX-XXXX"
                    className="font-mono tracking-[0.08em]"
                    error={errors.code}
                  />
                )}

                <div>
                  <PasswordField
                    id="password"
                    label={mode === "recover" ? "Nova senha" : "Senha"}
                    hint={mode === "signin" ? undefined : `mín. ${MIN_PASSWORD} caracteres`}
                    autoComplete={mode === "signin" ? "current-password" : "new-password"}
                    value={password}
                    onChange={(event) => setPassword(event.target.value)}
                    placeholder="••••••••"
                    error={errors.password}
                    showStrength={mode !== "signin"}
                  />
                  {mode === "signin" && (
                    <div className="mt-2.5 flex items-center justify-between gap-3">
                      <label className="flex cursor-pointer items-center gap-2">
                        <input
                          type="checkbox"
                          checked={remember}
                          onChange={(event) => setRemember(event.target.checked)}
                          className="h-3.5 w-3.5 accent-[#c7452a]"
                        />
                        <span className="text-[12.5px] text-ink-2">Manter conectado</span>
                      </label>
                      <button
                        type="button"
                        onClick={() => onMode("recover")}
                        className="text-[12.5px] text-signal underline decoration-rule underline-offset-2 hover:decoration-signal"
                      >
                        Esqueci a senha
                      </button>
                    </div>
                  )}
                </div>

                {mode === "signup" && (
                  <label className="flex cursor-pointer items-center gap-2">
                    <input
                      type="checkbox"
                      checked={remember}
                      onChange={(event) => setRemember(event.target.checked)}
                      className="h-3.5 w-3.5 accent-[#c7452a]"
                    />
                    <span className="text-[12.5px] text-ink-2">Manter conectado neste navegador</span>
                  </label>
                )}

                <Button type="submit" size="lg" className="w-full" disabled={loading}>
                  {loading ? (
                    <>
                      <Spinner className="h-3.5 w-3.5" />
                      {mode === "signup" ? "Criando conta" : mode === "recover" ? "Redefinindo" : "Entrando"}
                    </>
                  ) : (
                    <>
                      {mode === "signup" ? "Criar conta" : mode === "recover" ? "Redefinir senha" : "Entrar"}
                      <Icon name="arrow-right" className="h-3.5 w-3.5" />
                    </>
                  )}
                </Button>
              </form>

              <div className="mt-5 flex flex-col gap-3 border-t border-rule pt-4 text-[12.5px] text-warm sm:flex-row sm:items-center sm:justify-between">
                {mode === "recover" ? (
                  <button
                    type="button"
                    onClick={() => onMode("signin")}
                    className="inline-flex items-center gap-1.5 text-ink hover:text-signal"
                  >
                    <Icon name="arrow-right" className="h-3 w-3 rotate-180" />
                    Voltar para entrar
                  </button>
                ) : (
                  <button type="button" onClick={onBack} className="text-left text-ink hover:text-signal">
                    Continuar sem conta
                  </button>
                )}
                <span className="inline-flex items-center gap-1.5 font-mono text-[10.5px]">
                  <Icon name="lock" className="h-3 w-3" />
                  Senha protegida por hash. Nunca em texto.
                </span>
              </div>
            </div>
          )}
        </div>

        <div className="flex items-center justify-between gap-3 border-t border-rule pt-3">
          <p className="label text-warm">© {new Date().getFullYear()} NOVA</p>
          <p className="label text-warm">Windows 11 · x64</p>
        </div>
      </div>

      {/* ------------------------------ lateral ------------------------------ */}
      <aside className="hairline-grid-dark relative hidden overflow-hidden bg-film lg:flex lg:flex-col lg:justify-between lg:px-12 lg:py-10 xl:px-16">
        <div className="flex items-center justify-between">
          <Brand tone="paper" />
          <span className="label border border-paper/20 px-2.5 py-1.5 text-bone/55">Conta NOVA</span>
        </div>

        <div className="my-10">
          <h2 className="display text-[clamp(2.4rem,3.8vw,3.6rem)] text-bone">
            Um login.
            <br />
            <span className="text-signal">Site e app.</span>
          </h2>
          <p className="mt-4 max-w-[44ch] text-[14.5px] leading-[1.65] text-bone/60">
            Vincule o app com um código de 8 caracteres. Preferências, apelidos e rotinas acompanham
            você — o resto fica no seu PC.
          </p>

          <div className="mt-8 max-w-[500px]">
            <AppMock name={previewName} email={previewEmail} color={previewColor} />
            <p className="mt-3 font-mono text-[10.5px] text-bone/40">
              Assim aparece em Configurações ▸ Conta, no app.
            </p>
          </div>
        </div>

        <ul className="grid grid-cols-3 gap-4 border-t border-paper/15 pt-5">
          {[
            ["Vínculo", "código de 8 caracteres"],
            ["Recuperação", "código de uso único"],
            ["Privacidade", "dados sensíveis só no PC"],
          ].map(([title, body]) => (
            <li key={title}>
              <p className="label text-bone">{title}</p>
              <p className="mt-1 font-mono text-[10.5px] leading-snug text-bone/45">{body}</p>
            </li>
          ))}
        </ul>
      </aside>
    </div>
  );
}
