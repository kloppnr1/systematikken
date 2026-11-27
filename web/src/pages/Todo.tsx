import { Link } from "react-router";
import { useQuery } from "@tanstack/react-query";
import { api } from "../api";
import { useMe } from "../session";
import { Empty, Failed, Loading, Panel, Puzzle, Tile, useTitle } from "../ui";

type Item = { id: string; title: string; detail: string; link: string; action: string; important: boolean };

/** Which tile goes with which kind of item. Pink means "look here", sage means good news, butter means news. */
function tileFor(i: Item): Parameters<typeof Tile>[0] {
  if (i.id.startsWith("enrol-")) return { icon: "check", tone: "sage" };
  if (i.id.startsWith("payer-")) return { icon: "person", tone: "pink" };
  if (i.id.startsWith("remind-")) return { icon: "clock", tone: "butter" };
  if (i.id.startsWith("import-")) return { icon: "upload", tone: "sage" };
  if (i.id === "owing") return { icon: "coin", tone: "pink" };
  if (i.id === "billing") return i.important && i.title.includes("rettes") ? { icon: "alert", tone: "pink" } : { icon: "coin", tone: "sage" };
  if (i.id === "new") return { icon: "list", tone: "butter" };
  return { icon: "puzzle", tone: "sage" };
}

function greeting(now: Date) {
  const h = now.getHours();
  return h < 10 ? "Godmorgen" : h < 17 ? "Goddag" : "Godaften";
}

/** The home screen, like an e-mail inbox: what needs doing, each with the button that does it. */
export function Todo() {
  useTitle("Opgaver");
  const me = useMe();
  const q = useQuery({ queryKey: ["todo"], queryFn: () => api.get<{ items: Item[]; setup: Item[] }>("/api/todo") });
  const now = new Date();
  const first = (me.data?.name ?? "").split(/[ @]/)[0];
  const firstName = first ? first[0].toUpperCase() + first.slice(1) : "";
  const count = q.data?.items.length ?? 0;

  return (
    <>
      <div className="greeting">
        <div>
          <p className="muted" style={{ margin: 0 }}>{now.toLocaleDateString("da-DK", { weekday: "long", day: "numeric", month: "long" }).replace(/^./, (c) => c.toUpperCase())}</p>
          <h1>{greeting(now)}{firstName && `, ${firstName}`}</h1>
        </div>
        {q.data && <span className="waiting">{count === 0 ? "Intet venter" : count === 1 ? "1 ting venter" : `${count} ting venter`}</span>}
      </div>

      {q.isPending && <Loading />}
      {q.error && <Failed error={q.error} />}
      {q.data && q.data.items.length === 0 && q.data.setup.length === 0 && (
        <Panel><Empty><b>Alt er på plads.</b><br />Der er ikke noget, der venter på dig lige nu.</Empty></Panel>
      )}
      {q.data && q.data.items.length > 0 && (
        <section className="panel" aria-label="Det venter">
          {q.data.items.map((i) => <Row key={i.id} item={i} />)}
        </section>
      )}
      {q.data && q.data.setup.length > 0 && (
        <section className="panel soft" aria-labelledby="setup">
          <div className="panel-h" style={{ alignItems: "center" }}>
            <div style={{ display: "flex", gap: "1rem", alignItems: "center" }}>
              <Puzzle size={52} />
              <div>
                <h2 id="setup">Kom godt i gang</h2>
                <span className="hint">{q.data.setup.length} brikker mangler, før alt er på plads.</span>
              </div>
            </div>
          </div>
          {q.data.setup.map((i) => <Row key={i.id} item={i} />)}
        </section>
      )}
    </>
  );
}

function Row({ item }: { item: Item }) {
  return (
    <div className={`row ${item.important ? "important" : ""}`}>
      <Tile {...tileFor(item)} />
      <div className="grow">
        <div className="title">{item.title}</div>
        <div className="hint">{item.detail}</div>
      </div>
      <Link className={`btn ${item.important ? "primary" : ""}`} to={item.link}>{item.action}</Link>
    </div>
  );
}
