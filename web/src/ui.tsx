import { createContext, useCallback, useContext, useEffect, useRef, useState, type ReactNode } from "react";
import { ApiError } from "./api";
import puzzleImg from "./assets/puzzle.png";

// ---- Toast with undo, like Gmail: do it at once, offer "Fortryd" for a few seconds instead of asking first.

type Toast = { id: number; text: string; undo?: () => Promise<unknown> | void };
const ToastContext = createContext<(text: string, undo?: Toast["undo"]) => void>(() => {});
export const useToast = () => useContext(ToastContext);

export function ToastProvider({ children }: { children: ReactNode }) {
  const [toast, setToast] = useState<Toast | null>(null);
  const timer = useRef<number>(undefined);

  const show = useCallback((text: string, undo?: Toast["undo"]) => {
    window.clearTimeout(timer.current);
    setToast({ id: Date.now(), text, undo });
    timer.current = window.setTimeout(() => setToast(null), undo ? 7000 : 4000);
  }, []);

  return (
    <ToastContext.Provider value={show}>
      {children}
      {toast && (
        <div className="toast" role="status" key={toast.id}>
          <span>{toast.text}</span>
          {toast.undo && (
            <button
              onClick={async () => {
                const undo = toast.undo!;
                setToast(null);
                await undo();
              }}
            >
              Fortryd
            </button>
          )}
        </div>
      )}
    </ToastContext.Provider>
  );
}

// ---- Forms

/** Field errors from the API, keyed by field name. */
export function useFieldErrors() {
  const [errors, setErrors] = useState<Record<string, string>>({});
  const [message, setMessage] = useState<string | null>(null);
  const handle = (e: unknown) => {
    if (e instanceof ApiError) {
      setErrors(e.fields);
      setMessage(Object.keys(e.fields).length ? null : e.message);
    } else setMessage("Noget gik galt. Prøv igen.");
  };
  const clear = () => {
    setErrors({});
    setMessage(null);
  };
  return { errors, message, handle, clear };
}

export function Field(props: {
  label: string;
  name: string;
  error?: string;
  hint?: string;
  children: ReactNode;
}) {
  return (
    <div className="field">
      <label htmlFor={props.name}>{props.label}</label>
      {props.children}
      {props.hint && <div className="hint">{props.hint}</div>}
      {props.error && (
        <div className="error" role="alert">
          {props.error}
        </div>
      )}
    </div>
  );
}

/** Reads a form into an object of strings and booleans (checkboxes). */
export function formValues(form: HTMLFormElement) {
  const out: Record<string, string | boolean> = {};
  for (const el of Array.from(form.elements) as HTMLInputElement[]) {
    if (!el.name) continue;
    out[el.name] = el.type === "checkbox" ? el.checked : el.value;
  }
  return out;
}

// ---- Small pieces

export function Panel(props: { title?: ReactNode; aside?: ReactNode; children: ReactNode; id?: string; className?: string }) {
  return (
    <section className={`panel ${props.className ?? ""}`} id={props.id}>
      {(props.title || props.aside) && (
        <div className="panel-h">
          <h2>{props.title}</h2>
          {props.aside}
        </div>
      )}
      {props.children}
    </section>
  );
}

export function Pill({ tone, children }: { tone?: string; children: ReactNode }) {
  return <span className={`pill ${tone ?? ""}`}>{children}</span>;
}

export function Loading() {
  return <p className="muted" aria-busy="true">Henter…</p>;
}

export function Failed({ error }: { error: unknown }) {
  return <p className="error">{error instanceof Error ? error.message : "Noget gik galt."}</p>;
}

export function CopyButton({ text, label = "Kopiér" }: { text: string; label?: string }) {
  const [done, setDone] = useState(false);
  return (
    <button
      type="button"
      className="btn"
      onClick={async () => {
        try {
          await navigator.clipboard.writeText(text);
          setDone(true);
          setTimeout(() => setDone(false), 2000);
        } catch {
          window.prompt("Kopiér teksten:", text);
        }
      }}
    >
      {done ? "Kopieret" : label}
    </button>
  );
}

// ---- Identity A: tiles, avatars and the drawn puzzle piece

const iconPaths: Record<string, string> = {
  alert: "M12 8v5M12 16.5v.5M12 3a9 9 0 1 0 0 18 9 9 0 0 0 0-18z",
  check: "M5 12l4 4 10-10",
  list: "M4 7h16M4 12h16M4 17h10",
  clock: "M12 7v5l3 2M12 3a9 9 0 1 0 0 18 9 9 0 0 0 0-18z",
  person: "M12 11a4 4 0 1 0 0-8 4 4 0 0 0 0 8zM4 21c0-4 3.6-7 8-7s8 3 8 7",
  upload: "M12 16V4M7 9l5-5 5 5M4 20h16",
  coin: "M12 3a9 9 0 1 0 0 18 9 9 0 0 0 0-18zM9 9.5c0-1 1.3-1.8 3-1.8s3 .8 3 1.8-1.3 1.6-3 1.9-3 .9-3 1.9 1.3 1.9 3 1.9 3-.8 3-1.9M12 6v1.7M12 16.3V18",
  puzzle: "M4 8h4a2 2 0 1 1 4 0h4v4a2 2 0 1 1 0 4v4H4z",
};

export function Tile({ icon, tone }: { icon: keyof typeof iconPaths; tone: "pink" | "sage" | "butter" }) {
  return (
    <span className={`tile ${tone}`} aria-hidden="true">
      <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth={2} strokeLinecap="round" strokeLinejoin="round">
        <path d={iconPaths[icon]} />
      </svg>
    </span>
  );
}

/** Initials in a coloured circle, like the phone's contacts. The colour follows the name, so it never jumps. */
export function Avatar({ name }: { name: string }) {
  const initials = name.split(" ").filter(Boolean).slice(0, 2).map((w) => w[0]!.toUpperCase()).join("");
  const hue = [...name].reduce((a, ch) => a + ch.charCodeAt(0), 0) % 3;
  return <span className={`avatar c${hue}`} aria-hidden="true">{initials}</span>;
}

/** The hand-drawn puzzle from systematikken.dk. For empty screens and finished months. */
export function Puzzle({ size = 96 }: { size?: number }) {
  return <img className="puzzle" src={puzzleImg} width={Math.round(size * 1.4)} alt="" />;
}

export function Empty({ children }: { children: ReactNode }) {
  return (
    <div className="empty">
      <Puzzle size={84} />
      <div>{children}</div>
    </div>
  );
}

export function useTitle(title: string) {
  useEffect(() => {
    document.title = `${title} – Systematikken`;
  }, [title]);
}
