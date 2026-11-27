import { useState } from "react";
import { Link, useSearchParams } from "react-router";
import { useQuery } from "@tanstack/react-query";
import { api } from "../api";
import { fmtDate, groupName } from "../format";
import { CopyButton, Empty, Failed, Loading, Panel, Pill, useTitle } from "../ui";

type Row = {
  id: string; position: number; childName: string; dateOfBirth: string; desiredStart: string; hasSibling: boolean; createdAt: string;
  group: string; status: string; tone: string; emails: string[];
};

/** The waiting list in sign-up order, like a playlist. Siblings are marked; staff decide who gets a place. */
export function WaitingList() {
  useTitle("Venteliste");
  const [params, setParams] = useSearchParams();
  const filter = params.get("filter") ?? "aktive";
  const q = useQuery({ queryKey: ["applicants", filter], queryFn: () => api.get<{ applicants: Row[]; signupUrl: string }>(`/api/applicants?filter=${filter}`) });
  const [selected, setSelected] = useState<Set<string>>(new Set());
  const signupUrl = q.data ? `${location.origin}${q.data.signupUrl}` : "";
  const mails = q.data?.applicants.filter((a) => selected.has(a.id)).flatMap((a) => a.emails) ?? [];

  return (
    <>
      <div className="head">
        <h1>Venteliste</h1>
        {q.data && <a className="btn" href={q.data.signupUrl} target="_blank" rel="noopener">Skriv et barn op</a>}
      </div>
      <div className="chips" role="group" aria-label="Vis">
        <button className="chip" aria-pressed={filter === "aktive"} onClick={() => setParams({})}>På listen</button>
        <button className="chip" aria-pressed={filter === "afsluttede"} onClick={() => setParams({ filter: "afsluttede" })}>Sagt nej eller fjernet</button>
      </div>

      <Panel>
        {q.isPending && <div className="panel-b" style={{ paddingTop: "1rem" }}><Loading /></div>}
        {q.error && <Failed error={q.error} />}
        {q.data?.applicants.length === 0 && <Empty>Ingen på listen endnu. Når forældre skriver sig op via jeres link, kommer de her.</Empty>}
        {q.data?.applicants.map((a) => (
          <div className="row" key={a.id}>
            <input
              type="checkbox"
              aria-label={`Vælg ${a.childName}`}
              checked={selected.has(a.id)}
              onChange={(e) => {
                const next = new Set(selected);
                if (e.target.checked) next.add(a.id); else next.delete(a.id);
                setSelected(next);
              }}
            />
            <span className="hint num" style={{ width: "2rem" }}>{a.position}.</span>
            <Link className="grow" to={`/venteliste/${a.id}`} style={{ color: "inherit", textDecoration: "none" }}>
              <b>{a.childName}</b> {a.hasSibling && <Pill tone="accent">Søskende</Pill>}
              <div className="hint">
                Født {fmtDate(a.dateOfBirth)} · ønsker start {fmtDate(a.desiredStart)} ({groupName(a.group).toLowerCase()}) · skrevet op {fmtDate(a.createdAt)}
              </div>
            </Link>
            <Pill tone={a.tone}>{a.status}</Pill>
          </div>
        ))}
        {q.data && q.data.applicants.length > 0 && (
          <div className="row">
            <span className="hint grow">Vælg nogle og skriv til dem. Adresserne kommer i Bcc, så forældrene ikke ser hinandens.</span>
            <a className={`btn ${mails.length ? "" : "disabled"}`} href={`mailto:?bcc=${encodeURIComponent(mails.join(","))}`} aria-disabled={!mails.length}>
              Skriv til de valgte ({selected.size})
            </a>
          </div>
        )}
      </Panel>

      <Panel title="Jeres tilmeldingslink" id="link">
        <div className="panel-b">
          <p>Læg linket på jeres hjemmeside. Forældre skriver sig op der, og de kommer direkte på listen.</p>
          <div className="inline">
            <code className="message" style={{ flex: "1 1 20rem" }}>{signupUrl}</code>
            <CopyButton text={signupUrl} />
          </div>
        </div>
      </Panel>
    </>
  );
}
