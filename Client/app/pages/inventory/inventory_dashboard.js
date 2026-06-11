// פונקציית ה-init המרכזית שנקראת אוטומטית על ידי app.js כשהדף נטען
window.initInventoryDashboard = function () {
    console.log("Inventory Dashboard הופעל בהצלחה!");

    // טעינה דינמית של ספריית הגרפים Chart.js משרת חיצוני (CDN)
    if (!window.Chart) {
        const script = document.createElement('script');
        script.src = 'https://cdn.jsdelivr.net/npm/chart.js';
        script.onload = function () {
            console.log("ספריית Chart.js נטענה בהצלחה!");
            loadSavedCharts(); // פונקציה שנכתוב בהמשך לשליפת הגרפים הקיימים
        };
        document.head.appendChild(script);
    } else {
        loadSavedCharts();
    }
};

// פונקציה זמנית רק כדי שהדף לא יישבר כשה-init קורא לה
function loadSavedCharts() {
    console.log("כאן נטען בעתיד את הגרפים השמורים מה-SQL...");
}

// ==========================================
// פונקציות ניהול חלונית העריכה (Modal Popup)
// ==========================================

// 1. פתיחת חלונית העריכה של ה-AI
window.openEditDashboardModal = function () {
    const modal = document.getElementById("dashboardEditModal");
    if (modal) {
        modal.style.display = "flex";
        canSaveGeneratedResult = false;
        setSaveButtonEnabled(false);
        console.log("מודל עריכה נפתח");
    }
};

// 2. סגירת חלונית העריכה של ה-AI
window.closeEditDashboardModal = function () {
    const modal = document.getElementById("dashboardEditModal");
    if (modal) {
        modal.style.display = "none";
        console.log("מודל עריכה נסגר");
    }
};

// משתנים גלובליים לשמירת נתוני הגרף הנוכחי שנוצר ב-AI
let currentPreviewChartInstance = null;
let savedChartInstances = {};
let modalChartInstance = null;
let savedVisualizationsState = {};
let savedChartsState = [];
let layoutSnapshot = [];
let isArrangeMode = false;
let draggedChartId = null;
let lastGeneratedSql = "";
let lastGeneratedChartType = "bar";
let canSaveGeneratedResult = false;

const DASHBOARD_TYPE = "Inventory";
const DASHBOARD_NAME = "דוח מלאי";
const DASHBOARD_COLUMNS = 3;
const DASHBOARD_SIZE_OPTIONS = {
    small: { label: "קטן", columns: 1, rows: 1 },
    wide: { label: "רחב", columns: 2, rows: 1 },
    fullWidth: { label: "רוחב מלא", columns: 3, rows: 1 },
    large: { label: "גדול", columns: 2, rows: 2 },
    extraLarge: { label: "גדול מאוד", columns: 3, rows: 2 }
};

function normalizeVisualizationType(data, fallback = "bar") {
    const raw = (data?.visualizationType || data?.chartType || data?.resultType || fallback || "bar").toString().toLowerCase().trim();
    if (raw === "table") return "table";
    if (raw === "line") return "line";
    if (raw === "pie") return "pie";
    if (raw === "bar") return "bar";
    if (raw === "single_series") return "bar";
    if (raw === "multi_series") return "line";
    return "bar";
}

function setPreviewMessage(message) {
    const msg = document.getElementById("previewMessage");
    if (!msg) return;
    msg.style.display = "block";
    msg.textContent = message;
}

function clearPreview() {
    if (currentPreviewChartInstance) {
        currentPreviewChartInstance.destroy();
        currentPreviewChartInstance = null;
    }

    const canvas = document.getElementById("previewChartCanvas");
    const tableWrap = document.getElementById("previewTableContainer");
    const msg = document.getElementById("previewMessage");
    const chartWrap = canvas?.parentElement;

    if (chartWrap) chartWrap.style.display = "none";
    if (tableWrap) {
        tableWrap.style.display = "none";
        tableWrap.innerHTML = "";
    }
    if (msg) {
        msg.style.display = "none";
        msg.textContent = "";
    }
}

function setSaveButtonEnabled(enabled) {
    const saveBtn = document.querySelector('#previewChartContainer button[onclick="saveGeneratedChart()"]');
    if (!saveBtn) return;
    saveBtn.disabled = !enabled;
    saveBtn.style.opacity = enabled ? "1" : "0.6";
    saveBtn.style.cursor = enabled ? "pointer" : "not-allowed";
}

function renderHtmlTable(containerId, rows) {
    const container = document.getElementById(containerId);
    if (!container) return;

    if (!Array.isArray(rows) || rows.length === 0) {
        container.innerHTML = '<div class="dashboard-preview-message">לא נמצאו נתונים להצגה</div>';
        container.style.display = "block";
        return;
    }

    const headers = Object.keys(rows[0] || {});
    if (headers.length === 0) {
        container.innerHTML = '<div class="dashboard-preview-message">לא נמצאו נתונים להצגה</div>';
        container.style.display = "block";
        return;
    }

    const headHtml = headers.map(h => `<th>${escapeHtml(h)}</th>`).join("");
    const bodyHtml = rows.map(r => {
        const tds = headers.map(h => `<td>${escapeHtml(r[h] === null || r[h] === undefined ? "" : String(r[h]))}</td>`).join("");
        return `<tr>${tds}</tr>`;
    }).join("");

    container.innerHTML = `
        <div class="dashboard-table-scroll">
            <table class="dashboard-table">
                <thead><tr>${headHtml}</tr></thead>
                <tbody>${bodyHtml}</tbody>
            </table>
        </div>`;
    container.style.display = "block";
}

