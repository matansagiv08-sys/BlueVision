(() => {
    const DASHBOARD_TYPE = "Monthly";
    const DASHBOARD_NAME = "דוח חודשי";
    const state = {
        currentPreviewChartInstance: null,
        savedChartInstances: {},
        modalChartInstance: null,
        savedVisualizationsState: {},
        savedChartsState: [],
        layoutSnapshot: [],
        isArrangeMode: false,
        draggedChartId: null,
        lastGeneratedSql: "",
        lastGeneratedChartType: "bar",
        canSaveGeneratedResult: false
    };

    const DASHBOARD_COLUMNS = 3;
    const DASHBOARD_SIZE_OPTIONS = {
        small: { label: "קטן", columns: 1, rows: 1 },
        wide: { label: "רחב", columns: 2, rows: 1 },
        fullWidth: { label: "רוחב מלא", columns: 3, rows: 1 },
        large: { label: "גדול", columns: 2, rows: 2 },
        extraLarge: { label: "גדול מאוד", columns: 3, rows: 2 }
    };

    window.initMonthlyDashboard = function () {
        if (!window.Chart) {
            const script = document.createElement('script');
            script.src = 'https://cdn.jsdelivr.net/npm/chart.js';
            script.onload = function () {
                loadSavedCharts();
            };
            document.head.appendChild(script);
        } else {
            loadSavedCharts();
        }
    };

    function normalizeVisualizationType(data, fallback) {
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
        if (state.currentPreviewChartInstance) {
            state.currentPreviewChartInstance.destroy();
            state.currentPreviewChartInstance = null;
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

        if (mapKey && state.savedChartInstances[mapKey]) {
            state.savedChartInstances[mapKey].destroy();
            delete state.savedChartInstances[mapKey];
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
            state.savedChartInstances[mapKey] = instance;
        } else {
            state.currentPreviewChartInstance = instance;
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
        state.savedChartsState.forEach((chart, index) => {
            const item = layoutById.get(String(getChartId(chart)));
            chart.LayoutSize = normalizeLayoutSize(item?.LayoutSize || item?.layoutSize || chart.LayoutSize || chart.layoutSize || "small");
            chart.layoutSize = chart.LayoutSize;
            chart.DisplayOrder = Number(item?.DisplayOrder ?? item?.displayOrder ?? index) || 0;
            chart.GridX = Number(item?.GridX ?? item?.gridX ?? 0) || 0;
            chart.GridY = Number(item?.GridY ?? item?.gridY ?? 0) || 0;
        });
        state.savedChartsState.sort((a, b) => (Number(a.DisplayOrder ?? 0) - Number(b.DisplayOrder ?? 0)) || (getChartId(a) - getChartId(b)));
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
        if (grid) grid.classList.toggle("arrange-mode", state.isArrangeMode);
        if (gridShell) gridShell.classList.toggle("arrange-mode-shell", state.isArrangeMode);
        if (arrangeBanner) arrangeBanner.style.display = state.isArrangeMode ? "block" : "none";
        if (arrangeActions) arrangeActions.style.display = state.isArrangeMode ? "flex" : "none";
        if (arrangeBtn) arrangeBtn.classList.toggle("active", state.isArrangeMode);
    }

    function resizeDashboardCharts() {
        requestAnimationFrame(() => {
            Object.values(state.savedChartInstances).forEach(instance => {
                if (instance && typeof instance.resize === "function") instance.resize();
            });
        });
    }

    function renderVisualizationFromStateOrFetch(chart) {
        const chartID = getChartId(chart);
        const cardState = state.savedVisualizationsState[chartID];
        if (!cardState) {
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

        if (state.savedChartInstances[`chart_${chartID}`]) {
            state.savedChartInstances[`chart_${chartID}`].destroy();
            delete state.savedChartInstances[`chart_${chartID}`];
        }

        if (tableWrap) {
            tableWrap.style.display = "none";
            tableWrap.innerHTML = "";
        }

        if (cardState.type === "table") {
            if (canvasWrap) canvasWrap.style.display = "none";
            renderHtmlTable(`table_${chartID}`, cardState.rows || []);
            return;
        }

        if (!cardState.labels?.length || !cardState.values?.length) {
            if (canvasWrap) canvasWrap.style.display = "none";
            if (msgEl) {
                msgEl.style.display = "block";
                msgEl.textContent = "לא נמצאו נתונים להצגה";
            }
            return;
        }

        if (canvasWrap) canvasWrap.style.display = "block";
        renderChartInCanvas(`canvas_${chartID}`, cardState.type, cardState.labels, cardState.values, `chart_${chartID}`);
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
        const chart = state.savedChartsState.find(item => String(getChartId(item)) === String(chartID));
        if (!chart) return;
        const normalizedSize = normalizeLayoutSize(size);
        chart.LayoutSize = normalizedSize;
        chart.layoutSize = normalizedSize;
        closeAllCardMenus();
        renderDashboardCards(state.savedChartsState, false);
        if (!state.isArrangeMode) persistDashboardLayout(false);
    };

    window.enterArrangeDashboard = function () {
        if (state.isArrangeMode) {
            cancelArrangeDashboard();
            return;
        }
        if (window.matchMedia && window.matchMedia("(max-width: 760px)").matches) {
            showAppMessage("סידור בגרירה זמין במסך רחב. ניתן עדיין לשנות גדלים מתפריט הכרטיסים.");
            return;
        }
        state.layoutSnapshot = cloneLayout(state.savedChartsState);
        state.isArrangeMode = true;
        renderDashboardCards(state.savedChartsState, false);
    };

    window.cancelArrangeDashboard = function () {
        applyLayoutToCharts(state.layoutSnapshot);
        state.isArrangeMode = false;
        renderDashboardCards(state.savedChartsState, false);
    };

    window.resetDashboardLayout = function () {
        applyLayoutToCharts(createDefaultLayout(state.savedChartsState));
        renderDashboardCards(state.savedChartsState, false);
    };

    window.saveDashboardLayout = function () {
        persistDashboardLayout(true);
    };

    function buildLayoutPayload() {
        const packedLayout = calculatePackedLayout(state.savedChartsState);
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
                    const chart = state.savedChartsState.find(existing => String(getChartId(existing)) === String(item.ChartID));
                    if (!chart) return;
                    chart.LayoutSize = item.LayoutSize;
                    chart.layoutSize = item.LayoutSize;
                    chart.DisplayOrder = item.DisplayOrder;
                    chart.GridX = item.GridX;
                    chart.GridY = item.GridY;
                });
                state.layoutSnapshot = cloneLayout(state.savedChartsState);
                if (exitArrangeMode) {
                    state.isArrangeMode = false;
                    renderDashboardCards(state.savedChartsState, false);
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
                if (!state.isArrangeMode) {
                    e.preventDefault();
                    return;
                }
                state.draggedChartId = this.dataset.chartId;
                this.classList.add('dragging');
                e.dataTransfer.effectAllowed = 'move';
                e.dataTransfer.setData('text/plain', state.draggedChartId);
            });

            card.addEventListener('dragend', function () {
                this.classList.remove('dragging');
                state.draggedChartId = null;
                document.querySelectorAll('.dashboard-drop-target').forEach(el => el.classList.remove('dashboard-drop-target'));
                resizeDashboardCharts();
            });

            card.addEventListener('dragover', function (e) {
                if (!state.isArrangeMode || !state.draggedChartId || this.dataset.chartId === state.draggedChartId) return;
                e.preventDefault();
                this.classList.add('dashboard-drop-target');
            });

            card.addEventListener('dragleave', function () {
                this.classList.remove('dashboard-drop-target');
            });

            card.addEventListener('drop', function (e) {
                if (!state.isArrangeMode || !state.draggedChartId || this.dataset.chartId === state.draggedChartId) return;
                e.preventDefault();
                this.classList.remove('dashboard-drop-target');
                reorderCharts(state.draggedChartId, this.dataset.chartId);
            });
        });
    }

    function reorderCharts(sourceId, targetId) {
        const sourceIndex = state.savedChartsState.findIndex(chart => String(getChartId(chart)) === String(sourceId));
        const targetIndex = state.savedChartsState.findIndex(chart => String(getChartId(chart)) === String(targetId));
        if (sourceIndex < 0 || targetIndex < 0) return;
        const [moved] = state.savedChartsState.splice(sourceIndex, 1);
        state.savedChartsState.splice(targetIndex, 0, moved);
        state.savedChartsState.forEach((chart, index) => chart.DisplayOrder = index);
        renderDashboardCards(state.savedChartsState, false);
    };

    window.openVisualizationModal = function (chartID) {
        closeAllCardMenus();
        const cardState = state.savedVisualizationsState[chartID];
        if (!cardState) return;

        const modal = document.getElementById('dashboardCardModal');
        const title = document.getElementById('dashboardCardModalTitle');
        const chartWrap = document.getElementById('dashboardCardModalChartWrap');
        const tableWrap = document.getElementById('dashboardCardModalTableWrap');
        const queryWrap = document.getElementById('dashboardCardModalQueryWrap');

        if (!modal || !title || !chartWrap || !tableWrap || !queryWrap) return;

        title.textContent = cardState.title || 'תצוגה';
        queryWrap.style.display = 'none';
        queryWrap.textContent = '';

        if (state.modalChartInstance) {
            state.modalChartInstance.destroy();
            state.modalChartInstance = null;
        }

        if (cardState.type === 'table') {
            chartWrap.style.display = 'none';
            tableWrap.style.display = 'block';
            renderHtmlTable('dashboardCardModalTableWrap', cardState.rows || []);
        } else {
            tableWrap.style.display = 'none';
            tableWrap.innerHTML = '';
            chartWrap.style.display = 'block';
            const canvas = document.getElementById('dashboardCardModalCanvas');
            if (canvas) {
                const ctx = canvas.getContext('2d');
                state.modalChartInstance = new Chart(ctx, {
                    type: cardState.type,
                    data: {
                        labels: cardState.labels || [],
                        datasets: [{
                            label: 'נתונים',
                            data: cardState.values || [],
                            backgroundColor: cardState.type === 'pie'
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
                                display: cardState.type === 'pie',
                                position: 'bottom',
                                labels: cardState.type === 'pie' ? {
                                    boxWidth: 12,
                                    padding: 12
                                } : undefined
                            }
                        },
                        scales: cardState.type !== 'pie' ? { y: { beginAtZero: true } } : {}
                    }
                });
            }
        }

        modal.style.display = 'flex';
    };

    window.showVisualizationQuery = function (chartID) {
        closeAllCardMenus();
        const cardState = state.savedVisualizationsState[chartID];
        if (!cardState) return;

        const modal = document.getElementById('dashboardCardModal');
        const title = document.getElementById('dashboardCardModalTitle');
        const chartWrap = document.getElementById('dashboardCardModalChartWrap');
        const tableWrap = document.getElementById('dashboardCardModalTableWrap');
        const queryWrap = document.getElementById('dashboardCardModalQueryWrap');

        if (!modal || !title || !chartWrap || !tableWrap || !queryWrap) return;

        title.textContent = `שאילתה: ${cardState.title || ''}`;
        chartWrap.style.display = 'none';
        tableWrap.style.display = 'none';
        tableWrap.innerHTML = '';
        queryWrap.style.display = 'block';
        queryWrap.textContent = cardState.sql || 'לא קיימת שאילתה להצגה';

        if (state.modalChartInstance) {
            state.modalChartInstance.destroy();
            state.modalChartInstance = null;
        }

        modal.style.display = 'flex';
    };

    window.exportVisualizationToExcel = async function (chartID) {
        closeAllCardMenus();
        const cardState = state.savedVisualizationsState[chartID];
        console.info("[MonthlyDashboard] Export block requested", { chartID, cardState });
        if (!cardState) {
            showAppMessage("הנתונים עדיין לא נטענו. נסו שוב בעוד רגע.");
            return;
        }

        if (!window.DashboardExcelExport) {
            console.error("[MonthlyDashboard] DashboardExcelExport helper is not loaded");
            showAppMessage("רכיב הייצוא לא נטען. רעננו את הדף ונסו שוב.", { title: "שגיאה" });
            return;
        }

        try {
            const result = await window.DashboardExcelExport.exportBlock(cardState);
            if (!result.ok) showAppMessage(result.message || "לא נמצאו נתונים לייצוא");
        } catch (error) {
            console.error("[MonthlyDashboard] Block Excel export failed", { chartID, cardState, error });
            showAppMessage("ייצוא לאקסל נכשל. נסו שוב מאוחר יותר.", { title: "שגיאה" });
        }
    };

    window.exportDashboardToExcel = async function () {
        const blocks = state.savedChartsState.map(chart => {
            const chartID = getChartId(chart);
            return state.savedVisualizationsState[chartID] || {
                id: chartID,
                title: chart.chartTitle || chart.ChartTitle || "תצוגה",
                type: normalizeVisualizationType({ chartType: chart.chartType || chart.ChartType })
            };
        });
        console.info("[MonthlyDashboard] Export full dashboard requested", { dashboardName: DASHBOARD_NAME, blocks });

        if (!window.DashboardExcelExport) {
            console.error("[MonthlyDashboard] DashboardExcelExport helper is not loaded");
            showAppMessage("רכיב הייצוא לא נטען. רעננו את הדף ונסו שוב.", { title: "שגיאה" });
            return;
        }

        try {
            const result = await window.DashboardExcelExport.exportDashboard(blocks, DASHBOARD_NAME);
            if (!result.ok) showAppMessage(result.message || "אין נתונים לייצוא");
        } catch (error) {
            console.error("[MonthlyDashboard] Dashboard Excel export failed", { dashboardName: DASHBOARD_NAME, blocks, error });
            showAppMessage("ייצוא הדוח לאקסל נכשל. נסו שוב מאוחר יותר.", { title: "שגיאה" });
        }
    };

    window.closeDashboardCardModal = function () {
        const modal = document.getElementById('dashboardCardModal');
        if (modal) modal.style.display = 'none';
        if (state.modalChartInstance) {
            state.modalChartInstance.destroy();
            state.modalChartInstance = null;
        }
    };

    window.openEditDashboardModal = function () {
        const modal = document.getElementById("dashboardEditModal");
        if (modal) {
            modal.style.display = "flex";
            state.canSaveGeneratedResult = false;
            setSaveButtonEnabled(false);
        }
    };

    window.closeEditDashboardModal = function () {
        const modal = document.getElementById("dashboardEditModal");
        if (modal) {
            modal.style.display = "none";
        }
    };

    window.generateAiChart = function () {
        const promptInput = document.getElementById("aiPromptInput");
        const previewContainer = document.getElementById("previewChartContainer");

        if (!promptInput || !promptInput.value.trim()) {
            showAppMessage("נא להזין שאלה או בקשה עבור ה-AI");
            return;
        }

        $.ajax({
            url: server + "api/dashboard/generate",
            type: "POST",
            contentType: "application/json",
            data: JSON.stringify({
                Prompt: promptInput.value.trim(),
                DashboardType: DASHBOARD_TYPE
            }),
            success: function (data) {
                clearPreview();

                state.lastGeneratedSql = data.sqlQuery;
                state.lastGeneratedChartType = normalizeVisualizationType(data);
                state.canSaveGeneratedResult = false;
                setSaveButtonEnabled(false);
                previewContainer.style.display = "block";

                const labels = Array.isArray(data.labels) ? data.labels : [];
                const values = Array.isArray(data.values) ? data.values : [];
                const rows = Array.isArray(data.rows) ? data.rows : [];
                const previewCanvas = document.getElementById('previewChartCanvas');
                const previewTable = document.getElementById('previewTableContainer');
                const chartWrap = previewCanvas?.parentElement;

                if (state.lastGeneratedChartType === "table") {
                    if (chartWrap) chartWrap.style.display = "none";
                    if (previewTable) renderHtmlTable("previewTableContainer", rows);
                    state.canSaveGeneratedResult = !!state.lastGeneratedSql;
                    setSaveButtonEnabled(state.canSaveGeneratedResult);
                    return;
                }

                if (!labels.length || !values.length) {
                    if (chartWrap) chartWrap.style.display = "none";
                    setPreviewMessage("לא נמצאו נתונים להצגה");
                    return;
                }

                if (chartWrap) chartWrap.style.display = "block";
                renderChartInCanvas("previewChartCanvas", state.lastGeneratedChartType, labels, values, null);
                state.canSaveGeneratedResult = !!state.lastGeneratedSql;
                setSaveButtonEnabled(state.canSaveGeneratedResult);
            },
            error: function (xhr) {
                clearPreview();
                previewContainer.style.display = "block";
                setPreviewMessage(xhr.responseJSON?.error || "ה-AI לא הצליח לייצר גרף.");
                state.canSaveGeneratedResult = false;
                setSaveButtonEnabled(false);
                showAppMessage("ה-AI לא הצליח לייצר גרף. שגיאה: " + (xhr.responseJSON?.error || xhr.statusText), { title: "שגיאה" });
            }
        });
    };

    window.saveGeneratedChart = function () {
        const titleInput = document.getElementById("newChartTitleInput");
        if (!titleInput || !titleInput.value.trim()) {
            showAppMessage("נא להזין שם עבור הגרף החדש");
            return;
        }

        if (!state.canSaveGeneratedResult || !state.lastGeneratedSql) {
            showAppMessage("אין תוצאה תקינה לשמירה כרגע.");
            return;
        }

        const userRaw = sessionStorage.getItem("bluevisionUser");
        if (!userRaw) {
            showAppMessage("שגיאה: משתמש לא מחובר למערכת", { title: "שגיאה" });
            return;
        }
        const userObj = JSON.parse(userRaw);

        const saveData = {
            ChartTitle: titleInput.value.trim(),
            DashboardType: DASHBOARD_TYPE,
            UserID: userObj.userID || 1,
            ChartType: state.lastGeneratedChartType,
            SqlLogic: state.lastGeneratedSql
        };

        $.ajax({
            url: server + "api/dashboard/save",
            type: "POST",
            contentType: "application/json",
            data: JSON.stringify(saveData),
            success: function () {
                showAppMessage("הגרף נשמר בהצלחה והתווסף לדשבורד!", { title: "בוצע" });
                closeEditDashboardModal();

                titleInput.value = "";
                document.getElementById("aiPromptInput").value = "";
                document.getElementById("previewChartContainer").style.display = "none";
                clearPreview();
                state.canSaveGeneratedResult = false;
                setSaveButtonEnabled(false);

                loadSavedCharts();
            },
            error: function (xhr) {
                showAppMessage("שגיאה בשמירת הגרף: " + (xhr.responseJSON?.error || xhr.statusText), { title: "שגיאה" });
            }
        });
    };

    function loadSavedCharts() {
        $.ajax({
            url: server + `api/dashboard/get-charts?dashboardType=${DASHBOARD_TYPE}`,
            type: "GET",
            success: function (charts) {
                state.savedChartsState = Array.isArray(charts) ? charts : [];
                state.savedChartsState.forEach((chart, index) => {
                    chart.LayoutSize = getLayoutSize(chart);
                    chart.layoutSize = chart.LayoutSize;
                    chart.DisplayOrder = Number(chart.displayOrder ?? chart.DisplayOrder ?? index) || index;
                    chart.GridX = Number(chart.gridX ?? chart.GridX ?? 0) || 0;
                    chart.GridY = Number(chart.gridY ?? chart.GridY ?? 0) || 0;
                });
                state.savedChartsState.sort((a, b) => (Number(a.DisplayOrder ?? 0) - Number(b.DisplayOrder ?? 0)) || (getChartId(a) - getChartId(b)));
                state.layoutSnapshot = cloneLayout(state.savedChartsState);
                renderDashboardCards(state.savedChartsState, true);
            },
            error: function () {
                state.savedChartsState = [];
                renderDashboardCards([], true);
            }
        });
    }

    function renderDashboardCards(charts, resetVisualizationState = false) {
        const grid = document.getElementById("chartsGrid");
        const manageList = document.getElementById("manageChartsList");
        if (!grid) return;

        grid.innerHTML = "";
        if (manageList) manageList.innerHTML = "";
        if (resetVisualizationState) state.savedVisualizationsState = {};

        if (!Array.isArray(charts) || charts.length === 0) {
            grid.innerHTML = `
                <div class="dashboard-empty-state">
                    <h3 style="margin-bottom: 8px; color: #0c2340;">לא נוספו עדיין דוחות או גרפים לדוח החודשי</h3>
                    <p style="margin-bottom: 14px;">לחצו על עריכת דוחות וגרפים כדי להוסיף את הוויזואליזציה הראשונה.</p>
                    <button class="standard-button btn-primary-action" onclick="openEditDashboardModal()" style="background-color: #0c2340; color: white; padding: 8px 16px; border-radius: 6px; border: none; cursor: pointer; font-weight: 600;">
                        הוסף דוח או גרף
                    </button>
                </div>`;
            updateArrangeControls();
            return;
        }

        calculatePackedLayout(charts).forEach(item => {
            const chart = item.chart;
            const chartID = getChartId(chart);
            const chartIDArg = JSON.stringify(chartID);
            const chartTitle = chart.chartTitle || chart.ChartTitle || "תצוגה";
            const chartType = (chart.chartType || chart.ChartType || "bar").toLowerCase();
            chart.DisplayOrder = item.index;
            chart.GridX = item.x;
            chart.GridY = item.y;

            const cardHtml = `
                <div class="chart-card dashboard-grid-card size-${item.size}" id="chartCard_${chartID}" draggable="${state.isArrangeMode}" data-chart-id="${chartID}" data-size="${item.size}" style="grid-column: span ${item.columns}; grid-row: span ${item.rows};">
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

                state.savedVisualizationsState[chartID] = {
                    id: chartID,
                    title: chart.chartTitle || chart.ChartTitle || '',
                    type: chartType,
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

                if (state.savedChartInstances[`chart_${chartID}`]) {
                    state.savedChartInstances[`chart_${chartID}`].destroy();
                    delete state.savedChartInstances[`chart_${chartID}`];
                }

                const tableWrap = document.getElementById(tableId);
                if (tableWrap) {
                    tableWrap.style.display = "none";
                    tableWrap.innerHTML = "";
                }

                if (chartType === "table") {
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
                renderChartInCanvas(`canvas_${chartID}`, chartType, rawLabels, rawValues, `chart_${chartID}`);
            },
            error: function (xhr) {
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
                        loadSavedCharts();
                    },
                    error: function () {
                        showAppMessage("מחיקת הגרף נכשלה.", { title: "שגיאה" });
                    }
                });
            }
        });
    };

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
})();
