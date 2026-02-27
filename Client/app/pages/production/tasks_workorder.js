let allTasks = [];
let currentStation = 'all';
let originalOrderByStation = {};
let currentOrderByStation = {};
let dragSrcPo = null;

window.initTasksWorkOrder = function () {
    // MOCK DATA (easy to replace later with API)
    // Later you can replace this with: allTasks = await fetch(...).then(r => r.json())
    allTasks = [
        // templates_paint
        { po: "100245", sku: "WB-BODY-01", desc: "גוף WanderB G2", sn: "SN-9921", status: "active", urgency: "critical", deadline: "24/02", station: "templates_paint" },
        { po: "100253", sku: "WB-TMP-02", desc: "תבנית עליונה", sn: "SN-9931", status: "waiting", urgency: "high", deadline: "25/02", station: "templates_paint" },
        { po: "100254", sku: "WB-TMP-03", desc: "תבנית תחתונה", sn: "SN-9932", status: "waiting", urgency: "medium", deadline: "26/02", station: "templates_paint" },

        // layup_upper
        { po: "100246", sku: "WB-WING-02", desc: "כנף ימין", sn: "SN-9922", status: "waiting", urgency: "high", deadline: "25/02", station: "layup_upper" },
        { po: "100255", sku: "WB-SKIN-U1", desc: "סקין עליון 1", sn: "SN-9933", status: "active", urgency: "medium", deadline: "27/02", station: "layup_upper" },
        { po: "100256", sku: "WB-SKIN-U2", desc: "סקין עליון 2", sn: "SN-9934", status: "waiting", urgency: "low", deadline: "28/02", station: "layup_upper" },

        // layup_lower
        { po: "100247", sku: "WB-WING-03", desc: "כנף שמאל", sn: "SN-9923", status: "active", urgency: "medium", deadline: "26/02", station: "layup_lower" },
        { po: "100257", sku: "WB-SKIN-L1", desc: "סקין תחתון 1", sn: "SN-9935", status: "waiting", urgency: "high", deadline: "27/02", station: "layup_lower" },
        { po: "100258", sku: "WB-SKIN-L2", desc: "סקין תחתון 2", sn: "SN-9936", status: "waiting", urgency: "low", deadline: "01/03", station: "layup_lower" },

        // closure
        { po: "100248", sku: "WB-CANOPY-1", desc: "חופת מצלמה", sn: "SN-9924", status: "waiting", urgency: "low", deadline: "27/02", station: "closure" },
        { po: "100259", sku: "WB-CLS-01", desc: "סגירת גוף", sn: "SN-9937", status: "active", urgency: "high", deadline: "28/02", station: "closure" },
        { po: "100260", sku: "WB-CLS-02", desc: "סגירת כנף", sn: "SN-9938", status: "waiting", urgency: "medium", deadline: "02/03", station: "closure" },

        // extraction
        { po: "100249", sku: "WB-TAIL-01", desc: "זנב", sn: "SN-9925", status: "waiting", urgency: "high", deadline: "28/02", station: "extraction" },
        { po: "100261", sku: "WB-EXT-01", desc: "חליצת כנף", sn: "SN-9939", status: "active", urgency: "medium", deadline: "01/03", station: "extraction" },
        { po: "100262", sku: "WB-EXT-02", desc: "חליצת גוף", sn: "SN-9940", status: "waiting", urgency: "low", deadline: "03/03", station: "extraction" },

        // finishes
        { po: "100250", sku: "WB-FINS-01", desc: "סט פינים", sn: "SN-9926", status: "active", urgency: "medium", deadline: "01/03", station: "finishes" },
        { po: "100263", sku: "WB-FIN-02", desc: "שיוף ראשוני", sn: "SN-9941", status: "waiting", urgency: "low", deadline: "02/03", station: "finishes" },
        { po: "100264", sku: "WB-FIN-03", desc: "פיניש סופי", sn: "SN-9942", status: "waiting", urgency: "high", deadline: "04/03", station: "finishes" },

        // paint
        { po: "100251", sku: "WB-PAINT-01", desc: "צביעה שכבה ראשונה", sn: "SN-9927", status: "waiting", urgency: "low", deadline: "02/03", station: "paint" },
        { po: "100265", sku: "WB-PAINT-02", desc: "צביעה שכבה שנייה", sn: "SN-9943", status: "active", urgency: "medium", deadline: "03/03", station: "paint" },
        { po: "100266", sku: "WB-PAINT-03", desc: "לכה/הגנה", sn: "SN-9944", status: "waiting", urgency: "high", deadline: "05/03", station: "paint" },

        // qc
        { po: "100252", sku: "WB-QC-01", desc: "בדיקת איכות סופית", sn: "SN-9928", status: "waiting", urgency: "critical", deadline: "03/03", station: "qc" },
        { po: "100267", sku: "WB-QC-02", desc: "בדיקת מידות", sn: "SN-9945", status: "waiting", urgency: "high", deadline: "04/03", station: "qc" },
        { po: "100268", sku: "WB-QC-03", desc: "בדיקת ויזואלית", sn: "SN-9946", status: "active", urgency: "medium", deadline: "06/03", station: "qc" }
    ];

    setTimeout(() => {
        buildOrderMaps(allTasks);
        window.filterWorkOrder(); // render with current filters
        updateDirtyState();
        console.log("Workorder table rendered with", allTasks.length, "items");
    }, 10);
};

