import { useState } from "react";
import { Link } from "react-router";
import { useQuery, useQueryClient } from "@tanstack/react-query";
import { api } from "../api";
import { fmtDate, fmtKr } from "../format";
import { Failed, Loading, Panel, useFieldErrors, useTitle, useToast } from "../ui";

type Outcome = { alreadyImported: boolean; booked: number; total: number; notBooked: string[] };
type Import = { id: number; fileName: string; importedAt: string; paymentCount: number; total: number; notBooked: string[] };

/** Read the bank's payment file back in, so the system knows who has paid. */
export function Payments() {
  useTitle("Indlæs betalinger");
  const list = useQuery({ queryKey: ["imports"], queryFn: () => api.get<Import[]>("/api/billing/payments") });
  const [outcome, setOutcome] = useState<Outcome | null>(null);
  const [busy, setBusy] = useState(false);
  const errors = useFieldErrors();
  const qc = useQueryClient();
  const toast = useToast();

  return (
    <div className="narrow">
      <Link className="back" to="/opkraevning">← Opkrævning</Link>
      <h1>Indlæs betalinger</h1>
      <Panel title="Betalingsfil fra banken">
        <form
          className="panel-b"
          onSubmit={async (e) => {
            e.preventDefault();
            errors.clear();
            setOutcome(null);
            const data = new FormData(e.currentTarget);
            setBusy(true);
            try {
              const r = await api.post<Outcome>("/api/billing/payments", data);
              setOutcome(r);
              await qc.invalidateQueries();
              if (!r.alreadyImported) toast(`${r.booked} betalinger er bogført`);
            } catch (err) {
              errors.handle(err);
            } finally {
              setBusy(false);
            }
          }}
        >
          <p>Hent filen med betalingsoplysninger hos Betalingsservice. Læg den ind her.</p>
          <input type="file" name="file" required aria-label="Betalingsfil" />
          {(errors.errors.file || errors.message) && <p className="error">{errors.errors.file ?? errors.message}</p>}
          <p><button className="btn primary" type="submit" disabled={busy}>{busy ? "Læser filen…" : "Indlæs"}</button></p>
        </form>
      </Panel>

      {outcome?.alreadyImported && <Panel><p className="empty">Den fil er allerede lagt ind. Intet er bogført to gange.</p></Panel>}
      {outcome && !outcome.alreadyImported && (
        <section className="panel done-hero">
          <h2>{outcome.booked} betalinger på {fmtKr(outcome.total)} er bogført</h2>
          {outcome.notBooked.length > 0 && <NotBooked lines={outcome.notBooked} />}
        </section>
      )}

      <Panel title="Tidligere indlæst">
        {list.isPending && <div className="panel-b"><Loading /></div>}
        {list.error && <Failed error={list.error} />}
        {list.data?.length === 0 && <p className="empty">Ingen filer endnu.</p>}
        {list.data?.map((i) => (
          <div className="row" key={i.id}>
            <div className="grow">
              {i.fileName}
              <div className="hint">{fmtDate(i.importedAt)} · {i.paymentCount} betalinger</div>
              {i.notBooked.length > 0 && <NotBooked lines={i.notBooked} />}
            </div>
            <span className="num">{fmtKr(i.total)}</span>
          </div>
        ))}
      </Panel>
    </div>
  );
}

function NotBooked({ lines }: { lines: string[] }) {
  return (
    <div style={{ marginTop: ".5rem" }}>
      <b>Ikke bogført, se selv efter:</b>
      <ul style={{ margin: ".25rem 0 0", paddingLeft: "1.2rem" }}>
        {lines.map((l, i) => <li key={i} className="hint">{l}</li>)}
      </ul>
      <p className="hint">Er det en betaling, så registrér den på barnets side under Konto.</p>
    </div>
  );
}