function renderChartInCanvas(canvasId, chartType, labels, values, mapKey) {
    const canvas = document.getElementById(canvasId);
    if (!canvas) return;

    if (mapKey && savedChartInstances[mapKey]) {
        savedChartInstances[mapKey].destroy();
        delete savedChartInstances[mapKey];
    }

    const ctx = canvas.getContext("2d");
    const instance = new Chart(ctx, {
        type: chartType,
        data: {
            labels: labels,
            datasets: [{
                label: 'נתונים',
                data: values,
                backgroundColor: chartType === 'pie'
                    ? ['#0c2340', '#1d4ed8', '#3b82f6', '#60a5fa', '#93c5fd', '#cbd5e1']
                    : '#0c2340',
                borderColor: '#0c2340',
                borderWidth: 1
            }]
        },
        options: {
            responsive: true,
            maintainAspectRatio: false,
            layout: { padding: 4 },
            plugins: {
                legend: {
                    display: chartType === 'pie',
                    position: 'bottom',
                    labels: chartType === 'pie' ? {
                        boxWidth: 12,
                        padding: 12
                    } : undefined
                }
            },
            scales: chartType !== 'pie' ? {
                x: { ticks: { autoSkip: true, maxRotation: 35 } },
                y: { beginAtZero: true, ticks: { precision: 0 } }
            } : {}
        }
    });

    if (mapKey) {
        savedChartInstances[mapKey] = instance;
    } else {
        currentPreviewChartInstance = instance;
    }
}

function closeAllCardMenus() {
    document.querySelectorAll('.card-menu-panel').forEach(panel => {
        panel.style.display = 'none';
        panel.classList.remove('submenu-flip');
    });
    document.querySelectorAll('.card-size-menu.open').forEach(menu => menu.classList.remove('open'));
}

function normalizeLayoutSize(size) {
    return DASHBOARD_SIZE_OPTIONS[size] ? size : "small";
}

function getChartId(chart) {
    return chart.chartID || chart.ChartID;
}

function getLayoutSize(chart) {
    return normalizeLayoutSize(chart.LayoutSize || chart.layoutSize || "small");
}

function getLayoutDimensions(size) {
    return DASHBOARD_SIZE_OPTIONS[normalizeLayoutSize(size)] || DASHBOARD_SIZE_OPTIONS.small;
}

function cloneLayout(charts) {
    return (charts || []).map((chart, index) => ({
        ChartID: getChartId(chart),
        LayoutSize: getLayoutSize(chart),
        DisplayOrder: index,
        GridX: Number(chart.gridX ?? chart.GridX ?? 0) || 0,
        GridY: Number(chart.gridY ?? chart.GridY ?? 0) || 0
    }));
}

function createDefaultLayout(charts) {
    return (charts || []).map((chart, index) => ({
        ChartID: getChartId(chart),
        LayoutSize: "small",
        DisplayOrder: index,
        GridX: index % DASHBOARD_COLUMNS,
        GridY: Math.floor(index / DASHBOARD_COLUMNS)
    }));
}

function applyLayoutToCharts(layout) {
    const layoutById = new Map((layout || []).map(item => [String(item.ChartID), item]));
    savedChartsState.forEach((chart, index) => {
        const item = layoutById.get(String(getChartId(chart)));
        chart.LayoutSize = normalizeLayoutSize(item?.LayoutSize || item?.layoutSize || chart.LayoutSize || chart.layoutSize || "small");
        chart.DisplayOrder = Number(item?.DisplayOrder ?? item?.displayOrder ?? index) || 0;
        chart.GridX = Number(item?.GridX ?? item?.gridX ?? 0) || 0;
        chart.GridY = Number(item?.GridY ?? item?.gridY ?? 0) || 0;
    });
    savedChartsState.sort((a, b) => (Number(a.DisplayOrder ?? 0) - Number(b.DisplayOrder ?? 0)) || (getChartId(a) - getChartId(b)));
}

function calculatePackedLayout(charts) {
    const occupied = [];
    const layout = [];

    function isFree(x, y, w, h) {
        if (x + w > DASHBOARD_COLUMNS) return false;
        for (let yy = y; yy < y + h; yy++) {
            for (let xx = x; xx < x + w; xx++) {
                if (occupied[yy]?.[xx]) return false;
            }
        }
        return true;
    }

    function occupy(x, y, w, h) {
        for (let yy = y; yy < y + h; yy++) {
            occupied[yy] = occupied[yy] || [];
            for (let xx = x; xx < x + w; xx++) occupied[yy][xx] = true;
        }
    }

    (charts || []).forEach((chart, index) => {
        const size = getLayoutSize(chart);
        const dimensions = getLayoutDimensions(size);
        let y = 0;
        let placed = false;
        while (!placed) {
            for (let x = 0; x < DASHBOARD_COLUMNS; x++) {
                if (isFree(x, y, dimensions.columns, dimensions.rows)) {
                    occupy(x, y, dimensions.columns, dimensions.rows);
                    layout.push({ chart, index, size, x, y, columns: dimensions.columns, rows: dimensions.rows });
                    placed = true;
                    break;
                }
            }
            if (!placed) y++;
        }
    });

    return layout;
}

