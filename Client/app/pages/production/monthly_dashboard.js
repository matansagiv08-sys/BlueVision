(() => {
    const DASHBOARD_TYPE = "Monthly";
    const state = {
        currentPreviewChartInstance: null,
        savedChartInstances: {},
        modalChartInstance: null,
        savedVisualizationsState: {},
        lastGeneratedSql: "",
        lastGeneratedChartType: "bar",
        canSaveGeneratedResult: false
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
                scales: chartType !== 'pie' ? { y: { beginAtZero: true } } : {}
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
        });
    }

    window.toggleCardMenu = function (chartID) {
        const panel = document.getElementById(`menu_${chartID}`);
        if (!panel) return;
        const next = panel.style.display !== 'block';
        closeAllCardMenus();
        panel.style.display = next ? 'block' : 'none';
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
            alert("נא להזין שאלה או בקשה עבור ה-AI");
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
                alert("ה-AI לא הצליח לייצר גרף. שגיאה: " + (xhr.responseJSON?.error || xhr.statusText));
            }
        });
    };

    window.saveGeneratedChart = function () {
        const titleInput = document.getElementById("newChartTitleInput");
        if (!titleInput || !titleInput.value.trim()) {
            alert("נא להזין שם עבור הגרף החדש");
            return;
        }

        if (!state.canSaveGeneratedResult || !state.lastGeneratedSql) {
            alert("אין תוצאה תקינה לשמירה כרגע.");
            return;
        }

        const userRaw = sessionStorage.getItem("bluevisionUser");
        if (!userRaw) {
            alert("שגיאה: משתמש לא מחובר למערכת");
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
                alert("הגרף נשמר בהצלחה והתווסף לדשבורד!");
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
                alert("שגיאה בשמירת הגרף: " + (xhr.responseJSON?.error || xhr.statusText));
            }
        });
    };

    function loadSavedCharts() {
        $.ajax({
            url: server + `api/dashboard/get-charts?dashboardType=${DASHBOARD_TYPE}`,
            type: "GET",
            success: function (charts) {
                renderDashboardCards(Array.isArray(charts) ? charts : []);
            },
            error: function () {
                renderDashboardCards([]);
            }
        });
    }

    function renderDashboardCards(charts) {
        const grid = document.getElementById("chartsGrid");
        const manageList = document.getElementById("manageChartsList");
        if (!grid) return;

        grid.innerHTML = "";
        if (manageList) manageList.innerHTML = "";
        state.savedVisualizationsState = {};

        if (!Array.isArray(charts) || charts.length === 0) {
            grid.innerHTML = `
                <div style="grid-column: 1/-1; text-align: center; padding: 40px; color: #64748b; background: #ffffff; border: 1px dashed #cbd5e1; border-radius: 12px;">
                    <h3 style="margin-bottom: 8px; color: #0c2340;">לא נוספו עדיין דוחות או גרפים לדוח החודשי</h3>
                    <p style="margin-bottom: 14px;">לחצו על עריכת דוחות וגרפים כדי להוסיף את הוויזואליזציה הראשונה.</p>
                    <button class="standard-button btn-primary-action" onclick="openEditDashboardModal()" style="background-color: #0c2340; color: white; padding: 8px 16px; border-radius: 6px; border: none; cursor: pointer; font-weight: 600;">
                        הוסף דוח או גרף
                    </button>
                </div>`;
            return;
        }

        charts.forEach(chart => {
            const chartID = chart.chartID || chart.ChartID;
            const chartTitle = chart.chartTitle || chart.ChartTitle;
            const chartType = (chart.chartType || chart.ChartType || "bar").toLowerCase();

            const cardHtml = `
                <div class="chart-card" id="chartCard_${chartID}" style="background: white; border-radius: 8px; padding: 15px; box-shadow: 0 1px 3px rgba(0,0,0,0.1);">
                    <div class="chart-card-header" style="margin-bottom: 10px; font-weight: bold; color: #0c2340; position:relative; display:flex; align-items:center; gap:6px;">
                        <span class="chart-card-title">${chartTitle}</span>
                        <div class="card-menu-wrap" style="margin-inline-start:auto; position:relative;">
                            <button class="card-menu-btn" onclick="toggleCardMenu(${chartID})" title="פעולות">⋯</button>
                            <div class="card-menu-panel" id="menu_${chartID}" style="display:none;">
                                <button onclick="openVisualizationModal(${chartID})">הגדל תצוגה</button>
                                <button onclick="showVisualizationQuery(${chartID})">הצג שאילתה</button>
                                <button onclick="deleteChart(${chartID})">מחק</button>
                            </div>
                        </div>
                    </div>
                    <div class="chart-wrapper" style="position: relative; height: 250px; width: 100%;">
                        <canvas id="canvas_${chartID}"></canvas>
                    </div>
                    <div id="table_${chartID}" class="dashboard-table-wrap" style="display:none;"></div>
                    <div id="msg_${chartID}" class="dashboard-preview-message" style="display:none;"></div>
                </div>`;
            grid.insertAdjacentHTML('beforeend', cardHtml);

            if (manageList) {
                const itemHtml = `
                    <div class="manage-chart-item" id="manageItem_${chartID}" style="display: flex; justify-content: space-between; align-items: center; padding: 8px; background: white; border: 1px solid #e2e8f0; border-radius: 6px;">
                        <span>📊 ${chartTitle} (${chartType})</span>
                        <button class="btn-delete-chart" onclick="deleteChart(${chartID})" style="background: none; border: none; cursor: pointer; font-size: 16px;">🗑️</button>
                    </div>`;
                manageList.insertAdjacentHTML('beforeend', itemHtml);
            }

            fetchAndRenderChartData(chart);
        });
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
        if (!confirm("האם אתה בטוח שברצונך למחוק גרף זה לצמיתות מהדשבורד?")) return;

        $.ajax({
            url: server + `api/dashboard/delete/${chartID}`,
            type: "DELETE",
            success: function () {
                alert("הגרף נמחק בהצלחה");
                loadSavedCharts();
            },
            error: function () {
                alert("מחיקת הגרף נכשלה.");
            }
        });
    };

    document.addEventListener('click', function (e) {
        const target = e.target;
        if (!(target instanceof Element)) return;
        if (!target.closest('.card-menu-wrap')) {
            closeAllCardMenus();
        }
    });

    function escapeHtml(value) {
        return String(value)
            .replace(/&/g, "&amp;")
            .replace(/</g, "&lt;")
            .replace(/>/g, "&gt;")
            .replace(/\"/g, "&quot;")
            .replace(/'/g, "&#39;");
    }
})();
