import { Link, Navigate } from "react-router";
import { useQuery } from "@tanstack/react-query";
import { api } from "../api";
import { fmtDate } from "../format";
import { useMe } from "../session";
import { Failed, Loading, Panel, useTitle } from "../ui";

type Row = { day: string; name: string; source: string; count: number };
type Data = { today: string; countingSince: string | null; rows: Row[] };

const kinds = [
  ["besog", "Besøg på forsiden"],
  ["demo", "Klik på Prøv demoen"],
  ["kom-i-gang", "Klik på Kom i gang"],
  ["log-ind", "Klik på Log ind"],
  ["login", "Logins i appen"],
  ["institution", "Nye institutioner"],
] as const;

const sourceName = (s: string) =>
  ({ "": "–", direkte: "Direkte eller ukendt", linkedin: "LinkedIn", google: "Google", facebook: "Facebook", bing: "Bing", andet: "Andet" })[s] ?? s;

/** For the people who run Systematikken: what visitors do on the front page and in the app, day by day. */
export function Stats() {
  useTitle("Tal");
  const me = useMe();
  const q = useQuery({ queryKey: ["stats"], queryFn: () => api.get<Data>("/api/stats"), enabled: !!me.data?.operator });

  if (me.isPending) return <main className="wrap"><Loading /></main>;
  if (!me.data?.signedIn) return <Navigate to="/login?returnUrl=%2Ftal" replace />;
  if (!me.data.operator) return <Navigate to="/" replace />;

  return (
    <main className="wrap">
      <p><Link to="/">← Til appen</Link></p>
      <h1>Tal</h1>
      <p className="hint">
        Talt uden cookies og uden oplysninger om den besøgende. Skriv <b>?kilde=linkedin</b> bag systematikken.dk i et opslag,
        så bliver besøg fra det talt for sig.
        {q.data?.countingSince && <> Talt siden {fmtDate(q.data.countingSince)}.</>}
      </p>
      {q.isPending ? <Loading /> : q.isError ? <Failed error={q.error} /> : <Numbers data={q.data} />}
    </main>
  );
}

function Numbers({ data }: { data: Data }) {
  const since = (days: number) => {
    const d = new Date(data.today + "T00:00:00Z");
    d.setUTCDate(d.getUTCDate() - days + 1);
    return d.toISOString().slice(0, 10);
  };
  const total = (name: string, from: string, source?: string) =>
    data.rows.filter((r) => r.name === name && r.day >= from && (source === undefined || r.source === source)).reduce((s, r) => s + r.count, 0);

  const from30 = since(30);
  const sources = [...new Set(data.rows.filter((r) => r.day >= from30 && r.source !== "").map((r) => r.source))]
    .map((s) => ({ s, visits: total("besog", from30, s), demo: total("demo", from30, s), start: total("kom-i-gang", from30, s) + total("log-ind", from30, s) }))
    .sort((a, b) => b.visits - a.visits);

  const days = [...new Set(data.rows.map((r) => r.day))].sort().reverse();
  const onDay = (day: string, name: string) => data.rows.filter((r) => r.day === day && r.name === name).reduce((s, r) => s + r.count, 0);

  if (data.rows.length === 0) return <Panel><p className="panel-b">Ingen tal endnu. Det første besøg på forsiden bliver talt med det samme.</p></Panel>;

  return (
    <>
      <Panel title="Overblik">
        <div className="tbl">
          <table>
            <thead><tr><th></th><th className="r">I dag</th><th className="r">7 dage</th><th className="r">30 dage</th></tr></thead>
            <tbody>
              {kinds.map(([name, label]) => (
                <tr key={name}>
                  <td>{label}</td>
                  <td className="r num">{total(name, data.today)}</td>
                  <td className="r num">{total(name, since(7))}</td>
                  <td className="r num">{total(name, from30)}</td>
                </tr>
              ))}
              <tr>
                <td>Heraf besøg fra LinkedIn</td>
                <td className="r num">{total("besog", data.today, "linkedin")}</td>
                <td className="r num">{total("besog", since(7), "linkedin")}</td>
                <td className="r num">{total("besog", from30, "linkedin")}</td>
              </tr>
            </tbody>
          </table>
        </div>
      </Panel>

      {sources.length > 0 && (
        <Panel title="Hvor kommer de fra" aside={<span className="hint">30 dage</span>}>
          <div className="tbl">
            <table>
              <thead><tr><th>Kilde</th><th className="r">Besøg</th><th className="r">Prøv demoen</th><th className="r">Kom i gang / Log ind</th></tr></thead>
              <tbody>
                {sources.map((x) => (
                  <tr key={x.s}><td>{sourceName(x.s)}</td><td className="r num">{x.visits}</td><td className="r num">{x.demo}</td><td className="r num">{x.start}</td></tr>
                ))}
              </tbody>
            </table>
          </div>
        </Panel>
      )}

      <Panel title="Dag for dag" aside={<span className="hint">60 dage</span>}>
        <div className="tbl">
          <table>
            <thead><tr><th>Dag</th><th className="r">Besøg</th><th className="r">Demo</th><th className="r">Kom i gang</th><th className="r">Logins</th><th className="r">Nye inst.</th></tr></thead>
            <tbody>
              {days.map((d) => (
                <tr key={d}>
                  <td className="num">{fmtDate(d)}</td>
                  <td className="r num">{onDay(d, "besog")}</td>
                  <td className="r num">{onDay(d, "demo")}</td>
                  <td className="r num">{onDay(d, "kom-i-gang") + onDay(d, "log-ind")}</td>
                  <td className="r num">{onDay(d, "login")}</td>
                  <td className="r num">{onDay(d, "institution")}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      </Panel>
    </>
  );
}
