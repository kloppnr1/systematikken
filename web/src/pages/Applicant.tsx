import { Link, Navigate, useNavigate, useParams } from "react-router";
import { useQuery, useQueryClient } from "@tanstack/react-query";
import { api } from "../api";
import { fmtDate, toDateInput } from "../format";
import { CopyButton, Failed, Field, Loading, Panel, Pill, formValues, useTitle, useToast } from "../ui";

type Applicant = {
  id: string; childName: string; dateOfBirth: string; desiredStart: string; hasSibling: boolean; note: string; createdAt: string;
  offeredAt: string | null; offeredStart: string | null; childId: string | null;
  state: "Waiting" | "Offered" | "Accepted" | "Declined" | "Enrolled" | "Removed"; status: string; tone: string;
  parents: { name: string; email: string; phone: string }[];
  address: string;
  message: { reminder: boolean; subject: string; text: string; emails: string[]; phone: string } | null;
};

/**
 * One applicant: offer a place, see the answer, turn a yes into a child.
 * No e-mail server: the manager sends the message from their own mail or phone, with text and link filled in.
 */
export function ApplicantPage() {
  const { id } = useParams();
  const q = useQuery({ queryKey: ["applicant", id], queryFn: () => api.get<Applicant>(`/api/applicants/${id}`) });
  const qc = useQueryClient();
  const toast = useToast();
  const navigate = useNavigate();
  useTitle(q.data?.childName ?? "Venteliste");

  if (q.isPending) return <Loading />;
  if (q.error) return <Failed error={q.error} />;
  const a = q.data;
  if (a.state === "Enrolled" && a.childId) return <Navigate to={`/born/${a.childId}`} replace />;
  const base = `/api/applicants/${a.id}`;
  const first = a.childName.split(" ")[0];

  const act = async (fn: () => Promise<unknown>, text: string, undo?: () => Promise<unknown>) => {
    try {
      await fn();
      await qc.invalidateQueries();
      toast(text, undo ? async () => { await undo(); await qc.invalidateQueries(); } : undefined);
    } catch (e) {
      toast(e instanceof Error ? e.message : "Noget gik galt");
    }
  };

  const m = a.message;
  const mailto = m && `mailto:${m.emails.join(",")}?subject=${encodeURIComponent(m.subject)}&body=${encodeURIComponent(m.text)}`;
  const sms = m && `sms:${m.phone}?&body=${encodeURIComponent(m.text)}`;

  return (
    <>
      <Link className="back" to="/venteliste">← Venteliste</Link>
      <div className="head">
        <h1>{a.childName}</h1>
        <Pill tone={a.tone}>{a.status}</Pill>
      </div>
      <p className="muted">
        Født {fmtDate(a.dateOfBirth)} · ønsker start {fmtDate(a.desiredStart)} · skrevet op {fmtDate(a.createdAt)}
        {a.hasSibling && " · har søskende her"}
      </p>

      <div className="side">
        <div>
          {a.state === "Waiting" && (
            <Panel title="Tilbyd en plads">
              <form
                className="panel-b inline"
                onSubmit={(e) => {
                  e.preventDefault();
                  const start = String(formValues(e.currentTarget).start);
                  act(() => api.post(`${base}/offer`, { start }), "Pladsen er tilbudt. Send beskeden.", () => api.post(`${base}/restore`));
                }}
              >
                <label htmlFor="start" style={{ margin: 0 }}>Fra</label>
                <input id="start" name="start" type="date" defaultValue={toDateInput(a.desiredStart)} required />
                <button className="btn primary" type="submit">Tilbyd plads</button>
              </form>
            </Panel>
          )}

          {a.state === "Offered" && m && (
            <Panel title={m.reminder ? "Send en påmindelse" : "Send tilbuddet"}>
              <div className="panel-b">
                <p className="hint">Teksten og svarlinket er skrevet for dig. Send den fra din egen mail eller telefon.</p>
                <pre className="message">{m.text}</pre>
                <p className="inline" style={{ marginTop: ".75rem" }}>
                  {m.emails.length > 0 && <a className="btn primary" href={mailto!}>Åbn i mail</a>}
                  {m.phone && <a className="btn" href={sms!}>Send som sms</a>}
                  <CopyButton text={m.text} label="Kopiér tekst" />
                </p>
                <p className="hint">
                  Til: {m.emails.length ? m.emails.join(", ") : "ingen e-mail"}{m.phone && ` · ${m.phone}`}
                </p>
              </div>
              <div className="row">
                <span className="grow hint">Har forælderen svaret på anden måde?</span>
                <button className="btn" onClick={() => act(() => api.post(`${base}/answer`, { yes: true }), `${a.childName} har sagt ja`)}>Sagde ja</button>
                <button className="btn" onClick={() => act(() => api.post(`${base}/answer`, { yes: false }), `${a.childName} har sagt nej`)}>Sagde nej</button>
              </div>
            </Panel>
          )}

          {a.state === "Accepted" && (
            <section className="panel done-hero">
              <h2>{a.childName} har sagt ja til pladsen fra {fmtDate(a.offeredStart)}</h2>
              <p>Indmeld {first}, så kommer barnet og forældrene med i opkrævningen. Den første forælder bliver betaler.</p>
              <button
                className="btn primary big"
                onClick={async () => {
                  try {
                    const child = await api.post<{ id: string; name: string }>(`${base}/enrol`);
                    await qc.invalidateQueries();
                    toast(`${child.name} er indmeldt`);
                    navigate(`/born/${child.id}`);
                  } catch (e) {
                    toast(e instanceof Error ? e.message : "Noget gik galt");
                  }
                }}
              >
                Indmeld {first}
              </button>
            </section>
          )}

          {(a.state === "Declined" || a.state === "Removed") && (
            <Panel title={a.state === "Declined" ? "Sagde nej til pladsen" : "Fjernet fra ventelisten"}>
              <div className="panel-b">
                <p className="hint">Bliver slettet automatisk efter den tid, I har valgt i indstillingerne.</p>
                <button className="btn" onClick={() => act(() => api.post(`${base}/restore`), `${a.childName} er tilbage på ventelisten`)}>Sæt tilbage på ventelisten</button>
              </div>
            </Panel>
          )}

          <Panel title="Oplysninger">
            <form
              className="panel-b"
              onSubmit={(e) => {
                e.preventDefault();
                const v = formValues(e.currentTarget);
                act(() => api.put(base, { childName: v.childName, dateOfBirth: v.dateOfBirth, desiredStart: v.desiredStart, hasSibling: v.hasSibling, note: v.note }), "Gemt");
              }}
            >
              <div className="grid">
                <Field label="Barnets navn" name="childName"><input id="childName" name="childName" defaultValue={a.childName} required /></Field>
                <Field label="Fødselsdato" name="dateOfBirth"><input id="dateOfBirth" name="dateOfBirth" type="date" defaultValue={toDateInput(a.dateOfBirth)} required /></Field>
                <Field label="Ønsket start" name="desiredStart"><input id="desiredStart" name="desiredStart" type="date" defaultValue={toDateInput(a.desiredStart)} required /></Field>
                <label className="check"><input type="checkbox" name="hasSibling" defaultChecked={a.hasSibling} /> Har søskende her</label>
              </div>
              <Field label="Note" name="note"><textarea id="note" name="note" defaultValue={a.note} /></Field>
              <button className="btn" type="submit">Gem</button>
            </form>
          </Panel>
        </div>

        <aside>
          <Panel title="Forældre">
            {a.parents.map((p) => (
              <div className="row" key={p.name + p.email}>
                <div className="grow">
                  <b>{p.name}</b>
                  <div className="hint">
                    {p.phone && <a href={`tel:${p.phone}`}>{p.phone}</a>}
                    {p.phone && p.email && " · "}
                    {p.email && <a href={`mailto:${p.email}`}>{p.email}</a>}
                  </div>
                </div>
              </div>
            ))}
            <div className="row hint">{a.address}</div>
          </Panel>
          {(a.state === "Waiting" || a.state === "Offered" || a.state === "Accepted") && (
            <button className="btn danger" onClick={() => act(() => api.post(`${base}/remove`), `${a.childName} er fjernet fra ventelisten`, () => api.post(`${base}/restore`))}>
              Fjern fra ventelisten
            </button>
          )}
        </aside>
      </div>
    </>
  );
}
