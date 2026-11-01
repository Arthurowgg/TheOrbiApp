import { useState, type FormEvent } from "react";
import { Avatar, Button, CopyButton, Icon, Panel, TextField } from "../ui";
import { PageHeader, type PageProps } from "./AccountShell";
import { AVATAR_COLORS, AuthError, formatDate, isEmail, updateProfile } from "../../lib/accounts";

export default function Profile({ account, reload, notify }: PageProps) {
  const [name, setName] = useState(account.name);
  const [email, setEmail] = useState(account.email);
  const [errors, setErrors] = useState<{ name?: string; email?: string }>({});

  const dirty = name.trim() !== account.name || email.trim().toLowerCase() !== account.email;

  const save = (event: FormEvent) => {
    event.preventDefault();
    const next: typeof errors = {};
    if (name.trim().length < 2) next.name = "Digite como você quer ser chamado.";
    if (!isEmail(email)) next.email = "Digite um e-mail válido.";
    setErrors(next);
    if (Object.keys(next).length) return;

    try {
      updateProfile(account.id, { name, email });
      reload();
      notify("Perfil atualizado.");
    } catch (error) {
      if (error instanceof AuthError && error.code === "email_taken") {
        setErrors({ email: error.message });
      } else {
        notify(error instanceof Error ? error.message : "Não foi possível salvar.");
      }
    }
  };

  const pickColor = (color: string) => {
    try {
      updateProfile(account.id, { color });
      reload();
    } catch (error) {
      notify(error instanceof Error ? error.message : "Não foi possível salvar.");
    }
  };

  return (
    <>
      <PageHeader title="Perfil" description="Como você aparece no site e no app NOVA." />

      <div className="grid gap-5 lg:grid-cols-[1fr_310px]">
        <Panel title="Informações pessoais" description="Estes dados aparecem em Configurações ▸ Conta, no app.">
          <form onSubmit={save} className="space-y-4" noValidate>
            <TextField
              id="profile-name"
              label="Nome"
              autoComplete="name"
              value={name}
              onChange={(event) => setName(event.target.value)}
              error={errors.name}
            />
            <TextField
              id="profile-email"
              label="E-mail"
              type="email"
              autoComplete="email"
              value={email}
              onChange={(event) => setEmail(event.target.value)}
              error={errors.email}
              hint="usado para entrar"
            />
            <div className="flex flex-wrap items-center gap-3 border-t border-rule pt-4">
              <Button type="submit" disabled={!dirty}>
                Salvar alterações
              </Button>
              {dirty && (
                <button
                  type="button"
                  onClick={() => {
                    setName(account.name);
                    setEmail(account.email);
                    setErrors({});
                  }}
                  className="text-[13px] text-warm hover:text-ink"
                >
                  Descartar
                </button>
              )}
            </div>
          </form>
        </Panel>

        <div className="space-y-5">
          <Panel title="Avatar">
            <div className="flex items-center gap-4">
              <Avatar name={account.name} color={account.color} size="xl" />
              <div>
                <p className="text-[14px] font-medium text-ink">{account.name}</p>
                <p className="mt-0.5 break-all font-mono text-[11px] text-warm">{account.email}</p>
              </div>
            </div>
            <p className="label mt-5 text-warm">Cor</p>
            <div className="mt-2 flex flex-wrap gap-2">
              {AVATAR_COLORS.map((color) => (
                <button
                  key={color}
                  type="button"
                  onClick={() => pickColor(color)}
                  aria-label={`Usar a cor ${color}`}
                  aria-pressed={account.color === color}
                  className={`grid h-8 w-8 place-items-center rounded-full ring-offset-2 ring-offset-[#fbf9f3] transition-shadow ${
                    account.color === color ? "ring-2 ring-ink" : "hover:ring-2 hover:ring-rule"
                  }`}
                  style={{ backgroundColor: color }}
                >
                  {account.color === color && <Icon name="check" className="h-3.5 w-3.5 text-white" strokeWidth={2.4} />}
                </button>
              ))}
            </div>
          </Panel>

          <Panel title="Identificação">
            <dl className="space-y-3 text-[13px]">
              <div className="flex items-center justify-between gap-3">
                <dt className="text-warm">ID da conta</dt>
                <dd className="flex items-center gap-2">
                  <span className="font-mono text-ink">{account.id}</span>
                </dd>
              </div>
              <div className="flex items-center justify-between gap-3">
                <dt className="text-warm">Conta criada em</dt>
                <dd className="text-ink">{formatDate(account.createdAt)}</dd>
              </div>
              <div className="flex items-center justify-between gap-3">
                <dt className="text-warm">Idioma</dt>
                <dd className="text-ink">Português (Brasil)</dd>
              </div>
            </dl>
            <div className="mt-4 border-t border-rule pt-4">
              <CopyButton value={account.id} label="Copiar ID" />
            </div>
          </Panel>
        </div>
      </div>
    </>
  );
}
