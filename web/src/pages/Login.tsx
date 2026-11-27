import { useState } from "react";
import { Navigate, useNavigate, useSearchParams } from "react-router";
import { useQueryClient } from "@tanstack/react-query";
import { api } from "../api";
import { useMe } from "../session";
import { Field, Loading, useFieldErrors, useTitle } from "../ui";
import logo from "../assets/logo.png";

const errorTexts: Record<string, string> = {
  afbrudt: "Login blev afbrudt. Prøv igen.",
  email: "Din konto gav os ikke en e-mailadresse, så vi kan ikke logge dig ind.",
  oprettelse: "Vi kunne ikke oprette din bruger. Prøv igen.",
};

export function Login() {
  useTitle("Log ind");
  const me = useMe();
  const [params] = useSearchParams();
  const returnUrl = params.get("returnUrl") ?? "/";
  const error = errorTexts[params.get("fejl") ?? ""];
  const [email, setEmail] = useState("");
  const errors = useFieldErrors();
  const qc = useQueryClient();
  const navigate = useNavigate();

  if (me.isPending) return <main className="wrap"><Loading /></main>;
  if (me.data?.signedIn) return <Navigate to={returnUrl} replace />;

  return (
    <main className="wrap">
      <div className="public">
        <header>
          <h1 style={{ margin: 0 }}><img className="login-logo brand-logo" src={logo} alt="Systematikken" /></h1>
          <p className="muted">Venteliste, børn og forældrebetaling for private daginstitutioner. Gratis.</p>
        </header>
        <section className="panel">
          <div className="panel-h"><h2>Log ind</h2></div>
          <div className="panel-b">
            {error && <p className="error">{error}</p>}
            {me.data?.demo && (
              <>
                <p>Dette er en demo med en opdigtet institution. Alt du gør, bliver nulstillet i nat.</p>
                <p>
                  <button
                    className="btn primary big"
                    onClick={async () => {
                      try {
                        await api.post("/auth/demo");
                        await qc.invalidateQueries();
                        navigate("/", { replace: true });
                      } catch (err) {
                        errors.handle(err);
                      }
                    }}
                  >
                    Prøv demoen
                  </button>
                </p>
                {errors.message && <p className="error">{errors.message}</p>}
              </>
            )}
            {me.data?.providers.length === 0 && !me.data.devLogin && !me.data.demo && <p className="error">Login er ikke sat op endnu.</p>}
            {me.data?.providers.map((p) => (
              <p key={p.name}>
                <a className="btn big" href={`/auth/login/${p.name}?returnUrl=${encodeURIComponent(returnUrl)}`}>
                  Log ind med {p.displayName}
                </a>
              </p>
            ))}
            {!me.data?.demo && <p className="hint">Du logger ind med den konto, du allerede har. Vi gemmer ingen adgangskoder.</p>}

            {me.data?.devLogin && (
              <form
                onSubmit={async (e) => {
                  e.preventDefault();
                  try {
                    await api.post("/auth/dev", { email });
                    await qc.invalidateQueries();
                    navigate(returnUrl, { replace: true });
                  } catch (err) {
                    errors.handle(err);
                  }
                }}
              >
                <Field label="Udviklerlogin (kun til test)" name="email" error={errors.errors.email}>
                  <div className="inline">
                    <input id="email" type="email" required value={email} onChange={(e) => setEmail(e.target.value)} placeholder="navn@eksempel.dk" />
                    <button className="btn" type="submit">Log ind</button>
                  </div>
                </Field>
              </form>
            )}
          </div>
        </section>
      </div>
    </main>
  );
}
