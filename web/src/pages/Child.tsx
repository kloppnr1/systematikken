import { useState } from "react";
import { Link, useParams } from "react-router";
import { useQuery, useQueryClient } from "@tanstack/react-query";
import { api } from "../api";
import { fmtDate, fmtKr, fmtMonth, groupName, parseAmount, toDateInput, toMonthInput } from "../format";
import { Failed, Field, Loading, Panel, Pill, formValues, useFieldErrors, useTitle, useToast } from "../ui";

type Parent = { id: string; name: string; email: string; phone: string; address: string; postCode: string; city: string };
type ChildCard = {
  id: string; firstName: string; lastName: string; name: string; dateOfBirth: string; startDate: string | null; endDate: string | null;
  isPartTime: boolean; note: string; paymentNumber: number; payerId: string | null; group: string; status: string; tone: string;
  parents: Parent[];
  subsidies: { id: number; fromMonth: string; toMonth: string | null; freePlace: number; siblingDiscount: number }[];
  manual: { id: number; text: string; amount: number; month: string }[];
  ledger: { id: number; month: string; text: string; amount: number; kind: string }[];
  balance: number;
  nextMonth: string;
};

/** Everything about one child on one page, like a card in the phone's contacts app. */
export function ChildPage() {
  const { id } = useParams();
  const key = ["child", id];
  const q = useQuery({ queryKey: key, queryFn: () => api.get<ChildCard>(`/api/children/${id}`) });
  const qc = useQueryClient();
  const toast = useToast();
  useTitle(q.data?.name ?? "Barn");

  if (q.isPending) return <Loading />;
  if (q.error) return <Failed error={q.error} />;
  const c = q.data;
  const refresh = () => qc.invalidateQueries();
  const base = `/api/children/${c.id}`;

  /** Run an action, refresh, and show a toast. If an undo is given, the toast offers "Fortryd". */
  const act = async (fn: () => Promise<unknown>, text: string, undo?: () => Promise<unknown>) => {
    try {
      await fn();
      await refresh();
      toast(text, undo ? async () => { await undo(); await refresh(); } : undefined);
    } catch (e) {
      toast(e instanceof Error ? e.message : "Noget gik galt");
    }
  };

  return (
    <>
      <Link className="back" to="/born">← Børn</Link>
      <div className="head">
        <h1>{c.name}</h1>
        <Pill tone={c.tone}>{c.status}</Pill>
      </div>
      <p className="muted">{groupName(c.group)} · født {fmtDate(c.dateOfBirth)} · betalingsnummer {c.paymentNumber}</p>

      <div className="side">
        <div>
          <Panel title="Forældre">
            {c.parents.length === 0 && <p className="empty">Ingen forældre endnu. Tilføj mindst én, der kan betale.</p>}
            {c.parents.map((p) => (
              <ParentRow
                key={p.id}
                parent={p}
                isPayer={c.payerId === p.id}
                onSave={(body) => act(() => api.post(`${base}/parents`, { ...body, id: p.id }), `${body.name} er gemt`)}
                onPayer={() => {
                  const previous = c.payerId;
                  return act(() => api.put(`${base}/payer`, { parentId: p.id }), `${p.name} er nu betaler`,
                    previous ? () => api.put(`${base}/payer`, { parentId: previous }) : undefined);
                }}
                onRemove={() => act(() => api.del(`${base}/parents/${p.id}`), `${p.name} er fjernet fra ${c.firstName}`,
                  async () => {
                    const restored = await api.post<Parent>(`${base}/parents`, { ...p, id: null });
                    if (c.payerId === p.id) await api.put(`${base}/payer`, { parentId: restored.id });
                  })}
              />
            ))}
            <AddParent onAdd={(body) => act(() => api.post(`${base}/parents`, body), `${body.name} er tilføjet`)} />
          </Panel>

          <Subsidies card={c} act={act} />
          <Details card={c} onSaved={() => { refresh(); toast("Gemt"); }} />
        </div>

        <aside>
          <Account card={c} act={act} />
          <Manual card={c} act={act} />
        </aside>
      </div>
    </>
  );
}

type Act = (fn: () => Promise<unknown>, text: string, undo?: () => Promise<unknown>) => Promise<void>;

