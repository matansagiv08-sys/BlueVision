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

const USER_STORAGE_KEY = "bluevisionUser";

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
        file: "./pages/production/tasks_workorder.html",
        js: "./pages/production/tasks_workorder.js",
        css: "./pages/production/tasks_workorder.css",
        init: "initTasksWorkOrder",
        title: "ניהול סדר עבודה",
        subtitle: "",
        mode: "production"
    },
    "/production/add_item_to_production": {
        file: "./pages/production/add_item_to_production.html",
        js: "./pages/production/add_item_to_production.js",
        css: "./pages/production/add_item_to_production.css",
        init: "initAddItemToProduction",
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
    "/inventory/inventory_check/results": {
        file: "./pages/inventory/inventory_results.html",
        js: "./pages/inventory/inventory_results.js",
        css: "./pages/inventory/inventory_results.css",
        init: "initInventoryResults",
        title: "תוצאות בדיקת מלאי",
        subtitle: "פירוט החוסרים לפי דרישה",
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
    },
    "/inventory/uav_BOM": {
        file: "./pages/inventory/uav_BOM.html",
        js: "./pages/inventory/uav_BOM.js",
        css: "./pages/inventory/uav_BOM.css",
        init: "initUavBOM",
        title: "כמויות מעץ מוצר",
        subtitle: "פריטים וחלקים לייצור כטב״מים",
        mode: "inventory"
    },
    "/admin/users": {
        file: "./pages/admin/users.html",
        js: "./pages/admin/users.js",
        css: "./pages/admin/users.css",
        init: "initAdminUsers",
        title: "ניהול משתמשים",
        subtitle: "ניהול הרשאות משתמשים",
        mode: "admin"
    }
};

/* =========================
   GLOBAL HELPERS
========================= */
function setActiveMenu(path) {
    const activePath = path === "/inventory/inventory_check/results"
        ? "/inventory/inventory_check"
        : path;

    document.querySelectorAll(".top-menu a[data-route]").forEach(a => {
        a.classList.toggle("active", a.getAttribute("href") === `#${activePath}`);
    });
}

function updateTopMenuVisibility(path) {
    const topMenu = document.querySelector(".top-menu");
    if (!topMenu) return;

    topMenu.style.display = path === "/admin/users" ? "none" : "flex";
}

function getCurrentUser() {
    const raw = sessionStorage.getItem(USER_STORAGE_KEY);
    if (!raw) return null;

    try {
        return JSON.parse(raw);
    } catch (e) {
        sessionStorage.removeItem(USER_STORAGE_KEY);
        return null;
    }
}

function redirectToLogin() {
    window.location.href = "../Login/login.html";
}

function logoutUser() {
    sessionStorage.removeItem(USER_STORAGE_KEY);
    redirectToLogin();
}

function canAccessRoute(path, user) {
    if (!user || !path) return false;

    if (path.startsWith("/production/")) {
        return !!user.canViewProduction;
    }

    if (path.startsWith("/inventory/")) {
        return !!user.canViewStock;
    }

    if (path.startsWith("/admin/")) {
        return !!user.canManageUsers;
    }

    return false;
}

function getFirstAllowedRoute(user) {
    if (user?.canViewProduction) return "/production/tasks_board";
    if (user?.canViewStock) return "/inventory/inventory_dashboard";
    if (user?.canManageUsers) return "/admin/users";
    return null;
}

function getUserAvatarInitial(user) {
    const rawUsername = String(user?.username || user?.fullName || "").trim();
    if (!rawUsername) {
        return "?";
    }

    const cleanUsername = rawUsername.includes("@")
        ? rawUsername.split("@")[0].trim()
        : rawUsername;

    if (!cleanUsername) {
        return "?";
    }

    return cleanUsername.charAt(0).toLocaleUpperCase("he-IL");
}

