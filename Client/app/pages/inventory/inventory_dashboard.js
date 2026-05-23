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
let lastGeneratedSql = "";
let lastGeneratedChartType = "";

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

            lastGeneratedSql = data.sqlQuery;
            lastGeneratedChartType = data.chartType;
            previewContainer.style.display = "block";

            if (currentPreviewChartInstance) {
                currentPreviewChartInstance.destroy();
            }

            const ctx = document.getElementById('previewChartCanvas').getContext('2d');
            currentPreviewChartInstance = new Chart(ctx, {
                type: data.chartType,
                data: {
                    labels: data.labels,
                    datasets: [{
                        label: 'כמות / נתונים',
                        data: data.values,
                        backgroundColor: data.chartType === 'pie'
                            ? ['#0c2340', '#1d4ed8', '#3b82f6', '#60a5fa', '#93c5fd', '#cbd5e1']
                            : '#0c2340',
                        borderColor: '#0c2340',
                        borderWidth: 1
                    }]
                },
                options: {
                    responsive: true,
                    maintainAspectRatio: false,
                    scales: data.chartType !== 'pie' ? { y: { beginAtZero: true } } : {}
                }
            });
        },
        error: function (xhr) {
            console.error("שגיאה ביצירת הגרף:", xhr);
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
                        <div class="chart-card-header" style="margin-bottom: 10px; font-weight: bold; color: #0c2340;">
                            <span class="chart-card-title">${chartTitle}</span>
                        </div>
                        <div class="chart-wrapper" style="position: relative; height: 250px; width: 100%;">
                            <canvas id="canvas_${chartID}"></canvas>
                        </div>
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
    const chartTitle = chart.chartTitle || chart.ChartTitle;
    const chartType = (chart.chartType || chart.ChartType).toLowerCase();
    const sqlLogic = chart.sqlLogic || chart.SqlLogic;

    $.ajax({
        url: server + "api/dashboard/generate",
        type: "POST",
        contentType: "application/json",
        data: JSON.stringify({ Prompt: sqlLogic }),
        success: function (data) {
            // מיפוי גמיש למקרה שה-C# מחזיר אותיות גדולות (Labels/Values) או קטנות (labels/values)
            const rawLabels = data.labels || data.Labels || [];
            const rawValues = data.values || data.Values || [];

            const ctx = document.getElementById(`canvas_${chartID}`).getContext('2d');
            new Chart(ctx, {
                type: chartType,
                data: {
                    labels: rawLabels,
                    datasets: [{
                        label: 'נתוני מלאי',
                        data: rawValues,
                        backgroundColor: chartType === 'pie'
                            ? ['#0c2340', '#1d4ed8', '#3b82f6', '#60a5fa', '#93c5fd', '#cbd5e1']
                            : '#0c2340',
                        borderWidth: 1
                    }]
                },
                options: {
                    responsive: true,
                    maintainAspectRatio: false,
                    scales: chartType !== 'pie' ? { y: { beginAtZero: true } } : {}
                }
            });
        },
        error: function (xhr) {
            console.error(`שגיאה בטעינת נתונים עבור גרף ${chartID}:`, xhr);
        }
    });
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