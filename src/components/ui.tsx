import {
  useEffect,
  useId,
  useRef,
  useState,
  type ButtonHTMLAttributes,
  type InputHTMLAttributes,
  type ReactNode,
} from "react";
import { createPortal } from "react-dom";
import { MIN_PASSWORD, passwordStrength } from "../lib/accounts";

/* ------------------------------------------------------------------ */
/* Marca                                                              */
/* ------------------------------------------------------------------ */

/** O símbolo do Orbi: uma órbita aberta com o núcleo no centro. */
export function Mark({ className = "h-5 w-5" }: { className?: string }) {
  return (
    <svg viewBox="0 0 24 24" className={className} fill="none" aria-hidden>
      <circle cx="12" cy="12" r="3.1" fill="currentColor" />
      <path
        d="M12 4.2c4.3 0 7.8 3.5 7.8 7.8s-3.5 7.8-7.8 7.8S4.2 16.3 4.2 12 7.7 4.2 12 4.2Z"
        stroke="currentColor"
        strokeWidth="1.7"
        strokeLinecap="round"
        strokeDasharray="5.4 3.4"
        opacity="0.92"
      />
      <circle cx="19.1" cy="7.4" r="1.5" fill="currentColor" />
    </svg>
  );
}

export function Brand({
  onClick,
  tone = "ink",
  suffix,
}: {
  onClick?: () => void;
  tone?: "ink" | "paper";
  suffix?: string;
}) {
  const content = (
    <span className="flex items-center gap-2.5">
      <span
        className={`grid h-8 w-8 place-items-center ${
          tone === "paper" ? "bg-signal text-white" : "bg-ink text-paper"
        }`}
      >
        <Mark className="h-[18px] w-[18px]" />
      </span>
      <span className="flex items-baseline gap-2.5">
        <span
          className={`font-cond text-[23px] font-bold uppercase leading-none tracking-[0.02em] ${
            tone === "paper" ? "text-bone" : "text-ink"
          }`}
        >
          Orbi
        </span>
        {suffix && (
          <span className={`label hidden md:inline ${tone === "paper" ? "text-bone/55" : "text-warm"}`}>
            {suffix}
          </span>
        )}
      </span>
    </span>
  );
  if (!onClick) return <span className="shrink-0">{content}</span>;
  return (
    <button type="button" onClick={onClick} className="shrink-0" aria-label="Orbi — início">
      {content}
    </button>
  );
}

/** Luneta/avião de papel usado como textura de fundo em seções escuras. */
export function OrbitArt({ className = "" }: { className?: string }) {
  return (
    <svg viewBox="0 0 400 400" className={className} fill="none" aria-hidden>
      <g stroke="currentColor" strokeWidth="1">
        {[60, 96, 132, 168, 200].map((r, index) => (
          <ellipse
            key={r}
            cx="200"
            cy="200"
            rx={r}
            ry={r * (index % 2 === 0 ? 0.42 : 0.68)}
            transform={`rotate(${index * 26} 200 200)`}
            opacity={0.5 - index * 0.06}
          />
        ))}
      </g>
      <circle cx="200" cy="200" r="4.5" fill="currentColor" opacity="0.85" />
    </svg>
  );
}

/* ------------------------------------------------------------------ */
/* Ícones                                                             */
/* ------------------------------------------------------------------ */
export type IconName =
  | "apps"
  | "sliders"
  | "bell"
  | "bolt"
  | "monitor"
  | "lock"
  | "sparkle"
  | "arrow-right"
  | "arrow-down"
  | "arrow-up-right"
  | "download"
  | "check"
  | "chevron-down"
  | "chevron-right"
  | "menu"
  | "close"
  | "eye"
  | "eye-off"
  | "user"
  | "logout"
  | "windows"
  | "shield"
  | "play"
  | "laptop"
  | "key"
  | "copy"
  | "trash"
  | "link"
  | "grid"
  | "history"
  | "edit"
  | "alert"
  | "info"
  | "home"
  | "minus"
  | "square"
  | "google"
  | "mail"
  | "github"
  | "cpu"
  | "cloud"
  | "zap";

