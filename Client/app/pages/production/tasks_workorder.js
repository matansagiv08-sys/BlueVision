let allTasks = [];
let currentStation = 'all';
let originalGlobalOrder = [];
let currentGlobalOrder = [];

// פונקציית ה-Init שנקראת מה-app.js
window.initTasksWorkOrder = async function () {
    try {
        // 1. משיכת כל התחנות הקיימות במערכת (ה-API שמשתמש ב-GetProductionStages)
        ajaxCall("GET", server + "api/ProductionStages", "",
            (allStages) => {
                // בניית הסליידר לפי כל התחנות מה-DB
                renderStationSlider(allStages);

                // 2. משיכת הנתונים של המשימות (boardData)
                ajaxCall("GET", server + "api/ItemsInProduction/boardData", "",
                    (data) => {
                        console.log("Data received:", data);
                        allTasks = data;

                        // שמירת סדר האלגוריתם המקורי
                        originalGlobalOrder = allTasks.map(t => getTaskValue(t, ["serialNumber", "SerialNumber"], ""));
                        currentGlobalOrder = [...originalGlobalOrder];

                        // הצגת הטבלה (בברירת מחדל על "כל התחנות")
                        window.filterWorkOrder();
                    },
                    (err) => console.error("Error fetching board data:", err)
                );
            },
            (err) => console.error("Error fetching stages:", err)
        );

    } catch (error) {
        console.error("שגיאה בטעינת הדף:", error);
    }
};

