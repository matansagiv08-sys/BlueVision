//window.initTasksBoard = function () {
//    if (typeof ajaxCall === 'undefined') {
//        $.getScript("../../../JS/ajaxCalls.js").done(() => initBoard());
//    } else {
//        initBoard();
//    }
//};

window.initTasksBoard = function () {
    bindTaskBoardCellTooltips();
    initBoard();
};

// פונקציה לסגירת המודל של עדכון סטטוס
window.closeTaskStatusModal = function () {
    const modal = document.getElementById("tbStatusModal");
    if (modal) {
        modal.style.display = "none";
    }
};

let allBoardData = []; 
let allStagesData = []; 
let allStatusesData = [];
let taskBoardPayloadCounter = 0;
const taskBoardStatusPayloads = new Map();
const taskBoardRowPayloads = new Map();
let taskBoardEditOptions = null;
let taskBoardEditOptionsLoading = false;
let currentEditProductionRowKey = null;

function getStageIdValue(stage) {
    return parseInt(stage?.productionStageID || stage?.ProductionStageID || 0);
}

function getStageStatusById(stagesList, stageId) {
    const currentStageObj = (stagesList || []).find(s =>
        parseInt(s.stage?.productionStageID || s.Stage?.ProductionStageID || 0) === parseInt(stageId)
    );
    return currentStageObj?.status?.productionStatusID || currentStageObj?.Status?.ProductionStatusID || 1;
}

function bindTaskBoardCellTooltips() {
    const container = document.getElementById("tasks-board-container");
    if (!container || container.dataset.tooltipBound === "true") return;
    container.dataset.tooltipBound = "true";

    container.addEventListener("mouseover", function (e) {
        const target = e.target instanceof Element ? e.target.closest(".task-cell-tooltip") : null;
        if (target) showTaskBoardCellTooltip(target);
    });

    container.addEventListener("mouseout", function (e) {
        const target = e.target instanceof Element ? e.target.closest(".task-cell-tooltip") : null;
        if (!target) return;
        const related = e.relatedTarget instanceof Element ? e.relatedTarget.closest(".task-cell-tooltip") : null;
        if (related !== target) hideTaskBoardCellTooltip();
    });

    container.addEventListener("focusin", function (e) {
        const target = e.target instanceof Element ? e.target.closest(".task-cell-tooltip") : null;
        if (target) showTaskBoardCellTooltip(target);
    });

    container.addEventListener("focusout", hideTaskBoardCellTooltip);
    container.addEventListener("scroll", hideTaskBoardCellTooltip, true);
    window.addEventListener("scroll", hideTaskBoardCellTooltip, true);
    window.addEventListener("resize", hideTaskBoardCellTooltip);
}

function initBoard() {
    console.time("[TasksBoard] initial load");
    const container = document.getElementById("tasks-board-container");
    if (container) {
        container.innerHTML = "";
    }

    Promise.all([
        loadStatusesAsync(),
        loadStagesAsync(),
        loadBoardDataAsync()
    ]).then(([statuses, stages, boardData]) => {
        allStatusesData = statuses;
        allStagesData = stages;
        allBoardData = boardData;

        populateStageFilter(allStagesData);
        populateProjectFilter(allBoardData);
        applyFilters();
        console.timeEnd("[TasksBoard] initial load");
    }).catch(err => {
        console.error("Error loading tasks board:", err);
    });
}

function loadStatusesAsync() {
    return new Promise((resolve, reject) => loadStatuses(resolve, reject));
}

function loadStagesAsync() {
    return new Promise((resolve, reject) => loadStages(resolve, reject));
}

function loadBoardDataAsync() {
    return new Promise((resolve, reject) => loadBoardData(resolve, reject));
}

function loadStatuses(successCallback, errorCallback) {
    console.time("[TasksBoard] load statuses");
    ajaxCall(
        "GET",
        server + "api/ProductionStatuses",
        "",
        function (statuses) {
            console.timeEnd("[TasksBoard] load statuses");
            successCallback(statuses);
        },
        function (err) {
            console.error("Error fetching statuses:", err);
            if (typeof errorCallback === "function") errorCallback(err);
        }
    );
}

function loadStages(successCallback, errorCallback) {
    console.time("[TasksBoard] load stages");
    ajaxCall(
        "GET",
        server + "api/ProductionStages",
        "",
        function (allStages) {
            const sortedStages = [...allStages].sort((a, b) => {
                const orderA = parseInt(a.stageOrder || a.StageOrder || 0);
                const orderB = parseInt(b.stageOrder || b.StageOrder || 0);
                return orderA - orderB;
            });
            console.timeEnd("[TasksBoard] load stages");
            successCallback(sortedStages);
        },
        function (err) {
            console.error("Error stages:", err);
            if (typeof errorCallback === "function") errorCallback(err);
        }
    );
}

function loadBoardData(successCallback, errorCallback) {
    console.time("[TasksBoard] load boardData");
    ajaxCall(
        "GET",
        server + "api/ItemsInProduction/boardData",
        "",
        function (boardData) {
            console.timeEnd("[TasksBoard] load boardData");
            successCallback(boardData);
        },
        function (err) {
            console.error("Error board data:", err);
            if (typeof errorCallback === "function") errorCallback(err);
        }
    );
}

