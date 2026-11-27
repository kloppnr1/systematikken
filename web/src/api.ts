// One place that talks to the API. Every change sends X-Requested-With, which the API requires (CSRF guard).

export class ApiError extends Error {
  constructor(
    public status: number,
    public fields: Record<string, string> = {},
    message = "Noget gik galt. Prøv igen.",
  ) {
    super(message);
  }
}

async function request<T>(method: string, url: string, body?: unknown): Promise<T> {
  const isForm = body instanceof FormData;
  const res = await fetch(url, {
    method,
    credentials: "same-origin",
    headers: {
      ...(method === "GET" ? {} : { "X-Requested-With": "systematikken" }),
      ...(body && !isForm ? { "Content-Type": "application/json" } : {}),
    },
    body: body === undefined ? undefined : isForm ? body : JSON.stringify(body),
  });

  if (res.status === 401) {
    if (!location.pathname.startsWith("/login")) location.href = `/login?returnUrl=${encodeURIComponent(location.pathname)}`;
    throw new ApiError(401, {}, "Du er ikke logget ind.");
  }
  if (res.status === 204) return undefined as T;

  const data = res.headers.get("content-type")?.includes("json") ? await res.json() : undefined;
  if (!res.ok) {
    const fields: Record<string, string> = {};
    for (const [k, v] of Object.entries((data?.errors ?? {}) as Record<string, string[]>)) fields[k] = v[0];
    throw new ApiError(res.status, fields, data?.detail ?? Object.values(fields)[0] ?? "Noget gik galt. Prøv igen.");
  }
  return data as T;
}

export const api = {
  get: <T>(url: string) => request<T>("GET", url),
  post: <T = void>(url: string, body: unknown = {}) => request<T>("POST", url, body),
  put: <T = void>(url: string, body: unknown) => request<T>("PUT", url, body),
  del: <T = void>(url: string) => request<T>("DELETE", url),
};
