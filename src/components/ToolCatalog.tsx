import { useState } from "react";
import { Icon, Keycap } from "./ui";
import { GROUP_COUNT, TOOL_COUNT, TOOL_GROUPS } from "../data/tools";

/**
 * O catálogo do app, na página: a pessoa escolhe um grupo e vê o comando
 * exato que a barra aceita. Sem promessa, sem contagem inflada.
 */
export default function ToolCatalog() {
  const [active, setActive] = useState(TOOL_GROUPS[0].name);
  const group = TOOL_GROUPS.find((item) => item.name === active) ?? TOOL_GROUPS[0];

  return (
    <div className="grid gap-8 lg:grid-cols-[220px_minmax(0,1fr)] lg:gap-12">
      {/* grupos */}
      <div>
        <div className="flex flex-wrap gap-x-4 gap-y-1 border-t border-rule pt-3 lg:block lg:border-0 lg:pt-0">
          {TOOL_GROUPS.map((item) => {
            const selected = item.name === group.name;
            return (
              <button
                key={item.name}
                type="button"
                onClick={() => setActive(item.name)}
                aria-pressed={selected}
                className={`flex w-full items-baseline justify-between gap-3 border-b border-rule py-2 text-left transition-colors ${
                  selected ? "text-ink" : "text-warm hover:text-ink-2"
                }`}
              >
                <span className={`text-[13.5px] ${selected ? "font-semibold" : ""}`}>{item.name}</span>
                <span className="font-mono text-[10px] tnum text-warm">{item.tools.length}</span>
              </button>
            );
          })}
        </div>
        <p className="mt-3 font-mono text-[10px] uppercase tracking-[0.12em] text-warm">
          {TOOL_COUNT} ações · {GROUP_COUNT} grupos
        </p>
      </div>

      {/* ferramentas do grupo */}
      <div>
        <div className="flex flex-wrap items-baseline justify-between gap-2">
          <h3 className="display text-[22px] text-ink">{group.name}</h3>
          <p className="text-[13px] text-warm">{group.hint}</p>
        </div>

        <ul className="mt-4 grid gap-x-8 gap-y-0 border-t border-rule sm:grid-cols-2">
          {group.tools.map((tool) => (
            <li
              key={tool}
              className="flex items-center gap-3 border-b border-rule py-2.5"
            >
              <span className="flex items-center gap-2.5 text-[13.5px] text-ink-2">
                <Icon name="arrow-right" className="h-3 w-3 shrink-0 text-signal" />
                {tool}
              </span>
            </li>
          ))}
        </ul>

        <p className="mt-4 text-[13px] leading-relaxed text-warm">
          Na barra, digite <Keycap>/</Keycap> e o nome — ou peça do seu jeito que a IA encadeia.
        </p>
      </div>
    </div>
  );
}
