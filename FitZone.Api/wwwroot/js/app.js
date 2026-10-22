// ---------------------------------------------------------------------
// app.js - FitZone Training single-page front end
// Simple hash router: #/classes, #/class/5, #/login, #/bookings, ...
// ---------------------------------------------------------------------

const app = document.getElementById("app");
const modalEl = document.getElementById("modal");
const modal = new bootstrap.Modal(modalEl);
const toast = new bootstrap.Toast(document.getElementById("toast"), { delay: 3500 });
let lookups = null;   // categories, rooms, trainers (loaded once)

// ============================ helpers ================================

function esc(value) {
  return String(value ?? "").replace(/[&<>"']/g, c =>
    ({ "&": "&amp;", "<": "&lt;", ">": "&gt;", '"': "&quot;", "'": "&#39;" }[c]));
}

const fmtDate = d => new Date(d).toLocaleDateString(undefined, { weekday: "short", month: "short", day: "numeric", year: "numeric" });
const fmtTime = d => new Date(d).toLocaleTimeString(undefined, { hour: "numeric", minute: "2-digit" });
const fmtDateTime = d => `${fmtDate(d)} · ${fmtTime(d)}`;
const fmtMoney = n => `$${Number(n).toFixed(2)}`;

// value for <input type="datetime-local">
function toLocalInput(d) {
  const dt = new Date(d);
  dt.setMinutes(dt.getMinutes() - dt.getTimezoneOffset());
  return dt.toISOString().slice(0, 16);
}

function notify(message, type = "success") {
  const el = document.getElementById("toast");
  el.className = `toast align-items-center border-0 text-bg-${type}`;
  document.getElementById("toastBody").textContent = message;
  toast.show();
}

function openModal(title, html) {
  document.getElementById("modalTitle").textContent = title;
  document.getElementById("modalBody").innerHTML = html;
  modal.show();
}

function typeBadge(type) {
  return type === "private"
    ? `<span class="badge badge-private">Private</span>`
    : `<span class="badge badge-group">Group</span>`;
}

function statusBadge(status) {
  const colors = { confirmed: "success", cancelled: "secondary", attended: "primary", no_show: "danger",
                   scheduled: "success", completed: "primary" };
  return `<span class="badge text-bg-${colors[status] || "light"}">${esc(status.replace("_", " "))}</span>`;
}

function loading() {
  app.innerHTML = `<div class="text-center py-5"><div class="spinner-border text-secondary"></div></div>`;
}

function showError(err) {
  app.innerHTML = `<div class="alert alert-danger">${esc(err.message)}</div>`;
}

// Read all named inputs in a form into an object
function formData(form) {
  const data = {};
  new FormData(form).forEach((v, k) => { data[k] = v; });
  form.querySelectorAll("input[type=checkbox]").forEach(cb => { data[cb.name] = cb.checked; });
  return data;
}

async function getLookups() {
  if (!lookups) lookups = await api("/lookups");
  return lookups;
}

// ============================ nav / router ===========================

function renderNav() {
  const user = Auth.user;
  document.querySelectorAll("[data-role]").forEach(li => {
    li.classList.toggle("d-none", !user || user.role !== li.dataset.role);
  });

  const authNav = document.getElementById("authNav");
  authNav.innerHTML = user
    ? `<li class="nav-item dropdown">
         <a class="nav-link dropdown-toggle" href="#" data-bs-toggle="dropdown">
           <i class="bi bi-person-circle"></i> ${esc(user.first_name)}
           <span class="badge bg-secondary ms-1">${esc(user.role)}</span>
         </a>
         <ul class="dropdown-menu dropdown-menu-end">
           <li><a class="dropdown-item" href="#/profile">My Profile</a></li>
           <li><hr class="dropdown-divider"></li>
           <li><a class="dropdown-item" href="#" id="logoutLink">Log out</a></li>
         </ul>
       </li>`
    : `<li class="nav-item"><a class="nav-link" href="#/login">Log in</a></li>
       <li class="nav-item"><a class="btn btn-fz btn-sm ms-lg-2 mt-1" href="#/register">Sign up</a></li>`;

  document.getElementById("logoutLink")?.addEventListener("click", async e => {
    e.preventDefault();
    try { await api("/auth/logout", { method: "POST" }); } catch { /* ignore */ }
    Auth.clear();
    notify("You have been logged out.", "secondary");
    location.hash = "#/classes";
    renderNav();
  });
}

const routes = {
  classes: renderClasses,
  class: renderClassDetail,
  trainers: renderTrainers,
  login: renderLogin,
  register: renderRegister,
  profile: renderProfile,
  bookings: renderMyBookings,
  dashboard: renderDashboard
};

function router() {
  const [, page = "classes", param] = location.hash.split("/");
  document.querySelectorAll("[data-nav]").forEach(a => a.classList.toggle("active", a.dataset.nav === page));
  renderNav();
  (routes[page] || renderClasses)(param);
  window.scrollTo(0, 0);
}

window.addEventListener("hashchange", router);

// ============================ class search ===========================

async function renderClasses() {
  loading();
  let lk;
  try { lk = await getLookups(); } catch (err) { return showError(err); }

  const saved = JSON.parse(sessionStorage.getItem("fz_filters") || "{}");
  const opt = (list, idKey, current) => list.map(x =>
    `<option value="${x[idKey]}" ${String(current) === String(x[idKey]) ? "selected" : ""}>${esc(x.name)}</option>`).join("");
  const sel = (val, current) => String(val) === String(current ?? "") ? "selected" : "";

  app.innerHTML = `
    <section class="hero mb-4">
      <h1 class="fw-bold">Find your next workout</h1>
      <p class="lead mb-0">Browse group classes and private training sessions, then book your spot in seconds.</p>
    </section>

    <form id="searchForm" class="card card-body shadow-sm mb-4">
      <div class="row g-2">
        <div class="col-md-5">
          <div class="input-group">
            <span class="input-group-text"><i class="bi bi-search"></i></span>
            <input class="form-control" name="q" placeholder="Search classes, trainers, categories..." value="${esc(saved.q)}">
          </div>
        </div>
        <div class="col-6 col-md-2">
          <select class="form-select" name="category"><option value="">All categories</option>${opt(lk.categories, "category_id", saved.category)}</select>
        </div>
        <div class="col-6 col-md-2">
          <select class="form-select" name="type">
            <option value="">Group &amp; private</option>
            <option value="group" ${sel("group", saved.type)}>Group classes</option>
            <option value="private" ${sel("private", saved.type)}>Private training</option>
          </select>
        </div>
        <div class="col-6 col-md-3">
          <select class="form-select" name="difficulty">
            <option value="">Any level</option>
            ${["Beginner", "Intermediate", "Advanced"].map(d => `<option ${sel(d, saved.difficulty)}>${d}</option>`).join("")}
          </select>
        </div>
        <div class="col-6 col-md-3">
          <select class="form-select" name="trainer"><option value="">Any trainer</option>${opt(lk.trainers, "trainer_id", saved.trainer)}</select>
        </div>
        <div class="col-6 col-md-2">
          <input type="date" class="form-control" name="from" title="Sessions from" value="${esc(saved.from)}">
        </div>
        <div class="col-6 col-md-2">
          <input type="date" class="form-control" name="to" title="Sessions to" value="${esc(saved.to)}">
        </div>
        <div class="col-6 col-md-3">
          <select class="form-select" name="sort">
            <option value="next" ${sel("next", saved.sort)}>Sort: Next available</option>
            <option value="title" ${sel("title", saved.sort)}>Sort: Name (A-Z)</option>
            <option value="price_asc" ${sel("price_asc", saved.sort)}>Sort: Price (low to high)</option>
            <option value="price_desc" ${sel("price_desc", saved.sort)}>Sort: Price (high to low)</option>
            <option value="popular" ${sel("popular", saved.sort)}>Sort: Most popular</option>
            <option value="duration" ${sel("duration", saved.sort)}>Sort: Shortest first</option>
          </select>
        </div>
        <div class="col-md-2 d-flex gap-2">
          <button class="btn btn-fz flex-fill">Search</button>
          <button type="button" class="btn btn-outline-secondary" id="clearFilters" title="Clear"><i class="bi bi-x-lg"></i></button>
        </div>
      </div>
    </form>

    <div id="results"></div>`;

  const form = document.getElementById("searchForm");
  form.addEventListener("submit", e => { e.preventDefault(); runSearch(); });
  form.querySelectorAll("select, input[type=date]").forEach(el => el.addEventListener("change", runSearch));
  document.getElementById("clearFilters").addEventListener("click", () => {
    sessionStorage.removeItem("fz_filters");
    renderClasses();
  });

  runSearch();
}

async function runSearch() {
  const form = document.getElementById("searchForm");
  const filters = formData(form);
  sessionStorage.setItem("fz_filters", JSON.stringify(filters));

  const results = document.getElementById("results");
  results.innerHTML = `<div class="text-center py-4"><div class="spinner-border text-secondary"></div></div>`;
  try {
    const classes = await api("/classes" + toQuery(filters));
    if (!classes.length) {
      results.innerHTML = `<div class="alert alert-info">No classes match your search. Try clearing a filter.</div>`;
      return;
    }
    results.innerHTML = `
      <p class="text-muted">${classes.length} class${classes.length === 1 ? "" : "es"} found</p>
      <div class="row g-3">${classes.map(classCard).join("")}</div>`;
    results.querySelectorAll("[data-class]").forEach(card =>
      card.addEventListener("click", () => { location.hash = `#/class/${card.dataset.class}`; }));
  } catch (err) {
    results.innerHTML = `<div class="alert alert-danger">${esc(err.message)}</div>`;
  }
}

function classCard(c) {
  return `
    <div class="col-md-6 col-lg-4">
      <div class="card h-100 class-card shadow-sm" data-class="${c.class_id}">
        <div class="card-body d-flex flex-column">
          <div class="d-flex justify-content-between mb-2">
            <span class="text-muted small">${esc(c.category)}</span>
            ${typeBadge(c.class_type)}
          </div>
          <h5 class="card-title">${esc(c.title)}</h5>
          <p class="small text-muted mb-2"><i class="bi bi-person"></i> ${esc(c.trainer_name)}</p>
          <p class="card-text small flex-grow-1">${esc(c.description)}</p>
          <div class="d-flex flex-wrap gap-2 small mb-2">
            <span class="badge text-bg-light border">${esc(c.difficulty)}</span>
            <span class="badge text-bg-light border"><i class="bi bi-clock"></i> ${c.duration_min} min</span>
            <span class="badge text-bg-light border"><i class="bi bi-people"></i> max ${c.max_capacity}</span>
          </div>
          <div class="d-flex justify-content-between align-items-end">
            <div class="small">
              ${c.next_session
                ? `<span class="text-muted">Next:</span> ${fmtDateTime(c.next_session)}<br>
                   <span class="text-muted">${c.upcoming_sessions} upcoming session${c.upcoming_sessions == 1 ? "" : "s"}</span>`
                : `<span class="text-muted">No upcoming sessions</span>`}
            </div>
            <span class="fs-5 fw-bold text-fz">${fmtMoney(c.price)}</span>
          </div>
        </div>
      </div>
    </div>`;
}

// ============================ class detail ===========================

async function renderClassDetail(id) {
  loading();
  let c;
  try { c = await api(`/classes/${id}`); } catch (err) { return showError(err); }

  const isOwner = Auth.isTrainer() && Auth.user.user_id == c.trainer_id;

  app.innerHTML = `
    <a href="#/classes" class="btn btn-link px-0 mb-2"><i class="bi bi-arrow-left"></i> Back to classes</a>
    <div class="row g-4">
      <div class="col-lg-7">
        <div class="card shadow-sm">
          <div class="card-body">
            <div class="d-flex justify-content-between">
              <span class="text-muted">${esc(c.category)}</span>
              <div>${typeBadge(c.class_type)} ${c.is_active ? "" : `<span class="badge text-bg-secondary">Inactive</span>`}</div>
            </div>
            <h2 class="mt-1">${esc(c.title)}</h2>
            <p>${esc(c.description)}</p>
            <div class="row text-center g-2 mb-3">
              <div class="col"><div class="border rounded p-2"><div class="small text-muted">Level</div><strong>${esc(c.difficulty)}</strong></div></div>
              <div class="col"><div class="border rounded p-2"><div class="small text-muted">Duration</div><strong>${c.duration_min} min</strong></div></div>
              <div class="col"><div class="border rounded p-2"><div class="small text-muted">Capacity</div><strong>${c.max_capacity}</strong></div></div>
              <div class="col"><div class="border rounded p-2"><div class="small text-muted">Price</div><strong class="text-fz">${fmtMoney(c.price)}</strong></div></div>
            </div>
            <h6 class="mt-4">Your trainer</h6>
            <div class="d-flex gap-3">
              <i class="bi bi-person-badge fs-1 text-fz"></i>
              <div>
                <strong>${esc(c.trainer_name)}</strong> · <span class="text-muted">${esc(c.trainer_specialty)}</span><br>
                <span class="small">${esc(c.trainer_bio)}</span><br>
                <span class="small text-muted">${c.trainer_years ?? 0} years experience${c.trainer_certifications ? " · " + esc(c.trainer_certifications) : ""}</span>
              </div>
            </div>
            ${isOwner ? `<div class="mt-4"><a class="btn btn-outline-dark" href="#/dashboard"><i class="bi bi-gear"></i> Manage in dashboard</a></div>` : ""}
          </div>
        </div>
      </div>
      <div class="col-lg-5">
        <div class="card shadow-sm">
          <div class="card-header bg-white"><strong>${c.class_type === "private" ? "Available time slots" : "Upcoming sessions"}</strong></div>
          <ul class="list-group list-group-flush" id="sessionList">
            ${c.sessions.length ? c.sessions.map(s => sessionItem(s, c)).join("")
              : `<li class="list-group-item text-muted">No upcoming sessions scheduled.</li>`}
          </ul>
        </div>
        ${!Auth.user ? `<div class="alert alert-light border mt-3 small"><a href="#/login">Log in</a> or <a href="#/register">sign up</a> as a customer to book.</div>` : ""}
      </div>
    </div>`;

  app.querySelectorAll("[data-book]").forEach(btn => btn.addEventListener("click", () => confirmBooking(c, btn.dataset.book)));
  app.querySelectorAll("[data-cancel-booking]").forEach(btn => btn.addEventListener("click", async () => {
    if (!confirm("Cancel this booking?")) return;
    try {
      await api(`/bookings/${btn.dataset.cancelBooking}/cancel`, { method: "POST" });
      notify("Booking cancelled.", "secondary");
      renderClassDetail(id);
    } catch (err) { notify(err.message, "danger"); }
  }));
}

function sessionItem(s, c) {
  const full = s.seats_left <= 0;
  let action = "";
  if (s.my_booking_id) {
    action = `<span class="badge text-bg-success mb-1">Booked</span><br>
              <button class="btn btn-sm btn-outline-danger" data-cancel-booking="${s.my_booking_id}">Cancel</button>`;
  } else if (Auth.isCustomer()) {
    action = full ? `<span class="badge text-bg-secondary">Full</span>`
                  : `<button class="btn btn-sm btn-fz" data-book="${s.session_id}">Book</button>`;
  } else if (full) {
    action = `<span class="badge text-bg-secondary">Full</span>`;
  }
  const seats = c.class_type === "private"
    ? (full ? "Taken" : "Open")
    : `${s.seats_left} of ${s.max_capacity} spots left`;
  return `
    <li class="list-group-item d-flex justify-content-between align-items-center session-row ${full ? "full" : ""}">
      <div>
        <strong>${fmtDate(s.start_time)}</strong><br>
        <span class="small">${fmtTime(s.start_time)} - ${fmtTime(s.end_time)}${s.room_name ? " · " + esc(s.room_name) : ""}</span><br>
        <span class="small text-muted">${seats}</span>
      </div>
      <div class="text-end">${action}</div>
    </li>`;
}

function confirmBooking(c, sessionId) {
  const s = c.sessions.find(x => x.session_id == sessionId);
  openModal("Confirm booking", `
    <p>You're booking <strong>${esc(c.title)}</strong> with ${esc(c.trainer_name)}.</p>
    <ul>
      <li><strong>When:</strong> ${fmtDateTime(s.start_time)} (${c.duration_min} min)</li>
      <li><strong>Where:</strong> ${esc(s.room_name || "TBA")}</li>
      <li><strong>Price:</strong> ${fmtMoney(c.price)} (paid at the front desk)</li>
    </ul>
    <form id="bookForm">
      <label class="form-label">Notes for your trainer (optional)</label>
      <input class="form-control mb-3" name="notes" maxlength="255" placeholder="e.g. recovering from a knee injury">
      <button class="btn btn-fz w-100">Confirm booking</button>
    </form>`);

  document.getElementById("bookForm").addEventListener("submit", async e => {
    e.preventDefault();
    try {
      const b = await api("/bookings", { method: "POST", body: { sessionId: Number(sessionId), notes: e.target.notes.value || null } });
      openModal("Booking confirmed!", `
        <div class="text-center">
          <i class="bi bi-check-circle-fill text-success display-4"></i>
          <h4 class="mt-2">You're in!</h4>
          <p>${esc(b.title)} · ${fmtDateTime(b.start_time)}</p>
          <p class="mb-1 text-muted">Confirmation code</p>
          <p class="fs-3 fw-bold font-monospace">${esc(b.confirmation_code)}</p>
          <button class="btn btn-outline-dark" id="goBookings">View my bookings</button>
        </div>`);
      document.getElementById("goBookings").addEventListener("click", () => { modal.hide(); location.hash = "#/bookings"; });
      renderClassDetail(c.class_id);
    } catch (err) { notify(err.message, "danger"); }
  });
}

// ============================ trainers ===============================

async function renderTrainers() {
  loading();
  let lk;
  try { lk = await getLookups(); } catch (err) { return showError(err); }
  app.innerHTML = `
    <h2 class="mb-3">Meet our trainers</h2>
    <div class="row g-3">
      ${lk.trainers.map(t => `
        <div class="col-md-6 col-lg-4">
          <div class="card h-100 shadow-sm">
            <div class="card-body">
              <i class="bi bi-person-badge fs-1 text-fz"></i>
              <h5 class="mt-2 mb-0">${esc(t.name)}</h5>
              <p class="text-muted small">${esc(t.specialty || "Trainer")} · ${t.years_experience ?? 0} yrs</p>
              <p class="small">${esc(t.bio)}</p>
              <button class="btn btn-sm btn-outline-dark" data-trainer="${t.trainer_id}">View classes</button>
            </div>
          </div>
        </div>`).join("")}
    </div>`;
  app.querySelectorAll("[data-trainer]").forEach(btn => btn.addEventListener("click", () => {
    sessionStorage.setItem("fz_filters", JSON.stringify({ trainer: btn.dataset.trainer }));
    location.hash = "#/classes";
  }));
}

// ============================ auth pages =============================

function renderLogin() {
  app.innerHTML = `
    <div class="card shadow-sm auth-card">
      <div class="card-body p-4">
        <h3 class="mb-3">Log in</h3>
        <form id="loginForm">
          <div class="mb-3"><label class="form-label">Email</label><input type="email" class="form-control" name="email" required></div>
          <div class="mb-3"><label class="form-label">Password</label><input type="password" class="form-control" name="password" required></div>
          <button class="btn btn-fz w-100">Log in</button>
        </form>
        <p class="mt-3 mb-0 small text-center">New to FitZone? <a href="#/register">Create an account</a></p>
        <hr>
        <p class="small text-muted mb-1">Demo accounts (password <code>Password123!</code>):</p>
        <ul class="small text-muted mb-0">
          <li>Customer: emily.carter@email.com</li>
          <li>Trainer: maya.thompson@fitzone.com</li>
        </ul>
      </div>
    </div>`;

  document.getElementById("loginForm").addEventListener("submit", async e => {
    e.preventDefault();
    try {
      const res = await api("/auth/login", { method: "POST", body: formData(e.target) });
      Auth.save(res.token, res.user);
      notify(`Welcome back, ${res.user.first_name}!`);
      location.hash = res.user.role === "trainer" ? "#/dashboard" : "#/classes";
    } catch (err) { notify(err.message, "danger"); }
  });
}

function renderRegister() {
  app.innerHTML = `
    <div class="card shadow-sm auth-card">
      <div class="card-body p-4">
        <h3 class="mb-3">Create your account</h3>
        <form id="registerForm">
          <div class="mb-3">
            <label class="form-label d-block">I am a...</label>
            <div class="btn-group w-100" role="group">
              <input type="radio" class="btn-check" name="role" id="roleCustomer" value="customer" checked>
              <label class="btn btn-outline-dark" for="roleCustomer">Customer</label>
              <input type="radio" class="btn-check" name="role" id="roleTrainer" value="trainer">
              <label class="btn btn-outline-dark" for="roleTrainer">Trainer</label>
            </div>
          </div>
          <div class="row g-2 mb-3">
            <div class="col"><label class="form-label">First name</label><input class="form-control" name="firstName" required></div>
            <div class="col"><label class="form-label">Last name</label><input class="form-control" name="lastName" required></div>
          </div>
          <div class="mb-3"><label class="form-label">Email</label><input type="email" class="form-control" name="email" required></div>
          <div class="mb-3"><label class="form-label">Phone</label><input class="form-control" name="phone" placeholder="205-555-0100"></div>
          <div class="mb-3"><label class="form-label">Password</label><input type="password" class="form-control" name="password" minlength="8" required>
            <div class="form-text">At least 8 characters.</div></div>
          <button class="btn btn-fz w-100">Sign up</button>
        </form>
        <p class="mt-3 mb-0 small text-center">Already have an account? <a href="#/login">Log in</a></p>
      </div>
    </div>`;

  document.getElementById("registerForm").addEventListener("submit", async e => {
    e.preventDefault();
    try {
      const res = await api("/auth/register", { method: "POST", body: formData(e.target) });
      Auth.save(res.token, res.user);
      lookups = null;   // new trainer should show up in lists
      notify("Account created. Welcome to FitZone!");
      location.hash = "#/profile";
    } catch (err) { notify(err.message, "danger"); }
  });
}

// ============================ profile ================================

async function renderProfile() {
  if (!Auth.user) { location.hash = "#/login"; return; }
  loading();
  let p;
  try { p = await api("/profile"); } catch (err) { return showError(err); }

  const isTrainer = p.role === "trainer";
  app.innerHTML = `
    <div class="row g-4">
      <div class="col-lg-8">
        <div class="card shadow-sm">
          <div class="card-body">
            <h3>My profile</h3>
            <p class="text-muted small">${esc(p.email)} · ${esc(p.role)} since ${fmtDate(p.created_at)}</p>
            <form id="profileForm">
              <div class="row g-3">
                <div class="col-md-6"><label class="form-label">First name</label><input class="form-control" name="firstName" value="${esc(p.first_name)}" required></div>
                <div class="col-md-6"><label class="form-label">Last name</label><input class="form-control" name="lastName" value="${esc(p.last_name)}" required></div>
                <div class="col-md-6"><label class="form-label">Phone</label><input class="form-control" name="phone" value="${esc(p.phone)}"></div>
                ${isTrainer ? `
                  <div class="col-md-6"><label class="form-label">Specialty</label><input class="form-control" name="specialty" value="${esc(p.specialty)}"></div>
                  <div class="col-md-6"><label class="form-label">Years of experience</label><input type="number" min="0" max="60" class="form-control" name="yearsExperience" value="${p.years_experience ?? 0}"></div>
                  <div class="col-md-6"><label class="form-label">Certifications</label><input class="form-control" name="certifications" value="${esc(p.certifications)}"></div>
                  <div class="col-12"><label class="form-label">Bio</label><textarea class="form-control" rows="3" name="bio" maxlength="1000">${esc(p.bio)}</textarea></div>
                ` : `
                  <div class="col-md-6"><label class="form-label">Date of birth</label><input type="date" class="form-control" name="dateOfBirth" value="${p.date_of_birth ? p.date_of_birth.slice(0, 10) : ""}"></div>
                  <div class="col-md-6"><label class="form-label">Fitness goal</label><input class="form-control" name="fitnessGoal" value="${esc(p.fitness_goal)}"></div>
                  <div class="col-md-6"><label class="form-label">Emergency contact</label><input class="form-control" name="emergencyContact" value="${esc(p.emergency_contact)}"></div>
                `}
              </div>
              <button class="btn btn-fz mt-3">Save changes</button>
            </form>
          </div>
        </div>
      </div>
      <div class="col-lg-4">
        <div class="card shadow-sm">
          <div class="card-body">
            <h5>Change password</h5>
            <form id="pwForm">
              <input type="password" class="form-control mb-2" name="currentPassword" placeholder="Current password" required>
              <input type="password" class="form-control mb-2" name="newPassword" placeholder="New password" minlength="8" required>
              <button class="btn btn-outline-dark w-100">Update password</button>
            </form>
          </div>
        </div>
      </div>
    </div>`;

  document.getElementById("profileForm").addEventListener("submit", async e => {
    e.preventDefault();
    const body = formData(e.target);
    if (body.dateOfBirth === "") body.dateOfBirth = null;
    if (body.yearsExperience !== undefined) body.yearsExperience = Number(body.yearsExperience);
    try {
      const updated = await api("/profile", { method: "PUT", body });
      Auth.save(Auth.token, { ...Auth.user, first_name: updated.first_name, last_name: updated.last_name });
      lookups = null;
      renderNav();
      notify("Profile saved.");
    } catch (err) { notify(err.message, "danger"); }
  });

  document.getElementById("pwForm").addEventListener("submit", async e => {
    e.preventDefault();
    try {
      await api("/profile/password", { method: "PUT", body: formData(e.target) });
      e.target.reset();
      notify("Password updated.");
    } catch (err) { notify(err.message, "danger"); }
  });
}

// ============================ customer bookings ======================

async function renderMyBookings() {
  if (!Auth.isCustomer()) { location.hash = "#/login"; return; }
  loading();
  let bookings;
  try { bookings = await api("/bookings/mine"); } catch (err) { return showError(err); }

  const now = new Date();
  const upcoming = bookings.filter(b => b.status === "confirmed" && new Date(b.start_time) > now)
                           .sort((a, b) => new Date(a.start_time) - new Date(b.start_time));
  const history = bookings.filter(b => !upcoming.includes(b));
  const attended = bookings.filter(b => b.status === "attended").length;

  app.innerHTML = `
    <h2>My bookings</h2>
    <div class="row g-3 my-2">
      <div class="col-4"><div class="card card-body stat-card text-center"><div class="stat-value">${upcoming.length}</div><div class="text-muted small">Upcoming</div></div></div>
      <div class="col-4"><div class="card card-body stat-card text-center"><div class="stat-value">${attended}</div><div class="text-muted small">Classes attended</div></div></div>
      <div class="col-4"><div class="card card-body stat-card text-center"><div class="stat-value">${bookings.length}</div><div class="text-muted small">Total bookings</div></div></div>
    </div>

    <ul class="nav nav-tabs mt-3">
      <li class="nav-item"><button class="nav-link active" data-bs-toggle="tab" data-bs-target="#tabUpcoming">Upcoming (${upcoming.length})</button></li>
      <li class="nav-item"><button class="nav-link" data-bs-toggle="tab" data-bs-target="#tabHistory">Booking history (${history.length})</button></li>
    </ul>
    <div class="tab-content bg-white border border-top-0 p-3">
      <div class="tab-pane fade show active" id="tabUpcoming">${bookingTable(upcoming, true)}</div>
      <div class="tab-pane fade" id="tabHistory">${bookingTable(history, false)}</div>
    </div>`;

  app.querySelectorAll("[data-cancel]").forEach(btn => btn.addEventListener("click", async () => {
    if (!confirm("Cancel this booking?")) return;
    try {
      await api(`/bookings/${btn.dataset.cancel}/cancel`, { method: "POST" });
      notify("Booking cancelled.", "secondary");
      renderMyBookings();
    } catch (err) { notify(err.message, "danger"); }
  }));
}

function bookingTable(rows, canCancel) {
  if (!rows.length) {
    return canCancel
      ? `<p class="text-muted mb-0">No upcoming bookings. <a href="#/classes">Find a class</a></p>`
      : `<p class="text-muted mb-0">No past bookings yet.</p>`;
  }
  return `
    <div class="table-responsive">
      <table class="table table-hover mb-0">
        <thead><tr><th>Class</th><th>When</th><th>Trainer</th><th>Room</th><th>Code</th><th>Status</th>${canCancel ? "<th></th>" : ""}</tr></thead>
        <tbody>
          ${rows.map(b => `
            <tr>
              <td><a href="#/class/${b.class_id}">${esc(b.title)}</a> ${typeBadge(b.class_type)}</td>
              <td>${fmtDateTime(b.start_time)}</td>
              <td>${esc(b.trainer_name)}</td>
              <td>${esc(b.room_name || "-")}</td>
              <td class="font-monospace">${esc(b.confirmation_code)}</td>
              <td>${statusBadge(b.status)}${b.session_status === "cancelled" ? ` <span class="small text-danger">(session cancelled)</span>` : ""}</td>
              ${canCancel ? `<td><button class="btn btn-sm btn-outline-danger" data-cancel="${b.booking_id}">Cancel</button></td>` : ""}
            </tr>`).join("")}
        </tbody>
      </table>
    </div>`;
}

// ============================ trainer dashboard ======================

async function renderDashboard(tab) {
  if (!Auth.isTrainer()) { location.hash = "#/login"; return; }
  loading();
  let summary, lk;
  try { [summary, lk] = await Promise.all([api("/trainer/summary"), getLookups()]); }
  catch (err) { return showError(err); }

  const activeTab = tab || sessionStorage.getItem("fz_dash_tab") || "classes";
  const tabBtn = (id, label) =>
    `<li class="nav-item"><button class="nav-link ${activeTab === id ? "active" : ""}" data-tab="${id}">${label}</button></li>`;

  app.innerHTML = `
    <div class="d-flex justify-content-between align-items-center flex-wrap gap-2">
      <h2 class="mb-0">Trainer dashboard</h2>
      <button class="btn btn-fz" id="newClassBtn"><i class="bi bi-plus-lg"></i> New class</button>
    </div>
    <div class="row g-3 my-2">
      <div class="col-6 col-md-3"><div class="card card-body stat-card"><div class="stat-value">${summary.active_classes}</div><div class="text-muted small">Active classes</div></div></div>
      <div class="col-6 col-md-3"><div class="card card-body stat-card"><div class="stat-value">${summary.upcoming_sessions}</div><div class="text-muted small">Upcoming sessions</div></div></div>
      <div class="col-6 col-md-3"><div class="card card-body stat-card"><div class="stat-value">${summary.upcoming_bookings}</div><div class="text-muted small">Upcoming bookings</div></div></div>
      <div class="col-6 col-md-3"><div class="card card-body stat-card"><div class="stat-value">${fmtMoney(summary.revenue_30_days)}</div><div class="text-muted small">Attended revenue (30 days)</div></div></div>
    </div>
    <ul class="nav nav-tabs mt-3">
      ${tabBtn("classes", "My classes")}
      ${tabBtn("sessions", "Schedule")}
      ${tabBtn("bookings", "Bookings")}
    </ul>
    <div class="bg-white border border-top-0 p-3" id="dashContent"></div>`;

  document.getElementById("newClassBtn").addEventListener("click", () => classForm(null, lk));
  app.querySelectorAll("[data-tab]").forEach(btn => btn.addEventListener("click", () => {
    sessionStorage.setItem("fz_dash_tab", btn.dataset.tab);
    renderDashboard(btn.dataset.tab);
  }));

  const content = document.getElementById("dashContent");
  if (activeTab === "classes") await dashClasses(content, lk);
  else if (activeTab === "sessions") await dashSessions(content, lk, "upcoming");
  else await dashBookings(content, {});
}

// ----- My classes tab -----

async function dashClasses(content, lk) {
  const classes = await api("/trainer/classes");
  if (!classes.length) {
    content.innerHTML = `<p class="text-muted mb-0">You haven't created any classes yet. Click <strong>New class</strong> to get started.</p>`;
    return;
  }
  content.innerHTML = `
    <div class="table-responsive">
      <table class="table table-hover mb-0">
        <thead><tr><th>Class</th><th>Type</th><th>Level</th><th>Price</th><th>Cap.</th><th>Upcoming</th><th>Next session</th><th class="text-end">Actions</th></tr></thead>
        <tbody>
          ${classes.map(c => `
            <tr class="${c.is_active ? "" : "text-muted"}">
              <td><a href="#/class/${c.class_id}">${esc(c.title)}</a><br><span class="small text-muted">${esc(c.category)}</span>
                  ${c.is_active ? "" : `<span class="badge text-bg-secondary">Inactive</span>`}</td>
              <td>${typeBadge(c.class_type)}</td>
              <td>${esc(c.difficulty)}</td>
              <td>${fmtMoney(c.price)}</td>
              <td>${c.max_capacity}</td>
              <td>${Number(c.upcoming_sessions || 0)}</td>
              <td class="small">${c.next_session ? fmtDateTime(c.next_session) : "-"}</td>
              <td class="text-end text-nowrap">
                <button class="btn btn-sm btn-fz" data-add-session="${c.class_id}" title="Add sessions"><i class="bi bi-calendar-plus"></i></button>
                <button class="btn btn-sm btn-outline-dark" data-edit="${c.class_id}" title="Edit"><i class="bi bi-pencil"></i></button>
                <button class="btn btn-sm btn-outline-danger" data-delete="${c.class_id}" title="Delete"><i class="bi bi-trash"></i></button>
              </td>
            </tr>`).join("")}
        </tbody>
      </table>
    </div>`;

  const byId = id => classes.find(c => c.class_id == id);
  content.querySelectorAll("[data-edit]").forEach(b => b.addEventListener("click", () => classForm(byId(b.dataset.edit), lk)));
  content.querySelectorAll("[data-add-session]").forEach(b => b.addEventListener("click", () => sessionForm(byId(b.dataset.addSession), lk)));
  content.querySelectorAll("[data-delete]").forEach(b => b.addEventListener("click", async () => {
    const c = byId(b.dataset.delete);
    if (!confirm(`Delete "${c.title}"? Upcoming sessions and their bookings will be cancelled.`)) return;
    try {
      const res = await api(`/classes/${c.class_id}`, { method: "DELETE" });
      notify(res.message, "secondary");
      renderDashboard("classes");
    } catch (err) { notify(err.message, "danger"); }
  }));
}

function classForm(c, lk) {
  const editing = !!c;
  c = c || { class_type: "group", difficulty: "Beginner", duration_min: 60, price: 15, max_capacity: 20, is_active: true };
  openModal(editing ? "Edit class" : "Create a new class", `
    <form id="classForm">
      <div class="row g-3">
        <div class="col-md-8"><label class="form-label">Title</label><input class="form-control" name="title" value="${esc(c.title)}" maxlength="100" required></div>
        <div class="col-md-4"><label class="form-label">Category</label>
          <select class="form-select" name="categoryId" required>
            ${lk.categories.map(x => `<option value="${x.category_id}" ${x.category_id == c.category_id ? "selected" : ""}>${esc(x.name)}</option>`).join("")}
          </select></div>
        <div class="col-12"><label class="form-label">Description</label><textarea class="form-control" name="description" rows="3" maxlength="1000">${esc(c.description)}</textarea></div>
        <div class="col-md-4"><label class="form-label">Type</label>
          <select class="form-select" name="classType" id="classTypeSel">
            <option value="group" ${c.class_type === "group" ? "selected" : ""}>Group class</option>
            <option value="private" ${c.class_type === "private" ? "selected" : ""}>Private (1-on-1)</option>
          </select></div>
        <div class="col-md-4"><label class="form-label">Difficulty</label>
          <select class="form-select" name="difficulty">
            ${["Beginner", "Intermediate", "Advanced"].map(d => `<option ${d === c.difficulty ? "selected" : ""}>${d}</option>`).join("")}
          </select></div>
        <div class="col-md-4"><label class="form-label">Duration (min)</label><input type="number" class="form-control" name="durationMin" min="15" max="240" value="${c.duration_min}" required></div>
        <div class="col-md-4"><label class="form-label">Price ($)</label><input type="number" class="form-control" name="price" min="0" step="0.01" value="${c.price}" required></div>
        <div class="col-md-4"><label class="form-label">Max capacity</label><input type="number" class="form-control" name="maxCapacity" id="capInput" min="1" max="100" value="${c.max_capacity}" required></div>
        <div class="col-md-4 d-flex align-items-end"><div class="form-check form-switch mb-2">
          <input class="form-check-input" type="checkbox" name="isActive" id="activeChk" ${c.is_active ? "checked" : ""}>
          <label class="form-check-label" for="activeChk">Active (visible to customers)</label></div></div>
      </div>
      <button class="btn btn-fz mt-3">${editing ? "Save changes" : "Create class"}</button>
    </form>`);

  const typeSel = document.getElementById("classTypeSel");
  const cap = document.getElementById("capInput");
  const syncCap = () => { if (typeSel.value === "private") { cap.value = 1; cap.readOnly = true; } else cap.readOnly = false; };
  typeSel.addEventListener("change", syncCap);
  syncCap();

  document.getElementById("classForm").addEventListener("submit", async e => {
    e.preventDefault();
    const body = formData(e.target);
    body.categoryId = Number(body.categoryId);
    body.durationMin = Number(body.durationMin);
    body.price = Number(body.price);
    body.maxCapacity = Number(body.maxCapacity);
    try {
      const saved = await api(editing ? `/classes/${c.class_id}` : "/classes", { method: editing ? "PUT" : "POST", body });
      modal.hide();
      notify(editing ? "Class updated." : "Class created. Now add some sessions!");
      if (!editing) setTimeout(() => sessionForm(saved, lk), 400);
      renderDashboard("classes");
    } catch (err) { notify(err.message, "danger"); }
  });
}

function sessionForm(c, lk, existing) {
  const start = existing ? toLocalInput(existing.start_time) : "";
  openModal(existing ? `Reschedule: ${c.title}` : `Add sessions: ${c.title}`, `
    <form id="sessionForm">
      <div class="row g-3">
        <div class="col-md-6"><label class="form-label">Start date &amp; time</label>
          <input type="datetime-local" class="form-control" name="startTime" value="${start}" required></div>
        <div class="col-md-6"><label class="form-label">Room</label>
          <select class="form-select" name="roomId">
            <option value="">No room / TBA</option>
            ${lk.rooms.map(r => `<option value="${r.room_id}" ${existing && existing.room_id == r.room_id ? "selected" : ""}>${esc(r.name)} (holds ${r.capacity})</option>`).join("")}
          </select></div>
        ${existing ? "" : `
        <div class="col-md-6"><label class="form-label">Repeat weekly for</label>
          <select class="form-select" name="repeatWeeks">
            ${[1, 2, 4, 6, 8, 12].map(n => `<option value="${n}">${n === 1 ? "Just once" : n + " weeks"}</option>`).join("")}
          </select></div>`}
      </div>
      <p class="small text-muted mt-2 mb-0">Each session lasts ${c.duration_min} minutes. We'll check for room and schedule conflicts.</p>
      <button class="btn btn-fz mt-3">${existing ? "Save" : "Add session(s)"}</button>
    </form>`);

  document.getElementById("sessionForm").addEventListener("submit", async e => {
    e.preventDefault();
    const f = formData(e.target);
    const body = {
      startTime: f.startTime.length === 16 ? f.startTime + ":00" : f.startTime,
      roomId: f.roomId ? Number(f.roomId) : null,
      repeatWeeks: Number(f.repeatWeeks || 1)
    };
    try {
      const res = existing
        ? await api(`/trainer/sessions/${existing.session_id}`, { method: "PUT", body })
        : await api(`/classes/${c.class_id}/sessions`, { method: "POST", body });
      modal.hide();
      notify(res.message);
      renderDashboard(existing ? "sessions" : sessionStorage.getItem("fz_dash_tab") || "classes");
    } catch (err) { notify(err.message, "danger"); }
  });
}

// ----- Schedule tab -----

async function dashSessions(content, lk, scope) {
  const sessions = await api(`/trainer/sessions?scope=${scope}`);
  content.innerHTML = `
    <div class="btn-group mb-3">
      <button class="btn btn-sm ${scope === "upcoming" ? "btn-dark" : "btn-outline-dark"}" data-scope="upcoming">Upcoming</button>
      <button class="btn btn-sm ${scope === "past" ? "btn-dark" : "btn-outline-dark"}" data-scope="past">Past</button>
    </div>
    ${!sessions.length ? `<p class="text-muted mb-0">No ${scope} sessions.</p>` : `
    <div class="table-responsive">
      <table class="table table-hover mb-0">
        <thead><tr><th>When</th><th>Class</th><th>Room</th><th>Booked</th><th>Status</th><th class="text-end">Actions</th></tr></thead>
        <tbody>
          ${sessions.map(s => `
            <tr>
              <td>${fmtDateTime(s.start_time)}</td>
              <td>${esc(s.title)} ${typeBadge(s.class_type)}</td>
              <td>${esc(s.room_name || "-")}</td>
              <td>${s.booked_count} / ${s.max_capacity}</td>
              <td>${statusBadge(s.status)}</td>
              <td class="text-end text-nowrap">
                <button class="btn btn-sm btn-outline-dark" data-roster="${s.session_id}" title="Roster"><i class="bi bi-people"></i></button>
                ${scope === "upcoming" && s.status === "scheduled" ? `
                  <button class="btn btn-sm btn-outline-dark" data-resched="${s.session_id}" title="Reschedule"><i class="bi bi-calendar-event"></i></button>
                  <button class="btn btn-sm btn-outline-warning" data-cancel-session="${s.session_id}" title="Cancel session"><i class="bi bi-x-circle"></i></button>` : ""}
                ${scope === "upcoming" && s.booked_count == 0 ? `
                  <button class="btn btn-sm btn-outline-danger" data-del-session="${s.session_id}" title="Delete"><i class="bi bi-trash"></i></button>` : ""}
              </td>
            </tr>`).join("")}
        </tbody>
      </table>
    </div>`}`;

  const byId = id => sessions.find(s => s.session_id == id);
  content.querySelectorAll("[data-scope]").forEach(b => b.addEventListener("click", () => dashSessions(content, lk, b.dataset.scope)));
  content.querySelectorAll("[data-roster]").forEach(b => b.addEventListener("click", () => showRoster(byId(b.dataset.roster))));
  content.querySelectorAll("[data-resched]").forEach(b => b.addEventListener("click", async () => {
    const s = byId(b.dataset.resched);
    const cls = await api(`/classes/${s.class_id}`);
    sessionForm(cls, lk, s);
  }));
  content.querySelectorAll("[data-cancel-session]").forEach(b => b.addEventListener("click", async () => {
    if (!confirm("Cancel this session? Everyone booked will have their booking cancelled.")) return;
    try {
      const res = await api(`/trainer/sessions/${b.dataset.cancelSession}/cancel`, { method: "POST" });
      notify(res.message, "secondary");
      dashSessions(content, lk, scope);
    } catch (err) { notify(err.message, "danger"); }
  }));
  content.querySelectorAll("[data-del-session]").forEach(b => b.addEventListener("click", async () => {
    if (!confirm("Delete this session?")) return;
    try {
      const res = await api(`/trainer/sessions/${b.dataset.delSession}`, { method: "DELETE" });
      notify(res.message, "secondary");
      dashSessions(content, lk, scope);
    } catch (err) { notify(err.message, "danger"); }
  }));
}

async function showRoster(s) {
  let roster;
  try { roster = await api(`/trainer/sessions/${s.session_id}/roster`); } catch (err) { return notify(err.message, "danger"); }
  const started = new Date(s.start_time) <= new Date();

  openModal(`${s.title} · ${fmtDateTime(s.start_time)}`, `
    <p class="text-muted">${s.booked_count} of ${s.max_capacity} spots booked${s.room_name ? " · " + esc(s.room_name) : ""}</p>
    ${!roster.length ? `<p class="mb-0">No one has booked this session yet.</p>` : `
    <table class="table table-sm">
      <thead><tr><th>Customer</th><th>Contact</th><th>Goal / notes</th><th>Status</th><th></th></tr></thead>
      <tbody>
        ${roster.map(r => `
          <tr>
            <td>${esc(r.first_name)} ${esc(r.last_name)}<br><span class="small font-monospace text-muted">${esc(r.confirmation_code)}</span></td>
            <td class="small">${esc(r.email)}<br>${esc(r.phone)}</td>
            <td class="small">${esc(r.fitness_goal)}${r.notes ? `<br><em>"${esc(r.notes)}"</em>` : ""}</td>
            <td>${statusBadge(r.status)}</td>
            <td class="text-nowrap">${rosterActions(r, started)}</td>
          </tr>`).join("")}
      </tbody>
    </table>`}`);

  document.querySelectorAll("#modalBody [data-set-status]").forEach(btn => btn.addEventListener("click", async () => {
    try {
      await api(`/trainer/bookings/${btn.dataset.id}/status`, { method: "PUT", body: { status: btn.dataset.setStatus } });
      notify("Booking updated.");
      const refreshed = (await api(`/trainer/sessions?scope=${started ? "past" : "upcoming"}`)).find(x => x.session_id == s.session_id) || s;
      showRoster(refreshed);
    } catch (err) { notify(err.message, "danger"); }
  }));
}

function rosterActions(r, started) {
  const btn = (status, label, cls) =>
    `<button class="btn btn-sm ${cls}" data-set-status="${status}" data-id="${r.booking_id}">${label}</button>`;
  if (r.status === "cancelled") return "";
  if (started) {
    return [
      r.status !== "attended" ? btn("attended", "Attended", "btn-outline-primary") : "",
      r.status !== "no_show" ? btn("no_show", "No-show", "btn-outline-danger") : ""
    ].join(" ");
  }
  return btn("cancelled", "Cancel", "btn-outline-danger");
}

// ----- Bookings tab -----

async function dashBookings(content, filters) {
  const f = { scope: "upcoming", ...filters };
  const rows = await api("/trainer/bookings" + toQuery(f));
  content.innerHTML = `
    <form class="row g-2 mb-3" id="tbFilter">
      <div class="col-md-4"><input class="form-control" name="q" placeholder="Customer, email, class or code" value="${esc(f.q)}"></div>
      <div class="col-6 col-md-3">
        <select class="form-select" name="scope">
          <option value="upcoming" ${f.scope === "upcoming" ? "selected" : ""}>Upcoming sessions</option>
          <option value="past" ${f.scope === "past" ? "selected" : ""}>Past sessions</option>
        </select></div>
      <div class="col-6 col-md-3">
        <select class="form-select" name="status">
          <option value="">All statuses</option>
          ${["confirmed", "cancelled", "attended", "no_show"].map(s => `<option value="${s}" ${f.status === s ? "selected" : ""}>${s.replace("_", " ")}</option>`).join("")}
        </select></div>
      <div class="col-md-2"><button class="btn btn-dark w-100">Filter</button></div>
    </form>
    ${!rows.length ? `<p class="text-muted mb-0">No bookings found.</p>` : `
    <p class="small text-muted">${rows.length} booking(s)</p>
    <div class="table-responsive">
      <table class="table table-hover table-sm mb-0">
        <thead><tr><th>Session</th><th>Class</th><th>Customer</th><th>Code</th><th>Booked on</th><th>Status</th><th></th></tr></thead>
        <tbody>
          ${rows.map(r => `
            <tr>
              <td>${fmtDateTime(r.start_time)}</td>
              <td>${esc(r.title)} ${typeBadge(r.class_type)}</td>
              <td>${esc(r.customer_name)}<br><span class="small text-muted">${esc(r.email)}</span></td>
              <td class="font-monospace small">${esc(r.confirmation_code)}</td>
              <td class="small">${fmtDate(r.booked_at)}</td>
              <td>${statusBadge(r.status)}</td>
              <td class="text-nowrap">${rosterActions(r, f.scope === "past")}</td>
            </tr>`).join("")}
        </tbody>
      </table>
    </div>`}`;

  content.querySelector("#tbFilter").addEventListener("submit", e => {
    e.preventDefault();
    dashBookings(content, formData(e.target));
  });
  content.querySelectorAll("[data-set-status]").forEach(btn => btn.addEventListener("click", async () => {
    try {
      await api(`/trainer/bookings/${btn.dataset.id}/status`, { method: "PUT", body: { status: btn.dataset.setStatus } });
      notify("Booking updated.");
      dashBookings(content, f);
    } catch (err) { notify(err.message, "danger"); }
  }));
}

// ============================ start ==================================

// Verify a saved login is still valid, then render the current page
(async function start() {
  if (Auth.token) {
    try { await api("/auth/me"); } catch { Auth.clear(); }
  }
  router();
})();