const ICON_PATHS: Record<IconName, ReactNode> = {
  cloud: (
    <>
      <path d="M7 18a4 4 0 0 1 .4-7.98A5.5 5.5 0 0 1 18 10.5a3.5 3.5 0 0 1 .5 6.96" />
      <path d="M12 12v7M9 16l3-3 3 3" />
    </>
  ),
  apps: (
    <>
      <rect x="3" y="3" width="7" height="7" rx="1" />
      <rect x="14" y="3" width="7" height="7" rx="1" />
      <rect x="3" y="14" width="7" height="7" rx="1" />
      <rect x="14" y="14" width="7" height="7" rx="1" />
    </>
  ),
  sliders: (
    <>
      <path d="M4 6h9M17 6h3M4 12h3M11 12h9M4 18h11M19 18h1" />
      <circle cx="15" cy="6" r="2" />
      <circle cx="9" cy="12" r="2" />
      <circle cx="17" cy="18" r="2" />
    </>
  ),
  bell: (
    <>
      <path d="M6 8a6 6 0 0 1 12 0c0 5 2 6 2 6H4s2-1 2-6Z" />
      <path d="M10 18a2 2 0 0 0 4 0" />
    </>
  ),
  bolt: <path d="M13 2 4 14h6l-1 8 9-12h-6l1-8Z" />,
  monitor: (
    <>
      <rect x="3" y="4" width="18" height="12" rx="1.5" />
      <path d="M9 20h6M12 16v4" />
    </>
  ),
  lock: (
    <>
      <rect x="4" y="10" width="16" height="10" rx="1.5" />
      <path d="M8 10V7a4 4 0 0 1 8 0v3" />
    </>
  ),
  sparkle: (
    <>
      <path d="M12 3.5c.45 4.2 3.3 7.05 7.5 7.5-4.2.45-7.05 3.3-7.5 7.5-.45-4.2-3.3-7.05-7.5-7.5 4.2-.45 7.05-3.3 7.5-7.5Z" />
      <path d="M18.5 16.5c.18 1.6 1.4 2.82 3 3-1.6.18-2.82 1.4-3 3-.18-1.6-1.4-2.82-3-3 1.6-.18 2.82-1.4 3-3Z" />
    </>
  ),
  "arrow-right": <path d="M4 12h16M14 6l6 6-6 6" />,
  "arrow-down": <path d="M12 4v16M6 14l6 6 6-6" />,
  "arrow-up-right": <path d="M7 17 17 7M8 7h9v9" />,
  download: <path d="M12 3v12M7 11l5 5 5-5M4 20h16" />,
  check: <path d="M4 12.5 9.5 18 20 6.5" />,
  "chevron-down": <path d="m6 9 6 6 6-6" />,
  "chevron-right": <path d="m9 6 6 6-6 6" />,
  menu: <path d="M4 7h16M4 12h16M4 17h16" />,
  close: <path d="M6 6l12 12M18 6 6 18" />,
  eye: (
    <>
      <path d="M2 12s3.6-6.5 10-6.5S22 12 22 12s-3.6 6.5-10 6.5S2 12 2 12Z" />
      <circle cx="12" cy="12" r="2.6" />
    </>
  ),
  "eye-off": (
    <>
      <path d="M3 3l18 18M10.6 6.1A10.5 10.5 0 0 1 12 6c6.4 0 10 6 10 6a17 17 0 0 1-3.2 3.9M6.4 8.1A16.6 16.6 0 0 0 2 12s3.6 6 10 6a10.4 10.4 0 0 0 4.3-.9" />
    </>
  ),
  user: (
    <>
      <circle cx="12" cy="8" r="3.6" />
      <path d="M5 20c0-3.6 3.1-5.6 7-5.6s7 2 7 5.6" />
    </>
  ),
  logout: <path d="M9 4H5v16h4M15 8l4 4-4 4M19 12H9" />,
  windows: <path d="M4 5.2 11 4v7H4V5.2ZM13 3.8 20 2.6V11h-7V3.8ZM4 13h7v7L4 18.8V13ZM13 13h7v8.4L13 20.2V13Z" />,
  shield: (
    <>
      <path d="M12 3l8 3v6c0 5-3.4 8-8 9-4.6-1-8-4-8-9V6l8-3Z" />
      <path d="m8.5 12 2.5 2.5 4.5-5" />
    </>
  ),
  play: <path d="M7 4.5v15l13-7.5-13-7.5Z" />,
  laptop: (
    <>
      <rect x="4" y="4" width="16" height="11" rx="1.5" />
      <path d="M2 19h20" />
    </>
  ),
  key: (
    <>
      <circle cx="8" cy="8" r="4" />
      <path d="m11 11 9 9M17 17l2-2M14 14l2-2" />
    </>
  ),
  copy: (
    <>
      <rect x="8" y="8" width="12" height="12" rx="1.5" />
      <path d="M16 8V5.5A1.5 1.5 0 0 0 14.5 4H5.5A1.5 1.5 0 0 0 4 5.5v9A1.5 1.5 0 0 0 5.5 16H8" />
    </>
  ),
  trash: <path d="M4 7h16M9 7V5h6v2M6 7l1 13h10l1-13M10 11v6M14 11v6" />,
  link: <path d="M10 13.5a4 4 0 0 0 5.7 0l2.8-2.8a4 4 0 1 0-5.7-5.7L11.5 6.3M14 10.5a4 4 0 0 0-5.7 0l-2.8 2.8a4 4 0 1 0 5.7 5.7l1.3-1.3" />,
  grid: (
    <>
      <rect x="3" y="3" width="7.5" height="7.5" />
      <rect x="13.5" y="3" width="7.5" height="7.5" />
      <rect x="3" y="13.5" width="7.5" height="7.5" />
      <rect x="13.5" y="13.5" width="7.5" height="7.5" />
    </>
  ),
  history: (
    <>
      <path d="M3.5 12a8.5 8.5 0 1 0 2.6-6.1M3.5 4v4.5H8" />
      <path d="M12 8v4.5l3 1.8" />
    </>
  ),
  edit: <path d="M4 20h4L20 8l-4-4L4 16v4ZM14 6l4 4" />,
  alert: (
    <>
      <path d="M12 3 22 20H2L12 3Z" />
      <path d="M12 10v4M12 17h.01" />
    </>
  ),
  info: (
    <>
      <circle cx="12" cy="12" r="9" />
      <path d="M12 11v5M12 8h.01" />
    </>
  ),
  home: <path d="M4 11l8-7 8 7v9h-5v-6H9v6H4v-9Z" />,
  minus: <path d="M5 12h14" />,
  square: <rect x="5" y="5" width="14" height="14" />,
  google: (
    <>
      <path d="M21.6 12.2c0-.7-.06-1.2-.17-1.8H12v3.5h5.4a4.6 4.6 0 0 1-2 3v2.5h3.2c1.9-1.7 3-4.3 3-7.2Z" fill="currentColor" stroke="none" />
      <path d="M12 22c2.7 0 4.9-.9 6.5-2.4l-3.2-2.5c-.9.6-2 1-3.3 1a5.9 5.9 0 0 1-5.5-4H3.2v2.6A10 10 0 0 0 12 22Z" fill="currentColor" stroke="none" opacity="0.75" />
      <path d="M6.5 14.1a6 6 0 0 1 0-4.2V7.3H3.2a10 10 0 0 0 0 9.4l3.3-2.6Z" fill="currentColor" stroke="none" opacity="0.55" />
      <path d="M12 6c1.5 0 2.8.5 3.8 1.5l2.8-2.8A9.6 9.6 0 0 0 12 2a10 10 0 0 0-8.8 5.3l3.3 2.6A5.9 5.9 0 0 1 12 6Z" fill="currentColor" stroke="none" opacity="0.9" />
    </>
  ),
  mail: (
    <>
      <rect x="3" y="5" width="18" height="14" rx="1.5" />
      <path d="m3.5 7 8.5 6 8.5-6" />
    </>
  ),
  github: (
    <path
      d="M12 2.2a9.8 9.8 0 0 0-3.1 19.1c.5.1.7-.2.7-.5v-1.9c-2.7.6-3.3-1.3-3.3-1.3-.4-1.1-1.1-1.4-1.1-1.4-.9-.6.1-.6.1-.6 1 .1 1.5 1 1.5 1 .9 1.5 2.3 1.1 2.9.8.1-.7.4-1.1.6-1.4-2.2-.2-4.5-1.1-4.5-4.9 0-1.1.4-2 1-2.7-.1-.2-.4-1.2.1-2.5 0 0 .8-.3 2.7 1a9.3 9.3 0 0 1 5 0c1.9-1.3 2.7-1 2.7-1 .5 1.3.2 2.3.1 2.5.6.7 1 1.6 1 2.7 0 3.8-2.3 4.7-4.5 4.9.4.3.7.9.7 1.9v2.8c0 .3.2.6.7.5A9.8 9.8 0 0 0 12 2.2Z"
      fill="currentColor"
      stroke="none"
    />
  ),
  cpu: (
    <>
      <rect x="6" y="6" width="12" height="12" rx="1.5" />
      <path d="M10 10h4v4h-4zM9 2v4M15 2v4M9 18v4M15 18v4M2 9h4M2 15h4M18 9h4M18 15h4" />
    </>
  ),
  zap: <path d="M13 2 4 14h6l-1 8 9-12h-6l1-8Z" />,
};

