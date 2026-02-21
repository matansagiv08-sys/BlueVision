// נתונים גלובליים
let fullInventoryData = [
    { sku: "WB-5501", desc: "מנוע ראשי כטב״ם", platform: "WB", parent: "מכלול כנף", qty: 12 },
    { sku: "TBV-202", desc: "חיישן גובה לייזר", platform: "TBV", parent: "אווירונאוטיקה", qty: 4 },
    { sku: "PM-900", desc: "צלע גוף מרכזית", platform: "Puma", parent: "גוף המטוס", qty: 30 },
    { sku: "WB-102", desc: "סוללה 6S 10000mAh", platform: "WB", parent: "מערכת חשמל", qty: 8 }
];

window.initAllInventory = function () {
    console.log("Loading All Inventory...");
    renderInventoryTable(fullInventoryData);
};

function renderInventoryTable(data) {
    const tbody = document.getElementById("inventory-table-body");
    if (!tbody) return;

    tbody.innerHTML = data.map(item => `
        <tr onclick='window.showItemDetails(${JSON.stringify(item)})'>
            <td style="font-weight:700; color:#0c2340;">${item.sku}</td>
            <td>${item.desc}</td>
            <td><span class="badge badge-blue">${item.platform}</span></td>
            <td>${item.parent}</td>
            <td><strong>${item.qty}</strong> יח'</td>
        </tr>
    `).join('');
}

// פונקציית סינון
window.filterInventory = function () {
    const searchTerm = document.getElementById("inventorySearch").value.toLowerCase();
    const platform = document.getElementById("platformFilter").value;

    const filtered = fullInventoryData.filter(item => {
        const matchesSearch = item.sku.toLowerCase().includes(searchTerm) ||
            item.desc.toLowerCase().includes(searchTerm);
        const matchesPlatform = platform === "all" || item.platform === platform;
        return matchesSearch && matchesPlatform;
    });
    renderInventoryTable(filtered);
};

// פונקציית מיון
window.sortInventory = function () {
    fullInventoryData.sort((a, b) => a.qty - b.qty);
    renderInventoryTable(fullInventoryData);
};

// ניהול מודאל
window.showItemDetails = function (item) {
    const modal = document.getElementById("genericModal");
    const body = document.getElementById("modalBody");
    const submitBtn = document.getElementById("modalSubmitBtn");

    // חיפוש כפתור האישור בתוך הפוטר של המודאל
    const submitBtn = modal.querySelector(".btn-save");

    if (!modal || !body) {
        console.error("Modal elements not found!");
        return;
    }

    // --- שינוי: הגדרת כפתור האישור כ"כבוי" (אפור) בפתיחה ---
    if (submitBtn) submitBtn.disabled = true;

    // --- שינוי: הפיכת השדות ל-Input/Select במקום טקסט רגיל ---
    body.innerHTML = `
        <div class="info-item">
            <label>מק״ט פריט</label>
            <input type="text" value="${item.sku}" readonly style="background:#f1f5f9;">
        </div>
        <div class="info-item">
            <label>פלטפורמה</label>
            <select id="edit-platform">
                <option ${item.platform === 'WB' ? 'selected' : ''}>WB</option>
                <option ${item.platform === 'TBV' ? 'selected' : ''}>TBV</option>
                <option ${item.platform === 'Puma' ? 'selected' : ''}>Puma</option>
            </select>
        </div>
        <div class="info-item" style="grid-column: span 2;">
            <label>תיאור מק״ט</label>
            <input type="text" value="${item.desc}" id="edit-desc" style="width:100%;">
        </div>
        <div class="info-item">
            <label>מכלול אבא ישיר</label>
            <input type="text" value="${item.parent}" id="edit-parent">
        </div>
        <div class="info-item">
            <label>כמות במלאי</label>
            <input type="number" value="${item.qty}" id="edit-qty">
        </div>
    `;

    modal.style.display = "flex";

    // --- הוספה: מאזין לשינויים שמפעיל את הכפתור ---
    const inputs = body.querySelectorAll('input, select');
    inputs.forEach(input => {
        input.addEventListener('input', () => {
            if (submitBtn) submitBtn.disabled = false;
        });
    });
};

window.closeModal = function () {
    const modal = document.getElementById("itemModal");
    if (modal) modal.style.display = "none";
};