function buildSizeMenu(chartID, currentSize) {
    return Object.entries(DASHBOARD_SIZE_OPTIONS).map(([size, option]) => `
        <button class="card-size-option ${currentSize === size ? 'active' : ''}" type="button" data-chart-id="${chartID}" data-size="${size}">
            <span class="size-grid-icon size-${size}" aria-hidden="true">
                ${Array.from({ length: 6 }).map((_, index) => `<span class="${index % DASHBOARD_COLUMNS < option.columns && Math.floor(index / DASHBOARD_COLUMNS) < option.rows ? 'filled' : ''}"></span>`).join("")}
            </span>
            <span>${option.label}</span>
        </button>`).join("");
}

function adjustCardMenuDirection(panel) {
    if (!panel) return;
    panel.classList.remove('submenu-flip');
    const submenu = panel.querySelector('.card-size-submenu');
    if (!submenu) return;

    submenu.style.visibility = 'hidden';
    submenu.style.display = 'block';
    const rect = submenu.getBoundingClientRect();
    submenu.style.display = '';
    submenu.style.visibility = '';

    if (rect.right > window.innerWidth - 12 || rect.left < 12) {
        panel.classList.add('submenu-flip');
    }
}

function updateArrangeControls() {
    const grid = document.getElementById("chartsGrid");
    const gridShell = document.getElementById("dashboardGridShell");
    const arrangeBanner = document.getElementById("dashboardArrangeBanner");
    const arrangeActions = document.getElementById("arrangeActions");
    const arrangeBtn = document.getElementById("arrangeDashboardBtn");
    if (grid) grid.classList.toggle("arrange-mode", isArrangeMode);
    if (gridShell) gridShell.classList.toggle("arrange-mode-shell", isArrangeMode);
    if (arrangeBanner) arrangeBanner.style.display = isArrangeMode ? "block" : "none";
    if (arrangeActions) arrangeActions.style.display = isArrangeMode ? "flex" : "none";
    if (arrangeBtn) arrangeBtn.classList.toggle("active", isArrangeMode);
}

function resizeDashboardCharts() {
    requestAnimationFrame(() => {
        Object.values(savedChartInstances).forEach(instance => {
            if (instance && typeof instance.resize === "function") instance.resize();
        });
    });
}

function renderSavedCharts() {
    const grid = document.getElementById("chartsGrid");
    const manageList = document.getElementById("manageChartsList");
    if (!grid) return;

    grid.innerHTML = "";
    if (manageList) manageList.innerHTML = "";

    if (!savedChartsState.length) {
        grid.innerHTML = `<div class="dashboard-empty-state">
                            <h3>הדשבורד שלך עדיין ריק</h3>
                            <p>לחצי על כפתור העריכה למעלה כדי לייצר את הגרף הראשון שלך בעזרת AI!</p>
                          </div>`;
        updateArrangeControls();
        return;
    }

    calculatePackedLayout(savedChartsState).forEach(item => {
        const chart = item.chart;
        const chartID = getChartId(chart);
        const chartIDArg = JSON.stringify(chartID);
        const chartTitle = chart.chartTitle || chart.ChartTitle || "תצוגה";
        const chartType = (chart.chartType || chart.ChartType || "bar").toLowerCase();
        chart.DisplayOrder = item.index;
        chart.GridX = item.x;
        chart.GridY = item.y;

        const cardHtml = `
            <div class="chart-card dashboard-grid-card size-${item.size}" id="chartCard_${chartID}" draggable="${isArrangeMode}" data-chart-id="${chartID}" data-size="${item.size}" style="grid-column: span ${item.columns}; grid-row: span ${item.rows};">
                <div class="chart-card-header">
                    <button class="drag-handle" title="גרירה לסידור" aria-label="גרירה לסידור">✥</button>
                    <span class="chart-card-title">${escapeHtml(chartTitle)}</span>
                    <div class="card-menu-wrap">
                        <button class="card-menu-btn" onclick='toggleCardMenu(${chartIDArg})' title="פעולות">⋯</button>
                        <div class="card-menu-panel" id="menu_${chartID}" style="display:none;">
                            <button onclick='openVisualizationModal(${chartIDArg})'>הגדל תצוגה</button>
                            <button onclick='showVisualizationQuery(${chartIDArg})'>הצג שאילתה</button>
                            <button onclick='exportVisualizationToExcel(${chartIDArg})'>ייצוא לאקסל</button>
                            <div class="card-size-menu">
                                <button class="card-size-trigger" type="button" data-size-menu-trigger>
                                    <span>שינוי גודל</span>
                                    <span class="submenu-arrow" aria-hidden="true">‹</span>
                                </button>
                                <div class="card-size-submenu" role="menu">
                                    ${buildSizeMenu(chartID, item.size)}
                                </div>
                            </div>
                            <button onclick='deleteChart(${chartIDArg})'>מחק</button>
                        </div>
                    </div>
                </div>
                <div class="chart-wrapper">
                    <canvas id="canvas_${chartID}"></canvas>
                </div>
                <div id="table_${chartID}" class="dashboard-table-wrap" style="display:none;"></div>
                <div id="msg_${chartID}" class="dashboard-preview-message" style="display:none;"></div>
            </div>`;
        grid.insertAdjacentHTML('beforeend', cardHtml);

        if (manageList) {
            const itemHtml = `
                <div class="manage-chart-item" id="manageItem_${chartID}" style="display: flex; justify-content: space-between; align-items: center; padding: 8px; background: white; border: 1px solid #e2e8f0; border-radius: 6px;">
                    <span>📊 ${escapeHtml(chartTitle)} (${escapeHtml(chartType)})</span>
                    <button class="btn-delete-chart" onclick='deleteChart(${chartIDArg})' style="background: none; border: none; cursor: pointer; font-size: 16px;">🗑️</button>
                </div>`;
            manageList.insertAdjacentHTML('beforeend', itemHtml);
        }

        renderVisualizationFromStateOrFetch(chart);
    });

    bindArrangeDragEvents();
    updateArrangeControls();
    resizeDashboardCharts();
}

