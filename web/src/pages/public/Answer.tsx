import { useParams } from "react-router";
import { useQuery, useQueryClient } from "@tanstack/react-query";
import { api } from "../../api";
import { fmtDate } from "../../format";
import { Loading, useTitle } from "../../ui";
import { PublicShell } from "./Signup";

type Offer = { childName: string; institutionName: string; offeredStart: string | null; state: string };

/** The parent's answer to an offered place, from the link in the manager's message. */
export function Answer() {
  useTitle("Svar på plads");
  const { token } = useParams();
  const key = ["answer", token];
  const q = useQuery({ queryKey: key, queryFn: () => api.get<Offer>(`/api/public/answer/${token}`) });
  const qc = useQueryClient();

  const answer = async (yes: boolean) => {
    try {
      await api.post(`/api/public/answer/${token}`, { yes });
    } finally {
      await qc.invalidateQueries({ queryKey: key });
    }
  };

  if (q.isPending) return <PublicShell><Loading /></PublicShell>;
  if (q.error)
    return (
      <PublicShell>
        <header><h1>Linket virker ikke</h1></header>
        <p>Linket er forkert eller for gammelt. Kontakt institutionen.</p>
      </PublicShell>
    );

  const a = q.data;
  const first = a.childName.split(" ")[0];
  return (
    <PublicShell>
      {a.state === "Offered" && (
        <>
          <header><h1>Plads til {a.childName}</h1><p className="muted">{a.institutionName}</p></header>
          <section className="panel panel-b" style={{ paddingTop: "1rem" }}>
            <p>Vi kan tilbyde {first} en plads fra <b>{fmtDate(a.offeredStart)}</b>. Vil I have den?</p>
            <div className="inline">
              <button className="btn primary" onClick={() => answer(true)}>Ja tak</button>
              <button className="btn" onClick={() => answer(false)}>Nej tak</button>
            </div>
          </section>
        </>
      )}
      {(a.state === "Accepted" || a.state === "Enrolled") && (
        <>
          <header><h1>Velkommen</h1></header>
          <section className="panel done-hero">
            <p>I har sagt ja til pladsen til {a.childName} fra {fmtDate(a.offeredStart)}. {a.institutionName} kontakter jer om det praktiske.</p>
          </section>
        </>
      )}
      {a.state === "Declined" && (
        <>
          <header><h1>Tak for svaret</h1></header>
          <p>I har sagt nej til pladsen. Skulle I fortryde, så kontakt {a.institutionName}.</p>
        </>
      )}
      {(a.state === "Waiting" || a.state === "Removed") && (
        <>
          <header><h1>Tilbuddet gælder ikke længere</h1></header>
          <p>Kontakt {a.institutionName}, hvis I har spørgsmål.</p>
        </>
      )}
    </PublicShell>
  );
}
