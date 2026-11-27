import { useState } from "react";
import { Link, useNavigate } from "react-router";
import { useQuery, useQueryClient } from "@tanstack/react-query";
import { api } from "../api";
import { fmtDate, fmtKr, fmtMonth, groupName, parseAmount } from "../format";
import { Failed, Loading, Panel, Pill, useTitle, useToast } from "../ui";

type Line = { kind: string; month: string; amount: number; text: string };
type Draft = {
  setupProblem?: string;
  month: string;
  monthStart: string;
  canApprove: boolean;
  problems: { childId: string; name: string; kind: "MissingPayer" | "MissingSubsidy" | "NothingToCollect"; blocking: boolean }[];
  changes: { tag: string; tone: string; text: string }[];
  charges: { childId: string; name: string; payer: string | null; status: string; group: string | null; lines: Line[]; amount: number }[];
  totals: { toCollect: number; count: number; freePlace: number; siblingDiscount: number };
  runs: { id: number; month: string; approvedAt: string; chargeCount: number; total: number }[];
};

/** The month you step through, like approving payments in online banking: check, approve, send to the bank. */
export function Billing() {
  useTitle("Opkrævning");
  const q = useQuery({ queryKey: ["billing"], queryFn: () => api.get<Draft>("/api/billing") });
  const [confirming, setConfirming] = useState(false);
  const [filter, setFilter] = useState<"all" | "problems">("all");
  const [open, setOpen] = useState<string | null>(null);
  const qc = useQueryClient();
  const toast = useToast();
  const navigate = useNavigate();

  if (q.isPending) return <Loading />;
  if (q.error) return <Failed error={q.error} />;
  const d = q.data;

  if (d.setupProblem)
    return (
      <>
        <h1>Opkrævning</h1>
        <Panel><p className="empty">{d.setupProblem} <Link to="/indstillinger">Gå til indstillinger</Link></p></Panel>
        <Runs runs={d.runs} />
      </>
    );

  const blocking = d.problems.filter((p) => p.blocking);
  const problemIds = new Set(d.problems.map((p) => p.childId));
  const rows = filter === "problems" ? d.charges.filter((c) => problemIds.has(c.childId)) : d.charges;

  return (
    <>
      <h1>Opkrævning for {fmtMonth(d.monthStart)}</h1>
      <p className="muted">Opkrævningerne trækkes {fmtDate(d.monthStart)}.</p>
      <ol className="steps" aria-label="Trin">
        <li className={confirming ? "done" : "now"}>1 Tjek</li>
        <li className={confirming ? "now" : ""}>2 Godkend</li>
        <li>3 Send til banken</li>
      </ol>

      <div className="side">
        <div>
          <Panel title="Skal rettes" aside={<span className={`count ${blocking.length ? "bad" : "ok"}`}>{blocking.length}</span>}>
            {d.problems.length === 0 && <p className="empty">Alt er i orden. Du kan godkende.</p>}
            {d.problems.map((p) => <ProblemRow key={p.childId + p.kind} problem={p} onFixed={() => qc.invalidateQueries()} />)}
          </Panel>

          {d.changes.length > 0 && (
            <Panel title="Ændret siden sidst" aside={<span className="count">{d.changes.length}</span>}>
              {d.changes.map((c, i) => (
                <div className="row" key={i}><Pill tone={c.tone}>{c.tag}</Pill><span className="grow">{c.text}</span></div>
              ))}
            </Panel>
          )}

          <Panel
            title="Alle opkrævninger"
            aside={
              <div className="chips" style={{ margin: 0 }}>
                <button className="chip" aria-pressed={filter === "all"} onClick={() => setFilter("all")}>Alle ({d.charges.length})</button>
                <button className="chip" aria-pressed={filter === "problems"} onClick={() => setFilter("problems")}>Med fejl ({problemIds.size})</button>
              </div>
            }
          >
            {rows.length === 0 && <p className="empty">Ingen opkrævninger denne måned.</p>}
            {rows.map((c) => (
              <div key={c.childId} className={`row ${problemIds.has(c.childId) ? "bad" : ""}`}>
                <button className="grow btn link" style={{ textAlign: "left", color: "inherit", padding: 0 }} onClick={() => setOpen(open === c.childId ? null : c.childId)} aria-expanded={open === c.childId}>
                  <b>{c.name}</b>
                  <div className="hint">{groupName(c.group)} · betaler: {c.payer ?? "mangler"}{c.status === "DepositOnly" && " · kun depositum"}</div>
                  {open === c.childId && (
                    <ul className="lines">
                      {c.lines.map((l, i) => <li key={i}><span>{l.text}</span><span className="num">{fmtKr(l.amount)}</span></li>)}
                    </ul>
                  )}
                </button>
                <b className="num">{fmtKr(c.amount)}</b>
              </div>
            ))}
          </Panel>
        </div>

        <aside className="panel sum feature" aria-label="Godkend">
          <div className="label">{fmtMonth(d.monthStart)} i alt</div>
          <div className="total num">{fmtKr(d.totals.toCollect)}</div>
          <div className="kv">
            <div><span>Opkrævninger</span><span className="num">{d.totals.count}</span></div>
            <div><span>Friplads i alt</span><span className="num">−{fmtKr(d.totals.freePlace)}</span></div>
            <div><span>Søskenderabat i alt</span><span className="num">−{fmtKr(d.totals.siblingDiscount)}</span></div>
          </div>
          {!confirming ? (
            <>
              <button className="btn highlight big" disabled={!d.canApprove} onClick={() => setConfirming(true)}>Godkend {fmtMonth(d.monthStart).split(" ")[0]}</button>
              <p className="hint">{d.canApprove ? "Du får lige et spørgsmål mere, før noget bliver låst." : `Ret ${blocking.length === 1 ? "det ene punkt" : `de ${blocking.length} punkter`} under "Skal rettes" først.`}</p>
            </>
          ) : (
            <div className="confirm" role="alertdialog" aria-labelledby="confirmText">
              <p id="confirmText"><b>Godkend {d.totals.count} opkrævninger på {fmtKr(d.totals.toCollect)}?</b></p>
              <p className="hint" style={{ color: "inherit" }}>Bagefter kan de ikke ændres. En fejl rettes med en godtgørelse på næste måned.</p>
              <div className="inline">
                <button
                  className="btn primary"
                  autoFocus
                  onClick={async () => {
                    try {
                      const run = await api.post<{ id: number }>("/api/billing/approve", { month: d.month });
                      await qc.invalidateQueries();
                      toast(`${fmtMonth(d.monthStart)} er godkendt`);
                      navigate(`/opkraevning/${run.id}`);
                    } catch (e) {
                      toast(e instanceof Error ? e.message : "Noget gik galt");
                      setConfirming(false);
                    }
                  }}
                >
                  Ja, godkend
                </button>
                <button className="btn" onClick={() => setConfirming(false)}>Tilbage</button>
              </div>
            </div>
          )}
          <p className="hint">Efter godkendelse henter du en fil og lægger den op hos jeres bank. Vi viser dig hvordan.</p>
        </aside>
      </div>

      <Runs runs={d.runs} />
    </>
  );
}