function renderVisualizationFromStateOrFetch(chart) {
    const chartID = getChartId(chart);
    const state = savedVisualizationsState[chartID];
    if (!state) {
        fetchAndRenderChartData(chart);
        return;
    }

    const canvas = document.getElementById(`canvas_${chartID}`);
    const canvasWrap = canvas?.parentElement;
    const tableWrap = document.getElementById(`table_${chartID}`);
    const msgEl = document.getElementById(`msg_${chartID}`);

    if (msgEl) {
        msgEl.style.display = "none";
        msgEl.textContent = "";
    }

    if (savedChartInstances[`chart_${chartID}`]) {
        savedChartInstances[`chart_${chartID}`].destroy();
        delete savedChartInstances[`chart_${chartID}`];
    }

    if (tableWrap) {
        tableWrap.style.display = "none";
        tableWrap.innerHTML = "";
    }

    if (state.type === "table") {
        if (canvasWrap) canvasWrap.style.display = "none";
        renderHtmlTable(`table_${chartID}`, state.rows || []);
        return;
    }

    if (!state.labels?.length || !state.values?.length) {
        if (canvasWrap) canvasWrap.style.display = "none";
        if (msgEl) {
            msgEl.style.display = "block";
            msgEl.textContent = "לא נמצאו נתונים להצגה";
        }
        return;
    }

    if (canvasWrap) canvasWrap.style.display = "block";
    renderChartInCanvas(`canvas_${chartID}`, state.type, state.labels, state.values, `chart_${chartID}`);
}

window.toggleCardMenu = function (chartID) {
    const panel = document.getElementById(`menu_${chartID}`);
    if (!panel) return;
    const next = panel.style.display !== 'block';
    closeAllCardMenus();
    panel.style.display = next ? 'block' : 'none';
    if (next) requestAnimationFrame(() => adjustCardMenuDirection(panel));
};

window.changeVisualizationSize = function (chartID, size) {
    const chart = savedChartsState.find(item => String(getChartId(item)) === String(chartID));
    if (!chart) return;
    const normalizedSize = normalizeLayoutSize(size);
    chart.LayoutSize = normalizedSize;
    chart.layoutSize = normalizedSize;
    closeAllCardMenus();
    renderSavedCharts();
    if (!isArrangeMode) persistDashboardLayout(false);
};

window.enterArrangeDashboard = function () {
    if (isArrangeMode) {
        cancelArrangeDashboard();
        return;
    }
    if (window.matchMedia && window.matchMedia("(max-width: 760px)").matches) {
        showAppMessage("סידור בגרירה זמין במסך רחב. ניתן עדיין לשנות גדלים מתפריט הכרטיסים.");
        return;
    }
    layoutSnapshot = cloneLayout(savedChartsState);
    isArrangeMode = true;
    renderSavedCharts();
};

window.cancelArrangeDashboard = function () {
    applyLayoutToCharts(layoutSnapshot);
    isArrangeMode = false;
    renderSavedCharts();
};

window.resetDashboardLayout = function () {
    applyLayoutToCharts(createDefaultLayout(savedChartsState));
    renderSavedCharts();
};

window.saveDashboardLayout = function () {
    persistDashboardLayout(true);
};

function buildLayoutPayload() {
    const packedLayout = calculatePackedLayout(savedChartsState);
    return {
        DashboardType: DASHBOARD_TYPE,
        Items: packedLayout.map(item => ({
            ChartID: getChartId(item.chart),
            DisplayOrder: item.index,
            LayoutSize: item.size,
            GridX: item.x,
            GridY: item.y
        }))
    };
}