function applyPermissionVisibility(user) {
    document.querySelectorAll("[data-permission='production']").forEach(el => {
        el.classList.toggle("perm-hidden", !user?.canViewProduction);
    });

    document.querySelectorAll("[data-permission='stock']").forEach(el => {
        el.classList.toggle("perm-hidden", !user?.canViewStock);
    });

    document.querySelectorAll("[data-permission='manageUsers']").forEach(el => {
        el.classList.toggle("perm-hidden", !user?.canManageUsers);
    });

    const userNameEl = document.getElementById("sidebarUserName");
    if (userNameEl) {
        userNameEl.textContent = user?.fullName || user?.username || "משתמש";
    }

    const userRoleEl = document.getElementById("sidebarUserRole");
    if (userRoleEl) {
        userRoleEl.textContent = user?.canManageUsers ? "Admin" : "User";
    }

    const popupUserNameEl = document.getElementById("popupUserName");
    if (popupUserNameEl) {
        popupUserNameEl.textContent = user?.fullName || user?.username || "משתמש";
    }

    const popupUserRoleEl = document.getElementById("popupUserRole");
    if (popupUserRoleEl) {
        popupUserRoleEl.textContent = user?.canManageUsers ? "Admin" : "User";
    }

    const userAvatarEl = document.querySelector(".user-avatar");
    if (userAvatarEl) {
        userAvatarEl.textContent = getUserAvatarInitial(user);
    }

    syncSidebarOpenWidth();
}

function closeSidebarUserPopup() {
    const popup = document.getElementById("sidebarUserPopup");
    const trigger = document.getElementById("sidebarUserTrigger");
    const confirm = document.getElementById("popupLogoutConfirm");
    const actions = document.getElementById("popupActions");

    if (popup) {
        popup.hidden = true;
    }
    if (trigger) {
        trigger.setAttribute("aria-expanded", "false");
    }
    if (confirm) {
        confirm.hidden = true;
    }
    if (actions) {
        actions.hidden = false;
    }
}

function setupSidebarUserPopup() {
    const trigger = document.getElementById("sidebarUserTrigger");
    const popup = document.getElementById("sidebarUserPopup");
    const logoutBtn = document.getElementById("popupLogoutBtn");
    const confirmWrap = document.getElementById("popupLogoutConfirm");
    const confirmBtn = document.getElementById("popupConfirmLogoutBtn");
    const cancelBtn = document.getElementById("popupCancelLogoutBtn");
    const actions = document.getElementById("popupActions");

    if (!trigger || !popup) {
        return;
    }

    trigger.addEventListener("click", (e) => {
        e.stopPropagation();
        const shouldOpen = popup.hidden;
        if (shouldOpen) {
            popup.hidden = false;
            trigger.setAttribute("aria-expanded", "true");
        } else {
            closeSidebarUserPopup();
        }
    });

    popup.addEventListener("click", (e) => {
        e.stopPropagation();
    });

    logoutBtn?.addEventListener("click", () => {
        if (confirmWrap) {
            confirmWrap.hidden = false;
        }
        if (actions) {
            actions.hidden = true;
        }
    });

    cancelBtn?.addEventListener("click", () => {
        if (confirmWrap) {
            confirmWrap.hidden = true;
        }
        if (actions) {
            actions.hidden = false;
        }
    });

    confirmBtn?.addEventListener("click", () => {
        closeSidebarUserPopup();
        logoutUser();
    });

    document.addEventListener("click", (e) => {
        if (!popup.hidden && !trigger.contains(e.target) && !popup.contains(e.target)) {
            closeSidebarUserPopup();
        }
    });

    document.addEventListener("keydown", (e) => {
        if (e.key === "Escape") {
            closeSidebarUserPopup();
        }
    });
}

function syncSidebarOpenWidth() {
    const sidebar = document.getElementById("sidebar");
    if (!sidebar || sidebar.classList.contains("collapsed")) {
        return;
    }

    const cs = window.getComputedStyle(sidebar);
    const horizontalPadding = parseFloat(cs.paddingLeft || "0") + parseFloat(cs.paddingRight || "0");

    let contentMax = 0;
    const navLinks = Array.from(sidebar.querySelectorAll(".sidebar-nav a")).filter(el => !el.classList.contains("perm-hidden"));
    navLinks.forEach(link => {
        contentMax = Math.max(contentMax, link.scrollWidth);
    });

    const userSection = sidebar.querySelector(".sidebar-user");
    if (userSection && window.getComputedStyle(userSection).display !== "none") {
        contentMax = Math.max(contentMax, userSection.scrollWidth);
    }

    const logoutBtn = sidebar.querySelector(".sidebar-logout-btn");
    if (logoutBtn && window.getComputedStyle(logoutBtn).display !== "none") {
        contentMax = Math.max(contentMax, logoutBtn.scrollWidth);
    }

    const openWidth = Math.max(190, Math.min(320, Math.ceil(contentMax + horizontalPadding + 10)));
    document.documentElement.style.setProperty("--sidebar-open-width", `${openWidth}px`);
}

