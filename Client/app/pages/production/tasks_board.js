// 1. הגדרת התחנות
const tasksBoardStations = ["הכנת תבניות", "ליווח", "סגירה", "חליצה", "פינישים", "QC"];

// 2. פונקציית האתחול (נקראת מה-Router ב-app.js)
window.initTasksBoard = function () {
    // משתמשים בנתונים שהגדרת ב-app.js (mockData) או בנתונים מקומיים
    const dataToRender = (typeof mockData !== 'undefined') ? mockData : [
        { orderNum: "20936", partNum: "WB-CW-001", description: "Central wing", aircraftType: "WB", quantity: 2, progress: 25 },
        { orderNum: "1002", partNum: "PUMA-TAIL", description: "Tail Assembly", aircraftType: "Puma", quantity: 1, progress: 10 }
    ];

    renderTasksBoard(dataToRender);
};

// 3. פונקציית הרינדור המרכזית
function renderTasksBoard(data) {
    const container = document.getElementById("tasks-board-container");
    if (!container) return;

    let html = "";
    const grouped = data.reduce((acc, item) => {
        if (!acc[item.aircraftType]) acc[item.aircraftType] = [];
        acc[item.aircraftType].push(item);
        return acc;
    }, {});

    for (const type in grouped) {
        html += `
            <section class="aircraft-section" style="margin-bottom:30px;">
                <h2 style="margin-bottom:15px; border-right:4px solid #0c2340; padding-right:10px;">${type}</h2>
                <div class="table-container">
                    <table class="generic-data-table">
                        <thead>
                            <tr>
                                <th>פק"ע</th>
                                <th>מק"ט</th>
                                <th>תיאור</th>
                                <th>כמות</th>
                                ${tasksBoardStations.map(s => `<th>${s}</th>`).join('')}
                                <th>התקדמות</th>
                            </tr>
                        </thead>
                        <tbody>
                            ${grouped[type].map(item => {
            // כאן התיקון: אנחנו משתמשים ב-item ושולחים אותו לפונקציה
            const itemJson = JSON.stringify(item).replace(/'/g, "&apos;");
            return `
                                <tr onclick='window.showTaskDetails(${itemJson})' style="cursor:pointer;">
                                    <td style="font-weight:bold;">${item.orderNum}</td>
                                    <td>${item.partNum}</td>
                                    <td>${item.description}</td>
                                    <td>${item.quantity}</td>
                                    ${tasksBoardStations.map(s => `
                                        <td>
                                            <button class="status-pill gray" onclick="event.stopPropagation(); window.openStatusModal('${item.orderNum}', '${s}')">
                                                טרם
                                            </button>
                                        </td>
                                    `).join('')}
                                    <td>${item.progress}%</td>
                                </tr>`;
        }).join('')}
                        </tbody>
                    </table>
                </div>
            </section>
        `;
    }
    container.innerHTML = html;
}

window.openStatusModal = function (orderNum, station) {
    // שינוי כאן: מ-statusModal ל-genericModal
    const modal = document.getElementById("genericModal");
    const body = document.getElementById("modalBody");
    const submitBtn = document.getElementById("modalSubmitBtn");
    const title = document.getElementById("modalTitle");

    if (title) title.innerText = "עדכון סטטוס תחנה";

    body.innerHTML = `
        <div style="grid-column: span 2; background: #f8fafc; padding: 15px; border-radius: 8px;">
            <strong>פק"ע: ${orderNum}</strong> | <strong>תחנה: ${station}</strong>
        </div>
        <div class="info-item" style="grid-column: span 2;">
            <label>בחר סטטוס</label>
            <select id="new-status-select" style="width:100%; padding:10px;">
                <option value="" disabled selected>בחר...</option>
                <option>בוצע</option>
                <option>בתהליך</option>
                <option>עצור</option>
            </select>
        </div>
    `;

    modal.style.display = "flex";
    if (submitBtn) submitBtn.disabled = true;

    document.getElementById("new-status-select").addEventListener('change', () => {
        if (submitBtn) submitBtn.disabled = false;
    });
};

window.closeStatusModal = function () {
    const modal = document.getElementById("statusModal");
    if (modal) modal.style.display = "none";
};

// פונקציה חדשה שתיפתח בלחיצה על השורה
window.showTaskDetails = function (task) {
    const modal = document.getElementById("genericModal"); // וודאי שזה השם ב-HTML
    const body = document.getElementById("modalBody");
    const submitBtn = document.getElementById("modalSubmitBtn");

    if (!modal || !body) return;

    body.innerHTML = `
        <div class="info-item">
            <label>מספר פק"ע</label>
            <input type="text" value="${task.orderNum}" readonly style="background:#f1f5f9;">
        </div>
        <div class="info-item">
            <label>מק"ט</label>
            <input type="text" value="${task.partNum}">
        </div>
        <div class="info-item" style="grid-column: span 2;">
            <label>תיאור חלק</label>
            <textarea style="width:100%; height:60px;">${task.description}</textarea>
        </div>
    `;

    modal.style.display = "flex";
    if (submitBtn) submitBtn.disabled = true;
};

window.renderTasksBoard = renderTasksBoard;