import { useState, type ReactNode } from "react";
import { useLocation, useNavigate, useParams } from "react-router";
import { useQuery } from "@tanstack/react-query";
import { api } from "../../api";
import { fmtDate } from "../../format";
import { Failed, Field, Loading, formValues, useFieldErrors, useTitle } from "../../ui";

type Institution = { name: string; contactEmail: string; waitingListRetentionMonths: number };

const steps = ["Barnet", "Forældre", "Send"] as const;
const stepOf: Record<string, number> = {
  childName: 0, dateOfBirth: 0, desiredStart: 0, note: 0,
  parent1Name: 1, parent1Email: 1, parent1Phone: 1, parent2Email: 1, address: 1, postCode: 1, city: 1,
  consent: 2,
};

/** The public sign-up form, like ordering something online: three steps with a progress bar. */
export function Signup() {
  const { slug } = useParams();
  const inst = useQuery({ queryKey: ["public", slug], queryFn: () => api.get<Institution>(`/api/public/institutions/${slug}`) });
  const [step, setStep] = useState(0);
  const [busy, setBusy] = useState(false);
  const errors = useFieldErrors();
  const navigate = useNavigate();
  useTitle(inst.data ? `Skriv dit barn op – ${inst.data.name}` : "Skriv dit barn op");

  if (inst.isPending) return <PublicShell><Loading /></PublicShell>;
  if (inst.error) return <PublicShell><Failed error={new Error("Siden findes ikke. Tjek linket.")} /></PublicShell>;
  const i = inst.data;
  const e = errors.errors;

  /** Checks only the fields on the current step. The form has noValidate, because hidden steps cannot show a browser message. */
  const stepIsValid = (form: HTMLFormElement) => {
    const fields = Array.from(form.querySelectorAll<HTMLInputElement>(`[data-step="${step}"] input, [data-step="${step}"] textarea`));
    const bad = fields.find((f) => !f.checkValidity());
    bad?.reportValidity();
    return !bad;
  };

  return (
    <PublicShell>
      <header>
        <h1>Skriv dit barn op</h1>
        <p className="muted">{i.name}</p>
      </header>
      <form
        noValidate
        onSubmit={async (ev) => {
          ev.preventDefault();
          if (!stepIsValid(ev.currentTarget)) return;
          if (step < 2) { setStep(step + 1); return; }
          errors.clear();
          setBusy(true);
          const v = formValues(ev.currentTarget);
          try {
            const r = await api.post<{ receipt: string }>(`/api/public/institutions/${slug}/signup`, {
              ...v, dateOfBirth: v.dateOfBirth || null, desiredStart: v.desiredStart || null,
            });
            navigate(`/tilmeld/${slug}/tak`, { state: { receipt: r.receipt, name: i.name, email: i.contactEmail } });
          } catch (err) {
            errors.handle(err);
            const first = Object.keys((err as { fields?: Record<string, string> }).fields ?? {})[0];
            if (first && stepOf[first] !== undefined) setStep(stepOf[first]);
          } finally {
            setBusy(false);
          }
        }}
      >
        <div className="progress" aria-hidden="true">{steps.map((s, n) => <span key={s} className={n <= step ? "on" : ""} />)}</div>
        <p className="hint">Trin {step + 1} af 3: {steps[step]}</p>
        <input name="website" className="hp" tabIndex={-1} autoComplete="off" aria-hidden="true" />

        <fieldset className="panel panel-b" style={{ paddingTop: "1rem", border: 0 }} data-step="0" hidden={step !== 0}>
          <Field label="Barnets fulde navn" name="childName" error={e.childName}><input id="childName" name="childName" required /></Field>
          <Field label="Fødselsdato (eller termin)" name="dateOfBirth" error={e.dateOfBirth}><input id="dateOfBirth" name="dateOfBirth" type="date" required /></Field>
          <Field label="Hvornår ønsker I start?" name="desiredStart" error={e.desiredStart}><input id="desiredStart" name="desiredStart" type="date" required /></Field>
          <label className="check field"><input type="checkbox" name="hasSibling" /> Barnet har en bror eller søster, der går her nu</label>
          <Field label="Noget vi skal vide? (valgfrit)" name="note" error={e.note}><textarea id="note" name="note" maxLength={1000} /></Field>
        </fieldset>

        <fieldset className="panel panel-b" style={{ paddingTop: "1rem", border: 0 }} data-step="1" hidden={step !== 1}>
          <Field label="Dit navn" name="parent1Name" error={e.parent1Name}><input id="parent1Name" name="parent1Name" required autoComplete="name" /></Field>
          <Field label="Din e-mail" name="parent1Email" error={e.parent1Email}><input id="parent1Email" name="parent1Email" type="email" required autoComplete="email" /></Field>
          <Field label="Dit telefonnummer" name="parent1Phone" error={e.parent1Phone}><input id="parent1Phone" name="parent1Phone" type="tel" required autoComplete="tel" /></Field>
          <Field label="Adresse" name="address" error={e.address}><input id="address" name="address" required autoComplete="street-address" /></Field>
          <div className="grid">
            <Field label="Postnummer" name="postCode" error={e.postCode}><input id="postCode" name="postCode" required inputMode="numeric" pattern="\d{4}" autoComplete="postal-code" /></Field>
            <Field label="By" name="city" error={e.city}><input id="city" name="city" required autoComplete="address-level2" /></Field>
          </div>
          <p className="hint">Den anden forælder (valgfrit)</p>
          <Field label="Navn" name="parent2Name"><input id="parent2Name" name="parent2Name" /></Field>
          <Field label="E-mail" name="parent2Email" error={e.parent2Email}><input id="parent2Email" name="parent2Email" type="email" /></Field>
          <Field label="Telefon" name="parent2Phone"><input id="parent2Phone" name="parent2Phone" type="tel" /></Field>
        </fieldset>

        <fieldset className="panel panel-b" style={{ paddingTop: "1rem", border: 0 }} data-step="2" hidden={step !== 2}>
          <p>Når du sender, kommer dit barn på ventelisten hos {i.name}. I hører fra os, når der er en plads.</p>
          <p className="hint">
            Vi gemmer oplysningerne, så længe barnet står på listen. Siger I nej eller bliver fjernet fra listen, sletter vi dem efter {i.waitingListRetentionMonths} måneder.
            {i.contactEmail && ` Skriv til ${i.contactEmail}, hvis I vil have dem slettet før.`}
          </p>
          <label className="check field"><input type="checkbox" name="consent" required /> Jeg giver lov til, at {i.name} gemmer oplysningerne til ventelisten.</label>
          {e.consent && <p className="error">{e.consent}</p>}
        </fieldset>

        {errors.message && <p className="error">{errors.message}</p>}
        <div className="inline">
          {step > 0 && <button className="btn" type="button" onClick={() => setStep(step - 1)}>Tilbage</button>}
          <button className="btn primary" type="submit" disabled={busy}>{step < 2 ? "Næste" : busy ? "Sender…" : "Skriv op"}</button>
        </div>
      </form>
    </PublicShell>
  );
}

export function Thanks() {
  useTitle("Tak");
  const state = useLocation().state as { receipt?: string; name?: string; email?: string } | null;
  return (
    <PublicShell>
      <header><h1>Tak, vi har fået det</h1></header>
      <section className="panel done-hero">
        <p>Dit barn står nu på ventelisten{state?.name && <> hos <b>{state.name}</b></>}.</p>
        <p>I hører fra os, når der er en plads.{state?.email && ` Har I spørgsmål, så skriv til ${state.email}.`}</p>
        {state?.receipt && <p className="hint">Kvitteringsnummer {state.receipt} · {fmtDate(new Date().toISOString())}</p>}
      </section>
      <p className="hint">Gem gerne denne side som kvittering.</p>
    </PublicShell>
  );
}

export function PublicShell({ children }: { children: ReactNode }) {
  return (
    <main className="wrap">
      <div className="public">
        {children}
        <p className="hint" style={{ marginTop: "2rem" }}>Drevet med Systematikken.</p>
      </div>
    </main>
  );
}
