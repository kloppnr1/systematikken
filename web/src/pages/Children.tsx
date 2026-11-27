import { Link, useSearchParams } from "react-router";
import { useQuery } from "@tanstack/react-query";
import { api } from "../api";
import { fmtKr, groupName } from "../format";
import { Avatar, Empty, Failed, Loading, Panel, Pill, useTitle } from "../ui";

type Row = { id: string; name: string; group: string; payer: string | null; status: string; tone: string; balance: number };

const filters = [
  ["aktive", "Går her og kommer"],
  ["udmeldte", "Udmeldte"],
  ["gaeld", "Mangler at betale"],
  ["alle", "Alle"],
] as const;

/** The children, like the phone's contacts list. */
export function Children() {
  useTitle("Børn");
  const [params, setParams] = useSearchParams();
  const filter = params.get("filter") ?? "aktive";
  const q = useQuery({ queryKey: ["children", filter], queryFn: () => api.get<Row[]>(`/api/children?filter=${filter}`) });

  return (
    <>
      <div className="head">
        <h1>Børn</h1>
        <Link className="btn primary" to="/born/nyt">Indmeld et barn</Link>
      </div>
      <div className="chips" role="group" aria-label="Vis">
        {filters.map(([key, label]) => (
          <button key={key} className="chip" aria-pressed={filter === key} onClick={() => setParams({ filter: key })}>{label}</button>
        ))}
      </div>
      <Panel>
        {q.isPending && <div className="panel-b" style={{ paddingTop: "1rem" }}><Loading /></div>}
        {q.error && <Failed error={q.error} />}
        {q.data?.length === 0 && <Empty>Ingen børn her endnu. Børn kommer ind fra ventelisten, eller du kan indmelde et barn direkte.</Empty>}
        {q.data?.map((c) => (
          <Link className="row" key={c.id} to={`/born/${c.id}`}>
            <Avatar name={c.name} />
            <div className="grow">
              <b>{c.name}</b>
              <div className="hint">{groupName(c.group)} · betaler: {c.payer ?? "ikke valgt"}</div>
            </div>
            {c.balance > 0 && <Pill tone="bad">Mangler {fmtKr(c.balance)}</Pill>}
            <Pill tone={c.tone}>{c.status}</Pill>
          </Link>
        ))}
      </Panel>
    </>
  );
}
