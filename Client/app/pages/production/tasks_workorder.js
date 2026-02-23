let allTasks = [];

window.initWorkOrder = function () {
    // נתוני דוגמה לניסיון
    allTasks = [
        { po: "100245", sku: "WB-BODY-01", desc: "גוף WanderB G2", sn: "SN-9921", status: "active", urgency: "critical", deadline: "24/02", station: "plating" },
        { po: "100246", sku: "WB-WING-02", desc: "כנף ימין", sn: "SN-9922", status: "waiting", urgency: "high", deadline: "25/02", station: "layup" }
    ];

    // נחכה רגע קטנטן שה-HTML יתייצב ב-DOM
    setTimeout(() => {
        renderTasks(allTasks);
        console.log("Table rendered with", allTasks.length, "items");
    }, 10);
};

function renderTasks(data) {
    const tbody = document.getElementById("tasks-table-body");
    if (!tbody) return;

    if (data.length === 0) {
        tbody.innerHTML = `<tr><td colspan="7" style="text-align:center; padding:20px;">אין נתונים להתאמה</td></tr>`;
        return;
    }

    tbody.innerHTML = data.map(task => {
        const colorClass = task.status === 'active' ? 'pill-blue' : 'pill-yellow';
        const statusText = task.status === 'active' ? 'בביצוע' : 'ממתין';
        const urgencyClass = task.urgency === 'critical' ? 'pill-red' : 'pill-yellow';

        return `
            <tr onclick="window.openItemDetailsModal('${task.po}')">
                <td style="font-weight:700;">${task.po}</td>
                <td>${task.sku}</td>
                <td>${task.desc}</td>
                <td>${task.sn}</td>
                <td><span class="status-pill ${colorClass}">${statusText}</span></td>
                <td><span class="status-pill ${urgencyClass}">${task.urgency === 'critical' ? 'קריטי 🔥' : 'גבוה'}</span></td>
                <td>${task.deadline}</td>
            </tr>
        `;
    }).join('');
}

window.filterByStation = function (station, btn) {
    // 1. עדכון ויזואלי של הכפתורים בסליידר
    const buttons = btn.parentElement.querySelectorAll('.slider-btn');
    buttons.forEach(b => b.classList.remove('active', 'pill-blue'));
    btn.classList.add('active', 'pill-blue');

    // 2. לוגיקת הסינון
    if (station === 'all') {
        renderTasks(allTasks);
    } else {
        const filtered = allTasks.filter(t => t.station === station);
        renderTasks(filtered);
    }

    // 3. סימון שהיה שינוי (כדי לאפשר שמירה אם תרצי)
    window.markAsDirty();
};

// פונקציה להפעלת כפתור השמירה
window.markAsDirty = function () {
    const saveBtn = document.getElementById("saveOrderBtn");
    if (saveBtn) {
        saveBtn.disabled = false;
        saveBtn.style.opacity = "1";
    }
};

// בתוך פונקציית השמירה, נחזיר אותו למצב כבוי אחרי ההצלחה
window.saveWorkOrder = function () {
    console.log("Saving changes...");

    // כאן בא קוד השמירה שלך...

    // אחרי השמירה:
    const saveBtn = document.getElementById("saveOrderBtn");
    if (saveBtn) saveBtn.disabled = true;
    alert("השינויים נשמרו בהצלחה!");
};

// בכל פעם שמשנים דחיפות או מסננים - אנחנו "מלכלכים" את הנתונים ומאפשרים שמירה
window.filterWorkOrder = function () {
    // קוד הסינון הקיים שלך...
    window.markAsDirty();
};

window.optimizeRoute = function () {
    console.log("Optimizing...");
    // קוד האלגוריתם...
    window.markAsDirty(); // מאפשר שמירה כי האלגוריתם שינה סדר
};