/* =========================
   PROJECT STATUS PAGE
   Projects -> UAVs -> Parts
========================= */

/* Use same stations list as Tasks Board (for "current station" mapping) */
const psStations = [
    "הכנת תבניות וצבע",
    "ליווח סקין עליון",
    "ליווח סקין תחתון",
    "סגירה",
    "חליצה",
    "פינישים",
    "צבע",
    "QC"
];

/* =========================
   MOCK DATA (TEMP)
   Later replace with API call
========================= */
const projectsStatusMock = [
    {
        id: "p1",
        name: "מרוקו",
        uavCount: 2,
        status: { text: "עומד בזמנים", tone: "good" },
        deadline: "15/03/2025",
        progress: 40,
        uavs: [
            {
                id: "783",
                model: "WB",
                progress: 33,
                parts: [
                    { orderNum: "20936", partNum: "WB-CW-001", description: "WanderB VTOL G2 Central wing structure", serial: "SN-2024-001", qty: 2, progress: 33 },
                    { orderNum: "20937", partNum: "WB-MF-002", description: "WB Main fuselage assembly", serial: "SN-2024-002", qty: 1, progress: 55 },
                    { orderNum: "20938", partNum: "WB-TB-003", description: "WB Tail boom carbon fiber", serial: "SN-2024-003", qty: 3, progress: 100 }
                ]
            },
            {
                id: "784",
                model: "WB",
                progress: 50,
                parts: [
                    { orderNum: "21001", partNum: "WB-CW-010", description: "Central wing sub assembly", serial: "SN-2024-010", qty: 1, progress: 50 },
                    { orderNum: "21002", partNum: "WB-PN-011", description: "Panels kit", serial: "SN-2024-011", qty: 2, progress: 0 }
                ]
            }
        ]
    },
    {
        id: "p2",
        name: "תערוכה",
        uavCount: 1,
        status: { text: "באיחור", tone: "bad" },
        deadline: "01/02/2025",
        progress: 0,
        uavs: [
            {
                id: "501",
                model: "TBV",
                progress: 0,
                parts: [
                    { orderNum: "19876", partNum: "TB-PF-001", description: "Primary fuselage", serial: "SN-2023-921", qty: 1, progress: 0 }
                ]
            }
        ]
    },
    {
        id: "p3",
        name: "ניסוי מבצעי",
        uavCount: 2,
        status: { text: "מקדים לוח זמנים", tone: "info" },
        deadline: "20/04/2025",
        progress: 33,
        uavs: [
            {
                id: "900",
                model: "WB",
                progress: 33,
                parts: [
                    { orderNum: "23010", partNum: "WB-EL-100", description: "Electronics bay", serial: "SN-2024-101", qty: 1, progress: 33 }
                ]
            },
            {
                id: "901",
                model: "TBV",
                progress: 33,
                parts: [
                    { orderNum: "23011", partNum: "TB-WG-200", description: "Wing kit", serial: "SN-2024-102", qty: 1, progress: 66 }
                ]
            }
        ]
    },
    {
        id: "p4",
        name: "לקוח אסטרטגי",
        uavCount: 1,
        status: { text: "עומד בזמנים", tone: "good" },
        deadline: "10/05/2025",
        progress: 0,
        uavs: [
            {
                id: "777",
                model: "TBV",
                progress: 0,
                parts: [
                    { orderNum: "24001", partNum: "TB-XX-001", description: "Initial structure", serial: "SN-2024-777", qty: 1, progress: 0 }
                ]
            }
        ]
    }
];

/* =========================
   INIT
========================= */
function initProjectsStatus() {
    const root = document.getElementById("projects-status-root");
    const list = document.getElementById("projects-list");
    if (!root || !list) return;

    renderProjectsList(projectsStatusMock);
}

/* =========================
   RENDER: Projects List
========================= */
function renderProjectsList(projects) {
    const list = document.getElementById("projects-list");
    if (!list) return;

    list.innerHTML = projects.map(p => projectCardHtml(p)).join("");
}

/* =========================
   TOGGLES
   - Multiple projects can be open
   - Multiple UAVs can be open (inside each project)
========================= */
function toggleProject(projectId) {
    const card = document.getElementById(`ps-project-${projectId}`);
    const body = document.getElementById(`ps-project-body-${projectId}`);
    const chev = card?.querySelector(".ps-project-head .ps-chev");

    if (!card || !body) return;

    const isOpen = body.classList.contains("open");

    if (isOpen) {
        body.classList.remove("open");
        card.classList.remove("active");
        if (chev) chev.classList.remove("up");
    } else {
        body.classList.add("open");
        card.classList.add("active");
        if (chev) chev.classList.add("up");
    }
}

function toggleUav(projectId, uavId) {
    const body = document.getElementById(`ps-uav-body-${projectId}-${uavId}`);
    const row = document.getElementById(`ps-uav-row-${projectId}-${uavId}`);
    const chev = document.querySelector(`[data-ps-uav-toggle="${projectId}-${uavId}"] .ps-chev`);

    if (!body) return;

    const isOpen = body.classList.contains("open");

    if (isOpen) {
        body.classList.remove("open");
        if (row) row.classList.remove("active");
        if (chev) chev.classList.remove("up");
    } else {
        body.classList.add("open");
        if (row) row.classList.add("active");
        if (chev) chev.classList.add("up");
    }
}

