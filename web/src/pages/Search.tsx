import { Link, useSearchParams } from "react-router";
import { useQuery } from "@tanstack/react-query";
import { api } from "../api";
import { fmtDate } from "../format";
import { Failed, Loading, Panel, useTitle } from "../ui";

type Result = {
  children: { id: string; name: string; dateOfBirth: string }[];
  parents: { id: string; name: string; phone: string; email: string; childId: string; childName: string }[];
  applicants: { id: string; childName: string; desiredStart: string }[];
};

export function Search() {
  useTitle("Søg");
  const [params] = useSearchParams();
  const q = params.get("q") ?? "";
  const r = useQuery({ queryKey: ["search", q], queryFn: () => api.get<Result>(`/api/search?q=${encodeURIComponent(q)}`), enabled: q.length >= 2 });
  const none = r.data && r.data.children.length + r.data.parents.length + r.data.applicants.length === 0;

  return (
    <>
      <h1>Søg efter "{q}"</h1>
      {r.isFetching && <Loading />}
      {r.error && <Failed error={r.error} />}
      {none && <p className="muted">Ingen fundet.</p>}
      {r.data && r.data.children.length > 0 && (
        <Panel title="Børn">
          {r.data.children.map((c) => (
            <Link className="row" key={c.id} to={`/born/${c.id}`}><b className="grow">{c.name}</b><span className="hint">født {fmtDate(c.dateOfBirth)}</span></Link>
          ))}
        </Panel>
      )}
      {r.data && r.data.parents.length > 0 && (
        <Panel title="Forældre">
          {r.data.parents.map((p) => (
            <Link className="row" key={p.id} to={`/born/${p.childId}`}>
              <div className="grow"><b>{p.name}</b><div className="hint">{[p.phone, p.email].filter(Boolean).join(" · ")}</div></div>
              <span className="hint">{p.childName}</span>
            </Link>
          ))}
        </Panel>
      )}
      {r.data && r.data.applicants.length > 0 && (
        <Panel title="Venteliste">
          {r.data.applicants.map((a) => (
            <Link className="row" key={a.id} to={`/venteliste/${a.id}`}><b className="grow">{a.childName}</b><span className="hint">ønsker start {fmtDate(a.desiredStart)}</span></Link>
          ))}
        </Panel>
      )}
    </>
  );
}
