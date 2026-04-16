let allTasks = [];
let currentStation = 'all';
let originalGlobalOrder = [];
let currentGlobalOrder = [];

// פונקציית ה-Init שנקראת מה-app.js
window.initTasksWorkOrder = function () {
    console.log("Initializing Tasks WorkOrder...");

    // שימוש ב-ajaxCall בדיוק כמו ב-tasks_board
    ajaxCall("GET", server + "api/ItemsInProduction/boardData", "",
        (data) => {
            console.log("Data received:", data);
            allTasks = data;

            // שמירת סדר ברירת המחדל לפי ה-Serial
            originalGlobalOrder = allTasks.map(t => t.serialNumber || t.SerialNumber);
            currentGlobalOrder = [...originalGlobalOrder];

            // בניית הסליידר והטבלה
            renderStationSlider(allTasks);
            window.filterWorkOrder();
        },
        (err) => {
            console.error("Error fetching data:", err);
        }
    );
};

function renderTasks(tasks) {
    const tbody = document.getElementById('tasksTableBody');
    if (!tbody) return;
    tbody.innerHTML = '';

    if (!tasks || tasks.length === 0) {
        tbody.innerHTML = '<tr><td colspan="9" style="text-align:center;">אין משימות להצגה בתחנה זו</td></tr>';
        return;
    }

    tasks.forEach(task => {
        // התאמה למבנה הנתונים מה-API (תמיכה ב-PascalCase ו-camelCase)
        const sn = task.serialNumber || task.SerialNumber;
        const po = task.workOrderID || task.WorkOrderID || '-';
        const itemId = task.productionItem?.productionItemID || task.ProductionItem?.ProductionItemID || '';
        const itemName = task.productionItem?.productionItemName || task.ProductionItem?.ProductionItemName || 'פריט כללי';
        const progress = task.progress || task.Progress || 0;
        const score = task.calculatedScore || task.CalculatedScore || 0;

        // שליפת שם התחנה הנוכחית
        const currentStageObj = task.currentStage?.stage || task.CurrentStage?.Stage;
        const stageName = currentStageObj?.productionStageName || currentStageObj?.ProductionStageName || "לא הוגדר";
        const stageId = currentStageObj?.productionStageID || currentStageObj?.ProductionStageID || 0;

        const row = `
            <tr data-sn="${sn}" data-itemid="${itemId}" data-stageid="${stageId}">
                <td class="drag-col"><div class="drag-handle" draggable="true">⋮⋮</div></td>
                <td>${po}</td>
                <td>${itemId}</td>
                <td>${itemName}</td>
                <td>${sn}</td>
                <td>
                    <div class="progress-container">
                        <div class="progress-bar" style="width: ${progress}%"></div>
                    </div>
                </td>
                <td>${score.toFixed(1)}</td>
                <td>${stageName}</td>
            </tr>
        `;
        tbody.innerHTML += row;
    });

    if (typeof wireRowDnD === 'function') wireRowDnD();
}

// שומרים על פונקציית ה-Drag & Drop המקורית שלך עם התאמות קלות ל-Serial Number
function wireRowDnD() {
    const tbody = document.getElementById("tasksTableBody");
    if (!tbody) return;

    const rows = Array.from(tbody.querySelectorAll("tr"));
    const handles = Array.from(tbody.querySelectorAll(".drag-handle"));

    handles.forEach(h => {
        h.addEventListener('dragstart', (e) => {
            const row = h.closest('tr');
            dragSrcPo = row?.getAttribute('data-sn'); // משתמשים בסיריאלי
            row?.classList.add('dragging');
            e.dataTransfer.effectAllowed = 'move';
        });

        h.addEventListener('dragend', () => {
            const row = h.closest('tr');
            row?.classList.remove('dragging');
            rows.forEach(r => r.classList.remove('drag-over'));
        });
    });

    rows.forEach(r => {
        r.addEventListener('dragover', (e) => { e.preventDefault(); r.classList.add('drag-over'); });
        r.addEventListener('dragleave', () => { r.classList.remove('drag-over'); });
        r.addEventListener('drop', (e) => {
            e.preventDefault();
            const targetSn = r.getAttribute('data-sn');
            if (dragSrcPo && targetSn && dragSrcPo !== targetSn) {
                reorderGlobally(dragSrcPo, targetSn);
                window.filterWorkOrder();
                updateDirtyState();
            }
        });
    });
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
        tasks = tasks.filter(t => t.currentStage?.stage?.productionStageName === currentStation);
    }

    // סינון לפי חיפוש
    if (q) {
        tasks = tasks.filter(t =>
            t.serialNumber.toLowerCase().includes(q) ||
            (t.workOrderID && t.workOrderID.toString().includes(q))
        );
    }

    // מיון לפי הסדר הנוכחי (ידני או אלגוריתם)
    tasks = sortByCurrentOrder(tasks);
    renderTasks(tasks);
};

function sortByCurrentOrder(tasks) {
    const rank = new Map(currentGlobalOrder.map((sn, index) => [sn, index]));
    return [...tasks].sort((a, b) => {
        const aRank = rank.has(a.serialNumber) ? rank.get(a.serialNumber) : 999;
        const bRank = rank.has(b.serialNumber) ? rank.get(b.serialNumber) : 999;
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
function renderStationSlider(tasks) {
    const slider = document.getElementById('stationsSlider');
    if (!slider) return;

    // חילוץ תחנות ייחודיות מתוך הנתונים
    const stations = new Set();
    tasks.forEach(t => {
        const stage = t.currentStage?.stage || t.CurrentStage?.Stage;
        if (stage?.productionStageName) stations.add(stage.productionStageName);
        if (stage?.ProductionStageName) stations.add(stage.ProductionStageName);
    });

    // ניקוי הסליידר חוץ מכפתור "הכל"
    slider.innerHTML = '<button class="slider-btn active pill-blue" onclick="window.filterByStation(\'all\', this)">כל התחנות</button>';

    stations.forEach(name => {
        const btn = document.createElement('button');
        btn.className = 'slider-btn';
        btn.innerText = name;
        btn.onclick = (e) => window.filterByStation(name, e.target);
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
    const rows = Array.from(document.querySelectorAll("#tasksTableBody tr"));
    const updates = rows.map((row, index) => ({
        serial: parseInt(row.getAttribute('data-sn')),
        itemId: row.getAttribute('data-itemid'),
        stageId: parseInt(row.getAttribute('data-stageid')),
        newPriority: index + 1
    }));

    ajaxCall("POST", server + "api/ProductionItemStage/UpdateManualOrder", JSON.stringify(updates),
        (res) => {
            alert("הסדר נשמר בהצלחה!");
            originalGlobalOrder = [...currentGlobalOrder];
            updateDirtyState();
        },
        (err) => alert("שגיאה בשמירה")
    );
};