function buildOrderMaps(tasks) {
    const stations = [...new Set(tasks.map(t => t.station))];
    stations.forEach(st => {
        const order = tasks.filter(t => t.station === st).map(t => t.po);
        originalOrderByStation[st] = [...order];
        currentOrderByStation[st] = [...order];
    });
}

function isDirty() {
    const stations = Object.keys(originalOrderByStation);
    return stations.some(st => {
        const a = originalOrderByStation[st] || [];
        const b = currentOrderByStation[st] || [];
        if (a.length !== b.length) return true;
        for (let i = 0; i < a.length; i++) {
            if (a[i] !== b[i]) return true;
        }
        return false;
    });
}

function updateDirtyState() {
    const saveBtn = document.getElementById("saveOrderBtn");
    const resetBtn = document.getElementById("resetAlgoBtn");
    const dirty = isDirty();

    if (saveBtn) {
        saveBtn.disabled = !dirty;
        saveBtn.style.opacity = dirty ? "1" : "0.5";
    }
    if (resetBtn) {
        resetBtn.disabled = !dirty;
        resetBtn.style.opacity = dirty ? "1" : "0.5";
    }
}

function renderTasks(data) {
    const tbody = document.getElementById("tasks-table-body");
    if (!tbody) return;

    if (data.length === 0) {
        tbody.innerHTML = `<tr><td colspan="8" style="text-align:center; padding:20px;">אין נתונים להתאמה</td></tr>`;
        return;
    }

    tbody.innerHTML = data.map(task => {
        const colorClass = task.status === 'active' ? 'pill-blue' : 'pill-yellow';
        const statusText = task.status === 'active' ? 'בביצוע' : 'ממתין';

        let urgencyClass = 'pill-yellow';
        let urgencyText = 'גבוה';

        if (task.urgency === 'critical') {
            urgencyClass = 'pill-red';
            urgencyText = 'קריטי';
        }
        else if (task.urgency === 'medium') {
            urgencyClass = 'pill-blue';
            urgencyText = 'בינוני';
        }
        else if (task.urgency === 'low') {
            urgencyClass = 'pill-green';
            urgencyText = 'נמוך';
        }

        return `
        <tr data-po="${task.po}" data-station="${task.station}">
            <td class="drag-col">
                <span class="drag-handle" draggable="true" title="גרור לשינוי סדר">⋮⋮</span>
            </td>
            <td style="font-weight:700;">${task.po}</td>
            <td>${task.sku}</td>
            <td>${task.desc}</td>
            <td>${task.sn}</td>
            <td><span class="status-pill ${colorClass}">${statusText}</span></td>
            <td><span class="status-pill ${urgencyClass}">${urgencyText}</span></td>
            <td>${task.deadline}</td>
        </tr>
    `;
    }).join('');

    wireRowDnD();
}

