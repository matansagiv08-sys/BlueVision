$(document).ready(function () {
    console.log("Tasks Board: DOM ready.");
    if (typeof ajaxCall === 'undefined') {
        $.getScript("../../../JS/ajaxCalls.js").done(() => initBoard());
    } else {
        initBoard();
    }
});

function initBoard() {
    ajaxCall("GET", server + "api/ProductionStages", "",
        (allStages) => {
            ajaxCall("GET", server + "api/ItemsInProduction/boardData", "",
                (boardData) => renderTasksBoard(boardData, allStages),
                (err) => console.error("Error board data:", err)
            );
        },
        (err) => console.error("Error stages:", err)
    );
}

function renderTasksBoard(boardData, allStages) {
    const container = document.getElementById("tasks-board-container");
    if (!container) return;


    const grouped = boardData.reduce((acc, row) => {
        let planeTypeName = row.planeID?.type?.planeTypeName || row.PlaneID?.Type?.PlaneTypeName || "כללי";
        if (!acc[planeTypeName]) acc[planeTypeName] = [];
        acc[planeTypeName].push(row);
        return acc;
    }, {});

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
                                <th>שם פריט</th>
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
            </section>`;
    }
    container.innerHTML = html;
}

function renderRow(row, allStages) {
    const item = row.productionItem || row.ProductionItem || {};
    const itemID = item.productionItemID || item.ProductionItemID || '---';
    const itemName = item.productionItemDescription || item.ItemName || item.itemName || "---";
    const workOrder = row.workOrderID || row.WorkOrderID || '---';

    return `
        <tr>
            <td>${workOrder}</td>
            <td>${itemID}</td>
            <td class="tb-col-item-name">${itemName}</td>
            <td>${row.serialNumber || row.SerialNumber}</td>
            <td class="tb-col-qty">${row.plannedQty || row.PlannedQty}</td>
            ${allStages.map(stage => {
        const stagesList = row.stages || row.Stages || [];
        const itemStage = stagesList.find(s =>
            (s.stage?.productionStageID || s.Stage?.ProductionStageID) === stage.productionStageID
        );


        let isBlocked = false;
        if (stage.productionStageID > 1) {
            const prevStage = stagesList.find(s =>
                (s.stage?.productionStageID || s.Stage?.ProductionStageID) === stage.productionStageID - 1
            );
            const prevStatusID = prevStage?.status?.productionStatusID || prevStage?.Status?.ProductionStatusID || 1;

            if (prevStatusID !== 4) { 
                isBlocked = true;
            }
        }

        const status = itemStage?.status || itemStage?.Status || {};
        const sID = status.productionStatusID || status.ProductionStatusID || 1;
        const sName = status.productionStatusName || status.ProductionStatusName || "טרם בוצע";
        const comment = itemStage?.comment || itemStage?.Comment || "";


        const clickAttr = isBlocked ? "" : `onclick="window.openStatusModal('${row.serialNumber || row.SerialNumber}', '${itemID}', '${itemName}', '${workOrder}', ${stage.productionStageID}, '${stage.productionStageName}', ${sID}, this, '${comment.replace(/'/g, "\\'")}')"`;
        const blockedClass = isBlocked ? "blocked" : "";
        const titleText = isBlocked ? "חסום: בצע תחנה קודמת תחילה" : comment;

        return `<td>
                    <div class="status-pill status-${sID} ${blockedClass}" 
                         title="${titleText}" 
                         ${clickAttr}>
                         ${sName}
                    </div>
                </td>`;
    }).join("")}
        </tr>`;
}
window.openStatusModal = function (serialNumber, itemID, itemName, workOrder, stageID, stageName, currentStatusID, pillEl, currentComment) {
    const modal = document.getElementById("genericModal");
    const submitBtn = document.getElementById("modalSubmitBtn");
    const timeContainer = document.getElementById("timeInputContainer");
    const timeLabel = document.getElementById("timeLabel");
    const timeInput = document.getElementById("userTimeInput");


    timeContainer.style.display = "none";
    timeInput.value = "";
    document.getElementById("statusCommentInput").value = currentComment || "";
    submitBtn.disabled = true;
    function setCurrentTime() {
        const now = new Date();
        now.setMinutes(now.getMinutes() - now.getTimezoneOffset());
        timeInput.value = now.toISOString().slice(0, 16);
    }


    const displayItemName = (typeof itemName === 'object') ? "---" : itemName;
    const displayWorkOrder = (typeof workOrder === 'object') ? "---" : workOrder;

    document.getElementById("modalTaskInfo").innerHTML = `
        <div style="display:grid; grid-template-columns: 1fr 1fr; gap:10px;">
            <div><span class="tb-label">פק"ע:</span> <span class="tb-val">${displayWorkOrder}</span></div>
            <div><span class="tb-label">סידורי:</span> <span class="tb-val">${serialNumber}</span></div>
            <div style="grid-column: span 2;"><span class="tb-label">פריט:</span> <span class="tb-val">${itemID} - ${displayItemName}</span></div>
            <div style="grid-column: span 2; color:#3b82f6;"><span class="tb-label">תחנה:</span> <span class="tb-val">${stageName}</span></div>
        </div>`;

 
    const statusContainer = document.getElementById("statusButtonsContainer");
    statusContainer.innerHTML = `
        <button type="button" class="btn-choice" data-id="4">בוצע</button>
        <button type="button" class="btn-choice" data-id="2">בתהליך</button>
        <button type="button" class="btn-choice" data-id="3">תקוע</button>
        <button type="button" class="btn-choice" data-id="5">אחר</button>
        <button type="button" class="btn-choice full-width" data-id="1">טרם בוצע / איפוס</button>
    `;


    const currentBtn = $(`.btn-choice[data-id="${currentStatusID}"]`);
    if (currentBtn.length > 0) {
        currentBtn.addClass(`active status-${currentStatusID}`);
        if (currentStatusID == 2 || currentStatusID == 4) {
            timeContainer.style.display = "block";
            timeLabel.innerText = currentStatusID == 2 ? "זמן התחלה:" : "זמן סיום:";
        }
    }


    $(".btn-choice").on("click", function () {
        const sid = $(this).data("id"); 

        $(".btn-choice").removeClass("active status-1 status-2 status-3 status-4 status-5");
        $(this).addClass(`active status-${sid}`);
        submitBtn.disabled = false;

        if (sid == 2 || sid == 4) {
            timeContainer.style.display = "block";
            timeLabel.innerText = sid == 2 ? "זמן התחלה:" : "זמן סיום:";
            setCurrentTime(); 
        } else {
            timeContainer.style.display = "none";
            timeInput.value = "";
        }
    });

    submitBtn.onclick = function () {
        const activeBtn = $(".btn-choice.active");
        const statusId = parseInt(activeBtn.data("id"));
        const statusName = activeBtn.text();
        const comment = document.getElementById("statusCommentInput").value;
        const userTimeVal = timeInput.value;

        const updateData = {
            SerialNumber: parseInt(serialNumber),
            ProductionItemID: itemID.toString(),
            ProductionStageID: parseInt(stageID),
            ProductionStatusID: statusId,
            Comment: comment
        };

        if (userTimeVal && (statusId == 2 || statusId == 4)) {
            updateData.UserTime = userTimeVal;
        }

        ajaxCall("PUT", server + "api/ItemsInProduction/updateStatus", JSON.stringify(updateData),
            function (res) {
                console.log("Update successful");
                initBoard();
                window.closeGenericModal();
            },
            function (err) {
                alert("שגיאה בעדכון הסטטוס: " + err.responseText);
            }
        );
    };

    modal.style.display = "flex";
};

