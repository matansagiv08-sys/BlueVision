let currentResultsData = [];

window.initInventoryResults = function () {
    // נתונים (במציאות יגיעו מהחישוב בדף הקודם)
    currentResultsData = window.lastCheckResults || [
        { sku: "WB-ENG-001", desc: "מנוע ראשי WanderB", platform: "WB", current: 3, req: 7, shortage: 4, vendor: "אולפסון תעשיות", price: "45,000" },
        { sku: "WIV-WING-001", desc: "גוף מרכזי", platform: "WB", current: 1, req: 6, shortage: 5, vendor: "מערכות תעופה", price: "12,000" },
        { sku: "TP-FRAME-001", desc: "משגרי TBV", platform: "TBV", current: 1, req: 6, shortage: 5, vendor: "טק-אוור בע\"מ", price: "30,000" }
    ];

    renderResultsTable(currentResultsData);
    updateSummaryCards(currentResultsData);
};

function renderResultsTable(data) {
    const tbody = document.getElementById("results-table-body");
    if (!tbody) return;

    tbody.innerHTML = data.map(item => `
        <tr onclick="window.openItemDetails('${item.sku}')">
            <td>${item.sku}</td>
            <td>${item.desc}</td>
            <td>${item.current}</td>
            <td>${item.req}</td>
            <td class="shortage-cell">${item.shortage}</td>
            <td>${item.vendor}</td>
            <td>${item.price}</td>
        </tr>
    `).join('');
}

function updateSummaryCards(data) {
    const totalShortageCount = data.length;
    const totalUnits = data.reduce((sum, item) => sum + item.shortage, 0);

    document.getElementById("total-shortage").innerText = totalShortageCount;
    document.getElementById("total-items-count").innerText = `${totalUnits} יחידות`;
    document.getElementById("total-cost").innerText = "₪ 636,000";
    document.getElementById("ready-to-prod").innerText = "2";
}

window.filterResults = function () {
    const searchTerm = document.getElementById("resultsSearch").value.toLowerCase();
    const platform = document.getElementById("platformFilter").value;

    const filtered = currentResultsData.filter(item => {
        const matchesSearch = item.sku.toLowerCase().includes(searchTerm) || item.desc.toLowerCase().includes(searchTerm);
        const matchesPlatform = platform === 'all' || item.platform === platform;
        return matchesSearch && matchesPlatform;
    });

    renderResultsTable(filtered);
};

window.openItemDetails = function (sku) {
    const item = currentResultsData.find(i => i.sku === sku);
    if (!item) return;

    const modal = document.getElementById("genericModal");
    const body = document.getElementById("modalBody");
    const title = document.getElementById("modalTitle");

    title.innerText = `פירוט חוסר פריט: ${item.sku}`;
    body.innerHTML = `
        <div style="display: grid; grid-template-columns: 1fr 1fr; gap: 15px; padding: 10px;">
            <div><strong>תיאור:</strong> ${item.desc}</div>
            <div><strong>ספק:</strong> ${item.vendor}</div>
            <div><strong>כמות במלאי:</strong> ${item.current}</div>
            <div><strong>כמות חסרה:</strong> <span style="color:red; font-weight:bold;">${item.shortage}</span></div>
            <div><strong>מחיר ליחידה:</strong> ₪ ${item.price}</div>
        </div>
    `;
    modal.style.display = "flex";
};