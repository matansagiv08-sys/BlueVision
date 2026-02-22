// 1) Stations
const tasksBoardStations = ["הכנת תבניות", "ליווח", "סגירה", "חליצה", "פינישים", "QC"];

// 2) Init (called by router)
window.initTasksBoard = function () {
    const dataToRender = (typeof mockData !== "undefined") ? mockData : [
        { orderNum: "20936", partNum: "WB-CW-001", description: "Central wing", aircraftType: "WB", quantity: 2, progress: 25 },
        { orderNum: "1002", partNum: "PUMA-TAIL", description: "Tail Assembly", aircraftType: "Puma", quantity: 1, progress: 10 }
    ];

    renderTasksBoard(dataToRender);
};

// 3) Render table (NO row click)
function renderTasksBoard(data) {
    const container = document.getElementById("tasks-board-container");
    if (!container) return;

    const grouped = data.reduce((acc, item) => {
        (acc[item.aircraftType] ||= []).push(item);
        return acc;
    }, {});

    let html = "";

    for (const type in grouped) {
        html += `
      <section class="aircraft-section">
        <h2 class="tb-aircraft-title">${type}</h2>

        <div class="table-container">
          <table class="generic-data-table tb-table">
            <thead>
              <tr>
                <th class="tb-col-order">פק"ע</th>
                <th class="tb-col-part">מק"ט</th>
                <th class="tb-col-desc">תיאור</th>
                <th class="tb-col-qty">כמות</th>
                ${tasksBoardStations.map(s => `<th class="tb-col-station">${s}</th>`).join("")}
                <th class="tb-col-progress">התקדמות</th>
              </tr>
            </thead>
            <tbody>
              ${grouped[type].map(item => `
                <tr>
                  <td class="tb-col-order"><strong>${item.orderNum}</strong></td>
                  <td class="tb-col-part">${item.partNum}</td>
                  <td class="tb-col-desc">${item.description}</td>
                  <td class="tb-col-qty">${item.quantity}</td>

                  ${tasksBoardStations.map(station => `
                    <td class="tb-col-station">
                      <button
                        type="button"
                        class="status-pill status-none"
                        data-status="none"
                        onclick="event.stopPropagation(); window.openStatusModal('${item.orderNum}', '${station}', this)"
                      >טרם</button>
                    </td>
                  `).join("")}

                  <td class="tb-col-progress">${item.progress}%</td>
                </tr>
              `).join("")}
            </tbody>
          </table>
        </div>
      </section>
    `;
    }

    container.innerHTML = html;
}

window.renderTasksBoard = renderTasksBoard;

// ---- Modal logic (updates the clicked pill) ----
let tbSelectedStatus = "none";
let tbActivePillEl = null;

window.openStatusModal = function (orderNum, station, pillEl) {
    const modal = document.getElementById("genericModal");
    const body = document.getElementById("modalBody");
    const title = document.getElementById("modalTitle");
    const submitBtn = document.getElementById("modalSubmitBtn");

    if (!modal || !body || !submitBtn) return;

    tbActivePillEl = pillEl || null;
    tbSelectedStatus = pillEl?.dataset?.status || "none";

    if (title) title.innerText = "עדכון סטטוס תחנה";

    body.innerHTML = `
    <div class="tb-modal-top" style="grid-column: span 2;">
      <div class="tb-modal-line"><span class="tb-label">פק"ע:</span><span class="tb-val">${orderNum}</span></div>
      <div class="tb-modal-line"><span class="tb-label">תחנה:</span><span class="tb-val">${station}</span></div>
    </div>

    <div class="tb-status-title" style="grid-column: span 2;">בחר סטטוס חדש</div>

    <div class="tb-status-grid" style="grid-column: span 2;">
      <button type="button" class="tb-status-btn" data-status="done">בוצע</button>
      <button type="button" class="tb-status-btn" data-status="progress">בתהליך</button>
      <button type="button" class="tb-status-btn" data-status="hold">עצור זמנית</button>
      <button type="button" class="tb-status-btn" data-status="blocked">עצור כ"א</button>
    </div>

    <div class="tb-note-wrap" style="grid-column: span 2;">
      <div class="tb-note-title">הערות (אופציונלי)</div>
      <textarea id="tbNote" class="tb-note" placeholder="הוסף הערות או פרטים נוספים..."></textarea>
    </div>
  `;

    // mark active
    setTbStatusActive(tbSelectedStatus);

    // disable until user changes
    submitBtn.disabled = true;

    body.querySelectorAll(".tb-status-btn").forEach(btn => {
        btn.addEventListener("click", () => {
            tbSelectedStatus = btn.dataset.status;
            setTbStatusActive(tbSelectedStatus);
            submitBtn.disabled = false;
        });
    });

    // what Confirm does
    window.handleModalSubmit = function () {
        if (tbActivePillEl) applyStatusToPill(tbActivePillEl, tbSelectedStatus);
        window.closeGenericModal();
    };

    modal.style.display = "flex";
};

function setTbStatusActive(status) {
    const body = document.getElementById("modalBody");
    if (!body) return;

    body.querySelectorAll(".tb-status-btn").forEach(btn => {
        btn.classList.toggle("active", btn.dataset.status === status);
    });
}

function applyStatusToPill(pillEl, status) {
    pillEl.classList.remove("status-none", "status-done", "status-progress", "status-hold", "status-blocked");
    pillEl.dataset.status = status;

    switch (status) {
        case "done":
            pillEl.textContent = "בוצע";
            pillEl.classList.add("status-done");
            break;
        case "progress":
            pillEl.textContent = "בתהליך";
            pillEl.classList.add("status-progress");
            break;
        case "hold":
            pillEl.textContent = "עצור זמנית";
            pillEl.classList.add("status-hold");
            break;
        case "blocked":
            pillEl.textContent = 'עצור כ"א';
            pillEl.classList.add("status-blocked");
            break;
        default:
            pillEl.textContent = "טרם";
            pillEl.classList.add("status-none");
            break;
    }
}