function ParentFields({ p }: { p?: Partial<Parent> }) {
  return (
    <div className="grid">
      <Field label="Navn" name="pname"><input id="pname" name="name" defaultValue={p?.name} required autoComplete="name" /></Field>
      <Field label="E-mail" name="pemail"><input id="pemail" name="email" type="email" defaultValue={p?.email} /></Field>
      <Field label="Telefon" name="pphone"><input id="pphone" name="phone" type="tel" defaultValue={p?.phone} /></Field>
      <Field label="Adresse" name="paddress"><input id="paddress" name="address" defaultValue={p?.address} /></Field>
      <Field label="Postnummer" name="ppost"><input id="ppost" name="postCode" defaultValue={p?.postCode} inputMode="numeric" pattern="\d{4}" /></Field>
      <Field label="By" name="pcity"><input id="pcity" name="city" defaultValue={p?.city} /></Field>
    </div>
  );
}

function ParentRow(props: { parent: Parent; isPayer: boolean; onSave: (b: Record<string, unknown>) => Promise<void>; onPayer: () => Promise<void>; onRemove: () => Promise<void> }) {
  const p = props.parent;
  const [editing, setEditing] = useState(false);
  return (
    <div className="row">
      <div className="grow">
        <b>{p.name}</b> {props.isPayer && <Pill tone="good">Betaler</Pill>}
        <div className="hint">
          {p.phone && <a href={`tel:${p.phone}`}>{p.phone}</a>}
          {p.phone && p.email && " · "}
          {p.email && <a href={`mailto:${p.email}`}>{p.email}</a>}
        </div>
        <div className="hint">{[p.address, [p.postCode, p.city].filter(Boolean).join(" ")].filter(Boolean).join(", ")}</div>
        {editing && (
          <form
            style={{ paddingTop: ".75rem" }}
            onSubmit={async (e) => {
              e.preventDefault();
              await props.onSave(formValues(e.currentTarget));
              setEditing(false);
            }}
          >
            <ParentFields p={p} />
            <div className="inline">
              <button className="btn primary" type="submit">Gem</button>
              <button className="btn link" type="button" onClick={() => setEditing(false)}>Annullér</button>
              <button className="btn link danger" type="button" onClick={props.onRemove}>Fjern fra barnet</button>
            </div>
          </form>
        )}
      </div>
      {!editing && (
        <div className="inline">
          {!props.isPayer && <button className="btn" onClick={props.onPayer}>Gør til betaler</button>}
          <button className="btn link" onClick={() => setEditing(true)}>Ret</button>
        </div>
      )}
    </div>
  );
}

function AddParent({ onAdd }: { onAdd: (b: Record<string, unknown>) => Promise<void> }) {
  const [open, setOpen] = useState(false);
  if (!open) return <div className="row"><button className="btn link" onClick={() => setOpen(true)}>Tilføj forælder</button></div>;
  return (
    <form
      className="row"
      onSubmit={async (e) => {
        e.preventDefault();
        await onAdd(formValues(e.currentTarget));
        setOpen(false);
      }}
    >
      <div style={{ width: "100%" }}>
        <ParentFields />
        <div className="inline">
          <button className="btn primary" type="submit">Tilføj</button>
          <button className="btn link" type="button" onClick={() => setOpen(false)}>Annullér</button>
        </div>
      </div>
    </form>
  );
}

