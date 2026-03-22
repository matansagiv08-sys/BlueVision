$(document).ready(function () {
    console.log("Tasks Board: DOM ready.");

    if (typeof ajaxCall === 'undefined') {
        console.log("ajaxCall missing, loading script...");
        $.getScript("../../../JS/ajaxCalls.js")
            .done(function () {
                console.log("ajaxCalls.js loaded successfully.");
                initBoard();
            })
            .fail(function () {
                console.error("Critical: Could not load ajaxCalls.js. Check the path!");
            });
    } else {
        initBoard();
    }
});

function initBoard() {
    let api = "api/TaskBoard/stages"; // בלי סלאש בהתחלה כי הוא כבר ישנו ב-server
    ajaxCall("GET", server + api, "",
        getStagesSuccess,
        (err) => console.error("Error fetching stages:", err)
    );
}

function getStagesSuccess(stagesFromDB) {
    const dynamicStations = stagesFromDB.map(s => s.productionStageName);

    let api = "api/TaskBoard";
    ajaxCall("GET", server + api, "",
        (boardData) => renderTasksBoard(boardData, dynamicStations),
        (err) => console.error("Error fetching board data:", err)
    );
}

function renderTasksBoard(boardData, stations) {
    const container = document.getElementById("tasks-board-container");
    if (!container) return;

    // קיבוץ לפי סוג מטוס
    const grouped = boardData.reduce((acc, row) => {
        const planeTypeName = row.planeTypeName || "כללי";
        (acc[planeTypeName] ||= []).push(row);
        return acc;
    }, {});

    let html = "";
    for (const planeTypeName in grouped) {
        html += `
            <section class="aircraft-section">
                <h2 class="tb-aircraft-title">${planeTypeName}</h2>
                <div class="table-container">
                    <table class="generic-data-table tb-table">
                        <thead>
                            <tr>
                                <th>פק"ע</th>
                                <th>מק"ט</th>
                                <th>מספר סידורי</th>
                                <th>כמות</th>
                                ${stations.map(s => `<th>${s}</th>`).join("")}
                            </tr>
                        </thead>
                        <tbody>
                            ${grouped[planeTypeName].map(row => renderRow(row, stations)).join("")}
                        </tbody>
                    </table>
                </div>
            </section>
        `;
    }
    container.innerHTML = html;
}

// רינדור שורה
function renderRow(row, stations) {
    return `
        <tr>
            <td>${row.workOrderID}</td>
            <td>${row.productionItemID}</td>
            <td>${row.serialNumber}</td>
            <td class="tb-col-qty">${row.plannedQty}</td>
            ${stations.map(stationName => {
        const stage = row.stages.find(s => s.productionStageName === stationName);
        const status = stage ? stage.productionStatusName : "none";
        return `<td>${renderPill(row.workOrderID, stationName, status)}</td>`;
    }).join("")}
        </tr>
    `;
}

function renderPill(orderId, stationName, status) {
    const statusClass = `status-${status}`;
    const map = { "done": "בוצע", "progress": "בתהליך", "hold": "עצור", "blocked": 'עצור כ"א', "none": "טרם" };
    const statusText = map[status] || "טרם";

    return `<div class="status-pill ${statusClass}" 
                 data-status="${status}" 
                 onclick="window.openStatusModal('${orderId}', '${stationName}', this)">
                 ${statusText}
            </div>`;
}

var tbActivePillEl = null;
var tbSelectedStatus = "none";

window.openStatusModal = function (orderNum, station, pillEl) {
    const modal = document.getElementById("genericModal");
    const body = document.getElementById("modalBody");
    const submitBtn = document.getElementById("modalSubmitBtn");

    if (!modal || !body || !submitBtn) return;

    tbActivePillEl = pillEl;
    tbSelectedStatus = pillEl.dataset.status || "none";

    document.getElementById("modalTitle").innerText = "עדכון סטטוס תחנה";

    body.innerHTML = `
        <div class="tb-modal-top" style="grid-column: span 2;">
            <div class="tb-modal-line"><span class="tb-label">פק"ע:</span><span class="tb-val">${orderNum}</span></div>
            <div class="tb-modal-line"><span class="tb-label">תחנה:</span><span class="tb-val">${station}</span></div>
        </div>
        <div class="tb-status-grid" style="grid-column: span 2; margin-top:15px;">
            <button type="button" class="tb-status-btn" data-status="done">בוצע</button>
            <button type="button" class="tb-status-btn" data-status="progress">בתהליך</button>
            <button type="button" class="tb-status-btn" data-status="hold">עצור זמנית</button>
            <button type="button" class="tb-status-btn" data-status="blocked">עצור כ"א</button>
        </div>
    `;

    setTbStatusActive(tbSelectedStatus);
    submitBtn.disabled = true;

    $(body).find(".tb-status-btn").on("click", function () {
        tbSelectedStatus = $(this).data("status");
        setTbStatusActive(tbSelectedStatus);
        submitBtn.disabled = false;
    });

    submitBtn.onclick = function () {
        if (tbActivePillEl) applyStatusToPill(tbActivePillEl, tbSelectedStatus);
        window.closeGenericModal();
    };

    modal.style.display = "flex";
};

function setTbStatusActive(status) {
    $(".tb-status-btn").each(function () {
        $(this).toggleClass("active", $(this).data("status") === status);
    });
}

function applyStatusToPill(pillEl, status) {
    $(pillEl).attr("class", "status-pill status-" + status);
    $(pillEl).attr("data-status", status);
    const map = { "done": "בוצע", "progress": "בתהליך", "hold": "עצור זמנית", "blocked": 'עצור כ"א', "none": "טרם" };
    $(pillEl).text(map[status] || "טרם");
}