function persistDashboardLayout(exitArrangeMode) {
    const payload = buildLayoutPayload();

    $.ajax({
        url: server + "api/dashboard/layout",
        type: "POST",
        contentType: "application/json",
        data: JSON.stringify(payload),
        success: function () {
            payload.Items.forEach(item => {
                const chart = savedChartsState.find(existing => String(getChartId(existing)) === String(item.ChartID));
                if (!chart) return;
                chart.LayoutSize = item.LayoutSize;
                chart.DisplayOrder = item.DisplayOrder;
                chart.GridX = item.GridX;
                chart.GridY = item.GridY;
            });
            layoutSnapshot = cloneLayout(savedChartsState);
            if (exitArrangeMode) {
                isArrangeMode = false;
                renderSavedCharts();
            }
        },
        error: function (xhr) {
            showAppMessage("שמירת סידור הדשבורד נכשלה: " + (xhr.responseJSON?.error || xhr.statusText), { title: "שגיאה" });
        }
    });
}

function bindArrangeDragEvents() {
    document.querySelectorAll('.dashboard-grid-card').forEach(card => {
        card.addEventListener('dragstart', function (e) {
            if (!isArrangeMode) {
                e.preventDefault();
                return;
            }
            draggedChartId = this.dataset.chartId;
            this.classList.add('dragging');
            e.dataTransfer.effectAllowed = 'move';
            e.dataTransfer.setData('text/plain', draggedChartId);
        });

        card.addEventListener('dragend', function () {
            this.classList.remove('dragging');
            draggedChartId = null;
            document.querySelectorAll('.dashboard-drop-target').forEach(el => el.classList.remove('dashboard-drop-target'));
            resizeDashboardCharts();
        });

        card.addEventListener('dragover', function (e) {
            if (!isArrangeMode || !draggedChartId || this.dataset.chartId === draggedChartId) return;
            e.preventDefault();
            this.classList.add('dashboard-drop-target');
        });

        card.addEventListener('dragleave', function () {
            this.classList.remove('dashboard-drop-target');
        });

        card.addEventListener('drop', function (e) {
            if (!isArrangeMode || !draggedChartId || this.dataset.chartId === draggedChartId) return;
            e.preventDefault();
            this.classList.remove('dashboard-drop-target');
            reorderCharts(draggedChartId, this.dataset.chartId);
        });
    });
}

function reorderCharts(sourceId, targetId) {
    const sourceIndex = savedChartsState.findIndex(chart => String(getChartId(chart)) === String(sourceId));
    const targetIndex = savedChartsState.findIndex(chart => String(getChartId(chart)) === String(targetId));
    if (sourceIndex < 0 || targetIndex < 0) return;
    const [moved] = savedChartsState.splice(sourceIndex, 1);
    savedChartsState.splice(targetIndex, 0, moved);
    savedChartsState.forEach((chart, index) => chart.DisplayOrder = index);
    renderSavedCharts();
}

window.openVisualizationModal = function (chartID) {
    closeAllCardMenus();
    const state = savedVisualizationsState[chartID];
    if (!state) return;

    const modal = document.getElementById('dashboardCardModal');
    const title = document.getElementById('dashboardCardModalTitle');
    const chartWrap = document.getElementById('dashboardCardModalChartWrap');
    const tableWrap = document.getElementById('dashboardCardModalTableWrap');
    const queryWrap = document.getElementById('dashboardCardModalQueryWrap');

    if (!modal || !title || !chartWrap || !tableWrap || !queryWrap) return;

    title.textContent = state.title || 'תצוגה';
    queryWrap.style.display = 'none';
    queryWrap.textContent = '';

    if (modalChartInstance) {
        modalChartInstance.destroy();
        modalChartInstance = null;
    }

    if (state.type === 'table') {
        chartWrap.style.display = 'none';
        tableWrap.style.display = 'block';
        renderHtmlTable('dashboardCardModalTableWrap', state.rows || []);
    } else {
        tableWrap.style.display = 'none';
        tableWrap.innerHTML = '';
        chartWrap.style.display = 'block';
        const canvas = document.getElementById('dashboardCardModalCanvas');
        if (canvas) {
            const ctx = canvas.getContext('2d');
            modalChartInstance = new Chart(ctx, {
                type: state.type,
                data: {
                    labels: state.labels || [],
                    datasets: [{
                        label: 'נתונים',
                        data: state.values || [],
                        backgroundColor: state.type === 'pie'
                            ? ['#0c2340', '#1d4ed8', '#3b82f6', '#60a5fa', '#93c5fd', '#cbd5e1']
                            : '#0c2340',
                        borderColor: '#0c2340',
                        borderWidth: 1
                    }]
                },
                options: {
                    responsive: true,
                    maintainAspectRatio: false,
                    plugins: {
                        legend: {
                            display: state.type === 'pie',
                            position: 'bottom',
                            labels: state.type === 'pie' ? {
                                boxWidth: 12,
                                padding: 12
                            } : undefined
                        }
                    },
                    scales: state.type !== 'pie' ? { y: { beginAtZero: true } } : {}
                }
            });
        }
    }

    modal.style.display = 'flex';
};

