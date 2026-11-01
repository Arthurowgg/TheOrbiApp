import { Avatar, Icon, Mark } from "./ui";

type Props = {
  name: string;
  email?: string;
  color?: string;
  className?: string;
};

/* O painel IA do app Orbi, desenhado em HTML.
 * Na página de entrar ele reage ao nome que a pessoa digita. */
export default function AppMock({ name, color = "#c7452a", className = "" }: Props) {
  const shownName = name.trim() || "seu perfil";

  return (
    <div
      role="img"
      aria-label={`Painel do aplicativo Orbi, perfil de ${shownName}`}
      className={`overflow-hidden border border-white/10 bg-[#1a1511] text-bone shadow-[0_30px_70px_-30px_rgba(0,0,0,0.75)] ${className}`}
    >
      {/* barra de título */}
      <div className="flex items-center justify-between border-b border-white/10 bg-[#14100c] pl-3.5">
        <div className="flex items-center gap-2 py-2.5">
          <span className="grid h-4 w-4 place-items-center bg-signal text-white">
            <Mark className="h-2.5 w-2.5" />
          </span>
          <span className="font-mono text-[10.5px] text-bone/65">Orbi · IA</span>
        </div>
        <div className="flex text-bone/45">
          {(["minus", "square", "close"] as const).map((icon) => (
            <span key={icon} className="grid h-9 w-11 place-items-center">
              <Icon name={icon} className="h-3 w-3" strokeWidth={1.4} />
            </span>
          ))}
        </div>
      </div>

      <div className="p-4 sm:p-5">
        <div className="flex items-center gap-3 border border-white/10 bg-white/[0.03] p-3">
          <Avatar name={shownName} color={color} size="lg" />
          <div className="min-w-0">
            <p className="truncate text-[14px] font-medium text-bone">{shownName}</p>
            <p className="mt-0.5 truncate font-mono text-[10.5px] text-bone/50">neste PC · sem conta</p>
            <span className="mt-1.5 inline-flex items-center gap-1.5 font-mono text-[10px] text-[#e9846a]">
              <span className="h-1.5 w-1.5 rounded-full bg-signal" />
              chave da IA pronta
            </span>
          </div>
        </div>

        <p className="mt-4 font-mono text-[10px] uppercase tracking-[0.16em] text-bone/45">
          O que o Orbi controla
        </p>
        <ul className="mt-2 divide-y divide-white/10 border-y border-white/10">
          {[
            "Ver a tela e as janelas",
            "Clicar na posição exata",
            "Digitar e apertar atalhos",
            "Encadear passos na ordem",
          ].map((item) => (
            <li key={item} className="flex items-center justify-between gap-3 py-2">
              <span className="truncate text-[12px] text-bone/85">{item}</span>
              <span className="relative h-[16px] w-[28px] shrink-0 rounded-full bg-signal">
                <span className="absolute left-[14px] top-[2px] h-3 w-3 rounded-full bg-white" />
              </span>
            </li>
          ))}
        </ul>
      </div>
    </div>
  );
}