function Subsidies({ card: c, act }: { card: ChildCard; act: Act }) {
  const errors = useFieldErrors();
  const base = `/api/children/${c.id}`;
  return (
    <Panel title="Friplads og søskenderabat">
      <p className="panel-b hint" style={{ paddingBottom: 0 }}>
        Beløbet fra kommunen pr. måned. Det gælder fra den valgte måned, til du ændrer det eller sætter en slutmåned.
      </p>
      {c.subsidies.map((s) => (
        <div className="row" key={s.id}>
          <div className="grow num">
            {fmtMonth(s.fromMonth)} – {s.toMonth ? fmtMonth(s.toMonth) : "indtil videre"}
            <div className="hint">Friplads {fmtKr(s.freePlace)} · søskenderabat {fmtKr(s.siblingDiscount)}</div>
          </div>
          <button
            className="btn link danger"
            onClick={() => act(() => api.del(`${base}/subsidies/${s.id}`), "Tilskud fjernet", () =>
              api.post(`${base}/subsidies`, { fromMonth: toMonthInput(s.fromMonth), toMonth: s.toMonth ? toMonthInput(s.toMonth) : null, freePlace: s.freePlace, siblingDiscount: s.siblingDiscount }))}
          >
            Fjern
          </button>
        </div>
      ))}
      <form
        className="row"
        onSubmit={async (e) => {
          e.preventDefault();
          errors.clear();
          const form = e.currentTarget;
          const v = formValues(form);
          const fp = parseAmount(String(v.freePlace || "0"));
          const sd = parseAmount(String(v.siblingDiscount || "0"));
          if (Number.isNaN(fp) || Number.isNaN(sd)) { errors.handle(new Error()); return; }
          try {
            await api.post(`${base}/subsidies`, { fromMonth: v.fromMonth, toMonth: v.toMonth || null, freePlace: fp, siblingDiscount: sd });
            form.reset();
            await act(async () => {}, "Tilskud gemt");
          } catch (err) {
            errors.handle(err);
          }
        }}
      >
        <div className="grid" style={{ width: "100%" }}>
          <Field label="Fra måned" name="fromMonth" error={errors.errors.fromMonth}><input id="fromMonth" name="fromMonth" type="month" defaultValue={toMonthInput(c.nextMonth)} required /></Field>
          <Field label="Til og med (valgfri)" name="toMonth" error={errors.errors.toMonth}><input id="toMonth" name="toMonth" type="month" /></Field>
          <Field label="Friplads pr. måned" name="freePlace" error={errors.errors.freePlace}><input id="freePlace" name="freePlace" inputMode="decimal" placeholder="0,00" /></Field>
          <Field label="Søskenderabat pr. måned" name="siblingDiscount"><input id="siblingDiscount" name="siblingDiscount" inputMode="decimal" placeholder="0,00" /></Field>
        </div>
        {errors.message && <p className="error">Tjek beløbene.</p>}
        <button className="btn" type="submit">Gem tilskud</button>
      </form>
    </Panel>
  );
}

function Details({ card: c, onSaved }: { card: ChildCard; onSaved: () => void }) {
  const errors = useFieldErrors();
  return (
    <Panel title="Oplysninger">
      <form
        className="panel-b"
        onSubmit={async (e) => {
          e.preventDefault();
          errors.clear();
          const v = formValues(e.currentTarget);
          try {
            await api.put(`/api/children/${c.id}`, {
              firstName: v.firstName, lastName: v.lastName, dateOfBirth: v.dateOfBirth,
              startDate: v.startDate || null, endDate: v.endDate || null, isPartTime: v.isPartTime, note: v.note,
            });
            onSaved();
          } catch (err) {
            errors.handle(err);
          }
        }}
      >
        <div className="grid">
          <Field label="Fornavn" name="firstName" error={errors.errors.firstName}><input id="firstName" name="firstName" defaultValue={c.firstName} required /></Field>
          <Field label="Efternavn" name="lastName"><input id="lastName" name="lastName" defaultValue={c.lastName} /></Field>
          <Field label="Fødselsdato" name="dateOfBirth"><input id="dateOfBirth" name="dateOfBirth" type="date" defaultValue={toDateInput(c.dateOfBirth)} required /></Field>
          <Field label="Første dag" name="startDate"><input id="startDate" name="startDate" type="date" defaultValue={toDateInput(c.startDate)} /></Field>
          <Field label="Sidste dag (udmeldelse)" name="endDate" error={errors.errors.endDate}><input id="endDate" name="endDate" type="date" defaultValue={toDateInput(c.endDate)} /></Field>
          <label className="check"><input type="checkbox" name="isPartTime" defaultChecked={c.isPartTime} /> Deltidsplads</label>
        </div>
        <Field label="Note" name="note"><textarea id="note" name="note" defaultValue={c.note} /></Field>
        <button className="btn primary" type="submit">Gem</button>
      </form>
    </Panel>
  );
}