export function Icon({
  name,
  className = "h-4 w-4",
  strokeWidth = 1.6,
}: {
  name: IconName;
  className?: string;
  strokeWidth?: number;
}) {
  return (
    <svg
      viewBox="0 0 24 24"
      className={className}
      fill="none"
      stroke="currentColor"
      strokeWidth={strokeWidth}
      strokeLinecap="round"
      strokeLinejoin="round"
      aria-hidden
    >
      {ICON_PATHS[name]}
    </svg>
  );
}

/* ------------------------------------------------------------------ */
/* Botões                                                             */
/* ------------------------------------------------------------------ */
type ButtonProps = ButtonHTMLAttributes<HTMLButtonElement> & {
  variant?: "primary" | "secondary" | "ghost" | "inverse" | "outline" | "danger";
  size?: "sm" | "md" | "lg";
  loading?: boolean;
};

const BUTTON_VARIANTS: Record<NonNullable<ButtonProps["variant"]>, string> = {
  primary: "bg-ink text-paper hover:bg-signal",
  secondary: "border border-ink/25 bg-transparent text-ink hover:border-ink hover:bg-ink/[0.04]",
  ghost: "text-ink-2 hover:bg-ink/[0.06] hover:text-ink",
  inverse: "bg-paper text-ink hover:bg-signal hover:text-white",
  outline: "border border-white/25 text-bone hover:border-bone hover:bg-white/[0.06]",
  danger: "bg-signal text-white hover:bg-signal-dark",
};