function renderTasksBoard(boardData, allStages) {
    const container = document.getElementById("tasks-board-container");
    if (!container) return;
    hideTaskBoardCellTooltip();
    taskBoardPayloadCounter = 0;
    taskBoardStatusPayloads.clear();
    taskBoardRowPayloads.clear();


    const grouped = boardData.reduce((acc, row) => {
        let planeTypeName = row.planeID?.type?.planeTypeName || row.PlaneID?.Type?.PlaneTypeName || "כללי";
        if (!acc[planeTypeName]) acc[planeTypeName] = [];
        acc[planeTypeName].push(row);
        return acc;
    }, {});

    const orderedTypeNames = Object.keys(grouped).sort();

    let html = "";
    for (const typeName of orderedTypeNames) {
        html += `
            <section class="aircraft-section">
                <h2 class="tb-aircraft-title">${typeName}</h2>
                <div class="table-container tasks-table-wrapper">
                    <table class="generic-data-table tb-table">
                        <thead>
                            <tr>
                                <th class="tb-col-actions" aria-label="פעולות"></th>
                                <th class="tb-col-wo">פק&quot;ע</th>
                                <th class="tb-col-project">שם פרויקט</th>
                                <th class="tb-col-tail">מספר זנב</th>
                                <th class="tb-col-item-id">מק&quot;ט</th>
                                <th class="tb-col-item-name">שם פריט</th>
                                <th class="tb-col-sn">סיריאלי</th>
                                <th class="tb-col-qty">כמות</th>
                                ${allStages.map(s => `<th class="tb-col-stage status-col">${escapeHtml(s.productionStageName || s.ProductionStageName || "")}</th>`).join('')}
                                <th class="tb-col-progress">התקדמות</th>
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
    const itemName = (item.itemName || item.productionItemDescription || "---").replace(/['"]/g, "");

    const workOrder = row.workOrderID || row.WorkOrderID || '---';
    const projectName = row.projectName || row.ProjectName || row.planeID?.project?.projectName || row.PlaneID?.Project?.ProjectName || '---';
    const tailNumber = row.tailNumber || row.TailNumber || '---';
    const serial = row.serialNumber || row.SerialNumber || '---';
    const qty = row.plannedQty || row.PlannedQty || 0;
    const progressValue = Math.round(row.progress || row.Progress || 0);
    const rowPayloadKey = registerTaskPayload(taskBoardRowPayloads, buildProductionRowPayload(row));
    const stagesById = new Map((row.stages || row.Stages || []).map(stageRow => [getStageIdValue(stageRow.stage || stageRow.Stage), stageRow]));

    const stagesHtml = allStages.map((stage, stageIndex) => {
        const stagesList = row.stages || row.Stages || [];
        const stageId = getStageIdValue(stage);
        const itemStage = stagesById.get(stageId);

        //חסימת התחנות הבאות אם התחנה הנוכחית לא בוצעה במלואה
        let isBlocked = false;
        if (stageIndex > 0) {
            const prevStageId = getStageIdValue(allStages[stageIndex - 1]);
            const prevStatusID = getStageStatusById(stagesList, prevStageId);
            isBlocked = prevStatusID !== 4;
        }
        
        const status = itemStage?.status || itemStage?.Status || {};
        const sID = status.productionStatusID || status.ProductionStatusID || 1;
        const sName = status.productionStatusName || status.ProductionStatusName || "טרם בוצע";
        const comment = (itemStage?.comment || itemStage?.Comment || "").replace(/'/g, "\\'");
        const statusPayloadKey = registerTaskPayload(taskBoardStatusPayloads, {
            serialNumber: serial,
            itemID,
            itemName,
            workOrder,
            stageID: stageId,
            stageName: stage.productionStageName || stage.ProductionStageName || "",
            currentStatusID: sID,
            currentComment: itemStage?.comment || itemStage?.Comment || "",
            startTime: itemStage?.startTimeStamp || itemStage?.StartTimeStamp || "",
            finishTime: itemStage?.finishTimeStamp || itemStage?.FinishTimeStamp || ""
        });

        const clickAttr = isBlocked ? "" : `onclick="window.openStatusModal('${statusPayloadKey}', this)"`;
        const blockedClass = isBlocked ? "blocked" : "";
        const titleText = isBlocked ? "חסום: בצע תחנה קודמת תחילה" : (itemStage?.comment || "");

        return `
            <td class="status-cell status-col">
                <div class="status-wrapper">
                    <div class="status-pill status-${sID} ${blockedClass}" 
                         title="${titleText}" 
                         ${clickAttr}>
                         ${sName}
                    </div>
                </div>
            </td>`;
    }).join("");

    // הצגת השורה בטבלה
    return `
        <tr>
            <td class="tb-col-actions">
                <div class="row-actions-wrap">
                    <button class="row-actions-btn" type="button" onclick="window.toggleTaskRowMenu(this)" title="פעולות" aria-label="פעולות">
                        <span></span><span></span><span></span>
                    </button>
                    <div class="row-actions-menu">
                        <button type="button" onclick="window.openEditProductionRowModal('${rowPayloadKey}')">עריכת פריט בייצור</button>
                    </div>
                </div>
            </td>
            <td class="tb-col-wo">${renderTaskCellWithTooltip(workOrder)}</td>
            <td class="tb-col-project">${renderTaskCellWithTooltip(projectName)}</td>
            <td class="tb-col-tail">${renderTaskCellWithTooltip(tailNumber)}</td>
            <td class="tb-col-item-id">${renderTaskCellWithTooltip(itemID)}</td>
            <td class="tb-col-item-name">${renderTaskCellWithTooltip(itemName)}</td>
            <td class="tb-col-sn">${renderTaskCellWithTooltip(serial)}</td>
            <td class="tb-col-qty">${qty}</td>
            ${stagesHtml}
            <td class="progress-cell">
                <div class="progress-compact" style="--progress:${progressValue};" title="${progressValue}%">
                    <span class="progress-circle" aria-hidden="true"></span>
                    <span class="progress-text">${progressValue}%</span>
                </div>
            </td>
        </tr>`;
}

function registerTaskPayload(map, payload) {
    const key = `tbp_${++taskBoardPayloadCounter}`;
    map.set(key, payload);
    return key;
}

function buildProductionRowPayload(row) {
    const item = row.productionItem || row.ProductionItem || {};
    const plane = row.planeID || row.PlaneID || {};
    const planeType = plane.type || plane.Type || {};
    const productionItemID = item.productionItemID || item.ProductionItemID || row.inventoryItemID || row.InventoryItemID || row.productionItemID || row.ProductionItemID || "";
    return {
        originalSerialNumber: row.serialNumber || row.SerialNumber || 0,
        originalProductionItemID: productionItemID,
        serialNumber: row.serialNumber || row.SerialNumber || 0,
        productionItemID: productionItemID,
        itemName: item.itemName || item.ItemName || item.productionItemDescription || item.ProductionItemDescription || "",
        workOrderID: row.workOrderID || row.WorkOrderID || "",
        projectName: row.projectName || row.ProjectName || plane.project?.projectName || plane.Project?.ProjectName || "",
        tailNumber: row.tailNumber || row.TailNumber || plane.planeID || plane.PlaneID || "",
        planeTypeID: planeType.planeTypeID || planeType.PlaneTypeID || row.planeTypeID || row.PlaneTypeID || "",
        planeTypeName: planeType.planeTypeName || planeType.PlaneTypeName || row.planeTypeName || row.PlaneTypeName || "",
        plannedQty: row.plannedQty || row.PlannedQty || 1,
        comments: row.comments || row.Comments || ""
    };
}

function renderTaskCellWithTooltip(value) {
    const displayValue = displayTaskValue(value);
    const safeValue = escapeHtml(displayValue);
    if (displayValue === "---") return safeValue;
    return `<span class="task-cell-tooltip" tabindex="0" title="${safeValue}" data-tooltip="${safeValue}">${safeValue}</span>`;
}

function getTaskBoardCellTooltip() {
    let tooltip = document.getElementById("taskBoardCellTooltip");
    if (tooltip) return tooltip;

    tooltip = document.createElement("div");
    tooltip.id = "taskBoardCellTooltip";
    tooltip.className = "task-board-cell-tooltip-popover";
    tooltip.setAttribute("role", "tooltip");
    document.body.appendChild(tooltip);
    return tooltip;
}

function showTaskBoardCellTooltip(target) {
    const text = target?.dataset?.tooltip;
    if (!text || !isTaskBoardCellTextTruncated(target)) {
        hideTaskBoardCellTooltip();
        return;
    }

    const tooltip = getTaskBoardCellTooltip();
    tooltip.textContent = text;
    tooltip.style.display = "block";

    const targetRect = target.getBoundingClientRect();
    const tooltipRect = tooltip.getBoundingClientRect();
    const viewportPadding = 12;
    let top = targetRect.top - tooltipRect.height - 8;
    let left = targetRect.right - tooltipRect.width;

    if (top < viewportPadding) {
        top = targetRect.bottom + 8;
    }

    left = Math.max(viewportPadding, Math.min(left, window.innerWidth - tooltipRect.width - viewportPadding));
    tooltip.style.top = `${top}px`;
    tooltip.style.left = `${left}px`;
}

function hideTaskBoardCellTooltip() {
    const tooltip = document.getElementById("taskBoardCellTooltip");
    if (tooltip) tooltip.style.display = "none";
}

function isTaskBoardCellTextTruncated(target) {
    if (!target) return false;
    return target.scrollWidth > target.clientWidth + 1;
}

function displayTaskValue(value) {
    if (value === null || value === undefined) return "---";
    const text = String(value).trim();
    return text === "" ? "---" : text;
}

function escapeHtml(value) {
    return String(value)
        .replace(/&/g, "&amp;")
        .replace(/</g, "&lt;")
        .replace(/>/g, "&gt;")
        .replace(/\"/g, "&quot;")
        .replace(/'/g, "&#39;");
}
window.openStatusModal = function (payloadKey, pillEl) {
    const payload = taskBoardStatusPayloads.get(payloadKey);
    if (!payload) {
        showAppMessage("נתוני התחנה לא נמצאו. רעננו את הדף ונסו שוב.", { title: "שגיאה" });
        return;
    }
    const modal = document.getElementById("tbStatusModal");
    const submitBtn = document.getElementById("tbModalSubmitBtn");
    const startTimeContainer = document.getElementById("timeInputContainer");
    const startTimeLabel = document.getElementById("timeLabel");
    const startTimeInput = document.getElementById("userTimeInput");
    const finishTimeContainer = document.getElementById("finishTimeInputContainer");
    const finishTimeInput = document.getElementById("finishTimeInput");
    const currentStatusID = parseInt(payload.currentStatusID || 1);
    const currentStartTime = toDatetimeLocalValue(payload.startTime);
    const currentFinishTime = toDatetimeLocalValue(payload.finishTime);

    startTimeContainer.style.display = "none";
    finishTimeContainer.style.display = "none";
    startTimeLabel.innerText = "זמן התחלה:";
    startTimeInput.value = "";
    finishTimeInput.value = "";
    document.getElementById("statusCommentInput").value = payload.currentComment || "";
    submitBtn.disabled = true;

    function getCurrentTimeValue() {
        const now = new Date();
        now.setMinutes(now.getMinutes() - now.getTimezoneOffset());
        return now.toISOString().slice(0, 16);
    }

    const displayItemName = (typeof payload.itemName === 'object') ? "---" : payload.itemName;
    const displayWorkOrder = (typeof payload.workOrder === 'object') ? "---" : payload.workOrder;

    document.getElementById("modalTaskInfo").innerHTML = `
        <div style="display:grid; grid-template-columns: 1fr 1fr; gap:10px;">
            <div><span class="tb-label">פק"ע:</span> <span class="tb-val">${displayWorkOrder}</span></div>
            <div><span class="tb-label">סידורי:</span> <span class="tb-val">${payload.serialNumber}</span></div>
            <div style="grid-column: span 2;"><span class="tb-label">פריט:</span> <span class="tb-val">${escapeHtml(payload.itemID)} - ${escapeHtml(displayItemName)}</span></div>
            <div style="grid-column: span 2; color:#3b82f6;"><span class="tb-label">תחנה:</span> <span class="tb-val">${escapeHtml(payload.stageName)}</span></div>
        </div>`;

 
    const statusContainer = document.getElementById("statusButtonsContainer");
    statusContainer.innerHTML = allStatusesData.map(status => {
        const sID = status.productionStatusID || status.ProductionStatusID;
        const sName = status.productionStatusName || status.ProductionStatusName;
        const fullWidthClass = sID === 1 ? "full-width" : "";
        return `<button type="button" class="btn-choice ${fullWidthClass}" data-id="${sID}">${sName}</button>`;
    }).join('');


    const currentBtn = $(`.btn-choice[data-id="${currentStatusID}"]`);
    if (currentBtn.length > 0) {
        currentBtn.addClass(`active status-${currentStatusID}`);
        applyTimeFieldState(currentStatusID, false);
    }

    [startTimeInput, finishTimeInput, document.getElementById("statusCommentInput")].forEach(input => {
        input.oninput = () => submitBtn.disabled = false;
    });


    $(".btn-choice").on("click", function () {
        const sid = $(this).data("id"); 

        $(".btn-choice").removeClass("active status-1 status-2 status-3 status-4 status-5");
        $(this).addClass(`active status-${sid}`);
        submitBtn.disabled = false;
        applyTimeFieldState(sid, true);
    });

    function applyTimeFieldState(statusId, defaultMissingValues) {
        const started = isStartedStatus(statusId);
        const done = parseInt(statusId) === 4;

        startTimeContainer.style.display = started ? "block" : "none";
        startTimeInput.disabled = !started;
        startTimeInput.value = started ? (currentStartTime || (defaultMissingValues ? getCurrentTimeValue() : "")) : "";

        finishTimeContainer.style.display = done ? "block" : "none";
        finishTimeInput.disabled = !done;
        finishTimeInput.value = done ? (currentFinishTime || (defaultMissingValues ? getCurrentTimeValue() : "")) : "";
    }

    submitBtn.onclick = function () {
        const activeBtn = $(".btn-choice.active");
        const statusId = parseInt(activeBtn.data("id"));
        const comment = document.getElementById("statusCommentInput").value;
        const startTimeVal = startTimeInput.value;
        const finishTimeVal = finishTimeInput.value;
        const serialNumberValue = parseInt(payload.serialNumber);
        const stageIdValue = parseInt(payload.stageID);
        const itemIdValue = (payload.itemID || "").toString().trim();

        if (!statusId || !serialNumberValue || !stageIdValue || !itemIdValue || itemIdValue === "---") {
            showAppMessage("נתוני שורה לא תקינים לעדכון סטטוס", { title: "שגיאה" });
            return;
        }

        // 1. קודם כל מגדירים את הדגל: האם צריך לאפס תחנות עתידיות?
        // התנאי: הסטטוס המקורי היה "בוצע" (4) והסטטוס החדש הוא לא "בוצע"
        const shouldResetFuture = (parseInt(currentStatusID) === 4 && statusId !== 4);

        const executeUpdate = () => {
            const updateData = {
                SerialNumber: serialNumberValue,
                ProductionItemID: itemIdValue,
                ProductionStageID: stageIdValue,
                ProductionStatusID: statusId,
                Comment: comment,
                StartTime: isStartedStatus(statusId) && startTimeVal ? startTimeVal : null,
                FinishTime: statusId === 4 && finishTimeVal ? finishTimeVal : null,
                ResetFuture: shouldResetFuture
            };

            ajaxCall("PUT", server + "api/ItemsInProduction/updateStatus", JSON.stringify(updateData),
                function (res) {
                    window.closeTaskStatusModal();
                    refreshBoardAfterStatusUpdate();
                },
                function (err) {
                    showAppMessage("שגיאה בעדכון: " + err.responseText, { title: "שגיאה" });
                }
            );
        };

        // בדיקה אם להציג את האזהרה
        if (shouldResetFuture) {
            showConfirm("אזהרה", "שינוי הסטטוס יאפס את כל התחנות הבאות של פריט זה. להמשיך?", executeUpdate);
        } else {
            executeUpdate();
        }
    };

    modal.style.display = "flex";
};

function isStartedStatus(statusId) {
    return [2, 3, 4, 5].includes(parseInt(statusId));
}

function toDatetimeLocalValue(value) {
    if (!value) return "";
    const date = new Date(value);
    if (Number.isNaN(date.getTime())) return "";
    date.setMinutes(date.getMinutes() - date.getTimezoneOffset());
    return date.toISOString().slice(0, 16);
}

window.toggleTaskRowMenu = function (button) {
    const menu = button?.parentElement?.querySelector(".row-actions-menu");
    document.querySelectorAll(".row-actions-menu.open").forEach(existing => {
        if (existing !== menu) closeTaskRowMenu(existing);
    });
    if (!menu) return;

    if (menu.classList.contains("open")) {
        closeTaskRowMenu(menu);
        return;
    }

    menu.classList.add("open");
    positionTaskRowMenu(button, menu);
};

function closeTaskRowMenus() {
    document.querySelectorAll(".row-actions-menu.open").forEach(closeTaskRowMenu);
}

function closeTaskRowMenu(menu) {
    menu.classList.remove("open", "opens-up");
    menu.style.top = "";
    menu.style.right = "";
    menu.style.left = "";
}

function positionTaskRowMenu(button, menu) {
    const buttonRect = button.getBoundingClientRect();
    const viewportPadding = 8;

    menu.style.top = "0px";
    menu.style.right = "auto";
    menu.style.left = "0px";
    const menuRect = menu.getBoundingClientRect();

    const spaceBelow = window.innerHeight - buttonRect.bottom - viewportPadding;
    const opensUp = spaceBelow < menuRect.height && buttonRect.top > menuRect.height;
    const top = opensUp
        ? Math.max(viewportPadding, buttonRect.top - menuRect.height - 4)
        : Math.min(window.innerHeight - menuRect.height - viewportPadding, buttonRect.bottom + 4);

    const left = Math.max(
        viewportPadding,
        Math.min(buttonRect.right - menuRect.width, window.innerWidth - menuRect.width - viewportPadding)
    );

    menu.classList.toggle("opens-up", opensUp);
    menu.style.top = `${top}px`;
    menu.style.left = `${left}px`;
}

window.openEditProductionRowModal = function (payloadKey) {
    closeTaskRowMenus();
    const row = taskBoardRowPayloads.get(payloadKey);
    if (!row) {
        showAppMessage("נתוני השורה לא נמצאו. רעננו את הדף ונסו שוב.", { title: "שגיאה" });
        return;
    }
    const modal = document.getElementById("editProductionRowModal");
    if (!modal) return;

    currentEditProductionRowKey = payloadKey;
    showEditProductionRowMessage("טוען אפשרויות...", false);
    setEditProductionRowFieldsDisabled(true);

    document.getElementById("editOriginalSerialNumber").value = row.originalSerialNumber || row.serialNumber || "";
    document.getElementById("editOriginalProductionItemID").value = row.originalProductionItemID || row.productionItemID || "";
    document.getElementById("editWorkOrderID").value = row.workOrderID || "";
    document.getElementById("editTailNumber").value = row.tailNumber || "";
    document.getElementById("editItemName").value = row.itemName || "";
    document.getElementById("editSerialNumber").value = row.serialNumber || "";
    document.getElementById("editPlannedQty").value = row.plannedQty || 1;
    document.getElementById("editComments").value = row.comments || "";

    document.getElementById("editProductionRowSaveBtn").onclick = saveProductionRowEdit;
    document.getElementById("editProductionRowDeleteBtn").onclick = function () {
        confirmDeleteCurrentEditProductionRow();
    };
    modal.style.display = "flex";

    loadTaskBoardEditOptions(function () {
        renderEditProductionRowOptions(row);
        bindEditProductionRowOptionEvents();
        setEditProductionRowFieldsDisabled(false);
        hideEditProductionRowMessage();
    }, function () {
        showEditProductionRowMessage("טעינת אפשרויות העריכה נכשלה. רעננו את הדף ונסו שוב.", true);
    });
};

window.closeEditProductionRowModal = function () {
    const modal = document.getElementById("editProductionRowModal");
    if (modal) modal.style.display = "none";
    currentEditProductionRowKey = null;
    hideEditProductionRowMessage();
};

function saveProductionRowEdit() {
    const planeTypeID = parseNullableTaskInt(document.getElementById("editPlaneTypeID").value);
    const payload = {
        OriginalSerialNumber: parseInt(document.getElementById("editOriginalSerialNumber").value),
        OriginalProductionItemID: document.getElementById("editOriginalProductionItemID").value.trim(),
        SerialNumber: parseInt(document.getElementById("editSerialNumber").value),
        ProductionItemID: document.getElementById("editProductionItemID").value.trim(),
        ItemName: document.getElementById("editItemName").value.trim(),
        WorkOrderID: parseInt(document.getElementById("editWorkOrderID").value),
        ProjectName: document.getElementById("editProjectName").value.trim(),
        TailNumber: document.getElementById("editTailNumber").value.trim(),
        PlaneTypeID: planeTypeID,
        PlannedQty: parseInt(document.getElementById("editPlannedQty").value),
        Comments: document.getElementById("editComments").value.trim()
    };

    if (!payload.OriginalSerialNumber || !payload.OriginalProductionItemID || !payload.SerialNumber || !payload.ProductionItemID || !payload.WorkOrderID || !payload.PlannedQty || !payload.PlaneTypeID || !payload.ProjectName) {
        showAppMessage("נא למלא את שדות החובה בצורה תקינה", { title: "שגיאה" });
        return;
    }

    if (!isEditSelectionValid(payload.ProductionItemID, payload.PlaneTypeID)) {
        showAppMessage("המק״ט וסוג המטוס שנבחרו אינם תואמים לאפשרויות הייצור.", { title: "שגיאה" });
        return;
    }

    ajaxCall("PUT", server + "api/ItemsInProduction/updateRow", JSON.stringify(payload),
        function () {
            window.closeEditProductionRowModal();
            refreshBoardAfterStatusUpdate();
        },
        function (err) {
            showAppMessage("שגיאה בעדכון שורת הייצור: " + (err.responseJSON?.error || err.responseText || err.statusText), { title: "שגיאה" });
        }
    );
}

function loadTaskBoardEditOptions(onSuccess, onError) {
    if (taskBoardEditOptions) {
        onSuccess(taskBoardEditOptions);
        return;
    }

    if (taskBoardEditOptionsLoading) {
        setTimeout(function () { loadTaskBoardEditOptions(onSuccess, onError); }, 100);
        return;
    }

    taskBoardEditOptionsLoading = true;
    ajaxCall("GET", server + "api/ItemsInProduction/GetInitialFormData", "",
        function (data) {
            taskBoardEditOptions = normalizeTaskBoardEditOptions(data || {});
            taskBoardEditOptionsLoading = false;
            onSuccess(taskBoardEditOptions);
        },
        function (err) {
            taskBoardEditOptionsLoading = false;
            console.error("Error loading edit production row options:", err);
            onError(err);
        }
    );
}

function normalizeTaskBoardEditOptions(data) {
    const productionItems = normalizeTaskProductionItems(data.productionItems || []);
    const planeTypes = (data.planeTypes || []).map(t => ({
        planeTypeID: parseNullableTaskInt(t.planeTypeID ?? t.PlaneTypeID),
        planeTypeName: String(t.planeTypeName ?? t.PlaneTypeName ?? "").trim()
    })).filter(t => t.planeTypeID);

    return {
        productionItems,
        planeTypes,
        itemPlaneMappings: normalizeTaskItemPlaneMappings(data.itemPlaneMappings || productionItems),
        projects: data.projects || [],
        existingWorkOrders: data.existingWorkOrders || [],
        planes: data.planes || []
    };
}

function normalizeTaskProductionItems(items) {
    const map = new Map();
    (items || []).forEach(item => {
        const id = String(item.productionItemID || item.ProductionItemID || "").trim();
        const name = String(item.itemName || item.ItemName || "").trim();
        if (id && !map.has(id)) map.set(id, { productionItemID: id, itemName: name });
    });
    return Array.from(map.values());
}

function normalizeTaskItemPlaneMappings(rawMappings) {
    const pairSet = new Set();
    const normalized = [];
    (rawMappings || []).forEach(row => {
        const itemId = String(row.productionItemID || row.ProductionItemID || row.itemID || row.ItemID || "").trim();
        const planeTypeID = parseNullableTaskInt(row.planeTypeID ?? row.PlaneTypeID);
        if (!itemId || !planeTypeID) return;

        const key = `${itemId}|${planeTypeID}`;
        if (pairSet.has(key)) return;
        pairSet.add(key);
        normalized.push({ productionItemID: itemId, planeTypeID });
    });
    return normalized;
}

function renderEditProductionRowOptions(row) {
    const options = taskBoardEditOptions;
    if (!options) return;

    const selectedPlaneType = row.planeTypeID || getPlaneTypeIdByName(row.planeTypeName);

    renderTaskSelectOptions(
        document.getElementById("editProjectName"),
        (options.projects || []).map(project => ({ value: project.projectName || project.ProjectName || "", label: project.projectName || project.ProjectName || "" })),
        "בחר פרויקט...",
        row.projectName || ""
    );

    renderEditItemPlaneOptions(row.productionItemID || "", selectedPlaneType || "");
    renderTaskDatalist("editWorkOrdersList", options.existingWorkOrders || []);
    renderEditPlaneDatalist();
}

function bindEditProductionRowOptionEvents() {
    $("#editProductionItemID").off("change.taskEdit").on("change.taskEdit", function () {
        syncEditSelectedItemName();
    });

    $("#editPlaneTypeID").off("change.taskEdit").on("change.taskEdit", function () {
        renderEditPlaneDatalist();
    });

    $("#editProjectName").off("change.taskEdit").on("change.taskEdit", renderEditPlaneDatalist);
}

function renderEditItemPlaneOptions(selectedItem, selectedPlaneType, source) {
    const options = taskBoardEditOptions;
    if (!options) return;

    const itemValue = String(selectedItem || "").trim();
    const typeValue = parseNullableTaskInt(selectedPlaneType);

    renderTaskSelectOptions(
        document.getElementById("editProductionItemID"),
        options.productionItems.map(i => ({ value: i.productionItemID, label: i.productionItemID })),
        "בחר מק״ט...",
        itemValue
    );

    renderTaskSelectOptions(
        document.getElementById("editPlaneTypeID"),
        options.planeTypes.map(t => ({ value: t.planeTypeID, label: t.planeTypeName })),
        "בחר סוג...",
        typeValue
    );

    syncEditSelectedItemName();
    hideEditProductionRowMessage();
}

function syncEditSelectedItemName() {
    const selectedItem = String(document.getElementById("editProductionItemID")?.value || "").trim();
    const itemObj = taskBoardEditOptions?.productionItems.find(i => i.productionItemID === selectedItem);
    document.getElementById("editItemName").value = itemObj ? itemObj.itemName : "";
}

function renderTaskSelectOptions(select, values, placeholder, selectedValue) {
    if (!select) return;
    const selected = String(selectedValue || "");
    select.innerHTML = `<option value="">${escapeHtml(placeholder)}</option>`;
    (values || []).forEach(option => {
        if (!option.value) return;
        const optionEl = document.createElement("option");
        optionEl.value = String(option.value);
        optionEl.textContent = option.label || option.value;
        optionEl.title = option.label || option.value;
        select.appendChild(optionEl);
    });
    if (selected && Array.from(select.options).some(option => option.value === selected)) {
        select.value = selected;
    } else if (selected) {
        const currentOption = document.createElement("option");
        currentOption.value = selected;
        currentOption.textContent = selected;
        currentOption.title = selected;
        select.appendChild(currentOption);
        select.value = selected;
    }
}

function renderTaskDatalist(id, values) {
    const list = document.getElementById(id);
    if (!list) return;
    list.innerHTML = "";
    (values || []).forEach(value => {
        const val = typeof value === "object" ? (value.workOrderID || value.WorkOrderID || value.planeID || value.PlaneID || "") : value;
        if (val === undefined || val === null || String(val).trim() === "") return;
        const option = document.createElement("option");
        option.value = String(val);
        list.appendChild(option);
    });
}

function renderEditPlaneDatalist() {
    const options = taskBoardEditOptions;
    if (!options) return;

    const selectedProject = document.getElementById("editProjectName")?.value || "";
    const project = (options.projects || []).find(p => String(p.projectName || p.ProjectName || "") === selectedProject);
    const selectedProjectID = project ? (project.projectID ?? project.ProjectID) : null;
    const selectedTypeID = document.getElementById("editPlaneTypeID")?.value || "";

    const planes = (options.planes || []).filter(p => {
        const typeId = p.typeID ?? p.TypeID ?? p.planeTypeID ?? p.PlaneTypeID;
        const projectId = p.projectID ?? p.ProjectID;
        const matchesType = !selectedTypeID || String(typeId) === String(selectedTypeID);
        const matchesProject = !selectedProjectID || String(projectId) === String(selectedProjectID);
        return matchesType && matchesProject;
    }).map(p => p.planeID ?? p.PlaneID).filter(Boolean);

    renderTaskDatalist("editTrackedPlaneList", [...new Set(planes)]);
}

function setEditProductionRowFieldsDisabled(disabled) {
    ["editWorkOrderID", "editProjectName", "editTailNumber", "editProductionItemID", "editPlaneTypeID", "editSerialNumber", "editPlannedQty", "editComments", "editProductionRowSaveBtn", "editProductionRowDeleteBtn"].forEach(id => {
        const el = document.getElementById(id);
        if (el) el.disabled = disabled;
    });
}

function showEditProductionRowMessage(message, isError) {
    const el = document.getElementById("editProductionRowMessage");
    if (!el) return;
    el.textContent = message;
    el.classList.toggle("error", !!isError);
    el.style.display = "block";
}

function hideEditProductionRowMessage() {
    const el = document.getElementById("editProductionRowMessage");
    if (!el) return;
    el.textContent = "";
    el.classList.remove("error");
    el.style.display = "none";
}

function parseNullableTaskInt(value) {
    const parsed = parseInt(value, 10);
    return Number.isNaN(parsed) ? null : parsed;
}

function getPlaneTypeIdByName(name) {
    const normalized = String(name || "").trim();
    if (!normalized || !taskBoardEditOptions) return null;
    const match = taskBoardEditOptions.planeTypes.find(t => t.planeTypeName === normalized);
    return match ? match.planeTypeID : null;
}

function isEditSelectionValid(itemId, planeTypeID) {
    if (!taskBoardEditOptions) return true;
    const itemExists = taskBoardEditOptions.productionItems.some(i => i.productionItemID === itemId);
    const planeTypeExists = taskBoardEditOptions.planeTypes.some(t => t.planeTypeID === planeTypeID);
    return itemExists && planeTypeExists;
}

function confirmDeleteCurrentEditProductionRow() {
    if (!currentEditProductionRowKey) return;
    const row = taskBoardRowPayloads.get(currentEditProductionRowKey);
    if (!row) {
        showAppMessage("נתוני השורה לא נמצאו. רעננו את הדף ונסו שוב.", { title: "שגיאה" });
        return;
    }

    showAppConfirm({
        title: "מחיקת שורה מלוח המשימות",
        message: "האם אתה בטוח שברצונך למחוק את השורה מלוח המשימות? פעולה זו לא תמחק את הפריט ממאגר הפריטים הכללי.",
        confirmText: "מחק שורה",
        cancelText: "ביטול",
        destructive: true,
        onConfirm: function () {
            deleteProductionRow(row);
        }
    });
}

function deleteProductionRow(row) {
    ajaxCall("DELETE", server + `api/ItemsInProduction/deleteRow?serialNumber=${encodeURIComponent(row.originalSerialNumber)}&productionItemID=${encodeURIComponent(row.originalProductionItemID)}`, "",
        function () {
            window.closeEditProductionRowModal();
            refreshBoardAfterStatusUpdate();
        },
        function (err) {
            showAppMessage("שגיאה במחיקת שורת הייצור: " + (err.responseJSON?.error || err.responseText || err.statusText), { title: "שגיאה" });
        }
    );
}

window.confirmDeleteProductionRow = function (payloadKey) {
    closeTaskRowMenus();
    const row = taskBoardRowPayloads.get(payloadKey);
    if (!row) {
        showAppMessage("נתוני השורה לא נמצאו. רעננו את הדף ונסו שוב.", { title: "שגיאה" });
        return;
    }
    showAppConfirm({
        title: "מחיקת שורה מלוח המשימות",
        message: "פעולה זו תסיר את הפריט מלוח משימות הייצור בלבד. הפריט הכללי בקטלוג לא יימחק. להמשיך?",
        confirmText: "מחק שורה",
        cancelText: "ביטול",
        destructive: true,
        onConfirm: function () {
            deleteProductionRow(row);
        }
    });
};

document.addEventListener("click", function (e) {
    const target = e.target;
    if (!(target instanceof Element)) return;
    if (!target.closest(".row-actions-wrap")) closeTaskRowMenus();
});

function refreshBoardAfterStatusUpdate() {
    ajaxCall("GET", server + "api/ItemsInProduction/boardData", "",
        function (boardData) {
            allBoardData = boardData;
            applyFilters();
        },
        function (err) {
            console.error("Error refreshing board data:", err);
            initBoard();
        }
    );
}

function showConfirm(title, message, onConfirm) {
    const modal = document.getElementById('confirmModal');
    if (!modal) {
        showAppConfirm({ title, message, confirmText: "אישור", cancelText: "ביטול", onConfirm });
        return;
    }

    document.getElementById('confirmTitle').innerText = title;
    document.getElementById('confirmMessage').innerText = message;

    // הצגת המודל
    modal.style.display = 'flex';

    // הגדרת הכפתורים
    document.getElementById('confirmYesBtn').onclick = function () {
        modal.style.display = 'none';
        onConfirm();
    };

    document.getElementById('confirmNoBtn').onclick = function () {
        modal.style.display = 'none';
    };
}

function populateProjectFilter(data) {
    const projectSelect = document.getElementById("filter-project");
    if (!projectSelect) return;

    // שליפת שמות ייחודיים - תעדוף מוחלט ל-ProjectName מה-SQL החדש
    const projects = [...new Set(data.map(row =>
        row.ProjectName || row.projectName || "ללא פרויקט"
    ))].filter(p => p !== null && p !== ""); // מנקה ערכים ריקים

    projectSelect.innerHTML = '<option value="">כל הפרויקטים</option>';
    projects.sort().forEach(p => {
        const opt = document.createElement("option");
        opt.value = p;
        opt.innerText = p;
        projectSelect.appendChild(opt);
    });
}

function populateStageFilter(stages) {
    const stageSelect = document.getElementById("filter-stage");
    if (!stageSelect) return;

    stages.forEach(s => {
        const opt = document.createElement("option");
        opt.value = s.productionStageID;
        opt.innerText = s.productionStageName;
        stageSelect.appendChild(opt);
    });
}

window.clearAllFilters = function () {
    try {
        const searchEl = document.getElementById("filter-search");
        const projectEl = document.getElementById("filter-project");
        const stageEl = document.getElementById("filter-stage");
        const notDoneEl = document.getElementById("filter-not-done");

        if (searchEl) searchEl.value = "";
        if (projectEl) projectEl.value = "";
        if (stageEl) stageEl.value = "";
        if (notDoneEl) notDoneEl.checked = false;

        window.applyFilters();
    } catch (e) {
        console.error("Error clearing filters:", e);
    }
};

window.applyFilters = function () {
    try {
        const searchEl = document.getElementById("filter-search");
        const projectEl = document.getElementById("filter-project");
        const stageEl = document.getElementById("filter-stage");
        const notDoneEl = document.getElementById("filter-not-done");

        if (!searchEl || !projectEl || !stageEl || !notDoneEl) return;

        const searchTerm = searchEl.value.toLowerCase();
        const selectedProject = projectEl.value;
        const selectedStageId = stageEl.value;
        const onlyNotDone = notDoneEl.checked;

        const filteredData = allBoardData.filter(row => {
            const item = row.productionItem || row.ProductionItem || {};
            const itemName = (item.itemName || item.ItemName || item.productionItemDescription || "").toLowerCase();
            const itemID = (item.productionItemID || "").toString().toLowerCase();
            const workOrder = (row.workOrderID || row.WorkOrderID || "").toString().toLowerCase();
            const project = row.projectName || row.ProjectName || "ללא פרויקט";

            const matchesSearch = itemName.includes(searchTerm) ||
                itemID.includes(searchTerm) ||
                workOrder.includes(searchTerm);

            const matchesProject = !selectedProject || project === selectedProject;

            const isDone = (row.progress || row.Progress) >= 100;
            const matchesNotDone = !onlyNotDone || !isDone;

            let matchesStage = true;
            if (selectedStageId) {
                const stages = row.stages || row.Stages || [];
                const targetStageId = parseInt(selectedStageId);
                const currentStatus = getStageStatusById(stages, targetStageId);
                const targetStageIndex = allStagesData.findIndex(s => getStageIdValue(s) === targetStageId);

                if (targetStageIndex <= 0) {
                    matchesStage = (currentStatus !== 4);
                } else {
                    const prevStageId = getStageIdValue(allStagesData[targetStageIndex - 1]);
                    const prevStatus = getStageStatusById(stages, prevStageId);
                    matchesStage = (prevStatus === 4 && currentStatus !== 4);
                }
            }

            return matchesSearch && matchesProject && matchesNotDone && matchesStage;
        });

        renderTasksBoard(filteredData, allStagesData);

    } catch (error) {
        console.error("Critical error in applyFilters:", error);
    }
};
