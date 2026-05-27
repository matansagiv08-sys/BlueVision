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
let lastGeneratedSql = "";
let lastGeneratedChartType = "bar";
let canSaveGeneratedResult = false;

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
            scales: chartType !== 'pie' ? { y: { beginAtZero: true } } : {}
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
        alert("נא להזין שאלה או בקשה עבור ה-AI");
        return;
    }

    // משיכה מדויקת מתוך ה-sessionStorage שלכם
    const userRaw = sessionStorage.getItem("bluevisionUser");
    if (!userRaw) {
        alert("שגיאה: משתמש לא מחובר למערכת");
        return;
    }
    const userObj = JSON.parse(userRaw);

    console.log("שולח בקשה ל-AI עבור: " + promptInput.value);

    $.ajax({
        url: server + "api/dashboard/generate",
        type: "POST",
        contentType: "application/json",
        data: JSON.stringify({
            Prompt: promptInput.value.trim()
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
            alert("ה-AI לא הצליח לייצר גרף. שגיאה: " + (xhr.responseJSON?.error || xhr.statusText));
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
        alert("נא להזין שם עבור הגרף החדש");
        return;
    }

    const userRaw = sessionStorage.getItem("bluevisionUser");
    if (!canSaveGeneratedResult || !lastGeneratedSql) {
        alert("אין תוצאה תקינה לשמירה כרגע.");
        return;
    }
    const userObj = JSON.parse(userRaw);

    const saveData = {
        ChartTitle: titleInput.value.trim(),
        DashboardType: "Inventory",
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
            alert("הגרף נשמר בהצלחה והתווסף לדשבורד!");
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
            alert("שגיאה בשמירת הגרף: " + (xhr.responseJSON?.error || xhr.statusText));
        }
    });
};
// 2. טעינת כל הגרפים השמורים מה-SQL והזרקתם לגריד הראשי
function loadSavedCharts() {
    console.log("טוען גרפים שמורים מה-SQL עבור מסך המלאי...");

    // שינוי: קוראים לנתיב שמביא את הגרפים של ה-Inventory (בלי לסנן בשרת לפי מזהה המשתמש, כדי שכולם יראו)
    $.ajax({
        url: server + `api/dashboard/get-charts?dashboardType=Inventory`,
        type: "GET",
        success: function (charts) {
            const grid = document.getElementById("chartsGrid");
            const manageList = document.getElementById("manageChartsList");

            if (!grid) return;
            grid.innerHTML = "";
            if (manageList) manageList.innerHTML = "";

            if (!charts || charts.length === 0) {
                grid.innerHTML = `<div style="grid-column: 1/-1; text-align: center; padding: 40px; color: #64748b;">
                                    <h3>הדשבורד שלך עדיין ריק 📊</h3>
                                    <p>לחצי על כפתור העריכה למעלה כדי לייצר את הגרף הראשון שלך בעזרת AI!</p>
                                  </div>`;
                return;
            }

            charts.forEach(chart => {
                // תיקון קריאתיות לשדות - תמיכה גם באותיות גדולות וגם בקטנות מהשרת
                const chartID = chart.chartID || chart.ChartID;
                const chartTitle = chart.chartTitle || chart.ChartTitle;
                const chartType = (chart.chartType || chart.ChartType).toLowerCase();

                const cardHtml = `
                    <div class="chart-card" id="chartCard_${chartID}" style="background: white; border-radius: 8px; padding: 15px; box-shadow: 0 1px 3px rgba(0,0,0,0.1);">
                        <div class="chart-card-header" style="margin-bottom: 10px; font-weight: bold; color: #0c2340; position:relative;">
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
            ResultType: resultType
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

// 4. מחיקת גרף מהמערכת
window.deleteChart = function (chartID) {
    if (!confirm("האם אתה בטוח שברצונך למחוק גרף זה לצמיתות מהדשבורד?")) return;

    $.ajax({
        url: server + `api/dashboard/delete/${chartID}`,
        type: "DELETE",
        success: function () {
            alert("הגרף נמחק בהצלחה");
            loadSavedCharts(); // טעינה מחדש של הדשבורד המעודכן
        },
        error: function (xhr) {
            alert("מחיקת הגרף נכשלה.");
        }
    });
};
