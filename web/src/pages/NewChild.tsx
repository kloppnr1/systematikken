import { Link, useNavigate } from "react-router";
import { useQueryClient } from "@tanstack/react-query";
import { api } from "../api";
import { Field, Panel, formValues, useFieldErrors, useTitle, useToast } from "../ui";

export function NewChild() {
  useTitle("Indmeld et barn");
  const errors = useFieldErrors();
  const navigate = useNavigate();
  const qc = useQueryClient();
  const toast = useToast();
  const e = errors.errors;

  return (
    <div className="narrow">
      <Link className="back" to="/born">← Børn</Link>
      <h1>Indmeld et barn</h1>
      <p className="hint">Kommer barnet fra ventelisten, så brug "Indmeld" der i stedet. Så kommer forældrene med.</p>
      <form
        onSubmit={async (ev) => {
          ev.preventDefault();
          errors.clear();
          const v = formValues(ev.currentTarget);
          try {
            const r = await api.post<{ id: string; name: string }>("/api/children", {
              firstName: v.firstName, lastName: v.lastName, dateOfBirth: v.dateOfBirth || null, startDate: v.startDate || null, isPartTime: v.isPartTime,
              payer: { name: v.payerName, email: v.payerEmail, phone: v.payerPhone, address: v.address, postCode: v.postCode, city: v.city },
            });
            await qc.invalidateQueries();
            toast(`${r.name} er indmeldt`);
            navigate(`/born/${r.id}`);
          } catch (err) {
            errors.handle(err);
          }
        }}
      >
        <Panel title="Barnet">
          <div className="panel-b grid">
            <Field label="Fornavn" name="firstName" error={e.firstName}><input id="firstName" name="firstName" required /></Field>
            <Field label="Efternavn" name="lastName"><input id="lastName" name="lastName" /></Field>
            <Field label="Fødselsdato" name="dateOfBirth" error={e.dateOfBirth}><input id="dateOfBirth" name="dateOfBirth" type="date" required /></Field>
            <Field label="Første dag" name="startDate" error={e.startDate}><input id="startDate" name="startDate" type="date" required /></Field>
            <label className="check"><input type="checkbox" name="isPartTime" /> Deltidsplads</label>
          </div>
        </Panel>
        <Panel title="Betaler">
          <div className="panel-b grid">
            <Field label="Navn" name="payerName" error={e["payer.name"]}><input id="payerName" name="payerName" autoComplete="name" required /></Field>
            <Field label="E-mail" name="payerEmail"><input id="payerEmail" name="payerEmail" type="email" /></Field>
            <Field label="Telefon" name="payerPhone"><input id="payerPhone" name="payerPhone" type="tel" /></Field>
            <Field label="Adresse" name="address" error={e["payer.address"]}><input id="address" name="address" required /></Field>
            <Field label="Postnummer" name="postCode" error={e["payer.postCode"]}><input id="postCode" name="postCode" inputMode="numeric" pattern="\d{4}" required /></Field>
            <Field label="By" name="city"><input id="city" name="city" /></Field>
          </div>
        </Panel>
        {errors.message && <p className="error">{errors.message}</p>}
        <button className="btn primary big" type="submit">Indmeld</button>
      </form>
    </div>
  );
}
