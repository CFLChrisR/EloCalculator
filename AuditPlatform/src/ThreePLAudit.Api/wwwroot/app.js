"use strict";

// ------------------------------------------------------------------ helpers

const $ = (sel, root = document) => root.querySelector(sel);
const esc = (v) => String(v ?? "").replace(/[&<>"']/g, (c) => ({ "&": "&amp;", "<": "&lt;", ">": "&gt;", '"': "&quot;", "'": "&#39;" }[c]));
const money = (n) => n == null ? "" : Number(n).toLocaleString("en-US", { style: "currency", currency: "USD" });
const pct = (n) => `${Math.round(n * 100)}%`;
const date = (d) => d ? String(d).slice(0, 10) : "";
const slug = (s) => String(s ?? "").toLowerCase();
const badge = (text, cls) => `<span class="badge ${cls}">${esc(text)}</span>`;
const statusBadge = (s) => badge(cap(s), `s-${slug(s)}`);
const cap = (s) => s ? s[0].toUpperCase() + s.slice(1) : "";

async function api(path, opts = {}) {
  const res = await fetch(path, {
    ...opts,
    headers: opts.body ? { "Content-Type": "application/json" } : undefined,
    body: opts.body ? JSON.stringify(opts.body) : undefined,
  });
  if (!res.ok) {
    let msg = `${res.status} ${res.statusText}`;
    try { const j = await res.json(); msg = JSON.stringify(j.issues ?? j.validationIssues ?? j); } catch { /* not json */ }
    throw new Error(msg);
  }
  return res.status === 204 ? null : res.json();
}

function toast(msg) {
  const t = $("#toast");
  t.textContent = msg;
  t.hidden = false;
  clearTimeout(toast.timer);
  toast.timer = setTimeout(() => (t.hidden = true), 4000);
}

const dialog = $("#dialog");
function openDialog(html) {
  $("#dialog-body").innerHTML = html;
  dialog.showModal();
}
dialog.addEventListener("click", (e) => { if (e.target === dialog || e.target.matches("[data-close]")) dialog.close(); });

// ------------------------------------------------------------------ tabs

const loaders = { audits: loadRuns, library: loadLibrary, coverage: loadCoverage };
$("#tabs").addEventListener("click", (e) => {
  const btn = e.target.closest("button[data-tab]");
  if (btn) showTab(btn.dataset.tab);
});
function showTab(name) {
  document.querySelectorAll("#tabs button").forEach((b) => b.classList.toggle("active", b.dataset.tab === name));
  document.querySelectorAll(".tab").forEach((s) => (s.hidden = s.id !== `tab-${name}`));
  try { localStorage.setItem("tab", name); } catch { /* storage unavailable */ }
  loaders[name]();
}

// ------------------------------------------------------------------ audits

const state = { runId: null, run: null, filters: { category: "", disposition: "", review: "" }, library: new Map() };

$("#package-file").addEventListener("change", async (e) => {
  const file = e.target.files[0];
  e.target.value = "";
  if (!file) return;
  try {
    const pkg = JSON.parse(await file.text());
    const run = await api("/api/audit", { method: "POST", body: pkg });
    toast(`Audited ${run.clientName}: ${run.summary.findingCount} findings, ${money(run.summary.totalRecoverable)} recoverable.`);
    state.runId = run.runId;
    await loadRuns();
  } catch (err) {
    toast(`Upload failed: ${err.message}`);
  }
});

async function loadRuns() {
  const runs = await api("/api/runs");
  $("#run-list").innerHTML = runs.length ? runs.map((r) => `
    <li data-run="${esc(r.id)}" class="${r.id === state.runId ? "active" : ""}">
      <div><strong>${esc(r.clientName)}</strong></div>
      <div class="muted">${esc(r.packageId)} · ${esc(new Date(r.runAt).toLocaleString())}</div>
      <div><span class="amt">${money(r.totalRecoverable)}</span> <span class="muted">· ${r.findingCount} findings</span></div>
    </li>`).join("") : `<li class="muted">No runs yet.</li>`;
  if (!state.runId && runs.length) state.runId = runs[0].id;
  if (state.runId) await loadRun(state.runId);
}

$("#run-list").addEventListener("click", (e) => {
  const li = e.target.closest("li[data-run]");
  if (!li) return;
  state.runId = li.dataset.run;
  document.querySelectorAll("#run-list li").forEach((x) => x.classList.toggle("active", x === li));
  loadRun(state.runId);
});

async function loadRun(id) {
  const [run, lib] = await Promise.all([api(`/api/runs/${encodeURIComponent(id)}`), api("/api/regulations?includeFederal=true")]);
  state.run = run;
  state.library = new Map(lib.map((x) => [x.reference.id, x]));
  renderRun();
}

function renderRun() {
  const run = state.run;
  const s = run.summary;
  const f = state.filters;
  const rows = run.findings.filter((v) =>
    (!f.category || v.finding.category === f.category) &&
    (!f.disposition || v.finding.disposition === f.disposition) &&
    (!f.review || v.reviewStatus === f.review));
  const opt = (vals, cur, label) => `<option value="">${label}</option>` +
    vals.map((v) => `<option value="${v}" ${v === cur ? "selected" : ""}>${cap(v)}</option>`).join("");

  $("#run-view").innerHTML = `
    <div class="toolbar">
      <div><h2 style="margin:0">${esc(run.clientName)}</h2><div class="muted">${esc(run.packageId)} · run ${esc(new Date(run.runAt).toLocaleString())}</div></div>
      <span class="spacer"></span>
      <a class="btn" href="/api/runs/${encodeURIComponent(run.id)}/export.csv" download="findings-${esc(run.packageId)}.csv">Export CSV</a>
    </div>
    <div class="tiles">
      <div class="tile"><div class="label">Recoverable</div><div class="value">${money(s.totalRecoverable)}</div></div>
      <div class="tile"><div class="label">Auto-disputed</div><div class="value">${money(s.autoDisputeAmount)}</div><div class="muted">${s.autoDisputeCount} findings</div></div>
      <div class="tile"><div class="label">Needs review</div><div class="value">${s.needsReviewCount}</div></div>
      <div class="tile"><div class="label">Earliest deadline</div><div class="value">${date(s.earliestDeadline) || "—"}</div></div>
      <div class="tile"><div class="label">Audited spend</div><div class="value">${money((s.warehouseBilledAudited || 0) + (s.freightBilledAudited || 0) + (s.deductionsAudited || 0))}</div></div>
    </div>
    ${run.validationIssues?.length ? `<p class="note">${run.validationIssues.map((i) => `${esc(i.path)}: ${esc(i.message)}`).join("<br>")}</p>` : ""}
    <div class="filters">
      <select data-filter="category">${opt(["warehouseBilling", "freight", "deductions", "compliance"], f.category, "All categories")}</select>
      <select data-filter="disposition">${opt(["autoDispute", "needsReview", "informational"], f.disposition, "All dispositions")}</select>
      <select data-filter="review">${opt(["open", "disputed", "recovered", "rejected", "dismissed"], f.review, "Any review status")}</select>
      <span class="muted">${rows.length} of ${run.findings.length}</span>
    </div>
    <div class="table-wrap"><table>
      <thead><tr><th>Severity</th><th>Rule</th><th>Finding</th><th>Counterparty</th><th class="num">Recoverable</th><th class="num">Conf.</th><th>Deadline</th><th>Disposition</th><th>Review</th></tr></thead>
      <tbody>${rows.map((v) => `
        <tr data-finding="${esc(v.finding.id)}">
          <td>${badge(cap(v.finding.severity), `sev-${slug(v.finding.severity)}`)}</td>
          <td><code>${esc(v.finding.ruleId)}</code></td>
          <td>${esc(v.finding.title)}</td>
          <td>${esc(v.finding.counterparty)}</td>
          <td class="num">${money(v.finding.recoverableAmount)}</td>
          <td class="num">${pct(v.finding.confidence)}</td>
          <td>${date(v.finding.deadline)}</td>
          <td>${badge(cap(v.finding.disposition), `d-${slug(v.finding.disposition)}`)}</td>
          <td>${badge(cap(v.reviewStatus), `r-${slug(v.reviewStatus)}`)}</td>
        </tr>`).join("")}</tbody>
    </table></div>`;
}

$("#run-view").addEventListener("change", (e) => {
  const key = e.target.dataset.filter;
  if (!key) return;
  state.filters[key] = e.target.value;
  renderRun();
});

$("#run-view").addEventListener("click", (e) => {
  const tr = e.target.closest("tr[data-finding]");
  if (tr) openFinding(tr.dataset.finding);
});

function regLink(id) {
  const x = state.library.get(id);
  if (!x) return `<li><code>${esc(id)}</code> <span class="muted">(not in library)</span></li>`;
  const r = x.reference;
  return `<li>${statusBadge(x.status)} <strong>${esc(r.citation)}</strong>: ${esc(r.title)}
    ${r.sourceUrl ? `<br><a href="${esc(r.sourceUrl)}" target="_blank" rel="noopener">${esc(r.sourceAuthority || r.sourceUrl)}</a>` : ""}</li>`;
}

function openFinding(findingId) {
  const v = state.run.findings.find((x) => x.finding.id === findingId);
  const f = v.finding;
  const evidence = Object.entries(f.evidence || {});
  const reviewOpts = ["open", "disputed", "recovered", "rejected", "dismissed"]
    .map((s) => `<option value="${s}" ${s === v.reviewStatus ? "selected" : ""}>${cap(s)}</option>`).join("");
  openDialog(`
    <h3>${esc(f.title)}</h3>
    <div class="meta">${badge(cap(f.severity), `sev-${slug(f.severity)}`)} ${badge(cap(f.disposition), `d-${slug(f.disposition)}`)} <code>${esc(f.ruleId)}</code></div>
    <p>${esc(f.detail)}</p>
    <dl class="kv">
      <dt>Entity</dt><dd>${esc(f.entityType)} ${esc(f.entityRef)}</dd>
      <dt>Counterparty</dt><dd>${esc(f.counterparty || "—")}</dd>
      <dt>Billed / supported</dt><dd>${money(f.billedAmount) || "—"} / ${money(f.expectedAmount) || "—"}</dd>
      <dt>Recoverable</dt><dd><strong>${money(f.recoverableAmount)}</strong> at ${pct(f.confidence)} confidence</dd>
      <dt>Deadline</dt><dd>${date(f.deadline) || "—"} <span class="muted">${esc(f.deadlineBasis || "")}</span></dd>
      <dt>Why this disposition</dt><dd>${esc(f.dispositionReason || "")}</dd>
      ${evidence.map(([k, val]) => `<dt>${esc(k)}</dt><dd>${esc(val)}</dd>`).join("")}
    </dl>
    ${f.regulationIds?.length ? `<h4>Authority</h4><ul>${f.regulationIds.map(regLink).join("")}</ul>` : ""}
    <h4>Review</h4>
    <div class="form">
      <label>Status<select id="rv-status">${reviewOpts}</select></label>
      <label>Recovered amount<input id="rv-recovered" type="number" step="0.01" value="${v.recoveredAmount ?? ""}"></label>
      <label>Reviewer<input id="rv-by" type="text" value="${esc(v.reviewedBy || "")}"></label>
      <label class="full">Note<textarea id="rv-note">${esc(v.reviewNote || "")}</textarea></label>
    </div>
    <div class="dialog-actions">
      ${f.category !== "compliance" ? `<button class="btn" id="rv-draft">Draft dispute</button>` : ""}
      <button class="btn" data-close>Close</button>
      <button class="btn primary" id="rv-save">Save review</button>
    </div>
    <div id="rv-draft-out"></div>`);

  $("#rv-save").onclick = async () => {
    const recovered = $("#rv-recovered").value;
    await api(`/api/runs/${encodeURIComponent(state.run.id)}/findings/${encodeURIComponent(f.id)}`, {
      method: "PATCH",
      body: { status: $("#rv-status").value, note: $("#rv-note").value || null, reviewedBy: $("#rv-by").value || null, recoveredAmount: recovered === "" ? null : Number(recovered) },
    });
    dialog.close();
    toast("Review saved.");
    loadRun(state.run.id);
  };
  const draftBtn = $("#rv-draft");
  if (draftBtn) draftBtn.onclick = async () => {
    const d = await api(`/api/runs/${encodeURIComponent(state.run.id)}/findings/${encodeURIComponent(f.id)}/dispute`);
    $("#rv-draft-out").innerHTML = `
      <h4>${esc(d.subject)}</h4>
      ${d.warnings.length ? `<ul class="warnings">${d.warnings.map((w) => `<li>${esc(w)}</li>`).join("")}</ul>` : ""}
      <pre id="draft-body">${esc(d.body)}</pre>
      <button class="btn" id="copy-draft">Copy letter</button>`;
    $("#copy-draft").onclick = async () => {
      try { await navigator.clipboard.writeText(d.body); toast("Copied."); } catch { toast("Copy failed. Select the text instead."); }
    };
  };
}

// ------------------------------------------------------------------ library

let libInit = false;
async function initLibraryFilters() {
  if (libInit) return;
  const [jurisdictions, topics] = await Promise.all([api("/api/jurisdictions"), api("/api/topics")]);
  $("#lib-jurisdiction").innerHTML = `<option value="">All jurisdictions</option>` +
    jurisdictions.map((j) => `<option value="${esc(j.code)}">${esc(j.code)} · ${esc(j.name)}</option>`).join("");
  $("#lib-topic").innerHTML = `<option value="">All topics</option>` + topics.map((t) => `<option value="${esc(t)}">${esc(t)}</option>`).join("");
  state.jurisdictions = jurisdictions;
  state.topics = topics;
  libInit = true;
}

async function loadLibrary() {
  await initLibraryFilters();
  const params = new URLSearchParams();
  const j = $("#lib-jurisdiction").value, t = $("#lib-topic").value, q = $("#lib-q").value.trim();
  if (j) params.set("jurisdiction", j);
  if (t) params.set("topic", t);
  if (q) params.set("q", q);
  params.set("includeFederal", $("#lib-federal").checked);
  const results = await api(`/api/regulations?${params}`);
  state.library = new Map(results.map((x) => [x.reference.id, x]));
  $("#lib-count").textContent = `${results.length} entries`;
  $("#lib-results").innerHTML = results.map(renderCard).join("") || `<p class="muted">No entries match. Add one, or widen the filters.</p>`;
}

function renderCard({ reference: r, status }) {
  const v = r.verification || {};
  const req = r.requirement;
  return `<article class="card" data-id="${esc(r.id)}">
    <div class="meta">${badge(r.jurisdiction, "")} ${statusBadge(status)} ${badge(cap(r.kind), "")} ${(r.topics || []).map((t) => badge(t, "")).join(" ")}</div>
    <div class="cite">${esc(r.citation)}</div>
    <div>${esc(r.title)}</div>
    <div class="muted">${esc(r.summary)}</div>
    ${r.keyPoints?.length ? `<ul>${r.keyPoints.map((k) => `<li>${esc(k)}</li>`).join("")}</ul>` : ""}
    ${req ? `<div class="note">Enforced: facilities handling <strong>${esc(req.appliesToHandles.join(", "))}</strong> need ${esc(req.kind)} <code>${esc(req.recordType)}</code>${req.maxAgeMonths ? ` every ${req.maxAgeMonths} months` : ""}.</div>` : ""}
    <div class="src">${r.sourceUrl ? `Source: <a href="${esc(r.sourceUrl)}" target="_blank" rel="noopener">${esc(r.sourceAuthority || r.sourceUrl)}</a>` : `<span class="muted">No source yet.</span>`}
      ${(r.alternateUrls || []).map((u) => ` · <a href="${esc(u)}" target="_blank" rel="noopener">alt</a>`).join("")}</div>
    ${r.notes ? `<div class="muted"><em>${esc(r.notes)}</em></div>` : ""}
    <details><summary>Verification</summary>
      <dl class="kv">
        <dt>Expected phrase</dt><dd>${esc(r.verificationPhrase || "—")}</dd>
        <dt>Last checked</dt><dd>${v.lastCheckedAt ? esc(new Date(v.lastCheckedAt).toLocaleString()) : "never"} ${v.lastHttpStatus ? `(HTTP ${v.lastHttpStatus})` : ""} ${esc(v.lastError || "")}</dd>
        <dt>Phrase found</dt><dd>${v.lastPhraseFound == null ? "—" : v.lastPhraseFound ? "yes" : "no"}</dd>
        <dt>Verified by</dt><dd>${esc(v.verifiedBy || "—")} ${v.verifiedAt ? esc(new Date(v.verifiedAt).toLocaleDateString()) : ""}</dd>
        ${v.verifiedExcerpt ? `<dt>Excerpt</dt><dd>“${esc(v.verifiedExcerpt)}”</dd>` : ""}
        <dt>Id</dt><dd><code>${esc(r.id)}</code></dd>
      </dl>
    </details>
    <div class="actions">
      ${r.sourceUrl ? `<button class="btn small" data-act="check">Check source</button><button class="btn small" data-act="confirm">Confirm</button>` : ""}
      <button class="btn small" data-act="edit">Edit</button>
    </div>
  </article>`;
}

let searchTimer;
$("#lib-q").addEventListener("input", () => { clearTimeout(searchTimer); searchTimer = setTimeout(loadLibrary, 250); });
["#lib-jurisdiction", "#lib-topic", "#lib-federal"].forEach((s) => $(s).addEventListener("change", loadLibrary));

$("#lib-check-all").addEventListener("click", async () => {
  const j = $("#lib-jurisdiction").value;
  toast("Checking sources… this can take a minute.");
  try {
    const counts = await api(`/api/regulations/check-all${j ? `?jurisdiction=${encodeURIComponent(j)}` : ""}`, { method: "POST" });
    toast(Object.entries(counts).map(([k, n]) => `${k}: ${n}`).join(" · ") || "No sources to check.");
    loadLibrary();
  } catch (err) { toast(err.message); }
});

$("#lib-results").addEventListener("click", async (e) => {
  const btn = e.target.closest("button[data-act]");
  if (!btn) return;
  const id = btn.closest(".card").dataset.id;
  const entry = state.library.get(id);
  try {
    if (btn.dataset.act === "check") {
      const res = await api(`/api/regulations/${encodeURIComponent(id)}/check`, { method: "POST" });
      toast(`${id}: ${res.status}`);
      loadLibrary();
    } else if (btn.dataset.act === "confirm") {
      openConfirm(entry.reference);
    } else {
      openEditor(entry.reference);
    }
  } catch (err) { toast(err.message); }
});

function openConfirm(r) {
  openDialog(`
    <h3>Confirm ${esc(r.citation)}</h3>
    <p>Open the source, read it, and confirm it supports this entry. Paste the passage you relied on. If the page later changes and the expected phrase disappears, the entry is flagged for re-review.</p>
    <p><a href="${esc(r.sourceUrl)}" target="_blank" rel="noopener">${esc(r.sourceUrl)}</a></p>
    <div class="form">
      <label>Your name<input id="cf-by" type="text" required></label>
      <label class="full">Excerpt from source<textarea id="cf-excerpt"></textarea></label>
    </div>
    <div class="dialog-actions"><button class="btn" data-close>Cancel</button><button class="btn primary" id="cf-save">Confirm</button></div>`);
  $("#cf-save").onclick = async () => {
    const by = $("#cf-by").value.trim();
    if (!by) return toast("Enter your name.");
    await api(`/api/regulations/${encodeURIComponent(r.id)}/confirm`, { method: "POST", body: { verifiedBy: by, excerpt: $("#cf-excerpt").value || null } });
    dialog.close();
    toast("Confirmed.");
    loadLibrary();
  };
}

$("#lib-add").addEventListener("click", async () => {
  await initLibraryFilters();
  openEditor(null);
});

function openEditor(r) {
  const isNew = !r;
  r = r || { id: "", jurisdiction: $("#lib-jurisdiction").value || "US", kind: "statute", topics: [], title: "", citation: "", summary: "", keyPoints: [] };
  const kinds = ["statute", "regulation", "modelCode", "industryStandard", "agencyGuidance", "bestPractice"];
  const req = r.requirement;
  openDialog(`
    <h3>${isNew ? "Add reference" : `Edit ${esc(r.id)}`}</h3>
    <p class="muted">Changing the citation, summary, source or requirement clears any human verification.</p>
    <div class="form">
      <label>Id<input id="ed-id" type="text" value="${esc(r.id)}" ${isNew ? "" : "disabled"} placeholder="FL-WAREHOUSE-LICENSE"></label>
      <label>Jurisdiction<select id="ed-jur">${state.jurisdictions.map((j) => `<option value="${esc(j.code)}" ${j.code === r.jurisdiction ? "selected" : ""}>${esc(j.code)} · ${esc(j.name)}</option>`).join("")}</select></label>
      <label>Kind<select id="ed-kind">${kinds.map((k) => `<option value="${k}" ${k === r.kind ? "selected" : ""}>${k}</option>`).join("")}</select></label>
      <label>Citation<input id="ed-cite" type="text" value="${esc(r.citation)}"></label>
      <label class="full">Title<input id="ed-title" type="text" value="${esc(r.title)}"></label>
      <label class="full">Topics<select id="ed-topics" multiple size="5">${state.topics.map((t) => `<option ${r.topics.includes(t) ? "selected" : ""}>${esc(t)}</option>`).join("")}</select></label>
      <label class="full">Summary<textarea id="ed-summary">${esc(r.summary)}</textarea></label>
      <label class="full">Key points (one per line)<textarea id="ed-points">${esc((r.keyPoints || []).join("\n"))}</textarea></label>
      <label>Source URL (official source preferred)<input id="ed-url" type="url" value="${esc(r.sourceUrl || "")}"></label>
      <label>Source authority<input id="ed-auth" type="text" value="${esc(r.sourceAuthority || "")}"></label>
      <label>Expected phrase (for automated checks)<input id="ed-phrase" type="text" value="${esc(r.verificationPhrase || "")}"></label>
      <label>Notes<input id="ed-notes" type="text" value="${esc(r.notes || "")}"></label>
      <fieldset class="full"><legend>Enforce as a facility requirement (optional)</legend>
        <div class="form">
          <label>Kind<select id="ed-req-kind"><option value="">Not enforced</option>
            <option value="credential" ${req?.kind === "credential" ? "selected" : ""}>Credential (license, permit, registration)</option>
            <option value="training" ${req?.kind === "training" ? "selected" : ""}>Training</option></select></label>
          <label>Record type (matches intake)<input id="ed-req-type" type="text" value="${esc(req?.recordType || "")}" placeholder="StateWarehouseLicense"></label>
          <label>Applies to handles (comma separated, * = all)<input id="ed-req-handles" type="text" value="${esc((req?.appliesToHandles || ["*"]).join(", "))}"></label>
          <label>Training max age (months)<input id="ed-req-months" type="number" value="${req?.maxAgeMonths ?? ""}"></label>
        </div>
      </fieldset>
    </div>
    <div class="dialog-actions">
      ${isNew ? "" : `<button class="btn" id="ed-delete">Delete</button>`}
      <button class="btn" data-close>Cancel</button><button class="btn primary" id="ed-save">Save</button>
    </div>`);

  $("#ed-save").onclick = async () => {
    const id = (isNew ? $("#ed-id").value : r.id).trim();
    if (!id || !$("#ed-cite").value.trim() || !$("#ed-title").value.trim()) return toast("Id, citation and title are required.");
    const reqKind = $("#ed-req-kind").value;
    const body = {
      ...r,
      id,
      jurisdiction: $("#ed-jur").value,
      kind: $("#ed-kind").value,
      topics: [...$("#ed-topics").selectedOptions].map((o) => o.value),
      citation: $("#ed-cite").value.trim(),
      title: $("#ed-title").value.trim(),
      summary: $("#ed-summary").value.trim(),
      keyPoints: $("#ed-points").value.split("\n").map((s) => s.trim()).filter(Boolean),
      sourceUrl: $("#ed-url").value.trim() || null,
      sourceAuthority: $("#ed-auth").value.trim() || null,
      verificationPhrase: $("#ed-phrase").value.trim() || null,
      notes: $("#ed-notes").value.trim() || null,
      requirement: reqKind ? {
        kind: reqKind,
        recordType: $("#ed-req-type").value.trim(),
        appliesToHandles: $("#ed-req-handles").value.split(",").map((s) => s.trim()).filter(Boolean),
        maxAgeMonths: $("#ed-req-months").value ? Number($("#ed-req-months").value) : null,
      } : null,
    };
    delete body.status;
    if (!body.topics.length) return toast("Pick at least one topic.");
    if (reqKind && !body.requirement.recordType) return toast("Requirement needs a record type.");
    try {
      await api(`/api/regulations/${encodeURIComponent(id)}`, { method: "PUT", body });
      dialog.close();
      toast("Saved.");
      loadLibrary();
    } catch (err) { toast(err.message); }
  };
  const del = $("#ed-delete");
  if (del) del.onclick = async () => {
    if (!confirm(`Delete ${r.id}? This can't be undone.`)) return;
    await api(`/api/regulations/${encodeURIComponent(r.id)}`, { method: "DELETE" });
    dialog.close();
    loadLibrary();
  };
}

// ------------------------------------------------------------------ coverage

async function loadCoverage() {
  const cells = await api("/api/regulations/coverage");
  const topics = [...new Set(cells.map((c) => c.topic))];
  const states = [...new Set(cells.map((c) => c.jurisdiction))];
  const at = new Map(cells.map((c) => [`${c.jurisdiction}|${c.topic}`, c]));
  $("#coverage").innerHTML = `
    <thead><tr><th>State</th>${topics.map((t) => `<th>${esc(t)}</th>`).join("")}</tr></thead>
    <tbody>${states.map((s) => `<tr><td><strong>${esc(s)}</strong></td>${topics.map((t) => {
      const c = at.get(`${s}|${t}`);
      const cls = c.entries === 0 ? "gap" : c.verified === c.entries ? "done" : "some";
      return `<td class="cell ${cls}" data-state="${esc(s)}" data-topic="${esc(t)}" title="${c.entries} entries, ${c.verified} human-verified">${c.entries ? `${c.verified}/${c.entries}` : "—"}</td>`;
    }).join("")}</tr>`).join("")}</tbody>`;
}

$("#coverage").addEventListener("click", async (e) => {
  const td = e.target.closest("td.cell");
  if (!td) return;
  await initLibraryFilters();
  $("#lib-jurisdiction").value = td.dataset.state;
  $("#lib-topic").value = td.dataset.topic;
  $("#lib-federal").checked = false;
  showTab("library");
});

// ------------------------------------------------------------------ start

let initial = "audits";
try { initial = localStorage.getItem("tab") || "audits"; } catch { /* storage unavailable */ }
showTab(loaders[initial] ? initial : "audits");