const BUTTON_SIZES: Record<NonNullable<ButtonProps["size"]>, string> = {
  sm: "h-9 px-3.5 text-[12.5px] gap-1.5",
  md: "h-11 px-5 text-[13.5px] gap-2",
  lg: "h-[52px] px-6 text-[14.5px] gap-2.5",
};

export function Button({
  variant = "primary",
  size = "md",
  loading = false,
  className = "",
  children,
  disabled,
  ...rest
}: ButtonProps) {
  return (
    <button
      {...rest}
      disabled={disabled || loading}
      className={`inline-flex items-center justify-center font-medium transition-[background-color,border-color,color,transform] duration-150 active:translate-y-px disabled:cursor-not-allowed disabled:opacity-55 ${BUTTON_VARIANTS[variant]} ${BUTTON_SIZES[size]} ${className}`}
    >
      {loading && <Spinner className="h-3.5 w-3.5" />}
      {children}
    </button>
  );
}

/* ------------------------------------------------------------------ */
/* Texto e layout                                                     */
/* ------------------------------------------------------------------ */
export function Eyebrow({ children, tone = "ink" }: { children: ReactNode; tone?: "ink" | "paper" }) {
  return (
    <p className={`rule-label ${tone === "paper" ? "!text-bone/55 [&::before]:!bg-signal" : ""}`}>
      {children}
    </p>
  );
}

