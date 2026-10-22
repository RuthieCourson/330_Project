// ---------------------------------------------------------------------
// api.js - small wrapper around fetch() for talking to the C# API
// ---------------------------------------------------------------------

const Auth = {
  get token() { return localStorage.getItem("fz_token"); },
  get user() {
    const raw = localStorage.getItem("fz_user");
    return raw ? JSON.parse(raw) : null;
  },
  save(token, user) {
    localStorage.setItem("fz_token", token);
    localStorage.setItem("fz_user", JSON.stringify(user));
  },
  clear() {
    localStorage.removeItem("fz_token");
    localStorage.removeItem("fz_user");
  },
  isCustomer() { return this.user?.role === "customer"; },
  isTrainer() { return this.user?.role === "trainer"; }
};

async function api(path, { method = "GET", body } = {}) {
  const headers = { "Content-Type": "application/json" };
  if (Auth.token) headers.Authorization = `Bearer ${Auth.token}`;

  const res = await fetch(`/api${path}`, {
    method,
    headers,
    body: body ? JSON.stringify(body) : undefined
  });

  if (res.status === 204) return null;

  let data = null;
  try { data = await res.json(); } catch { /* empty body */ }

  if (!res.ok) {
    // Token expired or invalid -> log out locally
    if (res.status === 401 && Auth.token && !path.startsWith("/auth/login")) {
      Auth.clear();
    }
    const message = data?.message || data?.title || `Request failed (${res.status})`;
    throw new Error(message);
  }
  return data;
}

// Build a ?query=string from an object, skipping empty values
function toQuery(params) {
  const q = new URLSearchParams();
  Object.entries(params).forEach(([k, v]) => {
    if (v !== undefined && v !== null && v !== "") q.append(k, v);
  });
  const s = q.toString();
  return s ? `?${s}` : "";
}
