/* =========================
   TASKS BOARD PAGE LOGIC
========================= */

/* Stations definition (page-specific) */
const tasksBoardStations = [
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
   INIT FUNCTION (called from router)
========================= */
function initTasksBoard() {
    if (typeof mockData === "undefined") {
        console.warn("mockData not found.");
        return;
    }

    renderTasksBoard(mockData);
}

/* =========================
   MAIN RENDER FUNCTION
========================= */
function renderTasksBoard(data) {
    const container = document.getElementById("tasks-board-container");
    if (!container) return;

    const grouped = groupByAircraftType(data);

    let html = "";

    for (const type in grouped) {
        html += buildAircraftSection(type, grouped[type]);
    }

    container.innerHTML = html;
}

/* =========================
   HELPERS
========================= */

function groupByAircraftType(data) {
    return data.reduce((acc, item) => {
        if (!acc[item.aircraftType]) acc[item.aircraftType] = [];
        acc[item.aircraftType].push(item);
        return acc;
    }, {});
}

function buildAircraftSection(type, rows) {
    return `
        <section class="production-group">
            <div class="group-banner">${type}</div>
            <div class="table-wrapper">
                <table class="prod-table">
                    <thead>
                        <tr>
                            <th>מספר פק"ע</th>
                            <th>מק"ט פריט</th>
                            <th>סריאלי</th>
                            <th class="desc-cell">תיאור פריט</th>
                            <th>כמות</th>
                            ${tasksBoardStations.map(s => `<th>${s}</th>`).join("")}
                            <th>התקדמות</th>
                        </tr>
                    </thead>
                    <tbody>
                        ${rows.map(r => buildRow(r)).join("")}
                    </tbody>
                </table>
            </div>
        </section>
    `;
}

function buildRow(row) {
    const rowData = encodeURIComponent(JSON.stringify(row));

    return `
        <tr>
            <td class="id-cell">${row.orderNum}</td>
            <td>${row.partNum}</td>
            <td>${row.serial || "-"}</td>
            <td class="desc-cell">${row.description}</td>
            <td>${row.quantity}</td>

            ${tasksBoardStations.map(station => `
                <td>
                    <button class="status-pill"
                        onclick="openStatusModal && openStatusModal('${rowData}', '${station}')">
                        טרם
                    </button>
                </td>
            `).join("")}

            <td>
                <div class="progress-circle-container">
                    <div class="progress-circle-fill"
                        style="background: conic-gradient(#007bff ${row.progress || 0}%, #eef2f7 0deg)">
                        <span>${row.progress || 0}%</span>
                    </div>
                </div>
            </td>
        </tr>
    `;
}
