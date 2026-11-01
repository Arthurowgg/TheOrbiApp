import { useEffect, useState } from "react";
import { Badge, Button, Icon, Panel, Switch, TextField } from "../ui";
import { PageHeader, type PageProps } from "./AccountShell";
import {
  saveAppPrefs,
  saveRoutines,
  saveShortcuts,
  setSync,
  type AppPrefs,
  type Routine,
  type Shortcut,
  type SyncPrefs,
} from "../../lib/accounts";
import { SYNC_ITEMS } from "../../data/site";

type Tab = "atalhos" | "rotinas" | "app";

export default function Sync({ account, reload, notify }: PageProps) {
  const [tab, setTab] = useState<Tab>("atalhos");
  const [busy, setBusy] = useState(false);

  const [shortcuts, setShortcuts] = useState<Shortcut[]>(account.shortcuts);
  const [shortcutDraft, setShortcutDraft] = useState<Shortcut>({ trigger: "", expansion: "" });

  const [routines, setRoutines] = useState<Routine[]>(account.routines);
  const [routineDraft, setRoutineDraft] = useState<Routine>({ name: "", commands: [] });
  const [commandDraft, setCommandDraft] = useState("");

  const [prefs, setPrefs] = useState<AppPrefs>(account.appPrefs);

  // A conta é recarregada depois de cada gravação: mantém os campos espelhando o Firestore.
  useEffect(() => {
    setShortcuts(account.shortcuts);
    setRoutines(account.routines);
    setPrefs(account.appPrefs);
  }, [account]);

  const commit = async (
    action: () => Promise<void>,
    message: string,
    fallback: () => void,
  ) => {
    setBusy(true);
    try {
      await action();
      await reload();
      notify(message);
    } catch (error) {
      fallback();
      notify(error instanceof Error ? error.message : "Não foi possível salvar.");
    } finally {
      setBusy(false);
    }
  };

  /* ------------------------------ atalhos ------------------------------ */
  const addShortcut = () => {
    if (!shortcutDraft.trigger.trim() || !shortcutDraft.expansion.trim()) return;
    const next = [
      ...shortcuts.filter((item) => item.trigger !== shortcutDraft.trigger),
      shortcutDraft,
    ].slice(0, 100);
    const previous = shortcuts;
    setShortcuts(next);
    setShortcutDraft({ trigger: "", expansion: "" });
    void commit(() => saveShortcuts(next), "Atalho salvo.", () => setShortcuts(previous));
  };

  const removeShortcut = (trigger: string) => {
    const next = shortcuts.filter((item) => item.trigger !== trigger);
    const previous = shortcuts;
    setShortcuts(next);
    void commit(() => saveShortcuts(next), "Atalho removido.", () => setShortcuts(previous));
  };

  /* ------------------------------ rotinas ------------------------------ */
  const addCommand = () => {
    const command = commandDraft.trim();
    if (!command) return;
    setRoutineDraft((draft) => ({ ...draft, commands: [...draft.commands, command].slice(0, 12) }));
    setCommandDraft("");
  };

  const saveRoutine = () => {
    if (!routineDraft.name.trim() || routineDraft.commands.length === 0) return;
    const next = [...routines.filter((item) => item.name !== routineDraft.name), routineDraft].slice(0, 60);
    const previous = routines;
    setRoutines(next);
    setRoutineDraft({ name: "", commands: [] });
    void commit(() => saveRoutines(next), "Rotina salva.", () => setRoutines(previous));
  };

  const removeRoutine = (name: string) => {
    const next = routines.filter((item) => item.name !== name);
    const previous = routines;
    setRoutines(next);
    void commit(() => saveRoutines(next), "Rotina removida.", () => setRoutines(previous));
  };

  /* ------------------------------ preferências do app ------------------------------ */
  const savePreferences = () => {
    const previous = account.appPrefs;
    void commit(
      () => saveAppPrefs(prefs),
      "Preferências do app atualizadas.",
      () => setPrefs(previous),
    );
  };

  const toggleSync = (key: keyof SyncPrefs, value: boolean) => {
    const previous = account.sync;
    void commit(
      () => setSync({ ...account.sync, [key]: value }),
      value ? "Sincronização ligada." : "Sincronização desligada.",
      () => notify(`Não foi possível alterar. Estado anterior: ${String(previous[key])}`),
    );
  };

  const TABS: { id: Tab; label: string; count?: number }[] = [
    { id: "atalhos", label: "Atalhos", count: shortcuts.length },
    { id: "rotinas", label: "Rotinas", count: routines.length },
    { id: "app", label: "App", count: undefined },
  ];

  return (
    <>
      <PageHeader
        title="Sincronização"
        description="O que o site e o app compartilham pelo Firestore. Estes dados aparecem no app assim que ele abre."
        actions={<Badge tone="ok">Firestore ativo</Badge>}
      />

      <div className="grid gap-5 xl:grid-cols-[1.25fr_1fr]">
        <div className="space-y-4">
          <div className="flex flex-wrap gap-1 border border-rule bg-[#fbf9f3] p-1">
            {TABS.map((item) => (
              <button
                key={item.id}
                type="button"
                onClick={() => setTab(item.id)}
                className={`flex-1 px-3 py-2 font-mono text-[10.5px] uppercase tracking-[0.1em] transition-colors ${
                  tab === item.id ? "bg-ink text-paper" : "text-ink-2 hover:bg-ink/[0.06]"
                }`}
              >
                {item.label}
                {typeof item.count === "number" && item.count > 0 && (
                  <span className={tab === item.id ? "text-paper/60" : "text-warm"}> {item.count}</span>
                )}
              </button>
            ))}
          </div>

          {tab === "atalhos" && (
            <Panel
              title="Atalhos de texto"
              description="No app: “expande assinatura” cola o texto na área de transferência."
            >
              <div className="grid gap-3 sm:grid-cols-[0.4fr_0.6fr]">
                <TextField
                  id="shortcut-trigger"
                  label="Gatilho"
                  placeholder="assinatura"
                  value={shortcutDraft.trigger}
                  onChange={(event) => setShortcutDraft({ ...shortcutDraft, trigger: event.target.value })}
                />
                <TextField
                  id="shortcut-text"
                  label="Texto"
                  placeholder="Ana Souza · Orbi"
                  value={shortcutDraft.expansion}
                  onChange={(event) => setShortcutDraft({ ...shortcutDraft, expansion: event.target.value })}
                />
              </div>
              <Button className="mt-3" size="sm" onClick={addShortcut} disabled={busy}>
                <Icon name="edit" className="h-3.5 w-3.5" />
                Adicionar atalho
              </Button>
              <ul className="mt-4 divide-y divide-rule border-t border-rule">
                {shortcuts.length === 0 && (
                  <li className="py-4 text-[12.5px] text-warm">Nenhum atalho ainda.</li>
                )}
                {shortcuts.map((item) => (
                  <li key={item.trigger} className="flex items-center gap-3 py-2.5">
                    <span className="min-w-0 flex-1">
                      <span className="block truncate font-mono text-[11.5px] text-ink">“{item.trigger}”</span>
                      <span className="block truncate text-[12px] text-warm">{item.expansion}</span>
                    </span>
                    <button
                      type="button"
                      onClick={() => removeShortcut(item.trigger)}
                      className="text-warm transition-colors hover:text-signal"
                      aria-label={`Remover atalho ${item.trigger}`}
                    >
                      <Icon name="trash" className="h-3.5 w-3.5" />
                    </button>
                  </li>
                ))}
              </ul>
            </Panel>
          )}

          {tab === "rotinas" && (
            <Panel
              title="Rotinas"
              description="Uma frase no app executa vários comandos em sequência: “modo estudo”."
            >
              <TextField
                id="routine-name"
                label="Nome da rotina"
                placeholder="modo estudo"
                value={routineDraft.name}
                onChange={(event) => setRoutineDraft({ ...routineDraft, name: event.target.value })}
              />
              <div className="mt-3 flex flex-col gap-2 sm:flex-row">
                <div className="flex-1">
                  <TextField
                    id="routine-command"
                    label="Comando"
                    placeholder="abre o word"
                    value={commandDraft}
                    onChange={(event) => setCommandDraft(event.target.value)}
                    onKeyDown={(event) => {
                      if (event.key === "Enter") {
                        event.preventDefault();
                        addCommand();
                      }
                    }}
                  />
                </div>
                <Button size="sm" variant="secondary" className="sm:mt-[22px]" onClick={addCommand}>
                  Adicionar
                </Button>
              </div>

              {routineDraft.commands.length > 0 && (
                <ol className="mt-3 space-y-1.5">
                  {routineDraft.commands.map((command, index) => (
                    <li key={`${command}-${index}`} className="flex items-center gap-2 text-[12.5px] text-ink-2">
                      <span className="label tnum text-signal">{index + 1}</span>
                      <span className="font-mono">{command}</span>
                      <button
                        type="button"
                        onClick={() =>
                          setRoutineDraft((draft) => ({
                            ...draft,
                            commands: draft.commands.filter((_, position) => position !== index),
                          }))
                        }
                        className="ml-auto text-warm hover:text-signal"
                        aria-label="Remover comando"
                      >
                        <Icon name="close" className="h-3 w-3" />
                      </button>
                    </li>
                  ))}
                </ol>
              )}

              <Button
                className="mt-3"
                size="sm"
                onClick={saveRoutine}
                disabled={busy || !routineDraft.name.trim() || routineDraft.commands.length === 0}
              >
                <Icon name="bolt" className="h-3.5 w-3.5" />
                Salvar rotina
              </Button>

              <ul className="mt-4 divide-y divide-rule border-t border-rule">
                {routines.length === 0 && (
                  <li className="py-4 text-[12.5px] text-warm">Nenhuma rotina ainda.</li>
                )}
                {routines.map((routine) => (
                  <li key={routine.name} className="flex items-start gap-3 py-2.5">
                    <span className="min-w-0 flex-1">
                      <span className="block text-[13px] font-medium text-ink">{routine.name}</span>
                      <span className="block truncate font-mono text-[10.5px] text-warm">
                        {routine.commands.join(" → ")}
                      </span>
                    </span>
                    <button
                      type="button"
                      onClick={() => removeRoutine(routine.name)}
                      className="text-warm transition-colors hover:text-signal"
                      aria-label={`Remover rotina ${routine.name}`}
                    >
                      <Icon name="trash" className="h-3.5 w-3.5" />
                    </button>
                  </li>
                ))}
              </ul>
            </Panel>
          )}

          {tab === "app" && (
            <Panel
              title="Preferências do app"
              description="O app lê isto do Firestore ao abrir. Vale para todos os PCs vinculados."
            >
              <div className="space-y-4">
                <div className="flex items-center justify-between gap-4">
                  <div>
                    <p className="text-[13.5px] font-medium text-ink">Abrir com o Windows</p>
                    <p className="text-[12px] text-warm">Inicia na bandeja, pronto para Alt+Shift+O.</p>
                  </div>
                  <Switch
                    checked={prefs.startWithWindows}
                    onChange={(value) => setPrefs({ ...prefs, startWithWindows: value })}
                    label="Abrir com o Windows"
                  />
                </div>

                <div className="flex items-center justify-between gap-4 border-t border-rule pt-4">
                  <div>
                    <p className="text-[13.5px] font-medium text-ink">Bloqueio por inatividade</p>
                    <p className="text-[12px] text-warm">Avisa e bloqueia o Windows depois do tempo escolhido.</p>
                  </div>
                  <Switch
                    checked={prefs.lockEnabled}
                    onChange={(value) => setPrefs({ ...prefs, lockEnabled: value })}
                    label="Bloqueio por inatividade"
                  />
                </div>

                <div className="grid gap-3 border-t border-rule pt-4 sm:grid-cols-2">
                  <TextField
                    id="prefs-lock"
                    label="Bloquear depois de (min)"
                    type="number"
                    min={1}
                    max={120}
                    value={prefs.lockAfterMinutes}
                    onChange={(event) =>
                      setPrefs({ ...prefs, lockAfterMinutes: Number(event.target.value) || 5 })
                    }
                  />
                  <TextField
                    id="prefs-volume"
                    label="Volume padrão (%)"
                    type="number"
                    min={0}
                    max={100}
                    value={prefs.volume}
                    onChange={(event) => setPrefs({ ...prefs, volume: Number(event.target.value) || 0 })}
                  />
                </div>

                <div className="border-t border-rule pt-4">
                  <label htmlFor="prefs-model" className="label text-ink">
                    Modelo do Gemini no app
                  </label>
                  <select
                    id="prefs-model"
                    value={prefs.geminiModel}
                    onChange={(event) => setPrefs({ ...prefs, geminiModel: event.target.value })}
                    className="mt-1.5 h-11 w-full border border-rule bg-white/70 px-3 text-[13.5px] text-ink outline-none focus:border-ink"
                  >
                    <option value="gemini-2.0-flash">gemini-2.0-flash</option>
                    <option value="gemini-1.5-flash">gemini-1.5-flash</option>
                    <option value="gemini-1.5-flash-8b">gemini-1.5-flash-8b</option>
                  </select>
                  <p className="mt-1.5 text-[11.5px] text-warm">
                    A chave da API continua só no seu PC — ela nunca é enviada para o Firestore.
                  </p>
                </div>

                <Button onClick={savePreferences} disabled={busy}>
                  <Icon name="check" className="h-3.5 w-3.5" />
                  Salvar preferências
                </Button>
              </div>
            </Panel>
          )}
        </div>

        {/* ---------------------------- o que sincroniza ---------------------------- */}
        <div className="space-y-5">
          <Panel title="O que acompanha a conta" description="Escolha o que o app sincroniza pelo Firestore.">
            <ul className="divide-y divide-rule">
              {SYNC_ITEMS.map((item) => (
                <li key={item.key} className="flex items-center justify-between gap-4 py-3 first:pt-0">
                  <div className="min-w-0">
                    <p className="text-[13.5px] font-medium text-ink">{item.label}</p>
                    <p className="truncate font-mono text-[10.5px] text-warm">{item.hint}</p>
                  </div>
                  <Switch
                    checked={account.sync[item.key]}
                    onChange={(value) => toggleSync(item.key, value)}
                    label={item.label}
                  />
                </li>
              ))}
            </ul>
          </Panel>

          <Panel title="Nunca saem do PC" description="Estes dados não existem no Firestore.">
            <ul className="space-y-2">
              {["Notas", "Área de transferência", "Chave do Gemini", "Logs de segurança", "Lembretes"].map(
                (item) => (
                  <li key={item} className="flex items-center gap-2.5 text-[12.5px] text-ink-2">
                    <Icon name="lock" className="h-3.5 w-3.5 shrink-0 text-moss" />
                    {item}
                  </li>
                ),
              )}
            </ul>
          </Panel>

          <div className="border border-rule bg-paper-2/40 p-4">
            <p className="label text-warm">Como chega no app</p>
            <p className="mt-2 text-[12.5px] leading-relaxed text-ink-2">
              O app entra com a mesma conta (Firebase Authentication) e escuta{" "}
              <code className="font-mono text-[11px]">users/{account.id.slice(0, 6)}…</code> em tempo
              real. O que você editar aqui aparece lá em segundos; o que você mudar no app aparece aqui.
            </p>
          </div>
        </div>
      </div>
    </>
  );
}
