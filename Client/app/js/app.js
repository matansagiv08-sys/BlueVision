/* --- נתוני דמה (MOCK DATA) --- */
const mockData = [
    { orderNum: '20936', aircraftType: 'WB', partNum: 'WB-CW-001', description: 'Central wing structure', quantity: 2, status1: 'done', status2: 'process', progress: 25 },
    { orderNum: '21045', aircraftType: 'TBV', partNum: 'TB-PF-001', description: 'Primary fuselage', quantity: 1, status1: 'done', status2: 'none', progress: 10 }
];

const projectsMock = [
    {
        name: "פרויקט מרוקו",
        deadline: "15/03/2026",
        progress: 45,
        uavs: [
            { id: "783", model: "WB", progress: 60, parts: mockData }
        ]
    }
];

/* =========================
   ROUTES
========================= */
const routes = {
    "/production/tasks_board": {
        file: "./pages/production/tasks_board.html",
        title: "לוח משימות",
        subtitle: "מעקב אחר סטטוס תחנות ייצור לכל חלק",
        mode: "production"
    },
    "/production/projects_status": {
        file: "./pages/production/projects_status.html",
        title: "סטטוס פרויקטים",
        subtitle: "ניהול פרויקטים פעילים",
        mode: "production"
    },
    "/production/tasks_order": {
        file: "./pages/production/tasks_order.html",
        title: "ניהול סדר עבודה",
        subtitle: "תעדוף וארגון משימות ייצור לפי תחנות עבודה",
        mode: "production"
    },
    "/production/add_parts_to_production": {
        file: "./pages/production/add_parts_to_production.html",
        title: "הוספת חלק לייצור",
        subtitle: "הוספת חלק חדש לתהליך הייצור",
        mode: "production"
    },
    "/production/monthly_dashboard": {
        file: "./pages/production/monthly_dashboard.html",
        title: "דוח חודשי",
        subtitle: "סיכום ביצועים ותפוקה חודשית",
        mode: "production"
    },
    "/inventory/inventory_dashboard": {
        file: "./pages/inventory/inventory_dashboard.html",
        title: "דוח מלאי",
        subtitle: "סיכום סקירת מלאי כללי",
        mode: "inventory"
    },
    "/inventory/inventory_check": {
    file: "./pages/inventory/inventory_check.html",
    title: "בדיקת מלאי",
    subtitle: "חישוב חוסרי מלאי",
    mode: "inventory"
    },
    "/inventory/all_inventory": {
        file: "./pages/inventory/all_inventory.html",
        title: "מלאי כולל",
        subtitle: "",
        mode: "inventory"
    },
    "/inventory/orders_tracking": {
        file: "./pages/inventory/orders_tracking.html",
        title: "מעקב הזמנות רכש",
        subtitle: "",
        mode: "inventory"
    }

};

/* =========================
   ROUTER LOGIC
========================= */

async function loadRoute() {
    const hash = location.hash.replace("#", "") || "/production/tasks_board";
    const route = routes[hash];

    if (!route) return;

    try {
        if (hash === "/production/tasks_board") {
            renderTasksBoard(mockData);
        } else if (hash === "/production/projects_status") {
            renderProjectsStatus(projectsMock);
        }

    } catch (err) {
        console.error("טעינה נכשלה:", err);
    }
}

function handlePageLogic(hash) {
    if (hash === "/production/tasks_board") {
        if (typeof renderTasksBoard === "function") renderTasksBoard(mockData);
    }
    else if (hash === "/production/projects_status") {
        if (typeof renderProjectsStatus === "function") renderProjectsStatus(projectsMock);
    }
    // כאן תוסיפי בעתיד לוגיקה למלאי (למשל renderInventory)
}

function setActiveTopMenu(routePath) {
    document.querySelectorAll(".top-menu a[data-route]").forEach(a => {
        const linkPath = a.getAttribute("href").replace("#", "");
        a.classList.toggle("active", linkPath === routePath);
    });
}

/* =========================
   EVENT LISTENERS
========================= */

// האזנה לשינויים בכתובת (לחיצה על לינקים)
window.addEventListener("hashchange", loadRoute);

// טעינה ראשונית של הדף
window.addEventListener("load", loadRoute);

// Sidebar Toggle (נשאר קבוע)
document.getElementById("sidebarToggle")?.addEventListener("click", () => {
    document.getElementById("sidebar").classList.toggle("collapsed");
});