//function wireRowDnD() {
//    const tbody = document.getElementById("tasks-table-body");
//    if (!tbody) return;

//    const rows = Array.from(tbody.querySelectorAll("tr"));
//    const handles = Array.from(tbody.querySelectorAll(".drag-handle"));

//    // Keep row click for details, but ignore clicks on the handle
//    rows.forEach(r => {
//        r.addEventListener('click', (e) => {
//            if (e.target.classList.contains('drag-handle')) return;
//            const po = r.getAttribute('data-po');
//            window.openItemDetailsModal?.(po);
//        });
//    });


//    const rows = Array.from(tbody.querySelectorAll("tr"));
//    const handles = Array.from(tbody.querySelectorAll(".drag-handle"));
//    handles.forEach(h => {
//        h.addEventListener('dragstart', (e) => {
//            const row = h.closest('tr');
//            dragSrcPo = row?.getAttribute('data-po') || null;
//            row?.classList.add('dragging');
//            e.dataTransfer.effectAllowed = 'move';
//        });

//        h.addEventListener('dragend', () => {
//            const row = h.closest('tr');
//            row?.classList.remove('dragging');
//            rows.forEach(r => r.classList.remove('drag-over'));
//        });
//    });

//    rows.forEach(r => {
//        r.addEventListener('dragover', (e) => {
//            e.preventDefault();
//            r.classList.add('drag-over');
//        });

//        r.addEventListener('dragleave', () => {
//            r.classList.remove('drag-over');
//        });

//        r.addEventListener('drop', (e) => {
//            e.preventDefault();
//            r.classList.remove('drag-over');

//            const targetPo = r.getAttribute('data-po');
//            if (!dragSrcPo || !targetPo || dragSrcPo === targetPo) return;

//            const srcTask = allTasks.find(t => t.po === dragSrcPo);
//            const tgtTask = allTasks.find(t => t.po === targetPo);
//            if (!srcTask || !tgtTask) return;

//            // Do not allow moving between stations
//            if (srcTask.station !== tgtTask.station) return;

//            reorderWithinStation(srcTask.station, dragSrcPo, targetPo);
//            window.filterWorkOrder();   // re-render using current order
//            updateDirtyState();
//        });
//    });
//}

function wireRowDnD() {
    const tbody = document.getElementById("tasks-table-body");
    if (!tbody) return;

    const rows = Array.from(tbody.querySelectorAll("tr"));
    const handles = Array.from(tbody.querySelectorAll(".drag-handle"));

    // prevent opening modal when clicking/dragging the handle
    handles.forEach(h => h.addEventListener('click', (e) => e.stopPropagation()));

    // Row click (only if not clicking the handle)
    rows.forEach(r => {
        r.addEventListener('click', (e) => {
            if (e.target.classList.contains('drag-handle')) return;
            const po = r.getAttribute('data-po');
            window.openItemDetailsModal?.(po);
        });
    });

    // Drag start/end
    handles.forEach(h => {
        h.addEventListener('dragstart', (e) => {
            const row = h.closest('tr');
            dragSrcPo = row?.getAttribute('data-po') || null;
            row?.classList.add('dragging');
            e.dataTransfer.effectAllowed = 'move';
        });

        h.addEventListener('dragend', () => {
            const row = h.closest('tr');
            row?.classList.remove('dragging');
            rows.forEach(r => r.classList.remove('drag-over'));
        });
    });

    // Drag over / drop
    rows.forEach(r => {
        r.addEventListener('dragover', (e) => {
            e.preventDefault();
            r.classList.add('drag-over');
        });

        r.addEventListener('dragleave', () => {
            r.classList.remove('drag-over');
        });

        r.addEventListener('drop', (e) => {
            e.preventDefault();
            r.classList.remove('drag-over');

            const targetPo = r.getAttribute('data-po');
            if (!dragSrcPo || !targetPo || dragSrcPo === targetPo) return;

            const srcTask = allTasks.find(t => t.po === dragSrcPo);
            const tgtTask = allTasks.find(t => t.po === targetPo);
            if (!srcTask || !tgtTask) return;

            // allow drag only inside same station
            if (srcTask.station !== tgtTask.station) return;

            reorderWithinStation(srcTask.station, dragSrcPo, targetPo);
            window.filterWorkOrder();
            updateDirtyState();
        });
    });
}

