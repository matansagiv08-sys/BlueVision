/* =========================
   TEMP MOCK DATA (REMOVE LATER WHEN API CONNECTED)
========================= */
const mockData = [
    {
        orderNum: "20936",
        aircraftType: "WB",
        partNum: "WB-CW-001",
        description: "Central wing structure",
        quantity: 2,
        status1: "done",
        status2: "process",
        progress: 25
    },
    {
        orderNum: "21045",
        aircraftType: "TBV",
        partNum: "TB-PF-001",
        description: "Primary fuselage",
        quantity: 1,
        status1: "done",
        status2: "none",
        progress: 10
    }
];

/* =========================
   ROUTES
   IMPORTANT:
   - Each route can have: file, js, css, init, title, subtitle, mode
========================= */
const routes = {
    "/production/tasks_board": {
        file: "./pages/production/tasks_board.html",
        js: "./pages/production/tasks_board.js",
        css: "./pages/production/tasks_board.css",
        init: "initTasksBoard",
        title: "לוח משימות",
        subtitle: "מעקב אחר סטטוס תחנות ייצור לכל חלק",
        mode: "production"
    },
    "/production/projects_status": {
        file: "./pages/production/projects_status.html",
        js: "./pages/production/projects_status.js",
        css: "./pages/production/projects_status.css",
        init: "initProjectsStatus",
        title: "סטטוס פרויקטים",
        subtitle: "מעקב אחר התקדמות ייצור כטב\"מים בפרויקטים שונים",
        mode: "production"
    },
    "/production/tasks_order": {
        file: "./pages/production/tasks_order.html",
        js: "./pages/production/tasks_order.js",
        css: "./pages/production/tasks_order.css",
        init: "initTasksOrder",
        title: "ניהול סדר עבודה",
        subtitle: "",
        mode: "production"
    },
    "/production/add_parts_to_production": {
        file: "./pages/production/add_parts_to_production.html",
        js: "./pages/production/add_parts_to_production.js",
        css: "./pages/production/add_parts_to_production.css",
        init: "initAddPartsToProduction",
        title: "הוספת חלק לייצור",
        subtitle: "",
        mode: "production"
    },
    "/production/monthly_dashboard": {
        file: "./pages/production/monthly_dashboard.html",
        js: "./pages/production/monthly_dashboard.js",
        css: "./pages/production/monthly_dashboard.css",
        init: "initMonthlyDashboard",
        title: "דוח חודשי",
        subtitle: "",
        mode: "production"
    },

    "/inventory/inventory_dashboard": {
        file: "./pages/inventory/inventory_dashboard.html",
        js: "./pages/inventory/inventory_dashboard.js",
        css: "./pages/inventory/inventory_dashboard.css",
        init: "initInventoryDashboard",
        title: "דוח מלאי",
        subtitle: "סיכום סקירת מלאי כללי",
        mode: "inventory"
    },
    "/inventory/inventory_check": {
        file: "./pages/inventory/inventory_check.html",
        js: "./pages/inventory/inventory_check.js",
        css: "./pages/inventory/inventory_check.css",
        init: "initInventoryCheck",
        title: "בדיקת מלאי",
        subtitle: "חישוב חוסרי מלאי",
        mode: "inventory"
    },
    "/inventory/all_inventory": {
        file: "./pages/inventory/all_inventory.html",
        js: "./pages/inventory/all_inventory.js",
        css: "./pages/inventory/all_inventory.css",
        init: "initAllInventory",
        title: "מלאי כולל",
        subtitle: "",
        mode: "inventory"
    },
    "/inventory/orders_tracking": {
        file: "./pages/inventory/orders_tracking.html",
        js: "./pages/inventory/orders_tracking.js",
        css: "./pages/inventory/orders_tracking.css",
        init: "initOrdersTracking",
        title: "מעקב הזמנות רכש",
        subtitle: "",
        mode: "inventory"
    }
};

/* =========================
   GLOBAL HELPERS
========================= */
function setActiveMenu(path) {
    document.querySelectorAll(".top-menu a[data-route]").forEach(a => {
        a.classList.toggle("active", a.getAttribute("href") === `#${path}`);
    });
}

/* Load a JS file once (prevents duplicates) */
function loadScriptOnce(src) {
    return new Promise((resolve, reject) => {
        if (document.querySelector(`script[data-dynamic="true"][src="${src}"]`)) {
            return resolve();
        }

        const s = document.createElement("script");
        s.src = src;
        s.defer = true;
        s.dataset.dynamic = "true";
        s.onload = () => resolve();
        s.onerror = () => reject(new Error(`Failed to load script: ${src}`));
        document.body.appendChild(s);
    });
}

/* Remove previous page CSS (so pages do not override each other) */
function removeDynamicCss() {
    document.querySelectorAll(`link[data-dynamic="true"]`).forEach(l => l.remove());
}

/* Load a CSS file once */
function loadCssOnce(href) {
    return new Promise((resolve, reject) => {
        if (document.querySelector(`link[data-dynamic="true"][href="${href}"]`)) {
            return resolve();
        }

        const l = document.createElement("link");
        l.rel = "stylesheet";
        l.href = href;
        l.dataset.dynamic = "true";
        l.onload = () => resolve();
        l.onerror = () => reject(new Error(`Failed to load css: ${href}`));
        document.head.appendChild(l);
    });
}

/* =========================
   ROUTER
========================= */
async function loadRoute() {
    const path = location.hash.replace("#", "") || "/production/tasks_board";
    const route = routes[path];
    if (!route) return;

    try {
        // 1) Load HTML
        const res = await fetch(route.file);
        if (!res.ok) throw new Error(`Failed to fetch: ${route.file}`);
        const html = await res.text();

        const app = document.getElementById("app");
        if (!app) throw new Error("Missing #app element");
        app.innerHTML = html;

        // 2) Update title/subtitle
        const titleEl = document.getElementById("pageTitle");
        const subEl = document.getElementById("pageSubtitle");
        if (titleEl) titleEl.textContent = route.title || "";
        if (subEl) subEl.textContent = route.subtitle || "";

        // 3) Switch mode (production/inventory)
        document.body.className = `mode-${route.mode}`;

        // 4) Load page CSS (clean previous page css first)
        removeDynamicCss();
        if (route.css) {
            await loadCssOnce(route.css);
        }

        // 5) Load page JS
        if (route.js) {
            await loadScriptOnce(route.js);
        }

        // 6) Call init function if exists
        if (route.init && typeof window[route.init] === "function") {
            window[route.init]();
        }

        // 7) Highlight top menu active tab
        setActiveMenu(path);

    } catch (err) {
        console.error("שגיאה בטעינת הדף:", err);
    }
}

/* =========================
   SIDEBAR TOGGLE (global)
========================= */
document.getElementById("sidebarToggle")?.addEventListener("click", () => {
    document.getElementById("sidebar")?.classList.toggle("collapsed");
});

/* =========================
   EVENT LISTENERS
========================= */
window.addEventListener("hashchange", loadRoute);
window.addEventListener("load", loadRoute);

/* Close generic modal */
window.closeGenericModal = function () {
    const modal = document.getElementById("genericModal");
    if (modal) modal.style.display = "none";
};