$(document).ready(function () {
    console.log("Tasks Board: DOM ready.");
    if (typeof ajaxCall === 'undefined') {
        $.getScript("../../../JS/ajaxCalls.js").done(() => initBoard());
    } else {
        initBoard();
    }
});

function initBoard() {
    //  (שליפת התחנות (בשביל העמודות
    let apiStages = "api/ProductionStages";
    ajaxCall("GET", server + apiStages, "",
        getStagesSuccess,
        (err) => console.error("Error fetching stages:", err)
    );
}

function getStagesSuccess(allStages) {
    //   שליפת נתוני הלוח  
    let apiBoard = "api/ItemsInProduction/boardData";
    ajaxCall("GET", server + apiBoard, "",
        (boardData) => {
            renderTasksBoard(boardData, allStages);
        },
        (err) => console.error("Error fetching board data:", err)
    );
}

function renderTasksBoard(boardData, allStages) {
    const container = document.getElementById("tasks-board-container");
    if (!container) return;

    //  קיבוץ הנתונים לפי סוג מטוס
    const grouped = boardData.reduce((acc, row) => {
        let planeTypeName = "כללי"; // ברירת מחדל 
        if (row.planeID && row.planeID.type && row.planeID.type.planeTypeName) {
            planeTypeName = row.planeID.type.planeTypeName;
        } else if (row.PlaneID && row.PlaneID.Type) {
            planeTypeName = row.PlaneID.Type.PlaneTypeName;
        }
        if (!acc[planeTypeName]) {
            acc[planeTypeName] = [];
        }
        acc[planeTypeName].push(row);
        return acc;
    }, {});

    // בניית ה HTML 
    let html = "";
    for (const typeName in grouped) {
        html += `
            <section class="aircraft-section">
                <h2 class="tb-aircraft-title">${typeName}</h2>
                <div class="table-container">
                    <table class="generic-data-table tb-table">
                        <thead>
                            <tr>
                                <th>פק"ע</th>
                                <th>מק"ט</th>
                                <th>מספר סידורי</th>
                                <th>כמות</th>
                                ${allStages.map(s => `<th>${s.productionStageName}</th>`).join("")}
                            </tr>
                        </thead>
                        <tbody>
                            ${grouped[typeName].map(row => renderRow(row, allStages)).join("")}
                        </tbody>
                    </table>
                </div>
            </section>
        `;
    }

    container.innerHTML = html;
}

function renderRow(row, allStages) {
    return `
        <tr>
            <td>${row.workOrderID}</td>
            <td>${row.productionItem ? row.productionItem.productionItemID : '---'}</td>
            <td>${row.serialNumber}</td>
            <td class="tb-col-qty">${row.plannedQty}</td>
            ${allStages.map(stage => {
        const itemStage = row.stages.find(s => s.stage.productionStageID === stage.productionStageID);
        const statusName = itemStage ? itemStage.status.productionStatusName : "none";
        return `<td>${renderPill(row.serialNumber, stage.productionStageName, statusName, stage.productionStageID)}</td>`;
    }).join("")}
        </tr>`;
}

function renderPill(serialNumber, stationName, status, stageID) {
    const statusClass = `status-${status.toLowerCase()}`;
    // שינוי: הוספנו data-stageid כדי לשלוף אותו בקלות במודל
    return `<div class="status-pill ${statusClass}" 
                 data-status="${status}" 
                 data-stageid="${stageID}" 
                 onclick="window.openStatusModal('${serialNumber}', '${stationName}', this)">
                 ${status === "none" ? "טרם" : status}
            </div>`;
}

window.openStatusModal = function (serialNumber, station, pillEl) {
    const modal = document.getElementById("genericModal");
    const body = document.getElementById("modalBody");
    const submitBtn = document.getElementById("modalSubmitBtn");

    if (!modal || !body || !submitBtn) return;

    tbActivePillEl = pillEl;
    tbSelectedStatus = pillEl.dataset.status || "none";
    const stageID = pillEl.dataset.stageid; // שליפת ה-ID של התחנה מה-Pill

    document.getElementById("modalTitle").innerText = "עדכון סטטוס תחנה";

    body.innerHTML = `
        <div class="tb-modal-top">
            <div class="tb-modal-line"><span class="tb-label">מספר סידורי:</span><span class="tb-val">${serialNumber}</span></div>
            <div class="tb-modal-line"><span class="tb-label">תחנה:</span><span class="tb-val">${station}</span></div>
        </div>
        <div class="tb-status-grid" style="margin-top: 20px;">
            <button type="button" class="tb-status-btn" data-status="בוצע">בוצע</button>
            <button type="button" class="tb-status-btn" data-status="בתהליך">בתהליך</button>
            <button type="button" class="tb-status-btn" data-status="תקוע">תקוע</button>
            <button type="button" class="tb-status-btn" data-status="טרם בוצע">טרם בוצע</button>
        </div>`;

    setTbStatusActive(tbSelectedStatus);
    modal.style.display = "flex";

    $(body).find(".tb-status-btn").on("click", function () {
        tbSelectedStatus = $(this).data("status");
        setTbStatusActive(tbSelectedStatus);
        submitBtn.disabled = false;
    });

    submitBtn.onclick = function () {
        const statusMap = { "טרם בוצע": 1, "בתהליך": 2, "תקוע": 3, "בוצע": 4 };

        const updateData = {
            SerialNumber: parseInt(serialNumber),
            ProductionStageID: parseInt(stageID),
            ProductionStatusID: statusMap[tbSelectedStatus]
        };

        ajaxCall("PUT", server + "api/ItemsInProduction/updateStatus", JSON.stringify(updateData),
            (res) => {
                applyStatusToPill(tbActivePillEl, tbSelectedStatus);
                window.closeGenericModal();
                console.log("Status updated!");
            },
            (err) => {
                console.error("Update failed:", err);
                alert("שגיאה בעדכון הסטטוס בשרת");
            }
        );
    };
};