async function loadRoute() {
    const path = location.hash.replace("#", "") || "/production/tasks_board";
    const route = routes[path];
    if (!route) return;

    try {
        const res = await fetch(route.file);
        const html = await res.text();

        document.getElementById("app").innerHTML = html;
        document.getElementById("pageTitle").textContent = route.title;
        document.getElementById("pageSubtitle").textContent = route.subtitle;
        document.body.className = `mode-${route.mode}`;

        if (path === "/production/tasks_board") renderTasksBoard(mockData);
        if (path === "/production/projects_status") renderProjectsStatus(projectsMock);

        setActiveMenu(path);
    } catch (err) {
        console.error("שגיאה בטעינת הדף:", err);
    }
}

/* --- פונקציות רינדור פריטים לייצור --- */
const stations = ["הכנת תבניות וצבע", "ליווח סקין עליון", "ליווח סקין תחתון", "סגירה", "חליצה", "פינישים", "צבע", "QC"];
function renderTasksBoard(data) {
    const container = document.getElementById("tasks-board-container");
    if (!container) return;

    const grouped = data.reduce((acc, item) => {
        if (!acc[item.aircraftType]) acc[item.aircraftType] = [];
        acc[item.aircraftType].push(item);
        return acc;
    }, {});

    let html = "";
    for (const type in grouped) {
        html += `
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
                                ${stations.map(s => `<th>${s}</th>`).join('')}
                                <th>התקדמות</th>
                            </tr>
                        </thead>
                        <tbody>
                            ${grouped[type].map(row => createRowHtml(row)).join('')}
                        </tbody>
                    </table>
                </div>
            </section>`;
    }
    container.innerHTML = html;
}

// פונקציית עזר ליצירת כפתורי הסטטוס ייצור
function createStatusPill(status) {
    let cssClass = "";
    let text = "טרם התחיל";

    if (status === 'done') { cssClass = "status-done"; text = "בוצע"; }
    else if (status === 'process') { cssClass = "status-progress"; text = "בתהליך"; }

    return `<button class="status-pill ${cssClass}">${text}</button>`;
}


//הכנסת שורות לטבלת לוח משימות ייצור
function createRowHtml(row) {
    const rowData = encodeURIComponent(JSON.stringify(row));
    return `
        <tr onclick="openPartModal('${rowData}')">
            <td class="id-cell">${row.orderNum}</td>
            <td>${row.partNum}</td>
            <td>${row.serial || '-'}</td>
            <td class="desc-cell">${row.description}</td>
            <td>${row.quantity}</td>
            ${stations.map(s => `
                <td onclick="event.stopPropagation()">
                    <button class="status-pill" onclick="openStatusModal('${rowData}', '${s}')">טרם</button>
                </td>
            `).join('')}
            <td>
                <div class="progress-circle-container">
                    <div class="progress-circle-fill" style="background: conic-gradient(#007bff ${row.progress || 0}%, #eef2f7 0deg)">
                        <span>${row.progress || 0}%</span>
                    </div>
                </div>
            </td>
        </tr>`;
}

//יצירת כפתור סטטוס התקדמות פריט ייצור
function createStatusBtn(status) {
    const map = { 'done': 'status-done', 'process': 'status-progress', 'none': '' };
    const text = { 'done': 'בוצע', 'process': 'בתהליך', 'none': 'טרם' };
    return `<button class="status-pill ${map[status] || ''}" onclick="openStatusModal()">${text[status] || 'טרם'}</button>`;
}

//רינדור סטטוס פרוייקטים
function renderProjectsStatus(data) {
    const container = document.getElementById("projects-container");
    if (!container) return;

    container.innerHTML = data.map(project => `
        <div class="project-card">
            <div class="project-header" onclick="toggleAccordion(this)">
                <span>▼ ${project.name}</span>
                <span>${project.progress}%</span>
            </div>
            <div class="project-content" style="display:none; padding:20px;">
                טוען נתוני כטב"מים...
            </div>
        </div>
    `).join('');
}
function setActiveMenu(path) {
    document.querySelectorAll(".top-menu a").forEach(a => {
        a.classList.toggle("active", a.getAttribute("href") === `#${path}`);
    });
}

window.addEventListener("hashchange", loadRoute);
window.addEventListener("load", loadRoute);

// טעינה ראשונית
loadRoute();