window.showVisualizationQuery = function (chartID) {
    closeAllCardMenus();
    const state = savedVisualizationsState[chartID];
    if (!state) return;

    const modal = document.getElementById('dashboardCardModal');
    const title = document.getElementById('dashboardCardModalTitle');
    const chartWrap = document.getElementById('dashboardCardModalChartWrap');
    const tableWrap = document.getElementById('dashboardCardModalTableWrap');
    const queryWrap = document.getElementById('dashboardCardModalQueryWrap');

    if (!modal || !title || !chartWrap || !tableWrap || !queryWrap) return;

    title.textContent = `שאילתה: ${state.title || ''}`;
    chartWrap.style.display = 'none';
    tableWrap.style.display = 'none';
    tableWrap.innerHTML = '';
    queryWrap.style.display = 'block';
    queryWrap.textContent = state.sql || 'לא קיימת שאילתה להצגה';

    if (modalChartInstance) {
        modalChartInstance.destroy();
        modalChartInstance = null;
    }

    modal.style.display = 'flex';
};

window.exportVisualizationToExcel = async function (chartID) {
    closeAllCardMenus();
    const state = savedVisualizationsState[chartID];
    console.info("[InventoryDashboard] Export block requested", { chartID, state });
    if (!state) {
        showAppMessage("הנתונים עדיין לא נטענו. נסו שוב בעוד רגע.");
        return;
    }

    if (!window.DashboardExcelExport) {
        console.error("[InventoryDashboard] DashboardExcelExport helper is not loaded");
        showAppMessage("רכיב הייצוא לא נטען. רעננו את הדף ונסו שוב.", { title: "שגיאה" });
        return;
    }

    try {
        const result = await window.DashboardExcelExport.exportBlock(state);
        if (!result.ok) showAppMessage(result.message || "לא נמצאו נתונים לייצוא");
    } catch (error) {
        console.error("[InventoryDashboard] Block Excel export failed", { chartID, state, error });
        showAppMessage("ייצוא לאקסל נכשל. נסו שוב מאוחר יותר.", { title: "שגיאה" });
    }
};

window.exportDashboardToExcel = async function () {
    const blocks = savedChartsState.map(chart => {
        const chartID = getChartId(chart);
        return savedVisualizationsState[chartID] || {
            id: chartID,
            title: chart.chartTitle || chart.ChartTitle || "תצוגה",
            type: normalizeVisualizationType({ chartType: chart.chartType || chart.ChartType })
        };
    });
    console.info("[InventoryDashboard] Export full dashboard requested", { dashboardName: DASHBOARD_NAME, blocks });

    if (!window.DashboardExcelExport) {
        console.error("[InventoryDashboard] DashboardExcelExport helper is not loaded");
        showAppMessage("רכיב הייצוא לא נטען. רעננו את הדף ונסו שוב.", { title: "שגיאה" });
        return;
    }

    try {
        const result = await window.DashboardExcelExport.exportDashboard(blocks, DASHBOARD_NAME);
        if (!result.ok) showAppMessage(result.message || "אין נתונים לייצוא");
    } catch (error) {
        console.error("[InventoryDashboard] Dashboard Excel export failed", { dashboardName: DASHBOARD_NAME, blocks, error });
        showAppMessage("ייצוא הדוח לאקסל נכשל. נסו שוב מאוחר יותר.", { title: "שגיאה" });
    }
};

window.closeDashboardCardModal = function () {
    const modal = document.getElementById('dashboardCardModal');
    if (modal) modal.style.display = 'none';
    if (modalChartInstance) {
        modalChartInstance.destroy();
        modalChartInstance = null;
    }
};

// פונקציה השולחת את הפרומפט ל-AI ומציירת תצוגה מקדימה
window.generateAiChart = function () {
    const promptInput = document.getElementById("aiPromptInput");
    const previewContainer = document.getElementById("previewChartContainer");

    if (!promptInput || !promptInput.value.trim()) {
        showAppMessage("נא להזין שאלה או בקשה עבור ה-AI");
        return;
    }

    // משיכה מדויקת מתוך ה-sessionStorage שלכם
    const userRaw = sessionStorage.getItem("bluevisionUser");
    if (!userRaw) {
        showAppMessage("שגיאה: משתמש לא מחובר למערכת", { title: "שגיאה" });
        return;
    }
    const userObj = JSON.parse(userRaw);

    console.log("שולח בקשה ל-AI עבור: " + promptInput.value);

    $.ajax({
        url: server + "api/dashboard/generate",
        type: "POST",
        contentType: "application/json",
        data: JSON.stringify({
            Prompt: promptInput.value.trim(),
            DashboardType: DASHBOARD_TYPE
        }),
        success: function (data) {
            console.log("נתונים חזרו מה-AI בהצלחה!", data);

            clearPreview();

            lastGeneratedSql = data.sqlQuery;
            lastGeneratedChartType = normalizeVisualizationType(data);
            canSaveGeneratedResult = false;
            setSaveButtonEnabled(false);
            previewContainer.style.display = "block";
            const labels = Array.isArray(data.labels) ? data.labels : [];
            const values = Array.isArray(data.values) ? data.values : [];
            const rows = Array.isArray(data.rows) ? data.rows : [];
            const previewCanvas = document.getElementById('previewChartCanvas');
            const previewTable = document.getElementById('previewTableContainer');
            const chartWrap = previewCanvas?.parentElement;

            if (lastGeneratedChartType === "table") {
                if (chartWrap) chartWrap.style.display = "none";
                if (previewTable) {
                    renderHtmlTable("previewTableContainer", rows);
                }
                canSaveGeneratedResult = !!lastGeneratedSql;
                setSaveButtonEnabled(canSaveGeneratedResult);
                return;
            }

            if (!labels.length || !values.length) {
                if (chartWrap) chartWrap.style.display = "none";
                setPreviewMessage("לא נמצאו נתונים להצגה");
                return;
            }

            if (chartWrap) chartWrap.style.display = "block";
            renderChartInCanvas("previewChartCanvas", lastGeneratedChartType, labels, values, null);
            canSaveGeneratedResult = !!lastGeneratedSql;
            setSaveButtonEnabled(canSaveGeneratedResult);
        },
        error: function (xhr) {
            console.error("שגיאה ביצירת הגרף:", xhr);
            if (xhr?.responseJSON?.errorCode) {
                console.error("Dashboard validation error code:", xhr.responseJSON.errorCode);
            }
            clearPreview();
            previewContainer.style.display = "block";
            setPreviewMessage(xhr.responseJSON?.error || "ה-AI לא הצליח לייצר גרף.");
            canSaveGeneratedResult = false;
            setSaveButtonEnabled(false);
            showAppMessage("ה-AI לא הצליח לייצר גרף. שגיאה: " + (xhr.responseJSON?.error || xhr.statusText), { title: "שגיאה" });
        }
    });
};

