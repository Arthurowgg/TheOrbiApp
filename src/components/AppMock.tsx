import { Avatar, Icon, Mark } from "./ui";
import { SYNC_ITEMS } from "../data/site";

type Props = {
  name: string;
  email: string;
  color?: string;
  className?: string;
};

/* A tela Configurações ▸ Conta do app Orbi, desenhada em HTML.
 * Na página de login ela reage ao que a pessoa digita. */
export default function AppMock({ name, email, color = "#c7452a", className = "" }: Props) {
  const shownName = name.trim() || "Seu nome";
  const shownEmail = email.trim() || "voce@email.com";

  return (
    <div
      role="img"
      aria-label={`Tela Conta do aplicativo Orbi, conectada como ${shownName}`}
      className={`overflow-hidden border border-white/10 bg-[#1a1511] text-bone shadow-[0_30px_70px_-30px_rgba(0,0,0,0.75)] ${className}`}
    >
      {/* barra de título */}
      <div className="flex items-center justify-between border-b border-white/10 bg-[#14100c] pl-3.5">
        <div className="flex items-center gap-2 py-2.5">
          <span className="grid h-4 w-4 place-items-center bg-signal text-white">
            <Mark className="h-2.5 w-2.5" />
          </span>
          <span className="font-mono text-[10.5px] text-bone/65">Orbi · Configurações</span>
        </div>
        <div className="flex text-bone/45">
          {(["minus", "square", "close"] as const).map((icon) => (
            <span key={icon} className="grid h-9 w-11 place-items-center">
              <Icon name={icon} className="h-3 w-3" strokeWidth={1.4} />
            </span>
          ))}
        </div>
      </div>

      <div className="grid grid-cols-[112px_minmax(0,1fr)] sm:grid-cols-[132px_minmax(0,1fr)]">
        {/* navegação */}
        <nav className="space-y-0.5 border-r border-white/10 bg-[#16120e] p-2" aria-hidden>
          {["Geral", "IA", "Segurança", "Conta", "Rotinas", "Aparência"].map((item) => (
            <div
              key={item}
              className={`px-2.5 py-2 font-mono text-[11px] ${
                item === "Conta" ? "bg-white/10 text-bone" : "text-bone/45"
              }`}
            >
              {item}
            </div>
          ))}
        </nav>

        {/* conteúdo */}
        <div className="min-w-0 p-4 sm:p-5">
          <p className="font-mono text-[10px] uppercase tracking-[0.16em] text-bone/45">Conta</p>

          <div className="mt-3 flex items-center gap-3 border border-white/10 bg-white/[0.03] p-3">
            <Avatar name={shownName} color={color} size="lg" />
            <div className="min-w-0">
              <p className="truncate text-[14px] font-medium text-bone">{shownName}</p>
              <p className="truncate font-mono text-[10.5px] text-bone/50">{shownEmail}</p>
              <span className="mt-1.5 inline-flex items-center gap-1.5 font-mono text-[10px] text-[#e9846a]">
                <span className="h-1.5 w-1.5 rounded-full bg-signal" />
                Vinculada ao site
              </span>
            </div>
          </div>

          <p className="mt-4 font-mono text-[10px] uppercase tracking-[0.16em] text-bone/45">
            Na sua conta
          </p>
          <ul className="mt-2 divide-y divide-white/10 border-y border-white/10">
            {SYNC_ITEMS.map((item) => (
              <li key={item.key} className="flex items-center justify-between gap-3 py-2">
                <span className="truncate text-[12px] text-bone/85">{item.label}</span>
                <span className="relative h-[16px] w-[28px] shrink-0 rounded-full bg-signal">
                  <span className="absolute left-[14px] top-[2px] h-3 w-3 rounded-full bg-white" />
                </span>
              </li>
            ))}
          </ul>

        </div>
      </div>
    </div>
  );
}
