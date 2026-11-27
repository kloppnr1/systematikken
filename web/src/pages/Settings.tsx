import { useState } from "react";
import { useQuery, useQueryClient } from "@tanstack/react-query";
import { api } from "../api";
import { fmtDate, monthName, parseAmount } from "../format";
import { CopyButton, Failed, Field, Loading, Panel, formValues, useFieldErrors, useTitle, useToast } from "../ui";

type Settings = {
  name: string; contactEmail: string; contactPhone: string;
  nurseryPrice: number; kindergartenPrice: number; nurseryPartTimePrice: number | null; kindergartenPartTimePrice: number | null;
  monthWithoutCharge: number | null; halfMonthDay: number; deposit: number | null; depositMonthsAhead: number; signupFee: number | null; mealScheme: number | null;
  nurseryCapacity: number; kindergartenCapacity: number;
  cvr: string; pbsNumber: string; debtorGroup: string; betalingsserviceText: string;
  firstBillingMonth: string; waitingListRetentionMonths: number;
};
type Data = { settings: Settings; municipality: { name: string; cutoverYears: number; cutoverMonths: number; schoolStartMonth: number }; slug: string; billingStarted: boolean };
type Staff = { members: { id: number; email: string; displayName: string; me: boolean }[]; invitations: { id: number; email: string; expiresAt: string; url: string }[] };

const money = (v: number | null) => (v == null ? "" : v.toLocaleString("da-DK", { minimumFractionDigits: 2, maximumFractionDigits: 2 }));
const amount = (v: unknown) => {
  const n = parseAmount(String(v ?? ""));
  return Number.isNaN(n) ? null : n;
};