// ==========================================
// פונקציות שמירה, טעינה וניהול של גרפים מה-DB
// ==========================================

// 1. שמירת הגרף שנוצר ב-AI לתוך בסיס הנתונים
window.saveGeneratedChart = function () {
    const titleInput = document.getElementById("newChartTitleInput");
    if (!titleInput || !titleInput.value.trim()) {
        showAppMessage("נא להזין שם עבור הגרף החדש");
        return;
    }

    const userRaw = sessionStorage.getItem("bluevisionUser");
    if (!canSaveGeneratedResult || !lastGeneratedSql) {
        showAppMessage("אין תוצאה תקינה לשמירה כרגע.");
        return;
    }
    const userObj = JSON.parse(userRaw);

    const saveData = {
        ChartTitle: titleInput.value.trim(),
        DashboardType: DASHBOARD_TYPE,
        UserID: userObj.userID || 1, // שימוש ב-userID באותיות קטנות כפי שמופיע ב-Login.js
        ChartType: lastGeneratedChartType,
        SqlLogic: lastGeneratedSql
    };

    $.ajax({
        url: server + "api/dashboard/save",
        type: "POST",
        contentType: "application/json",
        data: JSON.stringify(saveData),
        success: function (res) {
            showAppMessage("הגרף נשמר בהצלחה והתווסף לדשבורד!", { title: "בוצע" });
            closeEditDashboardModal();

            titleInput.value = "";
            document.getElementById("aiPromptInput").value = "";
            document.getElementById("previewChartContainer").style.display = "none";
            clearPreview();
            canSaveGeneratedResult = false;
            setSaveButtonEnabled(false);

            loadSavedCharts();
        },
        error: function (xhr) {
            showAppMessage("שגיאה בשמירת הגרף: " + (xhr.responseJSON?.error || xhr.statusText), { title: "שגיאה" });
        }
    });
};
// 2. טעינת כל הגרפים השמורים מה-SQL והזרקתם לגריד הראשי
function loadSavedCharts() {
    console.log("טוען גרפים שמורים מה-SQL עבור מסך המלאי...");

    // שינוי: קוראים לנתיב שמביא את הגרפים של ה-Inventory (בלי לסנן בשרת לפי מזהה המשתמש, כדי שכולם יראו)
    $.ajax({
        url: server + `api/dashboard/get-charts?dashboardType=${DASHBOARD_TYPE}`,
        type: "GET",
        success: function (charts) {
            savedChartsState = Array.isArray(charts) ? charts : [];
            savedChartsState.forEach((chart, index) => {
                chart.LayoutSize = getLayoutSize(chart);
                chart.DisplayOrder = Number(chart.displayOrder ?? chart.DisplayOrder ?? index) || index;
                chart.GridX = Number(chart.gridX ?? chart.GridX ?? 0) || 0;
                chart.GridY = Number(chart.gridY ?? chart.GridY ?? 0) || 0;
            });
            savedChartsState.sort((a, b) => (Number(a.DisplayOrder ?? 0) - Number(b.DisplayOrder ?? 0)) || (getChartId(a) - getChartId(b)));
            layoutSnapshot = cloneLayout(savedChartsState);
            renderSavedCharts();
        },
        error: function (xhr) {
            console.error("שגיאה בטעינת הדשבורד:", xhr);
        }
    });
}