export function Reveal({
  children,
  delay = 0,
  className = "",
}: {
  children: ReactNode;
  delay?: number;
  className?: string;
}) {
  const ref = useRef<HTMLDivElement | null>(null);
  const [visible, setVisible] = useState(false);

  useEffect(() => {
    const node = ref.current;
    if (!node) return;
    if (typeof IntersectionObserver === "undefined") {
      setVisible(true);
      return;
    }
    const observer = new IntersectionObserver(
      (entries) => {
        entries.forEach((entry) => {
          if (entry.isIntersecting) {
            setVisible(true);
            observer.disconnect();
          }
        });
      },
      { rootMargin: "0px 0px -12% 0px", threshold: 0.08 },
    );
    observer.observe(node);
    return () => observer.disconnect();
  }, []);

  return (
    <div
      ref={ref}
      className={`reveal ${visible ? "is-visible" : ""} ${className}`}
      style={{ transitionDelay: `${delay}ms` }}
    >
      {children}
    </div>
  );
}

export function Keycap({ children }: { children: ReactNode }) {
  return (
    <kbd className="inline-flex h-6 min-w-6 items-center justify-center border border-ink/25 border-b-[2px] bg-[#fbf9f3] px-1.5 font-mono text-[10.5px] font-medium text-ink">
      {children}
    </kbd>
  );
}

export function Panel({
  title,
  description,
  actions,
  action,
  className = "",
  children,
  padded = true,
}: {
  title?: string;
  description?: string;
  actions?: ReactNode;
  action?: ReactNode;
  className?: string;
  children: ReactNode;
  padded?: boolean;
}) {
  const extra = actions ?? action;
  return (
    <section className={`border border-rule bg-[#fbf9f3] ${className}`}>
      {(title || extra) && (
        <header className="flex flex-wrap items-start justify-between gap-3 border-b border-rule px-5 py-4">
          <div>
            {title && <h2 className="text-[14.5px] font-semibold text-ink">{title}</h2>}
            {description && <p className="mt-0.5 max-w-[52ch] text-[12.5px] leading-snug text-warm">{description}</p>}
          </div>
          {extra && <div className="flex shrink-0 flex-wrap items-center gap-2">{extra}</div>}
        </header>
      )}
      <div className={padded ? "p-5" : ""}>{children}</div>
    </section>
  );
}

export function Badge({
  children,
  tone = "neutral",
}: {
  children: ReactNode;
  tone?: "neutral" | "signal" | "ok" | "ink" | "plain";
}) {
  const tones = {
    plain: "text-warm",
    neutral: "border border-rule text-warm",
    signal: "bg-signal-soft text-signal-dark",
    ok: "border border-[#3b6e3f]/35 text-[#3b6e3f]",
    ink: "bg-ink text-paper",
  };
  return (
    <span className={`label inline-flex items-center gap-1.5 px-2 py-1 ${tones[tone]}`}>{children}</span>
  );
}

/* ------------------------------------------------------------------ */
/* Campos                                                             */
/* ------------------------------------------------------------------ */
type FieldProps = InputHTMLAttributes<HTMLInputElement> & {
  label: string;
  hint?: string;
  error?: string;
  icon?: IconName;
};

