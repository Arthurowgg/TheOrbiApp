import {
  useEffect,
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
export function Mark({ className = "h-5 w-5" }: { className?: string }) {
  return (
    <svg viewBox="0 0 24 24" className={className} fill="currentColor" aria-hidden>
      <path d="M12 1.5c.62 5.9 4.6 9.88 10.5 10.5-5.9.62-9.88 4.6-10.5 10.5C11.38 16.6 7.4 12.62 1.5 12 7.4 11.38 11.38 7.4 12 1.5Z" />
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
        <Mark className="h-4 w-4" />
      </span>
      <span className="flex items-baseline gap-2.5">
        <span
          className={`font-cond text-[23px] font-bold uppercase leading-none tracking-[0.01em] ${
            tone === "paper" ? "text-bone" : "text-ink"
          }`}
        >
          Nova
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
    <button type="button" onClick={onClick} className="shrink-0" aria-label="NOVA — início">
      {content}
    </button>
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
  | "square";

const ICON_PATHS: Record<IconName, ReactNode> = {
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
      <path d="M18 9a6 6 0 1 0-12 0c0 7-3 7-3 9h18c0-2-3-2-3-9Z" />
      <path d="M10 21h4" />
    </>
  ),
  bolt: <path d="m13 2-8 12h6l-1 8 8-12h-6l1-8Z" />,
  monitor: (
    <>
      <rect x="3" y="4" width="18" height="12" rx="1" />
      <path d="M8 20h8M12 16v4" />
    </>
  ),
  lock: (
    <>
      <rect x="4" y="10" width="16" height="11" rx="1.5" />
      <path d="M8 10V7a4 4 0 0 1 8 0v3M12 14v3" />
    </>
  ),
  "arrow-right": <path d="M4 12h16m0 0-6-6m6 6-6 6" />,
  "arrow-down": <path d="M12 3v18m0 0 6-6m-6 6-6-6" />,
  "arrow-up-right": <path d="M7 17 17 7M8 7h9v9" />,
  download: (
    <>
      <path d="M12 3v12m0 0 5-5m-5 5-5-5" />
      <path d="M4 17v3h16v-3" />
    </>
  ),
  check: <path d="m4 12 5 5L20 6" />,
  "chevron-down": <path d="m6 9 6 6 6-6" />,
  "chevron-right": <path d="m9 6 6 6-6 6" />,
  menu: <path d="M3 7h18M3 12h18M3 17h18" />,
  close: <path d="m5 5 14 14M19 5 5 19" />,
  eye: (
    <>
      <path d="M2.5 12S6 5.5 12 5.5 21.5 12 21.5 12 18 18.5 12 18.5 2.5 12 2.5 12Z" />
      <circle cx="12" cy="12" r="3" />
    </>
  ),
  "eye-off": (
    <>
      <path d="M4 4l16 16" />
      <path d="M9.9 5.9A9.7 9.7 0 0 1 12 5.5c6 0 9.5 6.5 9.5 6.5a16 16 0 0 1-3 3.7M6.4 8A16.6 16.6 0 0 0 2.5 12S6 18.5 12 18.5c1 0 1.9-.2 2.8-.5" />
    </>
  ),
  user: (
    <>
      <circle cx="12" cy="8" r="4" />
      <path d="M4 21c0-4 3.6-6.5 8-6.5s8 2.5 8 6.5" />
    </>
  ),
  logout: <path d="M9 4H4v16h5M16 8l4 4-4 4M20 12H9" />,
  windows: (
    <path d="M4 5.5 11 4.5v7H4v-6ZM13 4.2 20 3v8.5h-7V4.2ZM4 12.5h7v7L4 18.5v-6ZM13 12.5h7V21l-7-1.2v-7.3Z" />
  ),
  shield: (
    <>
      <path d="M12 3 4 6v6c0 5 3.5 7.8 8 9 4.5-1.2 8-4 8-9V6l-8-3Z" />
      <path d="m8.5 12 2.5 2.5 4.5-5" />
    </>
  ),
  play: <path d="M8 5.5 19 12 8 18.5v-13Z" />,
  laptop: (
    <>
      <rect x="4" y="5" width="16" height="11" rx="1" />
      <path d="M2 19h20" />
    </>
  ),
  key: (
    <>
      <circle cx="8" cy="15" r="4" />
      <path d="m11 12 9-9M16 7l3 3M14 9l2 2" />
    </>
  ),
  copy: (
    <>
      <rect x="9" y="9" width="11" height="11" rx="1" />
      <path d="M5 15V5a1 1 0 0 1 1-1h10" />
    </>
  ),
  trash: <path d="M4 7h16M10 11v6M14 11v6M6 7l1 13h10l1-13M9 7V4h6v3" />,
  link: (
    <path d="M10 14a4 4 0 0 0 5.7 0l3-3a4 4 0 0 0-5.7-5.7l-1 1M14 10a4 4 0 0 0-5.7 0l-3 3a4 4 0 0 0 5.7 5.7l1-1" />
  ),
  grid: (
    <>
      <rect x="3" y="3" width="8" height="10" rx="1" />
      <rect x="13" y="3" width="8" height="6" rx="1" />
      <rect x="13" y="11" width="8" height="10" rx="1" />
      <rect x="3" y="15" width="8" height="6" rx="1" />
    </>
  ),
  history: <path d="M3 12a9 9 0 1 0 3-6.7L3 8M3 3v5h5M12 7v5l3 2" />,
  edit: <path d="M4 20h4L19 9l-4-4L4 16v4ZM13 7l4 4" />,
  alert: <path d="M12 3 2 20h20L12 3ZM12 10v5M12 18h.01" />,
  info: (
    <>
      <circle cx="12" cy="12" r="9" />
      <path d="M12 11v6M12 7.5h.01" />
    </>
  ),
  home: <path d="M3 11 12 3l9 8M5 10v10h14V10" />,
  minus: <path d="M5 12h14" />,
  square: <rect x="6" y="6" width="12" height="12" />,
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
type Variant = "primary" | "secondary" | "ghost" | "inverse" | "outline" | "danger";
type Size = "sm" | "md" | "lg";

const BTN_BASE =
  "inline-flex select-none items-center justify-center gap-2.5 rounded-[2px] font-mono font-medium uppercase tracking-[0.13em] transition-colors duration-200 disabled:pointer-events-none disabled:opacity-45";

const BTN_SIZES: Record<Size, string> = {
  sm: "h-[34px] px-3.5 text-[10px]",
  md: "h-[42px] px-5 text-[10.5px]",
  lg: "h-[50px] px-6 text-[11px]",
};

const BTN_VARIANTS: Record<Variant, string> = {
  primary: "bg-signal text-white hover:bg-signal-dark",
  secondary: "border border-ink/30 text-ink hover:border-ink hover:bg-ink hover:text-paper",
  ghost: "text-ink hover:bg-ink/[0.06]",
  inverse: "bg-paper text-ink hover:bg-white",
  outline: "border border-paper/30 text-paper hover:border-paper hover:bg-paper hover:text-ink",
  danger: "border border-signal/50 text-signal hover:bg-signal hover:text-white",
};

export function Button({
  variant = "primary",
  size = "md",
  className = "",
  type = "button",
  children,
  ...props
}: ButtonHTMLAttributes<HTMLButtonElement> & { variant?: Variant; size?: Size }) {
  return (
    <button
      type={type}
      className={`${BTN_BASE} ${BTN_SIZES[size]} ${BTN_VARIANTS[variant]} ${className}`}
      {...props}
    >
      {children}
    </button>
  );
}

/* ------------------------------------------------------------------ */
/* Editorial                                                          */
/* ------------------------------------------------------------------ */
export function Eyebrow({
  children,
  tone = "ink",
}: {
  children: ReactNode;
  tone?: "ink" | "paper";
}) {
  return (
    <span className={`rule-label ${tone === "paper" ? "!text-bone/65" : ""}`}>{children}</span>
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
  const ref = useRef<HTMLDivElement>(null);
  const [visible, setVisible] = useState(false);

  useEffect(() => {
    const el = ref.current;
    if (!el) return;
    if (window.matchMedia("(prefers-reduced-motion: reduce)").matches) {
      setVisible(true);
      return;
    }
    const observer = new IntersectionObserver(
      (entries) => {
        if (entries.some((entry) => entry.isIntersecting)) {
          setVisible(true);
          observer.disconnect();
        }
      },
      { rootMargin: "0px 0px -8% 0px", threshold: 0.06 },
    );
    observer.observe(el);
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
    <span className="inline-flex min-w-[26px] items-center justify-center rounded-[3px] border border-paper/25 bg-paper/10 px-1.5 py-1 font-mono text-[10px] uppercase tracking-[0.08em] text-bone">
      {children}
    </span>
  );
}

/* ------------------------------------------------------------------ */
/* Formulários                                                        */
/* ------------------------------------------------------------------ */
type TextFieldProps = InputHTMLAttributes<HTMLInputElement> & {
  id: string;
  label: string;
  error?: string;
  hint?: string;
  trailing?: ReactNode;
  wrapperClassName?: string;
};

export function TextField({
  id,
  label,
  error,
  hint,
  trailing,
  wrapperClassName = "",
  className = "",
  ...props
}: TextFieldProps) {
  return (
    <div className={wrapperClassName}>
      <div className="mb-1.5 flex items-baseline justify-between gap-3">
        <label htmlFor={id} className="label text-ink">
          {label}
        </label>
        {hint && <span className="font-mono text-[10px] text-warm">{hint}</span>}
      </div>
      <div className="relative">
        <input
          id={id}
          aria-invalid={Boolean(error)}
          aria-describedby={error ? `${id}-error` : undefined}
          className={`h-11 w-full rounded-[2px] border bg-[#fbf9f3] px-3.5 text-[14px] text-ink outline-none transition-colors placeholder:text-warm/50 focus:border-ink disabled:opacity-60 ${
            trailing ? "pr-11" : ""
          } ${error ? "border-signal" : "border-rule"} ${className}`}
          {...props}
        />
        {trailing && <div className="absolute inset-y-0 right-0 flex items-center">{trailing}</div>}
      </div>
      {error && (
        <p id={`${id}-error`} role="alert" className="mt-1.5 text-[11.5px] leading-snug text-signal">
          {error}
        </p>
      )}
    </div>
  );
}

export function PasswordField({
  showStrength = false,
  value,
  ...props
}: Omit<TextFieldProps, "type" | "trailing"> & { showStrength?: boolean }) {
  const [visible, setVisible] = useState(false);
  const [caps, setCaps] = useState(false);
  const text = typeof value === "string" ? value : "";

  return (
    <div className={props.wrapperClassName}>
      <TextField
        {...props}
        wrapperClassName=""
        value={value}
        type={visible ? "text" : "password"}
        onKeyUp={(event) => setCaps(event.getModifierState?.("CapsLock") ?? false)}
        onBlur={() => setCaps(false)}
        trailing={
          <button
            type="button"
            onClick={() => setVisible((v) => !v)}
            className="grid h-11 w-11 place-items-center text-warm transition-colors hover:text-ink"
            aria-label={visible ? "Ocultar senha" : "Mostrar senha"}
          >
            <Icon name={visible ? "eye-off" : "eye"} className="h-4 w-4" />
          </button>
        }
      />
      {caps && (
        <p className="mt-1.5 flex items-center gap-1.5 font-mono text-[10.5px] text-signal">
          <Icon name="alert" className="h-3 w-3" /> Caps Lock está ligado
        </p>
      )}
      {showStrength && <StrengthMeter password={text} />}
    </div>
  );
}

export function StrengthMeter({ password }: { password: string }) {
  const { score, label } = passwordStrength(password);
  if (!password) {
    return (
      <p className="mt-2 font-mono text-[10.5px] text-warm">Use pelo menos {MIN_PASSWORD} caracteres.</p>
    );
  }
  return (
    <div className="mt-2.5" aria-live="polite">
      <div className="flex gap-1">
        {[1, 2, 3, 4].map((step) => (
          <span
            key={step}
            className={`h-[3px] flex-1 transition-colors ${
              score >= step ? (score <= 1 ? "bg-signal" : "bg-ink") : "bg-rule"
            }`}
          />
        ))}
      </div>
      <p className="mt-1.5 font-mono text-[10.5px] text-warm">Senha: {label.toLowerCase()}</p>
    </div>
  );
}

export function Switch({
  checked,
  onChange,
  label,
  description,
  disabled,
  tone = "light",
}: {
  checked: boolean;
  onChange: (next: boolean) => void;
  label: string;
  description?: string;
  disabled?: boolean;
  tone?: "light" | "dark";
}) {
  return (
    <button
      type="button"
      role="switch"
      aria-checked={checked}
      disabled={disabled}
      onClick={() => onChange(!checked)}
      className="flex w-full items-center justify-between gap-4 text-left disabled:opacity-50"
    >
      <span className="min-w-0">
        <span className={`block text-[13.5px] font-medium ${tone === "dark" ? "text-bone" : "text-ink"}`}>
          {label}
        </span>
        {description && (
          <span className={`mt-0.5 block text-[12px] leading-snug ${tone === "dark" ? "text-bone/50" : "text-warm"}`}>
            {description}
          </span>
        )}
      </span>
      <span
        className={`relative h-[22px] w-[40px] shrink-0 rounded-full transition-colors ${
          checked ? "bg-signal" : tone === "dark" ? "bg-white/20" : "bg-rule"
        }`}
      >
        <span
          className={`absolute top-[3px] h-4 w-4 rounded-full bg-white shadow transition-[left] ${
            checked ? "left-[21px]" : "left-[3px]"
          }`}
        />
      </span>
    </button>
  );
}

/* ------------------------------------------------------------------ */
/* Painéis, selos e utilitários da área de conta                      */
/* ------------------------------------------------------------------ */
export function Panel({
  title,
  description,
  action,
  children,
  className = "",
  padded = true,
}: {
  title?: string;
  description?: string;
  action?: ReactNode;
  children: ReactNode;
  className?: string;
  padded?: boolean;
}) {
  return (
    <section className={`border border-rule bg-[#fbf9f3] ${className}`}>
      {title && (
        <header className="flex items-start justify-between gap-4 border-b border-rule px-5 py-3.5">
          <div className="min-w-0">
            <h2 className="text-[14px] font-semibold text-ink">{title}</h2>
            {description && <p className="mt-0.5 text-[12.5px] leading-[1.5] text-warm">{description}</p>}
          </div>
          {action}
        </header>
      )}
      <div className={padded ? "p-5" : ""}>{children}</div>
    </section>
  );
}

export function Badge({
  tone = "plain",
  children,
}: {
  tone?: "plain" | "ok" | "signal" | "alert";
  children: ReactNode;
}) {
  const tones = {
    plain: "border border-rule text-warm",
    ok: "bg-ink text-paper",
    signal: "bg-signal text-white",
    alert: "bg-signal-soft text-signal-dark",
  } as const;
  return (
    <span className={`label inline-flex items-center gap-1.5 rounded-full px-2.5 py-1 ${tones[tone]}`}>
      {children}
    </span>
  );
}

export function CopyButton({ value, label = "Copiar" }: { value: string; label?: string }) {
  const [copied, setCopied] = useState(false);
  const timer = useRef<number | null>(null);

  useEffect(
    () => () => {
      if (timer.current) window.clearTimeout(timer.current);
    },
    [],
  );

  const copy = async () => {
    try {
      await navigator.clipboard.writeText(value);
      setCopied(true);
      if (timer.current) window.clearTimeout(timer.current);
      timer.current = window.setTimeout(() => setCopied(false), 1600);
    } catch {
      setCopied(false);
    }
  };

  return (
    <button
      type="button"
      onClick={copy}
      className="label inline-flex items-center gap-1.5 border border-rule px-2.5 py-1.5 text-ink transition-colors hover:border-ink"
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
    sm: "h-7 w-7 text-[10px]",
    md: "h-9 w-9 text-[11px]",
    lg: "h-12 w-12 text-[14px]",
    xl: "h-20 w-20 text-[24px]",
  } as const;
  return (
    <span
      className={`grid shrink-0 place-items-center rounded-full font-mono font-medium uppercase text-white ${sizes[size]}`}
      style={{ backgroundColor: color }}
      aria-hidden
    >
      {initials || "N"}
    </span>
  );
}

export function Spinner({ className = "h-4 w-4" }: { className?: string }) {
  return (
    <span
      className={`inline-block animate-spin rounded-full border-[1.5px] border-current border-r-transparent ${className}`}
      aria-hidden
    />
  );
}

/* ------------------------------------------------------------------ */
/* Modal                                                              */
/* ------------------------------------------------------------------ */
export function Modal({
  open,
  onClose,
  children,
  labelledBy,
  width = "max-w-[560px]",
  hideClose = false,
}: {
  open: boolean;
  onClose: () => void;
  children: ReactNode;
  labelledBy?: string;
  width?: string;
  hideClose?: boolean;
}) {
  useEffect(() => {
    if (!open) return;
    const onKey = (event: KeyboardEvent) => {
      if (event.key === "Escape") onClose();
    };
    const previous = document.body.style.overflow;
    document.body.style.overflow = "hidden";
    document.addEventListener("keydown", onKey);
    return () => {
      document.body.style.overflow = previous;
      document.removeEventListener("keydown", onKey);
    };
  }, [open, onClose]);

  if (!open) return null;

  return createPortal(
    <div
      className="fixed inset-0 z-[80] flex items-end justify-center overflow-y-auto bg-ink/60 p-3 backdrop-blur-[3px] sm:items-center sm:p-6"
      onMouseDown={(event) => {
        if (event.target === event.currentTarget) onClose();
      }}
    >
      <div
        role="dialog"
        aria-modal="true"
        aria-labelledby={labelledBy}
        className={`animate-modal-in relative w-full ${width} border border-ink/15 bg-paper shadow-[0_32px_80px_-24px_rgba(23,18,14,0.55)]`}
      >
        {!hideClose && (
          <button
            type="button"
            onClick={onClose}
            className="absolute right-3 top-3 z-10 grid h-9 w-9 place-items-center text-ink-2 transition-colors hover:bg-ink/[0.06] hover:text-ink"
            aria-label="Fechar"
          >
            <Icon name="close" className="h-4 w-4" />
          </button>
        )}
        {children}
      </div>
    </div>,
    document.body,
  );
}