function ProblemRow({ problem: p, onFixed }: { problem: Draft["problems"][number]; onFixed: () => void }) {
  const [freePlace, setFreePlace] = useState("");
  const [sibling, setSibling] = useState("");
  const [error, setError] = useState<string | null>(null);
  const toast = useToast();

  if (p.kind === "MissingPayer")
    return (
      <div className="row bad">
        <div className="grow"><b>{p.name} har ingen betaler</b><div className="hint">Tilføj en forælder, der betaler.</div></div>
        <Link className="btn primary" to={`/born/${p.childId}`}>Vælg betaler</Link>
      </div>
    );

  if (p.kind === "NothingToCollect")
    return (
      <div className="row warn">
        <div className="grow"><b>{p.name} skal ikke betale noget</b><div className="hint">Beløbet er 0 eller derunder, fx efter en overbetaling. Det overføres til næste måned.</div></div>
        <Link className="btn" to={`/born/${p.childId}`}>Se barnet</Link>
      </div>
    );

  return (
    <form
      className="row bad"
      onSubmit={async (e) => {
        e.preventDefault();
        const fp = parseAmount(freePlace || "0");
        const sd = parseAmount(sibling || "0");
        if (Number.isNaN(fp) || Number.isNaN(sd)) { setError("Skriv beløb som 1.234,50"); return; }
        try {
          await api.post("/api/billing/subsidy", { childId: p.childId, freePlace: fp, siblingDiscount: sd });
          toast(`Tilskud for ${p.name} er gemt`);
          onFixed();
        } catch (err) {
          setError(err instanceof Error ? err.message : "Noget gik galt");
        }
      }}
    >
      <div className="grow">
        <b>{p.name}: tilskuddet stoppede sidste måned</b>
        <div className="hint">Har kommunen givet et nyt? Skriv beløbet pr. måned, eller 0 hvis der ikke er noget mere.</div>
        <div className="inline" style={{ marginTop: ".5rem" }}>
          <input aria-label="Friplads" placeholder="Friplads" inputMode="decimal" value={freePlace} onChange={(e) => setFreePlace(e.target.value)} style={{ width: "8rem" }} />
          <input aria-label="Søskenderabat" placeholder="Søskenderabat" inputMode="decimal" value={sibling} onChange={(e) => setSibling(e.target.value)} style={{ width: "9rem" }} />
          <button className="btn primary" type="submit">Gem</button>
        </div>
        {error && <div className="error">{error}</div>}
      </div>
    </form>
  );
}

function Runs({ runs }: { runs: Draft["runs"] }) {
  if (runs.length === 0) return null;
  return (
    <Panel title="Godkendte måneder">
      {runs.map((r) => (
        <Link className="row" key={r.id} to={`/opkraevning/${r.id}`}>
          <span className="grow">{fmtMonth(r.month)}</span>
          <span className="hint">{r.chargeCount} opkrævninger</span>
          <b className="num">{fmtKr(r.total)}</b>
        </Link>
      ))}
    </Panel>
  );
}