/* Load a JS file once (prevents duplicates) */
//function loadScriptOnce(src) {
//    return new Promise((resolve, reject) => {
//        if (document.querySelector(`script[data-dynamic="true"][src="${src}"]`)) {
//            return resolve();
//        }

//        const s = document.createElement("script");
//        s.src = src;
//        s.defer = true;
//        s.dataset.dynamic = "true";
//        s.onload = () => resolve();
//        s.onerror = () => reject(new Error(`Failed to load script: ${src}`));
//        document.body.appendChild(s);
//    });
//}

function loadScriptOnce(src) {
    return new Promise((resolve, reject) => {
        // Remove previous instance so edits are picked up
        document.querySelectorAll(`script[data-dynamic="true"][data-src="${src}"]`)
            .forEach(s => s.remove());

        const s = document.createElement("script");
        const bust = `v=${Date.now()}`;
        s.src = src.includes("?") ? `${src}&${bust}` : `${src}?${bust}`;
        s.defer = true;
        s.dataset.dynamic = "true";
        s.dataset.src = src; // store original for matching next time
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
        // If this css was already loaded, remove it so edits are picked up
        document.querySelectorAll(`link[data-dynamic="true"][data-href="${href}"]`)
            .forEach(l => l.remove());

        const l = document.createElement("link");
        l.rel = "stylesheet";

        // cache-bust so the browser fetches your updated file
        const bust = `v=${Date.now()}`;
        l.href = href.includes("?") ? `${href}&${bust}` : `${href}?${bust}`;

        l.dataset.dynamic = "true";
        l.dataset.href = href; // store original for matching next time

        l.onload = () => resolve();
        l.onerror = () => reject(new Error(`Failed to load css: ${href}`));
        document.head.appendChild(l);
    });
}

/* =========================
   ROUTER
========================= */
async function loadRoute() {
    const user = getCurrentUser();
    if (!user) {
        redirectToLogin();
        return;
    }

    applyPermissionVisibility(user);

    const initialPath = location.hash.replace("#", "");
    const path = initialPath || getFirstAllowedRoute(user) || "/admin/users";

    if (!initialPath && path) {
        location.hash = `#${path}`;
        return;
    }

    if (!canAccessRoute(path, user)) {
        const fallbackPath = getFirstAllowedRoute(user);
        if (!fallbackPath) {
            console.error("No permitted routes for current user.");
            return;
        }

        if (location.hash.replace("#", "") !== fallbackPath) {
            location.hash = `#${fallbackPath}`;
        }
        return;
    }

    const route = routes[path];
    if (!route) return;

    try {
        // 1) Load HTML
        const bust = `v=${Date.now()}`;
        const fileUrl = route.file.includes("?") ? `${route.file}&${bust}` : `${route.file}?${bust}`;
        const res = await fetch(fileUrl, { cache: "no-store" });
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
        updateTopMenuVisibility(path);

    } catch (err) {
        console.error("שגיאה בטעינת הדף:", err);
    }
}

/* =========================
   SIDEBAR TOGGLE (global)
========================= */
document.getElementById("sidebarToggle")?.addEventListener("click", () => {
    const sidebar = document.getElementById("sidebar");
    sidebar?.classList.toggle("collapsed");
    if (sidebar && !sidebar.classList.contains("collapsed")) {
        syncSidebarOpenWidth();
    }
});

setupSidebarUserPopup();

/* =========================
   EVENT LISTENERS
========================= */
window.addEventListener("hashchange", loadRoute);
window.addEventListener("load", loadRoute);
window.addEventListener("resize", syncSidebarOpenWidth);

/* Close generic modal */
window.closeGenericModal = function () {
    const modal = document.getElementById("genericModal");
    if (modal) modal.style.display = "none";

    const submitBtn = document.getElementById("modalSubmitBtn");
    if (submitBtn) {
        submitBtn.disabled = true;
        submitBtn.onclick = null;
    }
};