function reorderWithinStation(station, draggedPo, targetPo) {
    const order = currentOrderByStation[station] || [];
    const from = order.indexOf(draggedPo);
    const to = order.indexOf(targetPo);
    if (from === -1 || to === -1) return;

    order.splice(from, 1);
    order.splice(to, 0, draggedPo);
    currentOrderByStation[station] = order;
}

window.filterByStation = function (station, btn) {
    const buttons = btn.parentElement.querySelectorAll('.slider-btn');
    buttons.forEach(b => b.classList.remove('active', 'pill-blue'));
    btn.classList.add('active', 'pill-blue');

    currentStation = station;
    window.filterWorkOrder();
};

// פונקציה להפעלת כפתור השמירה
//window.markAsDirty = function () {
//    const saveBtn = document.getElementById("saveOrderBtn");
//    if (saveBtn) {
//        saveBtn.disabled = false;
//        saveBtn.style.opacity = "1";
//    }
//};

// בתוך פונקציית השמירה, נחזיר אותו למצב כבוי אחרי ההצלחה

window.saveWorkOrder = function () {
    console.log("Saving changes...");

    // Later: send currentOrderByStation to backend

    // Treat current as new baseline after successful save
    Object.keys(currentOrderByStation).forEach(st => {
        originalOrderByStation[st] = [...currentOrderByStation[st]];
    });

    updateDirtyState();
    alert("השינויים נשמרו בהצלחה!");
};

// בכל פעם שמשנים דחיפות או מסננים - אנחנו "מלכלכים" את הנתונים ומאפשרים שמירה
window.filterWorkOrder = function () {
    const q = (document.getElementById("taskSearch")?.value || "").trim().toLowerCase();
    const urgency = document.getElementById("urgencyFilter")?.value || "all";

    let tasks = allTasks;

    if (currentStation !== 'all') {
        tasks = tasks.filter(t => t.station === currentStation);
    }

    if (urgency !== 'all') {
        tasks = tasks.filter(t => t.urgency === urgency);
    }

    if (q) {
        tasks = tasks.filter(t =>
            (t.po || "").toLowerCase().includes(q) ||
            (t.sku || "").toLowerCase().includes(q) ||
            (t.sn || "").toLowerCase().includes(q)
        );
    }

    tasks = sortByCurrentOrder(tasks);
    renderTasks(tasks);
};

window.optimizeRoute = function () {
    Object.keys(originalOrderByStation).forEach(st => {
        currentOrderByStation[st] = [...originalOrderByStation[st]];
    });

    window.filterWorkOrder();
    updateDirtyState();
};

function sortByCurrentOrder(tasks) {
    const byStation = {};
    tasks.forEach(t => {
        byStation[t.station] = byStation[t.station] || [];
        byStation[t.station].push(t);
    });

    const stations = Object.keys(byStation);
    let result = [];

    stations.forEach(st => {
        const order = currentOrderByStation[st] || [];
        const map = new Map(byStation[st].map(t => [t.po, t]));

        order.forEach(po => {
            if (map.has(po)) result.push(map.get(po));
        });

        byStation[st].forEach(t => {
            if (!order.includes(t.po)) result.push(t);
        });
    });

    return result;
}








