// נתוני מוק לדוגמה
const bomData = {
    'WanderB': [
        { sku: "PRP-11846", desc: "WBVT G2 Main harness", req: 1, stock: 15, status: "זמין" },
        { sku: "PRP-11847", desc: "WBVT G2 Front harness", req: 1, stock: 12, status: "זמין" },
        { sku: "PRP-11848", desc: "Avionic-GNSS Unit", req: 1, stock: 8, status: "זמין" },
        { sku: "PRP-11849", desc: "Flight Controller v3.2", req: 1, stock: 3, status: "מלאי נמוך" }
    ],
    'ThunderBird V': [
        { sku: "TBV-772", desc: "Main Motor assembly", req: 2, stock: 0, status: "אזל מהמלאי" }
    ]
};

let currentModel = 'WanderB';

window.initUavBOM = function () {
    renderBomTable(bomData[currentModel]);
};

window.switchBomType = function (type, btn) {
    // עדכון ויזואלי של הכפתורים
    const container = btn.closest('.main-type-toggle');
    container.querySelectorAll('.slider-btn').forEach(b => b.classList.remove('active'));
    btn.classList.add('active');

    // כאן תוכלי להוסיף לוגיקה לסינון בין רכיבי גוף לרכיבי כטב"ם מלא
    console.log("Switching to:", type);
};

window.filterBomByModel = function (model, btn) {
    // עדכון ויזואלי
    const container = btn.closest('.model-selection-toggle');
    container.querySelectorAll('.slider-btn').forEach(b => b.classList.remove('active'));
    btn.classList.add('active');

    currentModel = model;
    renderBomTable(bomData[model] || []);
};

function renderBomTable(data) {
    const tbody = document.getElementById("bom-table-body");
    if (!tbody) return;

    tbody.innerHTML = data.map(item => {
        // התאמת הצבעים ל-CSS החדש ב-styles.css
        let colorClass = "pill-green"; // ברירת מחדל: במלאי
        if (item.status === "מלאי נמוך") colorClass = "pill-yellow";
        if (item.status === "אזל מהמלאי") colorClass = "pill-red";

        return `
            <tr>
                <td class="col-sku">${item.sku}</td>
                <td>${item.desc}</td>
                <td>${item.req}</td>
                <td>${item.stock}</td>
                <td><span class="status-pill ${colorClass}">${item.status}</span></td>
            </tr>
        `;
    }).join('');
}

window.filterBomTable = function () {
    const search = document.getElementById("bomSearch").value.toLowerCase();
    const filtered = (bomData[currentModel] || []).filter(item =>
        item.sku.toLowerCase().includes(search) || item.desc.toLowerCase().includes(search)
    );
    renderBomTable(filtered);
};