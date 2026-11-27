import { Link } from "react-router";
import { api } from "../api";
import { useMe } from "../session";
import { Panel, useTitle } from "../ui";

/** The fifth tab on phones: what does not fit in the bottom bar. */
export function More() {
  useTitle("Mere");
  const me = useMe();
  return (
    <>
      <h1>Mere</h1>
      <Panel>
        <Link className="row" to="/pladser">Pladser<span className="hint">Børn og ledige pladser måned for måned</span></Link>
        <Link className="row" to="/opkraevning/betalinger">Indlæs betalinger</Link>
        <Link className="row" to="/indstillinger">Indstillinger</Link>
      </Panel>
      <p className="hint">Logget ind som {me.data?.email}</p>
      <button className="btn" onClick={async () => { await api.post("/auth/logout"); location.href = "/login"; }}>Log ud</button>
    </>
  );
}
