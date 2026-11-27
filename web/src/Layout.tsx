import { useEffect, useState } from "react";
import { NavLink, Navigate, Outlet, useLocation, useNavigate } from "react-router";
import { api } from "./api";
import { useMe } from "./session";
import { Loading } from "./ui";
import logo from "./assets/logo.png";

const tabs = [
  { to: "/", label: "Opgaver", end: true },
  { to: "/born", label: "Børn" },
  { to: "/venteliste", label: "Venteliste" },
  { to: "/opkraevning", label: "Opkrævning" },
  { to: "/pladser", label: "Pladser" },
];

/**
 * The app shell for signed-in staff. The menu always sits at the top: on a wide screen as a row,
 * on a phone behind a "Menu" button, the way most websites do it.
 */
export function Layout() {
  const me = useMe();
  const location = useLocation();
  const navigate = useNavigate();
  const [q, setQ] = useState("");
  const [open, setOpen] = useState(false);

  // Close the phone menu after moving to another page.
  useEffect(() => setOpen(false), [location.pathname, location.search]);

  if (me.isPending) return <main className="wrap"><Loading /></main>;
  if (!me.data?.signedIn) return <Navigate to={`/login?returnUrl=${encodeURIComponent(location.pathname)}`} replace />;
  if (!me.data.institution) return <Navigate to="/start" replace />;

  const search = (
    <form
      className="search"
      role="search"
      onSubmit={(e) => {
        e.preventDefault();
        if (q.trim().length >= 2) navigate(`/soeg?q=${encodeURIComponent(q.trim())}`);
      }}
    >
      <input type="search" value={q} onChange={(e) => setQ(e.target.value)} placeholder="Søg barn, forælder eller telefon" aria-label="Søg" />
    </form>
  );

  return (
    <>
      {me.data.demo && (
        <div className="demo-bar">Demo med en opdigtet institution. Alt bliver nulstillet i nat.</div>
      )}
      <header className="top">
        <div className="top-in">
          <NavLink className="brand" to="/" aria-label={`Systematikken – ${me.data.institution.name}`}>
            <img className="brand-logo" src={logo} alt="" />
          </NavLink>
          <nav className="tabs" aria-label="Hovedmenu">
            {tabs.map((t) => (
              <NavLink key={t.to} to={t.to} end={t.end}>{t.label}</NavLink>
            ))}
          </nav>
          {search}
          <NavLink className="settings" to="/indstillinger">Indstillinger</NavLink>
          <button className="menu-button" aria-expanded={open} aria-controls="phone-menu" onClick={() => setOpen(!open)}>
            <svg viewBox="0 0 24 24" width="20" height="20" fill="none" stroke="currentColor" strokeWidth={2} strokeLinecap="round" aria-hidden="true">
              {open ? <path d="M6 6l12 12M18 6L6 18" /> : <path d="M4 7h16M4 12h16M4 17h16" />}
            </svg>
            {open ? "Luk" : "Menu"}
          </button>
        </div>
        {open && (
          <nav className="phone-menu" id="phone-menu" aria-label="Hovedmenu">
            {search}
            {tabs.map((t) => (
              <NavLink key={t.to} to={t.to} end={t.end}>{t.label}</NavLink>
            ))}
            <NavLink to="/opkraevning/betalinger">Indlæs betalinger</NavLink>
            <NavLink to="/indstillinger">Indstillinger</NavLink>
            <button
              className="btn link"
              onClick={async () => {
                await api.post("/auth/logout");
                window.location.href = "/login";
              }}
            >
              Log ud
            </button>
          </nav>
        )}
      </header>
      <main className="wrap">
        <Outlet />
      </main>
    </>
  );
}
