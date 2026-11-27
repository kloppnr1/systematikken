import { Link, useParams } from "react-router";
import { useQuery } from "@tanstack/react-query";
import { api } from "../api";
import { fmtDate, fmtKr, fmtMonth } from "../format";
import { Failed, Loading, Panel, Puzzle, useTitle } from "../ui";

type Run = {
  id: number; month: string; approvedAt: string; approvedBy: string; chargeCount: number; total: number; serialNumber: number;
  voucher: { opkraevningssum: number; opkraevninger: number; tilskudssum: number };
  fileName: string; hasPayments: boolean;
};

/** An approved month: send the file to the bank, give the voucher to the bookkeeper, then read the payments back in. */
export function BillingRun() {
  const { id } = useParams();
  const q = useQuery({ queryKey: ["run", id], queryFn: () => api.get<Run>(`/api/billing/runs/${id}`) });
  useTitle(q.data ? `Opkrævning ${fmtMonth(q.data.month)}` : "Opkrævning");
  if (q.isPending) return <Loading />;
  if (q.error) return <Failed error={q.error} />;
  const r = q.data;

  return (
    <>
      <Link className="back" to="/opkraevning">← Opkrævning</Link>
      <h1>Opkrævning for {fmtMonth(r.month)}</h1>
      <ol className="steps" aria-label="Trin">
        <li className="done">1 Tjek</li>
        <li className="done">2 Godkend</li>
        <li className={r.hasPayments ? "done" : "now"}>3 Send til banken</li>
      </ol>

      <section className="panel done-hero">
        <Puzzle size={96} />
        <div>
        <h2>{fmtMonth(r.month)} er godkendt</h2>
        <p style={{ margin: 0 }}>
          {r.chargeCount} opkrævninger på <b className="num">{fmtKr(r.total)}</b>, godkendt {fmtDate(r.approvedAt)} af {r.approvedBy}.
        </p>
        </div>
      </section>

      <div className="side">
        <div>
          <Panel title="Send til banken">
            <ol style={{ margin: "0 1.1rem 1rem 2.2rem", padding: 0, display: "grid", gap: ".4rem" }}>
              <li>Hent filen herunder. Den hedder <b>{r.fileName}</b>.</li>
              <li>Log på jeres netbank og find <b>Betalingsservice</b>.</li>
              <li>Vælg at sende en fil, og vælg filen. Beløbene trækkes {fmtDate(r.month)}.</li>
            </ol>
            <div className="panel-b">
              <a className="btn primary" href={`/api/billing/runs/${r.id}/file`} download>Hent fil til Betalingsservice</a>
              <p className="hint" style={{ marginTop: ".75rem" }}>Forældre uden aftale får et indbetalingskort fra Betalingsservice med tilbud om at tilmelde sig.</p>
            </div>
          </Panel>
          <Panel title="Bagefter">
            <div className="row">
              <span className="grow">Når pengene er trukket, så hent betalingsfilen hos banken og læg den ind. Så ved Systematikken, hvem der har betalt.</span>
              <Link className="btn" to="/opkraevning/betalinger">Indlæs betalinger</Link>
            </div>
          </Panel>
        </div>
        <aside className="panel sum">
          <div className="label">Bogføringsbilag</div>
          <div className="kv">
            <div><span>Opkrævningssum</span><span className="num">{fmtKr(r.voucher.opkraevningssum)}</span></div>
            <div><span>Opkrævninger</span><span className="num">{fmtKr(r.voucher.opkraevninger)}</span></div>
            <div><span>Tilskudssum</span><span className="num">{fmtKr(r.voucher.tilskudssum)}</span></div>
          </div>
          <a className="btn big" href={`/api/billing/runs/${r.id}/voucher`} download>Hent bogføringsbilag (PDF)</a>
          <p className="hint">Giv det til jeres bogholder. Overførte beløb og indbetalinger er ikke med.</p>
        </aside>
      </div>
    </>
  );
}
