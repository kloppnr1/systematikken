import { Link, useNavigate } from "react-router";
import { useQueryClient } from "@tanstack/react-query";
import { api } from "../api";
import { useMe } from "../session";
import { Tile, useTitle } from "../ui";
import logo from "../assets/logo.png";
import puzzle from "../assets/puzzle.png";

const features = [
  {
    icon: "list" as const, tone: "butter" as const, title: "Venteliste med tilmelding",
    text: "Forældre skriver sig op via et link på jeres hjemmeside. I ser listen i rækkefølge, tilbyder en plads, og forældrene svarer ja eller nej via et link.",
  },
  {
    icon: "coin" as const, tone: "sage" as const, title: "Opkrævning via Betalingsservice",
    text: "Pris, halve måneder, friplads, søskenderabat, kostordning og depositum regnes ud for jer. Godkend måneden, hent filen, og send den til Betalingsservice.",
  },
  {
    icon: "check" as const, tone: "sage" as const, title: "Betalinger og bogføringsbilag",
    text: "Læg bankens betalingsfil ind, så ved I, hvem der har betalt. Bogholderen får et bilag med tallene for måneden.",
  },
  {
    icon: "person" as const, tone: "pink" as const, title: "Pladser måned for måned",
    text: "Se hvor mange vuggestue- og børnehavebørn I har, og hvornår der bliver ledigt, med børn der skal i skole regnet med.",
  },
];

const steps = [
  ["Log ind", "med jeres Google- eller Microsoft-konto. Vi gemmer ingen adgangskoder."],
  ["Opret institutionen", "og skriv jeres priser og Betalingsservice-aftale ind."],
  ["Del tilmeldingslinket", "og indmeld de børn, der går hos jer i dag."],
] as const;

const faq = [
  ["Hvad koster det?", "Ingenting. Systematikken er gratis og bliver ved med at være det."],
  ["Hvor ligger vores data?", "På en server i EU (Helsinki, Finland). Hver institution ser kun sine egne data, og venteliste-oplysninger slettes automatisk efter den tid, I vælger."],
  ["Sender systemet mails til forældrene?", "Nej. Tilbud og påmindelser bliver skrevet for jer, og I sender dem fra jeres egen mail eller telefon. Opkrævningen går gennem Betalingsservice."],
  ["Hvad med CPR-numre?", "Dem gemmer vi ikke. Opkrævningen bruger et betalingsnummer pr. barn."],
  ["Kan andre se koden?", "Ja. Systematikken er open source, så alle kan se, hvordan den regner."],
] as const;

/** The front page for people who are not logged in: what Systematikken is, the demo, and how to start. */
export function Landing() {
  useTitle("Gratis system til private daginstitutioner");
  const me = useMe();
  const qc = useQueryClient();
  const navigate = useNavigate();

  const demoButton = me.data?.demo ? (
    <button
      className="btn primary"
      onClick={async () => {
        await api.post("/auth/demo");
        await qc.invalidateQueries();
        navigate("/");
      }}
    >
      Prøv demoen
    </button>
  ) : (
    <a className="btn primary" href="https://demo.systematikken.dk">Prøv demoen</a>
  );

  return (
    <div className="landing">
      <header className="landing-top">
        <img className="brand-logo" src={logo} alt="Systematikken" />
        <Link className="btn" to="/login">Log ind</Link>
      </header>

      <section className="hero">
        <div className="hero-text">
          <h1>Mindre tid på administration. Mere tid til børnene.</h1>
          <p className="lead">
            Systematikken samler venteliste, børn og forældrebetaling for private daginstitutioner ét sted.
            Lavet til ledere, der ikke sidder ved en computer hele dagen. Og det er gratis.
          </p>
          <div className="inline">
            {demoButton}
            <Link className="btn" to="/login">Kom i gang</Link>
          </div>
          <p className="hint">Demoen har en opdigtet institution. Ingen login, ingen tilmelding.</p>
        </div>
        <img className="hero-art" src={puzzle} alt="" />
      </section>

      <section className="features" aria-label="Det kan Systematikken">
        {features.map((f) => (
          <article key={f.title} className="feature">
            <Tile icon={f.icon} tone={f.tone} />
            <h2>{f.title}</h2>
            <p>{f.text}</p>
          </article>
        ))}
      </section>

      <section className="panel soft how">
        <div className="panel-h"><h2>Sådan kommer I i gang</h2></div>
        <ol className="how-steps">
          {steps.map(([title, text], i) => (
            <li key={title}>
              <span className="how-n" aria-hidden="true">{i + 1}</span>
              <div><b>{title}</b> {text}</div>
            </li>
          ))}
        </ol>
      </section>

      <section className="faq" aria-labelledby="faq-h">
        <h2 id="faq-h">Spørgsmål</h2>
        {faq.map(([q, a]) => (
          <details key={q}>
            <summary>{q}</summary>
            <p>{a}</p>
          </details>
        ))}
      </section>

      <footer className="landing-foot">
        <span>Systematikken – fra Klopp Consulting</span>
        <a href="https://github.com/kloppnr1/systematikken">Open source på GitHub</a>
        <a href="https://systematikken.dk">systematikken.dk</a>
      </footer>
    </div>
  );
}
