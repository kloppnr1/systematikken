import { useState } from "react";
import { Link } from "react-router";
import { useQuery } from "@tanstack/react-query";
import { api } from "../api";
import { fmtMonth, monthName, parseDate } from "../format";
import { Failed, Loading, Panel, useTitle } from "../ui";

type Person = { id: string; name: string };
type Group = { count: number; capacity: number; freePlaces: number; waitingList: number; present: Person[]; joining: Person[]; leaving: Person[] };
type Month = { month: string; nursery: Group; kindergarten: Group };
type Report = { schoolStartMonth: number; months: Month[] };

/** "Pladser": nursery and kindergarten month by month against capacity, to plan intake from the waiting list. */
export function Occupancy() {
  useTitle("Pladser");
  const thisYear = new Date().getFullYear();
  const [year, setYear] = useState<number | null>(null);
  const [showWaiting, setShowWaiting] = useState(false);
  const [selected, setSelected] = useState<string | null>(null);
  const from = year ? `${year}-01` : new Date().toISOString().slice(0, 7);
  const q = useQuery({ queryKey: ["occupancy", from], queryFn: () => api.get<Report>(`/api/occupancy?from=${from}&months=12`) });

  const month = q.data?.months.find((m) => m.month === selected) ?? q.data?.months[0];

  return (
    <>
      <h1>Pladser</h1>
      <div className="chips">
        <button className="chip" aria-pressed={year === null} onClick={() => setYear(null)}>De næste 12 måneder</button>
        {[thisYear, thisYear + 1].map((y) => (
          <button key={y} className="chip" aria-pressed={year === y} onClick={() => setYear(y)}>{y}</button>
        ))}
        <label className="check" style={{ marginLeft: "auto" }}>
          <input type="checkbox" checked={showWaiting} onChange={(e) => setShowWaiting(e.target.checked)} /> Vis venteliste
        </label>
      </div>

      {q.isPending && <Loading />}
      {q.error && <Failed error={q.error} />}
      {q.data && (
        <>
          <Panel>
            <div className="panel-b" style={{ paddingTop: "1rem" }}>
              <Chart months={q.data.months} showWaiting={showWaiting} selected={month?.month} onSelect={setSelected} />
            </div>
            <div className="legend">
              <span><i style={{ background: "var(--accent)" }} />Vuggestue</span>
              <span><i style={{ background: "var(--kinder)" }} />Børnehave</span>
              {showWaiting && <span><i style={{ background: "var(--muted)", opacity: .35 }} />Venteliste</span>}
              <span>– – Pladser</span>
              <span className="hint">Børn uden slutdato regnes med til skolestart i {monthName(q.data.schoolStartMonth)}.</span>
            </div>
          </Panel>

          <Panel>
            <div className="tbl">
              <table>
                <thead>
                  <tr>
                    <th>Måned</th><th className="r">Vuggestue</th><th className="r">Ledige</th><th className="r">Børnehave</th><th className="r">Ledige</th>
                    {showWaiting && <th className="r">Venteliste</th>}
                  </tr>
                </thead>
                <tbody>
                  {q.data.months.map((m) => (
                    <tr key={m.month} onClick={() => setSelected(m.month)} style={{ cursor: "pointer", background: m.month === month?.month ? "var(--selected)" : undefined }}>
                      <td>{fmtMonth(m.month)}</td>
                      <td className="r num">{m.nursery.count}</td>
                      <td className="r num" style={{ color: m.nursery.freePlaces < 0 ? "var(--bad)" : undefined }}>{m.nursery.freePlaces}</td>
                      <td className="r num">{m.kindergarten.count}</td>
                      <td className="r num" style={{ color: m.kindergarten.freePlaces < 0 ? "var(--bad)" : undefined }}>{m.kindergarten.freePlaces}</td>
                      {showWaiting && <td className="r num">{m.nursery.waitingList + m.kindergarten.waitingList}</td>}
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          </Panel>

          {month && (
            <div className="grid" style={{ gap: "0 1.25rem" }}>
              <MonthGroup title={`Vuggestue i ${fmtMonth(month.month)}`} g={month.nursery} />
              <MonthGroup title={`Børnehave i ${fmtMonth(month.month)}`} g={month.kindergarten} />
            </div>
          )}
        </>
      )}
    </>
  );
}

function MonthGroup({ title, g }: { title: string; g: Group }) {
  return (
    <Panel title={title} aside={<span className="count">{g.count} af {g.capacity}</span>}>
      {g.joining.length > 0 && <div className="row"><span className="grow"><b>Starter:</b> {names(g.joining)}</span></div>}
      {g.leaving.length > 0 && <div className="row"><span className="grow"><b>Stopper:</b> {names(g.leaving)}</span></div>}
      {g.present.length === 0 && <p className="empty">Ingen børn.</p>}
      {g.present.map((p) => <Link className="row" key={p.id} to={`/born/${p.id}`}>{p.name}</Link>)}
    </Panel>
  );
}

const names = (ps: Person[]) => ps.map((p) => p.name).join(", ");

/** Two stacked bars per month (nursery, kindergarten) with capacity as dashed lines. Drawn to one scale. */
function Chart({ months, showWaiting, selected, onSelect }: { months: Month[]; showWaiting: boolean; selected?: string; onSelect: (m: string) => void }) {
  const W = 760, H = 260, left = 32, bottom = 28, top = 10;
  const max = Math.max(
    5,
    ...months.flatMap((m) => [
      m.nursery.count + (showWaiting ? m.nursery.waitingList : 0), m.nursery.capacity,
      m.kindergarten.count + (showWaiting ? m.kindergarten.waitingList : 0), m.kindergarten.capacity,
    ]),
  );
  const step = max > 40 ? 10 : 5;
  const top_ = Math.ceil(max / step) * step;
  const y = (v: number) => top + (H - top - bottom) * (1 - v / top_);
  const slot = (W - left) / months.length;
  const bw = Math.min(18, slot / 2.6);

  const capLine = (key: "nursery" | "kindergarten") =>
    months.map((m, i) => `${i === 0 ? "M" : "L"}${left + i * slot + 2},${y(m[key].capacity)} H${left + (i + 1) * slot - 2}`).join(" ");

  return (
    <svg className="chart" viewBox={`0 0 ${W} ${H}`} role="img" aria-label="Børn og pladser pr. måned">
      {Array.from({ length: top_ / step + 1 }, (_, i) => i * step).map((v) => (
        <g key={v}>
          <line className="grid-line" x1={left} x2={W} y1={y(v)} y2={y(v)} />
          <text x={left - 6} y={y(v) + 4} textAnchor="end">{v}</text>
        </g>
      ))}
      {months.map((m, i) => {
        const x = left + i * slot + slot / 2;
        const bar = (g: Group, cls: string, dx: number) => (
          <>
            <rect className={cls} x={x + dx} y={y(g.count)} width={bw} height={y(0) - y(g.count)} rx={2} />
            {showWaiting && g.waitingList > 0 && (
              <rect className={`${cls} wl`} x={x + dx} y={y(g.count + g.waitingList)} width={bw} height={y(g.count) - y(g.count + g.waitingList)} rx={2} />
            )}
          </>
        );
        const label = parseDate(m.month).toLocaleDateString("da-DK", { month: "short" });
        return (
          <g key={m.month} className="bar" onClick={() => onSelect(m.month)}>
            <rect x={left + i * slot} y={top} width={slot} height={H - top - bottom} className={m.month === selected ? "sel" : "unsel"} />
            {bar(m.nursery, "n", -bw - 1)}
            {bar(m.kindergarten, "k", 1)}
            <text x={x} y={H - 8} textAnchor="middle">{label}</text>
            <title>{`${fmtMonth(m.month)}: vuggestue ${m.nursery.count}/${m.nursery.capacity}, børnehave ${m.kindergarten.count}/${m.kindergarten.capacity}`}</title>
          </g>
        );
      })}
      {months[0].nursery.capacity > 0 && <path className="cap n" d={capLine("nursery")} />}
      {months[0].kindergarten.capacity > 0 && <path className="cap k" d={capLine("kindergarten")} />}
    </svg>
  );
}