export function Settings() {
  useTitle("Indstillinger");
  const q = useQuery({ queryKey: ["settings"], queryFn: () => api.get<Data>("/api/settings") });
  const errors = useFieldErrors();
  const qc = useQueryClient();
  const toast = useToast();

  if (q.isPending) return <Loading />;
  if (q.error) return <Failed error={q.error} />;
  const { settings: s, municipality: m } = q.data;
  const e = errors.errors;

  return (
    <div className="narrow">
      <h1>Indstillinger</h1>
      <form
        onSubmit={async (ev) => {
          ev.preventDefault();
          errors.clear();
          const v = formValues(ev.currentTarget);
          try {
            await api.put("/api/settings", {
              name: v.name, contactEmail: v.contactEmail, contactPhone: v.contactPhone,
              nurseryPrice: amount(v.nurseryPrice) ?? 0, kindergartenPrice: amount(v.kindergartenPrice) ?? 0,
              nurseryPartTimePrice: amount(v.nurseryPartTimePrice), kindergartenPartTimePrice: amount(v.kindergartenPartTimePrice),
              monthWithoutCharge: v.monthWithoutCharge ? Number(v.monthWithoutCharge) : null,
              halfMonthDay: Number(v.halfMonthDay), deposit: amount(v.deposit), depositMonthsAhead: Number(v.depositMonthsAhead || 0),
              signupFee: amount(v.signupFee), mealScheme: amount(v.mealScheme),
              nurseryCapacity: Number(v.nurseryCapacity || 0), kindergartenCapacity: Number(v.kindergartenCapacity || 0),
              cvr: String(v.cvr).trim(), pbsNumber: String(v.pbsNumber).trim(), debtorGroup: String(v.debtorGroup).trim() || "00001",
              betalingsserviceText: v.betalingsserviceText, firstBillingMonth: v.firstBillingMonth, waitingListRetentionMonths: Number(v.waitingListRetentionMonths),
            });
            await qc.invalidateQueries();
            toast("Indstillingerne er gemt");
          } catch (err) {
            errors.handle(err);
            toast("Tjek de markerede felter");
          }
        }}
      >
        <Panel title="Institutionen">
          <div className="panel-b grid">
            <Field label="Navn" name="name" error={e.name}><input id="name" name="name" defaultValue={s.name} required /></Field>
            <Field label="E-mail forældre kan skrive til" name="contactEmail"><input id="contactEmail" name="contactEmail" type="email" defaultValue={s.contactEmail} /></Field>
            <Field label="Telefon" name="contactPhone"><input id="contactPhone" name="contactPhone" type="tel" defaultValue={s.contactPhone} /></Field>
          </div>
          <p className="panel-b hint">
            {m.name} Kommune: børnehavepris fra {m.cutoverYears} år og {m.cutoverMonths} mdr., skolestart i {monthName(m.schoolStartMonth)}. Kommunens regler gælder alle institutioner i kommunen.
          </p>
        </Panel>

        <Panel title="Priser pr. måned" id="priser">
          <div className="panel-b grid">
            <Field label="Vuggestue" name="nurseryPrice" error={e.nurseryPrice}><input id="nurseryPrice" name="nurseryPrice" inputMode="decimal" defaultValue={money(s.nurseryPrice)} /></Field>
            <Field label="Børnehave" name="kindergartenPrice" error={e.kindergartenPrice}><input id="kindergartenPrice" name="kindergartenPrice" inputMode="decimal" defaultValue={money(s.kindergartenPrice)} /></Field>
            <Field label="Vuggestue, deltid (valgfri)" name="nurseryPartTimePrice"><input id="nurseryPartTimePrice" name="nurseryPartTimePrice" inputMode="decimal" defaultValue={money(s.nurseryPartTimePrice)} /></Field>
            <Field label="Børnehave, deltid (valgfri)" name="kindergartenPartTimePrice"><input id="kindergartenPartTimePrice" name="kindergartenPartTimePrice" inputMode="decimal" defaultValue={money(s.kindergartenPartTimePrice)} /></Field>
            <Field label="Kostordning (valgfri)" name="mealScheme" hint="Fuldt beløb hver måned, også i en halv måned."><input id="mealScheme" name="mealScheme" inputMode="decimal" defaultValue={money(s.mealScheme)} /></Field>
            <Field label="Betalingsfri måned" name="monthWithoutCharge" error={e.monthWithoutCharge}>
              <select id="monthWithoutCharge" name="monthWithoutCharge" defaultValue={s.monthWithoutCharge ?? ""}>
                <option value="">Ingen</option>
                {Array.from({ length: 12 }, (_, i) => <option key={i} value={i + 1}>{monthName(i + 1)}</option>)}
              </select>
            </Field>
            <Field label="Halv måned fra dag" name="halfMonthDay" error={e.halfMonthDay} hint="Starter barnet denne dag eller senere, eller stopper det før, betales halv pris.">
              <input id="halfMonthDay" name="halfMonthDay" type="number" min={2} max={28} defaultValue={s.halfMonthDay} />
            </Field>
          </div>
        </Panel>

        <Panel title="Engangsbeløb">
          <div className="panel-b grid">
            <Field label="Depositum (valgfri)" name="deposit"><input id="deposit" name="deposit" inputMode="decimal" defaultValue={money(s.deposit)} /></Field>
            <Field label="Opkræv depositum måneder før start" name="depositMonthsAhead" error={e.depositMonthsAhead}><input id="depositMonthsAhead" name="depositMonthsAhead" type="number" min={0} max={12} defaultValue={s.depositMonthsAhead} /></Field>
            <Field label="Indmeldelsesgebyr (valgfri)" name="signupFee"><input id="signupFee" name="signupFee" inputMode="decimal" defaultValue={money(s.signupFee)} /></Field>
          </div>
        </Panel>

        <Panel title="Betalingsservice" id="betalingsservice">
          <div className="panel-b grid">
            <Field label="CVR-nummer" name="cvr" error={e.cvr}><input id="cvr" name="cvr" inputMode="numeric" maxLength={8} defaultValue={s.cvr} /></Field>
            <Field label="PBS-nummer" name="pbsNumber" error={e.pbsNumber}><input id="pbsNumber" name="pbsNumber" inputMode="numeric" maxLength={8} defaultValue={s.pbsNumber} /></Field>
            <Field label="Debitorgruppe" name="debtorGroup" error={e.debtorGroup}><input id="debtorGroup" name="debtorGroup" inputMode="numeric" maxLength={5} defaultValue={s.debtorGroup} /></Field>
            <Field label="Overskrift på forældrenes oversigt" name="betalingsserviceText" error={e.betalingsserviceText}><input id="betalingsserviceText" name="betalingsserviceText" maxLength={60} defaultValue={s.betalingsserviceText} /></Field>
            <Field label="Første måned der opkræves" name="firstBillingMonth" error={e.firstBillingMonth} hint={q.data.billingStarted ? "Låst, fordi opkrævningen er i gang." : undefined}>
              <input id="firstBillingMonth" name="firstBillingMonth" type="month" defaultValue={s.firstBillingMonth} disabled={q.data.billingStarted} />
            </Field>
          </div>
        </Panel>

        <Panel title="Pladser" id="pladser">
          <div className="panel-b grid">
            <Field label="Vuggestuepladser" name="nurseryCapacity" error={e.nurseryCapacity}><input id="nurseryCapacity" name="nurseryCapacity" type="number" min={0} defaultValue={s.nurseryCapacity} /></Field>
            <Field label="Børnehavepladser" name="kindergartenCapacity"><input id="kindergartenCapacity" name="kindergartenCapacity" type="number" min={0} defaultValue={s.kindergartenCapacity} /></Field>
          </div>
        </Panel>

        <Panel title="Venteliste">
          <div className="panel-b grid">
            <Field label="Slet afsluttede efter (måneder)" name="waitingListRetentionMonths" error={e.waitingListRetentionMonths} hint="Gælder dem, der har sagt nej eller er fjernet.">
              <input id="waitingListRetentionMonths" name="waitingListRetentionMonths" type="number" min={1} max={60} defaultValue={s.waitingListRetentionMonths} />
            </Field>
          </div>
        </Panel>

        {errors.message && <p className="error">{errors.message}</p>}
        <button className="btn primary big" type="submit">Gem indstillinger</button>
      </form>

      <StaffPanel />
    </div>
  );
}