export function TextField({ label, hint, error, icon, id, className = "", ...rest }: FieldProps) {
  const autoId = useId();
  const fieldId = id ?? autoId;
  return (
    <div>
      <div className="mb-1.5 flex items-baseline justify-between gap-3">
        <label htmlFor={fieldId} className="label text-ink">
          {label}
        </label>
        {hint && <span className="font-mono text-[10px] text-warm">{hint}</span>}
      </div>
      <div
        className={`flex h-11 items-center border bg-white/70 transition-colors focus-within:border-ink ${
          error ? "border-signal" : "border-rule"
        }`}
      >
        {icon && <Icon name={icon} className="ml-3 h-4 w-4 shrink-0 text-warm" />}
        <input
          {...rest}
          id={fieldId}
          aria-invalid={Boolean(error)}
          aria-describedby={error ? `${fieldId}-error` : undefined}
          className={`h-full w-full bg-transparent px-3 text-[14px] text-ink outline-none placeholder:text-warm/60 ${className}`}
        />
      </div>
      {error && (
        <p id={`${fieldId}-error`} className="mt-1.5 text-[11.5px] font-medium text-signal">
          {error}
        </p>
      )}
    </div>
  );
}

export function PasswordField({
  label,
  hint,
  error,
  id,
  showStrength = false,
  ...rest
}: FieldProps & { showStrength?: boolean }) {
  const [visible, setVisible] = useState(false);
  const autoId = useId();
  const fieldId = id ?? autoId;
  return (
    <div>
      <div className="mb-1.5 flex items-baseline justify-between gap-3">
        <label htmlFor={fieldId} className="label text-ink">
          {label}
        </label>
        {hint && <span className="font-mono text-[10px] text-warm">{hint}</span>}
      </div>
      <div
        className={`flex h-11 items-center border bg-white/70 transition-colors focus-within:border-ink ${
          error ? "border-signal" : "border-rule"
        }`}
      >
        <input
          {...rest}
          id={fieldId}
          type={visible ? "text" : "password"}
          aria-invalid={Boolean(error)}
          className="h-full w-full bg-transparent px-3 text-[14px] text-ink outline-none placeholder:text-warm/60"
        />
        <button
          type="button"
          onClick={() => setVisible((value) => !value)}
          className="grid h-full w-11 place-items-center text-warm transition-colors hover:text-ink"
          aria-label={visible ? "Ocultar senha" : "Mostrar senha"}
        >
          <Icon name={visible ? "eye-off" : "eye"} className="h-4 w-4" />
        </button>
      </div>
      {error && <p className="mt-1.5 text-[11.5px] font-medium text-signal">{error}</p>}
      {showStrength && <StrengthMeter password={String(rest.value ?? "")} />}
    </div>
  );
}

export function StrengthMeter({ password }: { password: string }) {
  const { score, label } = passwordStrength(password);
  if (!password) {
    return (
      <p className="mt-2 font-mono text-[10px] text-warm">
        Mínimo de {MIN_PASSWORD} caracteres · uma frase longa é melhor que “12345678”.
      </p>
    );
  }
  const colors = ["#c7452a", "#c7452a", "#9a6a1c", "#3b6e3f", "#2f5d62"];
  return (
    <div className="mt-2">
      <div className="flex gap-1">
        {[0, 1, 2, 3].map((index) => (
          <span
            key={index}
            className="h-[3px] flex-1 transition-colors"
            style={{ background: index < score ? colors[score] : "#cec4b4" }}
          />
        ))}
      </div>
      <p className="mt-1.5 font-mono text-[10px] text-warm">{label}</p>
    </div>
  );
}

export function Switch({
  checked,
  onChange,
  label,
  description,
}: {
  checked: boolean;
  onChange: (value: boolean) => void;
  label: string;
  description?: string;
}) {
  return (
    <div>
    <button
      type="button"
      role="switch"
      aria-checked={checked}
      aria-label={label}
      onClick={() => onChange(!checked)}
      className={`relative h-[22px] w-[38px] shrink-0 rounded-full transition-colors ${
        checked ? "bg-signal" : "bg-rule"
      }`}
    >
      <span
        className={`absolute top-[3px] h-4 w-4 rounded-full bg-white transition-all ${
          checked ? "left-[19px]" : "left-[3px]"
        }`}
      />
    </button>
      {description && <p className="mt-1 font-mono text-[10px] leading-snug text-warm">{description}</p>}
    </div>
  );
}

