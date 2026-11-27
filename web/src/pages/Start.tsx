import { useState } from "react";
import { Link, Navigate, useNavigate, useParams } from "react-router";
import { useQuery, useQueryClient } from "@tanstack/react-query";
import { api } from "../api";
import { monthName } from "../format";
import { useMe } from "../session";
import { Failed, Field, Loading, Panel, useFieldErrors, useTitle } from "../ui";

type Municipality = { id: number; name: string; confirmed: boolean; cutoverYears: number; cutoverMonths: number; schoolStartMonth: number };

async function logout() {
  await api.post("/auth/logout");
  location.href = "/login";
}

/** First visit: create your institution, or join one you were invited to. */
export function Start() {
  useTitle("Kom i gang");
  const me = useMe();
  const municipalities = useQuery({ queryKey: ["municipalities"], queryFn: () => api.get<Municipality[]>("/api/municipalities") });
  const [municipalityId, setMunicipalityId] = useState("");
  const errors = useFieldErrors();
  const qc = useQueryClient();
  const navigate = useNavigate();

  if (me.isPending) return <main className="wrap"><Loading /></main>;
  if (!me.data?.signedIn) return <Navigate to="/login" replace />;
  if (me.data.institution) return <Navigate to="/" replace />;

  const chosen = municipalities.data?.find((m) => String(m.id) === municipalityId);

  return (
    <main className="wrap">
      <div className="narrow">
        <h1>Kom i gang</h1>

        {me.data.invitations && me.data.invitations.length > 0 && (
          <Panel title="Du er inviteret">
            {me.data.invitations.map((i) => (
              <div className="row" key={i.token}>
                <span className="grow">{i.institutionName}</span>
                <Link className="btn primary" to={`/invitation/${i.token}`}>Bliv en del af {i.institutionName}</Link>
              </div>
            ))}
          </Panel>
        )}

        <form
          className="panel"
          onSubmit={async (e) => {
            e.preventDefault();
            errors.clear();
            const f = new FormData(e.currentTarget);
            try {
              await api.post("/api/institutions", {
                name: f.get("name"),
                municipalityId: municipalityId ? Number(municipalityId) : null,
                contactEmail: f.get("contactEmail"),
                cutoverYears: Number(f.get("cutoverYears") ?? 2),
                cutoverMonths: Number(f.get("cutoverMonths") ?? 11),
                schoolStartMonth: Number(f.get("schoolStartMonth") ?? 8),
              });
              await qc.invalidateQueries({ queryKey: ["me"] });
              navigate("/", { replace: true });
            } catch (err) {
              errors.handle(err);
            }
          }}
        >
          <div className="panel-h"><h2>Opret din institution</h2></div>
          <div className="panel-b">
            {errors.message && <p className="error">{errors.message}</p>}
            <Field label="Institutionens navn" name="name" error={errors.errors.name}>
              <input id="name" name="name" autoComplete="organization" required />
            </Field>
            <Field label="Kommune" name="municipalityId" error={errors.errors.municipalityId}>
              {municipalities.error ? <Failed error={municipalities.error} /> : (
                <select id="municipalityId" value={municipalityId} onChange={(e) => setMunicipalityId(e.target.value)} required>
                  <option value="">Vælg kommune</option>
                  {municipalities.data?.map((m) => <option key={m.id} value={m.id}>{m.name}</option>)}
                </select>
              )}
            </Field>
            <Field label="E-mail forældre kan skrive til" name="contactEmail" error={errors.errors.contactEmail}>
              <input id="contactEmail" name="contactEmail" type="email" defaultValue={me.data.email} required />
            </Field>

            {chosen && !chosen.confirmed && (
              <div className="panel" style={{ background: "var(--bg)" }}>
                <div className="panel-h"><h2>{chosen.name} Kommunes regler</h2></div>
                <div className="panel-b">
                  <p className="hint">I er de første i {chosen.name} Kommune. Reglerne gælder alle institutioner i kommunen, så tjek dem.</p>
                  <div className="grid">
                    <Field label="Overgang til børnehave ved alder" name="cutoverYears" error={errors.errors.cutoverYears}>
                      <div className="inline">
                        <input id="cutoverYears" name="cutoverYears" type="number" min={1} max={4} defaultValue={2} style={{ width: "5rem" }} /> år
                        <input name="cutoverMonths" type="number" min={0} max={11} defaultValue={11} style={{ width: "5rem" }} aria-label="Måneder" /> mdr.
                      </div>
                    </Field>
                    <Field label="Skolestart" name="schoolStartMonth">
                      <select id="schoolStartMonth" name="schoolStartMonth" defaultValue={8}>
                        {Array.from({ length: 12 }, (_, i) => <option key={i} value={i + 1}>{monthName(i + 1)}</option>)}
                      </select>
                    </Field>
                  </div>
                </div>
              </div>
            )}
            {chosen?.confirmed && (
              <p className="hint">
                {chosen.name}: børnehave fra {chosen.cutoverYears} år og {chosen.cutoverMonths} mdr., skolestart i {monthName(chosen.schoolStartMonth)}.
              </p>
            )}

            <button className="btn primary big" type="submit">Opret institution</button>
          </div>
        </form>
        <button className="btn link" onClick={logout}>Log ud</button>
      </div>
    </main>
  );
}

/** A colleague opens the invitation link and logs in with the e-mail address they were invited with. */
export function InvitationPage() {
  useTitle("Invitation");
  const { token } = useParams();
  const me = useMe();
  const inv = useQuery({
    queryKey: ["invitation", token],
    queryFn: () => api.get<{ institutionName: string; invitedEmail: string; matches: boolean }>(`/api/invitations/${token}`),
    enabled: !!me.data?.signedIn,
  });
  const qc = useQueryClient();
  const navigate = useNavigate();
  const [error, setError] = useState<string | null>(null);

  if (me.isPending) return <main className="wrap"><Loading /></main>;
  if (!me.data?.signedIn) return <Navigate to={`/login?returnUrl=${encodeURIComponent(`/invitation/${token}`)}`} replace />;

  return (
    <main className="wrap">
      <div className="narrow">
        <h1>Invitation</h1>
        {inv.isPending && <Loading />}
        {inv.error && <p className="error">Invitationen findes ikke eller er udløbet. Bed om en ny.</p>}
        {inv.data && !inv.data.matches && (
          <>
            <p className="error">Invitationen er sendt til {inv.data.invitedEmail}, men du er logget ind som {me.data.email}. Log ud og log ind med den rigtige konto.</p>
            <button className="btn" onClick={logout}>Log ud</button>
          </>
        )}
        {inv.data?.matches && (
          <>
            <p>Du er inviteret til at hjælpe med at drive <b>{inv.data.institutionName}</b> i Systematikken.</p>
            {error && <p className="error">{error}</p>}
            <button
              className="btn primary big"
              onClick={async () => {
                try {
                  await api.post(`/api/invitations/${token}/accept`);
                  await qc.invalidateQueries();
                  navigate("/", { replace: true });
                } catch (e) {
                  setError(e instanceof Error ? e.message : "Noget gik galt.");
                }
              }}
            >
              Bliv en del af {inv.data.institutionName}
            </button>
          </>
        )}
      </div>
    </main>
  );
}
