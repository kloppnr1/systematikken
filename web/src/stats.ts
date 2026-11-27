// Counts visits and clicks on the front page. No cookie and nothing stored in the browser: only the kind of
// event and where the visitor came from are sent, and the server adds one to that day's number.

export type PageEvent = "besog" | "demo" | "kom-i-gang" | "log-ind";

/** "?kilde=linkedin" in the link wins. Otherwise the site the visitor came from, or "direkte". */
function source(): string {
  const kilde = new URLSearchParams(location.search).get("kilde");
  if (kilde) return kilde.toLowerCase().replace(/[^a-z0-9.-]/g, "").slice(0, 40) || "andet";
  if (!document.referrer) return "direkte";
  let host: string;
  try {
    host = new URL(document.referrer).hostname.replace(/^www\./, "");
  } catch {
    return "andet";
  }
  if (host === location.hostname.replace(/^www\./, "")) return "direkte";
  if (host.endsWith("linkedin.com") || host === "lnkd.in") return "linkedin";
  if (/(^|\.)google\.[a-z.]+$/.test(host)) return "google";
  if (host.endsWith("facebook.com") || host === "fb.me") return "facebook";
  if (host.endsWith("bing.com")) return "bing";
  return host.slice(0, 40);
}

export function count(name: PageEvent) {
  if (navigator.webdriver) return;
  fetch("/api/stats/hit", {
    method: "POST",
    keepalive: true,
    headers: { "Content-Type": "application/json", "X-Requested-With": "systematikken" },
    body: JSON.stringify({ name, source: source() }),
  }).catch(() => {});
}