/* =========================
   HTML Builders
========================= */
function projectCardHtml(p) {
    const statusPill = psStatusPill(p.status);
    const progress = clampPct(p.progress);

    return `
    <div class="ps-project-card" id="ps-project-${p.id}">
      <button class="ps-project-head" type="button"
        data-ps-project-toggle="${p.id}"
        onclick="toggleProject('${p.id}')">

        <div class="ps-project-title">
          <div class="ps-project-name">${escapeHtml(p.name)}</div>
          <div class="ps-project-sub">(${p.uavCount} כטב"מים)</div>
        </div>

        <div class="ps-project-meta">
          <div class="ps-project-meta-right">
            ${statusPill}
            <div class="ps-deadline">
              <span class="ps-clock">🕒</span>
              <span>דד ליין: ${escapeHtml(p.deadline)}</span>
            </div>
          </div>

          <div class="ps-project-meta-left">
            <div class="ps-percent">${progress}%</div>
            <div class="ps-progress">
              <div class="ps-progress-bar" style="width:${progress}%"></div>
            </div>
          </div>
        </div>

        <span class="ps-chev">˅</span>
      </button>

      <div class="ps-project-body" id="ps-project-body-${p.id}">
        <div class="ps-uav-list">
          ${p.uavs.map(u => uavRowHtml(p.id, u)).join("")}
        </div>
      </div>
    </div>
  `;
}

function uavRowHtml(projectId, u) {
    const prog = clampPct(u.progress);
    const partsCount = Array.isArray(u.parts) ? u.parts.length : 0;

    return `
    <div class="ps-uav-block">
      <button class="ps-uav-row" id="ps-uav-row-${projectId}-${u.id}" type="button"
              data-ps-uav-toggle="${projectId}-${u.id}"
              onclick="toggleUav('${projectId}','${u.id}')">

        <div class="ps-uav-right">
          <span class="ps-tag">${escapeHtml(u.model)}</span>
          <span class="ps-uav-id">כטב"ם ${escapeHtml(u.id)}</span>
          <span class="ps-uav-parts">${partsCount} חלקים</span>
        </div>

        <div class="ps-uav-left">
          <span class="ps-uav-pct">${prog}%</span>
          <div class="ps-uav-progress">
            <div class="ps-uav-progress-bar" style="width:${prog}%"></div>
          </div>
        </div>

        <span class="ps-chev">˅</span>
      </button>

      <div class="ps-uav-body" id="ps-uav-body-${projectId}-${u.id}">
        ${partsTableHtml(u.parts || [])}
      </div>
    </div>
  `;
}

function partsTableHtml(parts) {
    return `
    <div class="ps-parts-tablewrap">
      <table class="ps-parts-table">
        <thead>
          <tr>
            <th>מספר פק"ע</th>
            <th>מק"ט פריט</th>
            <th class="ps-col-desc">תיאור פריט</th>
            <th>סריאלי</th>
            <th>כמות מתוכננת</th>
            <th>תחנה נוכחית</th>
            <th>סטטוס</th>
          </tr>
        </thead>
        <tbody>
          ${parts.map(p => partsRowHtml(p)).join("")}
        </tbody>
      </table>
    </div>
  `;
}

function partsRowHtml(p) {
    const { statusText, statusTone, currentStation } = derivePartStatus(p);

    return `
    <tr>
      <td><a class="ps-link" href="javascript:void(0)">${escapeHtml(p.orderNum)}</a></td>
      <td>${escapeHtml(p.partNum)}</td>
      <td class="ps-col-desc">${escapeHtml(p.description)}</td>
      <td>${escapeHtml(p.serial || "-")}</td>
      <td>${escapeHtml(String(p.qty ?? "-"))}</td>
      <td>${escapeHtml(currentStation)}</td>
      <td>${psStatusPill({ text: statusText, tone: statusTone })}</td>
    </tr>
  `;
}

/* =========================
   STATUS Logic
   For now: derived from progress.
   Later: connect to task-board station statuses.
========================= */
function derivePartStatus(part) {
    const prog = clampPct(part.progress ?? 0);

    // station index from progress
    const idx = Math.min(psStations.length - 1, Math.floor((prog / 100) * psStations.length));
    const currentStation = prog === 0 ? "טרם התחיל" : psStations[idx];

    // tone
    if (prog >= 100) return { statusText: "בוצע", statusTone: "good", currentStation };
    if (prog === 0) return { statusText: "בתחילת", statusTone: "info", currentStation };
    return { statusText: "בעבודה", statusTone: "warn", currentStation };
}

function psStatusPill(s) {
    const tone = s?.tone || "info";
    const text = s?.text || "";

    let cls = "ps-pill";
    if (tone === "good") cls += " ps-pill-good";
    else if (tone === "bad") cls += " ps-pill-bad";
    else if (tone === "warn") cls += " ps-pill-warn";
    else cls += " ps-pill-info";

    return `<span class="${cls}">${escapeHtml(text)}</span>`;
}

/* =========================
   UTIL
========================= */
function clampPct(v) {
    const n = Number(v);
    if (Number.isNaN(n)) return 0;
    return Math.max(0, Math.min(100, Math.round(n)));
}

function escapeHtml(str) {
    return String(str ?? "")
        .replaceAll("&", "&amp;")
        .replaceAll("<", "&lt;")
        .replaceAll(">", "&gt;")
        .replaceAll('"', "&quot;")
        .replaceAll("'", "&#039;");
}
