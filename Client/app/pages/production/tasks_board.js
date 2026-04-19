//window.initTasksBoard = function () {
//    if (typeof ajaxCall === 'undefined') {
//        $.getScript("../../../JS/ajaxCalls.js").done(() => initBoard());
//    } else {
//        initBoard();
//    }
//};

window.initTasksBoard = function () {
    initBoard();
};

window.closeTaskStatusModal = function () {
    const modal = document.getElementById("tbStatusModal");
    if (modal) {
        modal.style.display = "none";
    }
};

let allBoardData = []; 
let allStagesData = []; 
let allStatusesData = [];

function getStageIdValue(stage) {
    return parseInt(stage?.productionStageID || stage?.ProductionStageID || 0);
}

function getStageStatusById(stagesList, stageId) {
    const currentStageObj = (stagesList || []).find(s =>
        parseInt(s.stage?.productionStageID || s.Stage?.ProductionStageID || 0) === parseInt(stageId)
    );
    return currentStageObj?.status?.productionStatusID || currentStageObj?.Status?.ProductionStatusID || 1;
}

function initBoard() {
    const container = document.getElementById("tasks-board-container");
    if (container) {
        container.innerHTML = "";
    }

    loadStatuses(function (statuses) {
        allStatusesData = statuses;

        loadStages(function (stages) {
            allStagesData = stages;
            populateStageFilter(allStagesData);

            loadBoardData(function (boardData) {
                allBoardData = boardData;
                populateProjectFilter(allBoardData);
                applyFilters();
            });
        });
    });
}

function loadStatuses(successCallback) {
    ajaxCall(
        "GET",
        server + "api/ProductionStatuses",
        "",
        function (statuses) {
            successCallback(statuses);
        },
        function (err) {
            console.error("Error fetching statuses:", err);
        }
    );
}

function loadStages(successCallback) {
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
            successCallback(sortedStages);
        },
        function (err) {
            console.error("Error stages:", err);
        }
    );
}

function loadBoardData(successCallback) {
    ajaxCall(
        "GET",
        server + "api/ItemsInProduction/boardData",
        "",
        function (boardData) {
            successCallback(boardData);
        },
        function (err) {
            console.error("Error board data:", err);
        }
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

    const orderedTypeNames = Object.keys(grouped).sort();

    let html = "";
    for (const typeName of orderedTypeNames) {
        html += `
            <section class="aircraft-section">
                <h2 class="tb-aircraft-title">${typeName}</h2>
                <div class="table-container">
                    <table class="generic-data-table tb-table">
                        <thead>
                            <tr>
                                <th class="tb-col-wo">פק"ע</th>
                                <th class="tb-col-project">שם פרויקט</th>
                                <th class="tb-col-tail">מספר זנב</th>
                                <th style="width: 120px;">מק"ט</th>
                                <th class="tb-col-item-name">שם פריט</th>
                                <th class="tb-col-sn">סיריאלי</th>
                                <th class="tb-col-qty">כמות</th>
                                ${allStages.map(s => `<th class="tb-col-stage">${s.productionStageName}</th>`).join('')}
                                <th class="tb-col-progress">התקדמות</th> </tr>
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

    const stagesHtml = allStages.map((stage, stageIndex) => {
        const stagesList = row.stages || row.Stages || [];
        const itemStage = stagesList.find(s =>
            (s.stage?.productionStageID || s.Stage?.ProductionStageID) === stage.productionStageID
        );

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

        const clickAttr = isBlocked ? "" : `onclick="window.openStatusModal('${serial}', '${itemID}', '${itemName}', '${workOrder}', ${stage.productionStageID}, '${stage.productionStageName}', ${sID}, this, '${comment}')"`;
        const blockedClass = isBlocked ? "blocked" : "";
        const titleText = isBlocked ? "חסום: בצע תחנה קודמת תחילה" : (itemStage?.comment || "");

        return `
            <td class="status-cell">
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
            <td>${workOrder}</td>
            <td>${projectName}</td>
            <td>${tailNumber}</td>
            <td>${itemID}</td>
            <td class="tb-col-item-name" title="${itemName}">${itemName}</td>
            <td>${serial}</td>
            <td class="tb-col-qty">${qty}</td>
            ${stagesHtml}
            <td class="progress-cell">
                <div class="progress-wrapper">
                    <div class="pb-progress-wrapper">
                        <div class="pb-progress-bar">
                            <div class="pb-progress-fill" style="width: ${progressValue}%"></div>
                        </div>
                        <span class="pb-progress-text">${progressValue}%</span>
                    </div>
                </div>
            </td>
        </tr>`;
}
window.openStatusModal = function (serialNumber, itemID, itemName, workOrder, stageID, stageName, currentStatusID, pillEl, currentComment) {
    const modal = document.getElementById("tbStatusModal");
    const submitBtn = document.getElementById("tbModalSubmitBtn");
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
    statusContainer.innerHTML = allStatusesData.map(status => {
        const sID = status.productionStatusID;
        const sName = status.productionStatusName;
        const fullWidthClass = sID === 1 ? "full-width" : "";
        return `<button type="button" class="btn-choice ${fullWidthClass}" data-id="${sID}">${sName}</button>`;
    }).join('');


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
        const comment = document.getElementById("statusCommentInput").value;
        const userTimeVal = timeInput.value;
        const serialNumberValue = parseInt(serialNumber);
        const stageIdValue = parseInt(stageID);
        const itemIdValue = (itemID || "").toString().trim();

        if (!statusId || !serialNumberValue || !stageIdValue || !itemIdValue || itemIdValue === "---") {
            alert("נתוני שורה לא תקינים לעדכון סטטוס");
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
                // 2. עכשיו המשתנה מוכר לקוד ולא יזרוק שגיאת Identifier
                ResetFuture: shouldResetFuture
            };

            if (userTimeVal && (statusId == 2 || statusId == 4)) {
                updateData.UserTime = userTimeVal;
            }

            ajaxCall("PUT", server + "api/ItemsInProduction/updateStatus", JSON.stringify(updateData),
                function (res) {
                    window.closeTaskStatusModal();
                    refreshBoardAfterStatusUpdate();
                },
                function (err) {
                    alert("שגיאה בעדכון: " + err.responseText);
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
        // גיבוי למקרה שהמודל המעוצב לא קיים ב-HTML
        if (confirm(message)) onConfirm();
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