export function CopyButton({ value, label = "Copiar" }: { value: string; label?: string }) {
  const [copied, setCopied] = useState(false);
  return (
    <button
      type="button"
      onClick={async () => {
        try {
          await navigator.clipboard.writeText(value);
          setCopied(true);
          window.setTimeout(() => setCopied(false), 1800);
        } catch {
          setCopied(false);
        }
      }}
      className="label inline-flex items-center gap-1.5 border border-rule px-2 py-1 text-ink-2 transition-colors hover:border-ink hover:text-ink"
    >
      <Icon name={copied ? "check" : "copy"} className="h-3 w-3" />
      {copied ? "Copiado" : label}
    </button>
  );
}

export function Avatar({
  name,
  color = "#17120e",
  size = "md",
}: {
  name: string;
  color?: string;
  size?: "sm" | "md" | "lg" | "xl";
}) {
  const initials = name
    .trim()
    .split(/\s+/)
    .slice(0, 2)
    .map((part) => part[0]?.toUpperCase() ?? "")
    .join("");
  const sizes = {
    sm: "h-7 w-7 text-[10.5px]",
    md: "h-9 w-9 text-[12px]",
    lg: "h-12 w-12 text-[15px]",
    xl: "h-16 w-16 text-[19px]",
  };
  return (
    <span
      className={`grid shrink-0 place-items-center font-mono font-medium text-white ${sizes[size]}`}
      style={{ background: color }}
      aria-hidden
    >
      {initials || "O"}
    </span>
  );
}

export function Spinner({ className = "h-4 w-4" }: { className?: string }) {
  return (
    <svg viewBox="0 0 24 24" className={`animate-spin ${className}`} fill="none" aria-hidden>
      <circle cx="12" cy="12" r="9" stroke="currentColor" strokeWidth="2.5" opacity="0.22" />
      <path d="M21 12a9 9 0 0 0-9-9" stroke="currentColor" strokeWidth="2.5" strokeLinecap="round" />
    </svg>
  );
}

export function Modal({
  open,
  onClose,
  title,
  description,
  children,
  wide = false,
  width,
}: {
  open: boolean;
  onClose: () => void;
  title: string;
  description?: string;
  children: ReactNode;
  wide?: boolean;
  labelledBy?: string;
  width?: string;
}) {
  useEffect(() => {
    if (!open) return;
    const onKey = (event: KeyboardEvent) => {
      if (event.key === "Escape") onClose();
    };
    document.addEventListener("keydown", onKey);
    const previous = document.body.style.overflow;
    document.body.style.overflow = "hidden";
    return () => {
      document.removeEventListener("keydown", onKey);
      document.body.style.overflow = previous;
    };
  }, [open, onClose]);

  if (!open) return null;

  return createPortal(
    <div className="fixed inset-0 z-[80] flex items-end justify-center p-0 sm:items-center sm:p-6">
      <button
        type="button"
        aria-label="Fechar"
        onClick={onClose}
        className="absolute inset-0 cursor-default bg-ink/45 backdrop-blur-[2px]"
      />
      <div
        role="dialog"
        aria-modal="true"
        aria-label={title}
        className={`animate-modal-in relative max-h-[92vh] w-full overflow-y-auto border border-ink/20 bg-paper shadow-[0_40px_90px_-30px_rgba(20,16,12,0.6)] ${
          width ?? (wide ? "sm:max-w-[640px]" : "sm:max-w-[520px]")
        }`}
      >
        <div className="flex items-start justify-between gap-4 border-b border-rule px-5 py-4">
          <div>
            <h2 className="text-[16px] font-semibold text-ink">{title}</h2>
            {description && <p className="mt-1 text-[12.5px] leading-snug text-warm">{description}</p>}
          </div>
          <button
            type="button"
            onClick={onClose}
            className="grid h-8 w-8 shrink-0 place-items-center text-warm transition-colors hover:bg-ink/[0.06] hover:text-ink"
            aria-label="Fechar"
          >
            <Icon name="close" className="h-4 w-4" />
          </button>
        </div>
        <div className="p-5">{children}</div>
      </div>
    </div>,
    document.body,
  );
}