function Account({ card: c, act }: { card: ChildCard; act: Act }) {
  const [open, setOpen] = useState(false);
  const errors = useFieldErrors();
  const label = c.balance > 0 ? "Mangler at betale" : c.balance < 0 ? "Betalt for meget" : "Saldo";
  return (
    <Panel title="Konto">
      <div className="panel-b">
        <div className="label">{label}</div>
        <div className="total num" style={{ color: c.balance > 0 ? "var(--bad)" : undefined }}>{fmtKr(Math.abs(c.balance))}</div>
        <p className="hint">Et beløb, der mangler eller er betalt for meget, kommer automatisk med på næste opkrævning.</p>
      </div>
      {!open && <div className="row"><button className="btn link" onClick={() => setOpen(true)}>Registrér en betaling</button></div>}
      {open && (
        <form
          className="row"
          onSubmit={async (e) => {
            e.preventDefault();
            errors.clear();
            const v = formValues(e.currentTarget);
            const amount = parseAmount(String(v.amount));
            try {
              const r = await api.post<{ id: number }>(`/api/children/${c.id}/payments`, { amount: Number.isNaN(amount) ? 0 : amount, month: v.month, paidOn: v.paidOn || null });
              setOpen(false);
              await act(async () => {}, `Betaling på ${fmtKr(amount)} er registreret`, () => api.del(`/api/children/${c.id}/payments/${r.id}`));
            } catch (err) {
              errors.handle(err);
            }
          }}
        >
          <div style={{ width: "100%" }}>
            <p className="hint">Til betalinger, Betalingsservice ikke bogførte: indbetalingskort, bankoverførsel eller kontant.</p>
            <Field label="Beløb" name="payAmount" error={errors.errors.amount}><input id="payAmount" name="amount" inputMode="decimal" required /></Field>
            <Field label="For måned" name="payMonth" error={errors.errors.month}><input id="payMonth" name="month" type="month" defaultValue={toMonthInput(c.ledger[0]?.month ?? c.nextMonth)} required /></Field>
            <Field label="Betalt den" name="paidOn"><input id="paidOn" name="paidOn" type="date" /></Field>
            <div className="inline">
              <button className="btn primary" type="submit">Registrér</button>
              <button className="btn link" type="button" onClick={() => setOpen(false)}>Annullér</button>
            </div>
          </div>
        </form>
      )}
      {c.ledger.map((e) => (
        <div className="row" key={e.id}>
          <div className="grow">{e.text}<div className="hint">{fmtMonth(e.month)}</div></div>
          <span className="num">{fmtKr(e.amount)}</span>
        </div>
      ))}
    </Panel>
  );
}

function Manual({ card: c, act }: { card: ChildCard; act: Act }) {
  const errors = useFieldErrors();
  const base = `/api/children/${c.id}`;
  return (
    <Panel title="Næste opkrævning">
      <p className="panel-b hint" style={{ paddingBottom: 0 }}>Ekstra linjer på {fmtMonth(c.nextMonth)}, fx et gebyr eller en godtgørelse.</p>
      {c.manual.map((m) => (
        <div className="row" key={m.id}>
          <span className="grow">{m.text}</span>
          <span className="num">{fmtKr(m.amount)}</span>
          <button className="btn link danger" onClick={() => act(() => api.del(`${base}/manual/${m.id}`), "Linjen er fjernet", () => api.post(`${base}/manual`, { text: m.text, amount: m.amount }))}>Fjern</button>
        </div>
      ))}
      <form
        className="row"
        onSubmit={async (e) => {
          e.preventDefault();
          errors.clear();
          const form = e.currentTarget;
          const v = formValues(form);
          const amount = parseAmount(String(v.amount));
          try {
            await api.post(`${base}/manual`, { text: v.text, amount: (Number.isNaN(amount) ? 0 : amount) * (v.credit ? -1 : 1) });
            form.reset();
            await act(async () => {}, `Kommer med på opkrævningen for ${fmtMonth(c.nextMonth)}`);
          } catch (err) {
            errors.handle(err);
          }
        }}
      >
        <div style={{ width: "100%" }}>
          <Field label="Tekst" name="mText" error={errors.errors.text}><input id="mText" name="text" maxLength={45} required style={{ width: "100%" }} /></Field>
          <Field label="Beløb" name="mAmount" error={errors.errors.amount}><input id="mAmount" name="amount" inputMode="decimal" required /></Field>
          <label className="check"><input type="checkbox" name="credit" /> Det er en godtgørelse (trækkes fra)</label>
        </div>
        <button className="btn" type="submit">Tilføj linje</button>
      </form>
    </Panel>
  );
}
