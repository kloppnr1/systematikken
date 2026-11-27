// Danish formatting for everything the user reads.

const kr = new Intl.NumberFormat("da-DK", { minimumFractionDigits: 2, maximumFractionDigits: 2 });
const day = new Intl.DateTimeFormat("da-DK", { day: "numeric", month: "short", year: "numeric" });
const monthYear = new Intl.DateTimeFormat("da-DK", { month: "long", year: "numeric" });
const monthOnly = new Intl.DateTimeFormat("da-DK", { month: "long" });

export const fmtKr = (n: number) => `${kr.format(n)} kr.`;

/** "2026-12-01" → Date at local midnight, so the date never shifts with the time zone. */
export const parseDate = (s: string) => {
  const [y, m, d] = s.slice(0, 10).split("-").map(Number);
  return new Date(y, m - 1, d || 1);
};

export const fmtDate = (s?: string | null) => (s ? day.format(s.length > 10 ? new Date(s) : parseDate(s)) : "–");
export const fmtMonth = (s: string) => monthYear.format(parseDate(s));
export const monthName = (m: number) => monthOnly.format(new Date(2000, m - 1, 1));
export const groupName = (g?: string | null) => (g === "Kindergarten" ? "Børnehave" : g === "Nursery" ? "Vuggestue" : "");

/** "1.234,50" or "1234.50" as typed by a person. Returns NaN if it is not a number. */
export const parseAmount = (s: string) => {
  const t = s.replace(/kr\.?/i, "").trim();
  if (!t) return NaN;
  const normalized = t.includes(",") ? t.replace(/\./g, "").replace(",", ".") : t;
  return Number(normalized);
};

export const toMonthInput = (s: string) => s.slice(0, 7);
export const toDateInput = (s?: string | null) => (s ? s.slice(0, 10) : "");