function StaffPanel() {
  const q = useQuery({ queryKey: ["staff"], queryFn: () => api.get<Staff>("/api/staff") });
  const [email, setEmail] = useState("");
  const errors = useFieldErrors();
  const qc = useQueryClient();
  const toast = useToast();

  return (
    <Panel title="Kolleger" id="kolleger">
      <p className="panel-b hint" style={{ paddingBottom: 0 }}>I bør være mindst to, så den ene kan hjælpe den anden ind, hvis login driller.</p>
      {q.data?.members.map((m) => (
        <div className="row" key={m.id}>
          <div className="grow">{m.displayName || m.email}<div className="hint">{m.email}</div></div>
          {m.me ? <span className="hint">Dig</span> : (
            <button className="btn link danger" onClick={async () => { await api.del(`/api/staff/members/${m.id}`); await qc.invalidateQueries({ queryKey: ["staff"] }); toast(`${m.email} har ikke længere adgang`); }}>Fjern adgang</button>
          )}
        </div>
      ))}
      {q.data?.invitations.map((i) => (
        <div className="row" key={i.id}>
          <div className="grow">{i.email}<div className="hint">Inviteret · linket virker til {fmtDate(i.expiresAt)}</div></div>
          <CopyButton text={i.url} label="Kopiér link" />
          <button className="btn link danger" onClick={async () => { await api.del(`/api/staff/invitations/${i.id}`); await qc.invalidateQueries({ queryKey: ["staff"] }); }}>Træk tilbage</button>
        </div>
      ))}
      <form
        className="row"
        onSubmit={async (e) => {
          e.preventDefault();
          errors.clear();
          try {
            await api.post("/api/staff/invitations", { email });
            setEmail("");
            await qc.invalidateQueries({ queryKey: ["staff"] });
            toast("Invitationen er klar. Kopiér linket og send det til din kollega.");
          } catch (err) {
            errors.handle(err);
          }
        }}
      >
        <div className="grow">
          <Field label="Inviter en kollega" name="inviteEmail" error={errors.errors.email} hint="Kollegaen logger ind med den e-mail, du skriver her.">
            <input id="inviteEmail" type="email" value={email} onChange={(e) => setEmail(e.target.value)} placeholder="kollega@eksempel.dk" required />
          </Field>
        </div>
        <button className="btn" type="submit">Lav invitation</button>
      </form>
    </Panel>
  );
}
