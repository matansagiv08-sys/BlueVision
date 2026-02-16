const app = document.getElementById("app");
const pageTitle = document.getElementById("pageTitle");
const pageSubtitle = document.getElementById("pageSubtitle");

/* =========================
   ROUTES
========================= */
const routes = {
    "/production/dashboard": {
        file: "./pages/production/dashboard.html",
        title: "דוח ראשי",
        subtitle: "סקירה כללית"
    },
    "/production/stock-check": {
        file: "./pages/production/stock-check.html",
        title: "בדיקת מלאי",
        subtitle: "חישוב דרישות מלאי"
    },
    "/stock/home": {
        file: "./pages/stock/stock-home.html",
        title: "Stock",
        subtitle: "Stock overview"
    },
    "/admin/home": {
        file: "./pages/admin/admin-home.html",
        title: "Admin",
        subtitle: "System management"
    }
};

/* =========================
   ROUTER FUNCTIONS
========================= */

function getRouteFromHash() {
    const h = location.hash || "#/production/dashboard";
    const path = h.replace("#", "");
    return routes[path] ? path : "/production/dashboard";
}

async function loadRoute(routePath) {
    const route = routes[routePath];
    if (!route) return;

    try {
        const res = await fetch(route.file, { cache: "no-store" });
        const html = await res.text();

        app.innerHTML = html;

        pageTitle.textContent = route.title;
        pageSubtitle.textContent = route.subtitle;

        setActiveTopMenu(routePath);

    } catch (err) {
        app.innerHTML = "<h2>Page not found</h2>";
    }
}

function setActiveTopMenu(routePath) {
    document.querySelectorAll(".top-menu a[data-route]").forEach(a => {
        const linkPath = a.getAttribute("href").replace("#", "");
        a.classList.toggle("active", linkPath === routePath);
    });
}


/* =========================
   NAVIGATION
========================= */

document.addEventListener("click", (e) => {
    const link = e.target.closest("[data-route]");
    if (!link) return;

    e.preventDefault();
    const routePath = link.getAttribute("href").replace("#", "");
    location.hash = `#${routePath}`;
});

/* =========================
   SIDEBAR TOGGLE
========================= */

document.getElementById("sidebarToggle")
    .addEventListener("click", () => {
        document.getElementById("sidebar")
            .classList.toggle("collapsed");
    });

/* =========================
   INIT
========================= */

window.addEventListener("hashchange", () => {
    loadRoute(getRouteFromHash());
});

loadRoute(getRouteFromHash());
