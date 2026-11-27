import { Tile, useTitle } from "../ui";
import logo from "../assets/logo.png";
import puzzle from "../assets/puzzle.png";

const features = [
  {
    icon: "list" as const, tone: "butter" as const, title: "Venteliste med tilmelding",
    text: "Forældre skriver sig op fra jeres hjemmeside. I tilbyder pladsen, de svarer via et link.",
  },
  {
    icon: "coin" as const, tone: "sage" as const, title: "Opkrævning via Betalingsservice",
    text: "Friplads, søskenderabat, kostordning og halve måneder regnes ud. I godkender og sender.",
  },
  {
    icon: "check" as const, tone: "sage" as const, title: "Betalinger og bogføringsbilag",
    text: "Se hvem der har betalt. Bogholderen får et bilag for måneden.",
  },
  {
    icon: "person" as const, tone: "pink" as const, title: "Pladser måned for måned",
    text: "Se hvornår der bliver ledige pladser i vuggestue og børnehave.",
  },
];

const record = [
  ["7", "institutioner"],
  ["1.869", "børn"],
  ["4.000+", "års børnepasning"],
  ["135+ mio.", "kr. opkrævet via Betalingsservice"],
] as const;

const steps = [
  ["Log ind", "med Google eller Microsoft."],
  ["Opret institutionen", "med priser og Betalingsservice-aftale."],
  ["Del tilmeldingslinket", "og indmeld jeres børn."],
] as const;

const faq = [
  ["Hvad koster det?", "Ingenting. Ikke gratis i 30 dage, men gratis. Gebyrer til Betalingsservice betaler I som i dag."],
  ["Vi bruger Tid til Børn i dag. Hvordan skifter vi?", "Kontakt os, så flytter vi jeres data gratis."],
  ["Hvor ligger vores data?", "I EU. Hver institution ser kun sine egne data."],
  ["Hvad med CPR-numre?", "Dem gemmer vi ikke."],
  ["Hvorfor open source?", "Så I ikke er afhængige af én leverandør. Alle kan køre systemet videre."],
] as const;

const APP = "https://app.systematikken.dk";
const DEMO = "https://demo.systematikken.dk";

/** systematikken.dk: what Systematikken is, the demo, and how to start. The app itself is on app.systematikken.dk. */
export function Landing() {
  useTitle("Gratis system til private daginstitutioner");

  return (
    <div className="landing">
      <header className="landing-top">
        <img className="brand-logo" src={logo} alt="Systematikken" />
        <a className="btn" href={`${APP}/login`}>Log ind</a>
      </header>

      <section className="hero">
        <div className="hero-text">
          <h1>Mindre tid på administration. Mere tid til børnene.</h1>
          <p className="lead">
            Venteliste, børn og forældrebetaling for private daginstitutioner. Ét sted, og gratis.
          </p>
          <div className="inline">
            <a className="btn primary" href={DEMO}>Prøv demoen</a>
            <a className="btn" href={`${APP}/login`}>Kom i gang</a>
          </div>
          <p className="hint">Ingen login, ingen tilmelding.</p>
        </div>
        <img className="hero-art" src={puzzle} alt="" />
      </section>

      <section className="record" aria-labelledby="record-h">
        <div className="record-text">
          <h2 id="record-h">Bygget på 10 års erfaring</h2>
          <p>
            Systematikken afløser Tid til Børn og er skrevet forfra. De største institutioner har over 120 børn.
          </p>
        </div>
        <dl className="record-stats">
          {record.map(([n, label]) => (
            <div key={label}>
              <dt>{label}</dt>
              <dd>{n}</dd>
            </div>
          ))}
        </dl>
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

      <section className="panel soft contact" aria-labelledby="contact-h">
        <div className="panel-h"><h2 id="contact-h">Kontakt</h2></div>
        <p>Skriv eller ring til Martin.</p>
        <ul className="contact-list">
          <li><a href="mailto:martin@kloppconsulting.dk">martin@kloppconsulting.dk</a></li>
          <li><a href="tel:+4553687494">+45 53 68 74 94</a></li>
          <li className="muted">Klopp Consulting, Mariane Thomsens Gade 36, 8000 Aarhus C</li>
        </ul>
      </section>

      <footer className="landing-foot">
        <span>Systematikken – fra Klopp Consulting</span>
        <a href="https://github.com/kloppnr1/systematikken">Open source på GitHub</a>
        <a href="https://systematikken.dk">systematikken.dk</a>
      </footer>
    </div>
  );
}
