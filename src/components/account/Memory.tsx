import { useState } from "react";
import { Panel, Switch } from "../ui";
import { PageHeader, type PageProps } from "./AccountShell";
import { saveMemory, type MemoryPrefs } from "../../lib/accounts";

/**
 * Memória do Orbi: um sim ou não. Sem detalhes de armazenamento, sem lista de
 * campos — a pessoa escolhe se o Orbi lembra das coisas ou não.
 */
export default function Memory({ account, reload, notify }: PageProps) {
  const [busy, setBusy] = useState(false);
  const prefs: MemoryPrefs = account.memoryPrefs;

  const toggle = async (value: boolean) => {
    setBusy(true);
    try {
      await saveMemory({ ...prefs, enabled: value });
      await reload();
      notify(value ? "O Orbi vai lembrar das suas coisas." : "O Orbi deixou de lembrar.");
    } catch (error) {
      notify(error instanceof Error ? error.message : "Não foi possível salvar.");
    } finally {
      setBusy(false);
    }
  };

  return (
    <>
      <PageHeader title="Memória" />

      <div className="max-w-[560px]">
        <Panel>
          <Switch
            checked={prefs.enabled}
            onChange={(value) => void toggle(value)}
            label="Fazer o Orbi lembrar de mim"
            description="Ele lembra do que você pede e do que anota, para responder depois."
          />
          {busy && <p className="mt-3 font-mono text-[11px] text-warm">salvando…</p>}
        </Panel>

        {!prefs.enabled && (
          <p className="mt-4 px-1 text-[12.5px] leading-relaxed text-warm">
            Desligado, o Orbi continua funcionando normalmente e não guarda nada sobre você.
          </p>
        )}
      </div>
    </>
  );
}