function renderTasks(tasks) {
    const tbody = document.getElementById('tasksTableBody');
    if (!tbody) return;
    tbody.innerHTML = '';

    // סעיף 2: סינון פריטים שהסתיימו (100% התקדמות)
    const activeTasks = tasks.filter(t => Number(getTaskValue(t, ["progress", "Progress"], 0)) < 100);

    if (activeTasks.length === 0) {
        tbody.innerHTML = '<tr><td colspan="12" style="text-align:center;">אין משימות פעילות להצגה</td></tr>';
        return;
    }

    activeTasks.forEach(task => {
        const sn = getTaskValue(task, ["serialNumber", "SerialNumber"], '-');
        const workOrder = getTaskValue(task, ["workOrderNumber", "workOrderID", "WorkOrderNumber", "WorkOrderID"], '-');
        const itemId = getTaskValue(task, ["inventoryItemID", "InventoryItemID"], '') ||
            task.productionItem?.productionItemID || task.ProductionItem?.ProductionItemID || '';
        const itemName = getTaskValue(task, ["itemName", "ItemName"], '') ||
            task.productionItem?.itemName || task.ProductionItem?.ItemName || "---";
        const planeType = getTaskValue(task, ["planeTypeName", "PlaneTypeName"], '') ||
            task.planeID?.type?.planeTypeName || task.PlaneID?.Type?.PlaneTypeName || "-";
        const projectName = getTaskValue(task, ["projectName", "ProjectName"], '-') || "-";
        const projectDueDateRaw = getTaskValue(task, ["projectDueDate", "ProjectDueDate"], null) ||
            task.planeID?.project?.dueDate || task.PlaneID?.Project?.DueDate;
        const itemDueDateRaw = getTaskValue(task, ["itemDueDate", "ItemDueDate"], null);
        const planeNumber = getTaskValue(task, ["planeNumber", "PlaneNumber", "tailNumber", "TailNumber"], '-') || '-';
        const currentStationName = getTaskValue(task, ["currentStationName", "CurrentStationName"], '') ||
            task.currentStage?.stage?.productionStageName || task.CurrentStage?.Stage?.ProductionStageName || "לא הוגדר";
        const score = Number(getTaskValue(task, ["urgencyScore", "UrgencyScore", "calculatedScore", "CalculatedScore"], 0)) || 0;
        const currentStageId = getTaskValue(task, ["currentStage", "CurrentStage"], null)?.stage?.productionStageID ||
            getTaskValue(task, ["currentStage", "CurrentStage"], null)?.Stage?.ProductionStageID || 0;

        const projectDueDate = formatDate(projectDueDateRaw);
        const itemDueDate = formatDate(itemDueDateRaw);
        const isExpiredRow = isExpiredDate(projectDueDateRaw) || isExpiredDate(itemDueDateRaw);

        const hasManualPriority = task.currentStage?.manualPriority > 0 || task.CurrentStage?.ManualPriority > 0;
        const magicIconColor = hasManualPriority ? "orange" : "#94a3b8";
        const safeItemId = String(itemId).replace(/'/g, "\\'");

        const rowHtml = `
            <tr class="${isExpiredRow ? 'workorder-expired-row' : ''}" data-sn="${sn}" data-itemid="${itemId}" data-stageid="${currentStageId}">
                <td class="drag-col">
                    <div class="drag-handle">⋮⋮</div>
                    <button class="btn-tiny-algo" 
                            style="color: ${magicIconColor}; border:none; background:none; cursor:pointer;" 
                            onclick="window.resetToAlgo(${sn}, '${safeItemId}', ${currentStageId})" 
                            title="החזר לאלגוריתם">🪄</button>
                </td>
                <td>${workOrder}</td>
                <td>${itemId}</td>
                <td class="truncate-cell item-name-cell" title="${escapeAttribute(itemName)}">${itemName}</td>
                <td>${sn}</td>
                <td>${planeType}</td>
                <td class="truncate-cell project-name-cell" title="${escapeAttribute(projectName)}">${projectName}</td>
                <td>${projectDueDate}</td>
                <td>${itemDueDate}</td>
                <td>${planeNumber}</td>
                <td><span class="status-pill">${currentStationName}</span></td>
                <td>${score.toFixed(4)}</td>
            </tr>
        `;
        tbody.insertAdjacentHTML('beforeend', rowHtml);
    });

    if (typeof wireRowDnD === 'function') wireRowDnD();
}

function getTaskValue(task, keys, fallback = '') {
    for (const key of keys) {
        if (task && task[key] !== undefined && task[key] !== null) {
            return task[key];
        }
    }
    return fallback;
}

function formatDate(value) {
    if (!value) return '-';
    const dt = new Date(value);
    if (Number.isNaN(dt.getTime())) return '-';
    return dt.toLocaleDateString('he-IL');
}

function isExpiredDate(value) {
    if (!value) return false;

    const dt = new Date(value);
    if (Number.isNaN(dt.getTime())) return false;

    const dateOnly = new Date(dt.getFullYear(), dt.getMonth(), dt.getDate());
    const now = new Date();
    const today = new Date(now.getFullYear(), now.getMonth(), now.getDate());

    return dateOnly < today;
}

function escapeAttribute(value) {
    return String(value ?? '').replace(/"/g, '&quot;');
}

function wireRowDnD() {
    const tbody = document.getElementById("tasksTableBody");
    if (!tbody) return;

    const rows = tbody.querySelectorAll("tr");

    rows.forEach(row => {
        const handle = row.querySelector(".drag-handle");
        if (!handle) return;

        // הפיכת הידית בלבד לניתנת לגרירה
        handle.setAttribute('draggable', 'true');

        handle.addEventListener('dragstart', (e) => {
            row.classList.add('dragging'); // משתמש ב-CSS הקיים שלך
            e.dataTransfer.effectAllowed = 'move';
            // שמירת ה-SN של השורה שנגררת
            e.dataTransfer.setData('text/plain', row.getAttribute('data-sn'));
        });

        handle.addEventListener('dragend', () => {
            row.classList.remove('dragging');
            rows.forEach(r => r.classList.remove('drag-over'));
        });
    });

    tbody.addEventListener('dragover', (e) => {
        e.preventDefault();
        const draggingRow = tbody.querySelector('.dragging');
        const afterElement = getDragAfterElement(tbody, e.clientY);

        if (afterElement == null) {
            tbody.appendChild(draggingRow);
        } else {
            tbody.insertBefore(draggingRow, afterElement);
        }
    });

    tbody.addEventListener('drop', (e) => {
        e.preventDefault();
        // עדכון המערך הגלובלי לפי הסדר הוויזואלי החדש בטבלה
        const newOrder = Array.from(tbody.querySelectorAll('tr')).map(r => r.getAttribute('data-sn'));
        currentGlobalOrder = newOrder;

        updateDirtyState(); // מדליק את כפתור השמירה
    });
}

// פונקציית עזר לחישוב המיקום המדויק בזמן הגרירה
function getDragAfterElement(container, y) {
    const draggableElements = [...container.querySelectorAll('tr:not(.dragging)')];

    return draggableElements.reduce((closest, child) => {
        const box = child.getBoundingClientRect();
        const offset = y - box.top - box.height / 2;
        if (offset < 0 && offset > closest.offset) {
            return { offset: offset, element: child };
        } else {
            return closest;
        }
    }, { offset: Number.NEGATIVE_INFINITY }).element;
}
function reorderGlobally(draggedSn, targetSn) {
    const from = currentGlobalOrder.indexOf(draggedSn);
    const to = currentGlobalOrder.indexOf(targetSn);
    if (from === -1 || to === -1) return;
    currentGlobalOrder.splice(from, 1);
    currentGlobalOrder.splice(to, 0, draggedSn);
}

// שומרים על פונקציות הסינון והכפתורים
window.filterWorkOrder = function () {
    const q = (document.getElementById("taskSearch")?.value || "").trim().toLowerCase();

    let tasks = allTasks;

    // סינון לפי תחנה
    if (currentStation !== 'all') {
        tasks = tasks.filter(t => {
            const stationName = getTaskValue(t, ["currentStationName", "CurrentStationName"], '') ||
                t.currentStage?.stage?.productionStageName || t.CurrentStage?.Stage?.ProductionStageName || '';
            return stationName === currentStation;
        });
    }

    // סינון לפי חיפוש
    if (q) {
        tasks = tasks.filter(t => {
            const serial = String(getTaskValue(t, ["serialNumber", "SerialNumber"], '')).toLowerCase();
            const workOrder = String(getTaskValue(t, ["workOrderNumber", "workOrderID", "WorkOrderNumber", "WorkOrderID"], '')).toLowerCase();
            const itemCode = String(getTaskValue(t, ["inventoryItemID", "InventoryItemID"], '') || t.productionItem?.productionItemID || t.ProductionItem?.ProductionItemID || '').toLowerCase();
            const itemName = String(getTaskValue(t, ["itemName", "ItemName"], '') || t.productionItem?.itemName || t.ProductionItem?.ItemName || '').toLowerCase();
            const projectName = String(getTaskValue(t, ["projectName", "ProjectName"], '')).toLowerCase();
            const planeNumber = String(getTaskValue(t, ["planeNumber", "PlaneNumber", "tailNumber", "TailNumber"], '')).toLowerCase();

            return serial.includes(q)
                || workOrder.includes(q)
                || itemCode.includes(q)
                || itemName.includes(q)
                || projectName.includes(q)
                || planeNumber.includes(q);
        });
    }

    // מיון לפי הסדר הנוכחי (ידני או אלגוריתם)
    tasks = sortByCurrentOrder(tasks);
    renderTasks(tasks);
};

function sortByCurrentOrder(tasks) {
    const rank = new Map(currentGlobalOrder.map((sn, index) => [sn, index]));
    return [...tasks].sort((a, b) => {
        const aSn = getTaskValue(a, ["serialNumber", "SerialNumber"], "");
        const bSn = getTaskValue(b, ["serialNumber", "SerialNumber"], "");
        const aRank = rank.has(aSn) ? rank.get(aSn) : 999;
        const bRank = rank.has(bSn) ? rank.get(bSn) : 999;
        return aRank - bRank;
    });
}

function updateDirtyState() {
    const isDirty = JSON.stringify(originalGlobalOrder) !== JSON.stringify(currentGlobalOrder);
    const saveBtn = document.getElementById("saveOrderBtn");
    const resetBtn = document.getElementById("resetAlgoBtn");
    if (saveBtn) saveBtn.disabled = !isDirty;
    if (resetBtn) resetBtn.disabled = !isDirty;
}

// פונקציה לייצור כפתורי התחנות בסליידר
function renderStationSlider(stages) {
    const slider = document.getElementById('stationsSlider');
    if (!slider) return;

    // ניקוי הסליידר (מלבד כפתור "כל התחנות" אם הוא קיים ב-HTML, או פשוט לבנות מחדש)
    slider.innerHTML = '<button class="slider-btn active pill-blue" onclick="window.filterByStation(\'all\', this)">כל התחנות</button>';

    // מיון התחנות לפי ה-StageOrder שלהן
    stages.sort((a, b) => a.stageOrder - b.stageOrder);

    stages.forEach(stage => {
        const btn = document.createElement('button');
        btn.className = 'slider-btn';
        // משתמשים בשם התחנה לתצוגה
        btn.innerText = stage.productionStageName;
        // לחיצה תסנן לפי שם התחנה
        btn.onclick = (e) => window.filterByStation(stage.productionStageName, e.target);
        slider.appendChild(btn);
    });
}

window.filterByStation = function (station, btn) {
    const buttons = btn.parentElement.querySelectorAll('.slider-btn');
    buttons.forEach(b => b.classList.remove('active', 'pill-blue'));
    btn.classList.add('active', 'pill-blue');
    currentStation = station;
    window.filterWorkOrder();
};

window.optimizeRoute = function () {
    currentGlobalOrder = [...originalGlobalOrder];
    window.filterWorkOrder();
    updateDirtyState();
};

window.saveWorkOrder = function () {
    const rows = Array.from(document.querySelectorAll('#tasksTableBody tr'));

    const updates = rows.map((row, index) => ({
        serial: parseInt(row.getAttribute('data-sn')),
        itemId: row.getAttribute('data-itemid'),
        stageId: parseInt(row.getAttribute('data-stageid')),
        newPriority: index + 1
    }));

    console.log("Sending to server:", updates);

    ajaxCall("POST",
        server + "api/ProductionItemStage/UpdateManualOrder",
        JSON.stringify(updates), // <--- זה הקריטי!
        (res) => {
            alert("הסדר נשמר בהצלחה!"); 
            updateInterfaceState(false);
            window.initTasksWorkOrder();
        },
        (err) => {
            console.error("פרטי השגיאה מהשרת:", err.responseText);
            alert("שגיאה בשמירה, פרטים בקונסול");
        }
    );
};
// מניעת עזיבה אם יש שינויים לא שמורים
window.addEventListener('beforeunload', function (e) {
    const saveBtn = document.getElementById('saveOrderBtn');
    if (saveBtn && !saveBtn.disabled) {
        e.preventDefault();
        e.returnValue = ''; // מציג את הודעת הדפדפן הסטנדרטית
    }
});

// פונקציה לבדיקת מעבר עמוד בתוך האפליקציה (SPA)
// אם יש לך פונקציית ניווט מרכזית ב-app.js, כדאי לקרוא לזה משם
window.checkUnsavedChanges = function (nextPageCallback) {
    const saveBtn = document.getElementById('saveOrderBtn');
    if (saveBtn && !saveBtn.disabled) {
        // כאן אנחנו משתמשים במודל האישור שכבר קיים אצלך ב-HTML (confirmModal)
        const confirmModal = document.getElementById('confirmModal');
        if (confirmModal) {
            document.getElementById('confirmTitle').innerText = "שינויים לא שמורים";
            document.getElementById('confirmMessage').innerText = "ביצעת שינויים בסדר העבודה הידני. האם ברצונך לשמור אותם לפני המעבר?";

            // הגדרת כפתורי המודל
            const footer = confirmModal.querySelector('.confirm-modal-footer');
            footer.innerHTML = `
                <button class="btn-save" onclick="handleGuardSave('${nextPageCallback}')">שמור ועבור</button>
                <button class="btn-cancel" onclick="window.closeConfirmModal(); ${nextPageCallback}()">עבור בלי לשמור</button>
                <button class="btn-secondary" onclick="window.closeConfirmModal()">ביטול</button>
            `;
            confirmModal.style.display = 'flex';
        }
    } else {
        nextPageCallback();
    }
};

window.handleGuardSave = async function (nextPage) {
    await window.saveWorkOrder(); // מחכה לשמירה
    window.closeConfirmModal();
    window[nextPage](); // מבצע את הניווט
};

window.resetSingleRow = function (serial, itemId, stageId) {
    const updates = [{
        serial: serial,
        itemId: itemId,
        stageId: stageId,
        newPriority: null // שליחת null מחזירה אותו לאלגוריתם ב-DB
    }];

    ajaxCall("POST", server + "api/ProductionItemStage/UpdateManualOrder", JSON.stringify(updates),
        (res) => {
            showNotification("השורה חזרה לניהול אלגוריתם", "success");
            window.initTasksWorkOrder(); // טעינה מחדש של הנתונים מהשרת
        },
        (err) => console.error(err)
    );
};

window.resetToAlgo = function (serial, itemId, stageId) {
    const updates = [{
        serial: serial,
        itemId: itemId,
        stageId: stageId,
        newPriority: 0 // 0 אומר לשרת: "תחזיר את זה לאלגוריתם"
    }];

    ajaxCall("POST", server + "api/ProductionItemStage/UpdateManualOrder", JSON.stringify(updates),
        (res) => {
            showNotification("הפריט הוחזר לניהול אלגוריתם", "success");
            window.initTasksWorkOrder(); // טעינה מחדש של הנתונים המעודכנים
        },
        (err) => console.error("Error resetting priority:", err)
    );
};

// משתנה שיעזור לנו לדעת אם הניווט אושר
let isNavigationConfirmed = false;

// פונקציה שנקראת מה-UI (למשל כשלוחצים על כפתור "חזור" או עוברים דף ב-SPA)
window.safeNavigate = function (targetPage) {
    const saveBtn = document.getElementById('saveOrderBtn');

    // אם כפתור השמירה פעיל - סימן שיש שינויים!
    if (saveBtn && !saveBtn.disabled && !isNavigationConfirmed) {
        window.showConfirmModal(
            "שינויים לא שמורים",
            "שמת לב? שינית את סדר העבודה הידני ולא שמרת. האם לשמור לפני היציאה?",
            () => { // אם המשתמש לוחץ "אישור" (שמור)
                window.saveWorkOrder();
                // כאן תלוי איך ה-SPA שלך עובר דפים, למשל:
                // window.location.href = targetPage;
            },
            () => { // אם המשתמש לוחץ "ביטול" (צא בלי לשמור)
                isNavigationConfirmed = true;
                // ניווט ללא שמירה
            }
        );
    } else {
        // אין שינויים - נווט חופשי
    }
};

// כפתור "בטל שינויים לא שמורים" - פשוט טוען מחדש מהמערך המקורי
window.discardChanges = function () {
    allTasks = JSON.parse(JSON.stringify(originalTasksFromServer)); // שחזור מהגיבוי
    window.renderTasks(allTasks);
    updateInterfaceState(false);
};

// כפתור "החזר לסדר אלגוריתמי" - מוחק את ה-ManualPriority ב-DB
//window.resetToAlgorithm = function () {
//    if (confirm("האם להסיר את כל העדיפויות הידניות בתחנה זו ולהחזיר לניהול אוטומטי?")) {
//        ajaxCall("POST", server + "api/ProductionItemStage/ResetStationOrder", { stationId: currentStation },
//            (res) => {
//                window.initTasksWorkOrder(); // טעינה מחדש של הנתונים מהשרת
//            });
//    }
//};