// 3. הרצת השאילתה הקבועה של הכרטיס וציור הגרף הסופי שלו
function fetchAndRenderChartData(chart) {
    const chartID = chart.chartID || chart.ChartID;
    const chartType = normalizeVisualizationType({ chartType: chart.chartType || chart.ChartType });
    const sqlLogic = chart.sqlLogic || chart.SqlLogic;
    const resultType = chartType === "table" ? "table" : "single_series";

    $.ajax({
        url: server + "api/dashboard/generate",
        type: "POST",
        contentType: "application/json",
        data: JSON.stringify({
            Prompt: sqlLogic,
            VisualizationType: chartType,
            ResultType: resultType,
            DashboardType: DASHBOARD_TYPE
        }),
        success: function (data) {
            const rawLabels = data.labels || data.Labels || [];
            const rawValues = data.values || data.Values || [];
            const rawRows = data.rows || data.Rows || [];
            const serverType = chartType;
            savedVisualizationsState[chartID] = {
                id: chartID,
                title: chart.chartTitle || chart.ChartTitle || '',
                type: serverType,
                labels: Array.isArray(rawLabels) ? rawLabels : [],
                values: Array.isArray(rawValues) ? rawValues : [],
                rows: Array.isArray(rawRows) ? rawRows : [],
                sql: sqlLogic || ''
            };

            const canvas = document.getElementById(`canvas_${chartID}`);
            const canvasWrap = canvas?.parentElement;
            const tableId = `table_${chartID}`;
            const msgEl = document.getElementById(`msg_${chartID}`);

            if (msgEl) {
                msgEl.style.display = "none";
                msgEl.textContent = "";
            }

            if (savedChartInstances[`chart_${chartID}`]) {
                savedChartInstances[`chart_${chartID}`].destroy();
                delete savedChartInstances[`chart_${chartID}`];
            }

            const tableWrap = document.getElementById(tableId);
            if (tableWrap) {
                tableWrap.style.display = "none";
                tableWrap.innerHTML = "";
            }

            if (serverType === "table") {
                if (canvasWrap) canvasWrap.style.display = "none";
                renderHtmlTable(tableId, Array.isArray(rawRows) ? rawRows : []);
                return;
            }

            if (!Array.isArray(rawLabels) || !Array.isArray(rawValues) || rawLabels.length === 0 || rawValues.length === 0) {
                if (canvasWrap) canvasWrap.style.display = "none";
                if (msgEl) {
                    msgEl.style.display = "block";
                    msgEl.textContent = "לא נמצאו נתונים להצגה";
                }
                return;
            }

            if (canvasWrap) canvasWrap.style.display = "block";
            renderChartInCanvas(`canvas_${chartID}`, serverType, rawLabels, rawValues, `chart_${chartID}`);
        },
        error: function (xhr) {
            console.error(`שגיאה בטעינת נתונים עבור גרף ${chartID}:`, xhr);
            if (xhr?.responseJSON?.errorCode) {
                console.error("Dashboard validation error code:", xhr.responseJSON.errorCode);
            }
            const canvas = document.getElementById(`canvas_${chartID}`);
            const canvasWrap = canvas?.parentElement;
            if (canvasWrap) canvasWrap.style.display = "none";
            const msgEl = document.getElementById(`msg_${chartID}`);
            if (msgEl) {
                msgEl.style.display = "block";
                msgEl.textContent = xhr.responseJSON?.error || "שגיאה בטעינת נתונים";
            }
        }
    });
}

document.addEventListener('click', function (e) {
    const target = e.target;
    if (!(target instanceof Element)) return;
    const sizeOption = target.closest('.card-size-option');
    if (sizeOption) {
        e.preventDefault();
        e.stopPropagation();
        changeVisualizationSize(sizeOption.dataset.chartId, sizeOption.dataset.size);
        return;
    }
    const sizeTrigger = target.closest('[data-size-menu-trigger]');
    if (sizeTrigger) {
        e.preventDefault();
        e.stopPropagation();
        const sizeMenu = sizeTrigger.closest('.card-size-menu');
        const panel = sizeTrigger.closest('.card-menu-panel');
        document.querySelectorAll('.card-size-menu.open').forEach(menu => {
            if (menu !== sizeMenu) menu.classList.remove('open');
        });
        sizeMenu?.classList.toggle('open');
        adjustCardMenuDirection(panel);
        return;
    }
    if (!target.closest('.card-menu-wrap')) {
        closeAllCardMenus();
    }
});

window.addEventListener('resize', resizeDashboardCharts);

function escapeHtml(value) {
    return String(value)
        .replace(/&/g, "&amp;")
        .replace(/</g, "&lt;")
        .replace(/>/g, "&gt;")
        .replace(/\"/g, "&quot;")
        .replace(/'/g, "&#39;");
}

// 4. מחיקת גרף מהמערכת
window.deleteChart = function (chartID) {
    showAppConfirm({
        title: "מחיקת גרף",
        message: "האם למחוק את הגרף מהדשבורד?",
        confirmText: "מחק",
        cancelText: "ביטול",
        destructive: true,
        onConfirm: function () {
            $.ajax({
                url: server + `api/dashboard/delete/${chartID}`,
                type: "DELETE",
                success: function () {
                    showAppMessage("הגרף נמחק בהצלחה", { title: "בוצע" });
                    loadSavedCharts(); // טעינה מחדש של הדשבורד המעודכן
                },
                error: function () {
                    showAppMessage("מחיקת הגרף נכשלה.", { title: "שגיאה" });
                }
            });
        }
    });
};
