let fullInventoryData = [];
let currentInventoryPage = 1;
const inventoryPageSize = 100;
let lastLoadedCount = 0;
let currentSearch = "";
let currentStockStatus = "all";
let currentPlaneTypeId = "all";
let advancedBuyMethod = "all";
let advancedItemGrpID = "all";
let advancedSupplierID = "all";
let advancedBodyPlane = "all";
let advancedLastPODate = "";

window.initAllInventory = function () {
    loadPlatformOptions();
    loadInventoryPage(1);
};

function loadInventoryPage(page) {
    if (page < 1) return;

    const params = new URLSearchParams();
    params.set("page", page);
    params.set("pageSize", inventoryPageSize);
    if (currentSearch) params.set("search", currentSearch);
    if (currentStockStatus && currentStockStatus !== "all") params.set("stockStatus", currentStockStatus);
    if (currentPlaneTypeId && currentPlaneTypeId !== "all") params.set("planeTypeId", currentPlaneTypeId);

    const apiUrl = `https://localhost:7296/api/InventoryItems?${params.toString()}`;

    ajaxCall(
        "GET",
        apiUrl,
        null,
        function (data) {
            fullInventoryData = Array.isArray(data)
                ? data
                : (Array.isArray(data?.$values) ? data.$values : []);

            currentInventoryPage = page;
            lastLoadedCount = fullInventoryData.length;
            populateAdvancedFilterOptions(fullInventoryData);
            renderInventoryTable(getAdvancedFilteredInventory(fullInventoryData));
            updateInventoryPager();
        },
        function (xhr) {
            console.error("Failed to load inventory items", xhr);
            fullInventoryData = [];
            lastLoadedCount = 0;
            populateAdvancedFilterOptions([]);
            renderInventoryTable([]);
            updateInventoryPager();
        }
    );
}

function renderInventoryTable(data) {
    const tbody = document.getElementById("inventory-table-body");
    if (!tbody) return;

    tbody.innerHTML = data.map(item => {
        const inventoryItemID = item.inventoryItemID ?? item.InventoryItemID ?? "";
        const itemName = item.itemName ?? item.ItemName ?? "";
        const itemGrpName = item.itemGrpName ?? item.ItemGrpName ?? "";
        const buyMethod = item.buyMethod ?? item.BuyMethod ?? "";
        const price = item.price ?? item.Price;
        const supplierName = item.supplierName ?? item.SupplierName ?? "";
        const whse01 = item.whse01_QTY ?? item.Whse01_QTY ?? "";
        const whse03 = item.whse03_QTY ?? item.Whse03_QTY ?? "";
        const whse90 = item.whse90_QTY ?? item.Whse90_QTY ?? "";
        const openPurchaseRequestQty = item.openPurchaseRequestQty ?? item.OpenPurchaseRequestQty ?? "";
        const openPurchaseOrderQty = item.openPurchaseOrderQty ?? item.OpenPurchaseOrderQty ?? "";
        const approvedOrderQty = item.approvedOrderQty ?? item.ApprovedOrderQty ?? "";
        const unapprovedOrderQty = item.unapprovedOrderQty ?? item.UnapprovedOrderQty ?? "";
        const bodyPlane = item.bodyPlane ?? item.BodyPlane;
        const lastPODateRaw = item.lastPODate ?? item.LastPODate;
        const lastPODate = lastPODateRaw ? String(lastPODateRaw).split("T")[0] : "";

        return `
        <tr>
            <td class="col-inventory-id" title="${escapeHtml(displayOrDash(inventoryItemID))}">${displayOrDash(inventoryItemID)}</td>
            <td class="col-item-name" title="${escapeHtml(displayOrDash(itemName))}">${displayOrDash(itemName)}</td>
            <td>${displayOrDash(itemGrpName)}</td>
            <td>${displayOrDash(buyMethod)}</td>
            <td>${displayOrDash(price)}</td>
            <td>${displayOrDash(supplierName)}</td>
            <td>${displayOrDash(whse01)}</td>
            <td>${displayOrDash(whse03)}</td>
            <td>${displayOrDash(whse90)}</td>
            <td>${displayOrDash(openPurchaseRequestQty)}</td>
            <td>${displayOrDash(openPurchaseOrderQty)}</td>
            <td>${displayOrDash(approvedOrderQty)}</td>
            <td>${displayOrDash(unapprovedOrderQty)}</td>
            <td>${displayOrDash(bodyPlane)}</td>
            <td>${displayOrDash(lastPODate)}</td>
        </tr>`;
    }).join("");
}

function displayOrDash(value) {
    if (value === null || value === undefined) return "-";
    const text = String(value).trim();
    return text === "" ? "-" : text;
}

function escapeHtml(value) {
    return String(value)
        .replace(/&/g, "&amp;")
        .replace(/</g, "&lt;")
        .replace(/>/g, "&gt;")
        .replace(/\"/g, "&quot;")
        .replace(/'/g, "&#39;");
}

window.filterInventory = function () {
    currentSearch = (document.getElementById("inventorySearch")?.value || "").trim();
    currentStockStatus = document.getElementById("stockStatusFilter")?.value || "all";
    currentPlaneTypeId = document.getElementById("platformFilter")?.value || "all";
    loadInventoryPage(1);
};

window.sortInventory = function () {
    renderInventoryTable(getAdvancedFilteredInventory(fullInventoryData));
};

window.prevInventoryPage = function () {
    if (currentInventoryPage > 1) {
        loadInventoryPage(currentInventoryPage - 1);
    }
};

window.nextInventoryPage = function () {
    if (lastLoadedCount === inventoryPageSize) {
        loadInventoryPage(currentInventoryPage + 1);
    }
};

function updateInventoryPager() {
    const prevBtn = document.getElementById("prevInventoryPageBtn");
    const nextBtn = document.getElementById("nextInventoryPageBtn");
    const numbersWrap = document.getElementById("inventoryPageNumbers");
    const hasNext = lastLoadedCount === inventoryPageSize;

    if (prevBtn) prevBtn.disabled = currentInventoryPage <= 1;
    if (nextBtn) nextBtn.disabled = !hasNext;

    if (!numbersWrap) return;

    const startPage = Math.max(1, currentInventoryPage - 2);
    const endPage = currentInventoryPage + 2;
    let html = "";

    for (let page = startPage; page <= endPage; page++) {
        const isCurrent = page === currentInventoryPage;
        const canGoForward = page <= currentInventoryPage || hasNext;
        const disabledAttr = canGoForward ? "" : "disabled";
        const activeClass = isCurrent ? " is-active" : "";

        html += `<button class="inventory-page-number${activeClass}" onclick="window.goToInventoryPage(${page})" ${disabledAttr}>${page}</button>`;
    }

    numbersWrap.innerHTML = html;
}

window.goToInventoryPage = function (page) {
    if (page < 1 || page === currentInventoryPage) return;
    loadInventoryPage(page);
};

function loadPlatformOptions() {
    const platformSelect = document.getElementById("platformFilter");
    if (!platformSelect) return;

    ajaxCall(
        "GET",
        "https://localhost:7296/api/PlaneTypes",
        null,
        function (data) {
            const list = Array.isArray(data) ? data : (Array.isArray(data?.$values) ? data.$values : []);
            const relevant = list.filter(p => {
                const name = (p.planeTypeName ?? p.PlaneTypeName ?? "").toUpperCase();
                return name === "WB" || name === "TBV";
            });

            platformSelect.innerHTML = '<option value="all">כל הפלטפורמות</option>';
            relevant.forEach(p => {
                const id = p.planeTypeID ?? p.PlaneTypeID;
                const name = p.planeTypeName ?? p.PlaneTypeName;
                if (id !== undefined && name) {
                    platformSelect.insertAdjacentHTML("beforeend", `<option value="${id}">${name}</option>`);
                }
            });
        },
        function () {
            platformSelect.innerHTML = '<option value="all">כל הפלטפורמות</option>';
        }
    );
}

window.openAdvancedInventoryFilters = function () {
    syncAdvancedFilterControls();
    const modal = document.getElementById("advancedInventoryFiltersModal");
    if (modal) modal.style.display = "flex";
};

window.closeAdvancedInventoryFilters = function () {
    const modal = document.getElementById("advancedInventoryFiltersModal");
    if (modal) modal.style.display = "none";
};

window.applyAdvancedInventoryFilters = function () {
    advancedBuyMethod = document.getElementById("advBuyMethod")?.value || "all";
    advancedItemGrpID = document.getElementById("advItemGrpID")?.value || "all";
    advancedSupplierID = document.getElementById("advSupplierID")?.value || "all";
    advancedBodyPlane = document.getElementById("advBodyPlane")?.value || "all";
    advancedLastPODate = document.getElementById("advLastPODate")?.value || "";

    renderInventoryTable(getAdvancedFilteredInventory(fullInventoryData));
    window.closeAdvancedInventoryFilters();
};

window.resetAdvancedInventoryFilters = function () {
    advancedBuyMethod = "all";
    advancedItemGrpID = "all";
    advancedSupplierID = "all";
    advancedBodyPlane = "all";
    advancedLastPODate = "";

    syncAdvancedFilterControls();
    renderInventoryTable(getAdvancedFilteredInventory(fullInventoryData));
};

function getAdvancedFilteredInventory(data) {
    return data.filter(item => {
        const buyMethod = String(item.buyMethod ?? item.BuyMethod ?? "").trim();
        const itemGrpID = String(item.itemGrpID ?? item.ItemGrpID ?? "").trim();
        const supplierID = String(item.supplierID ?? item.SupplierID ?? "").trim();
        const bodyPlaneRaw = String(item.bodyPlane ?? item.BodyPlane ?? "").trim();
        const bodyPlane = bodyPlaneRaw === "" ? "-" : bodyPlaneRaw;
        const lastPODateRaw = item.lastPODate ?? item.LastPODate;
        const lastPODate = lastPODateRaw ? String(lastPODateRaw).split("T")[0] : "";

        if (advancedBuyMethod !== "all" && buyMethod !== advancedBuyMethod) return false;
        if (advancedItemGrpID !== "all" && itemGrpID !== advancedItemGrpID) return false;
        if (advancedSupplierID !== "all" && supplierID !== advancedSupplierID) return false;
        if (advancedBodyPlane !== "all" && bodyPlane !== advancedBodyPlane) return false;
        if (advancedLastPODate && lastPODate !== advancedLastPODate) return false;

        return true;
    });
}

function populateAdvancedFilterOptions(data) {
    const itemGrpSelect = document.getElementById("advItemGrpID");
    const supplierSelect = document.getElementById("advSupplierID");
    if (!itemGrpSelect || !supplierSelect) return;

    const itemGrpSet = new Set();
    const supplierSet = new Set();

    data.forEach(item => {
        const grp = String(item.itemGrpID ?? item.ItemGrpID ?? "").trim();
        const supplier = String(item.supplierID ?? item.SupplierID ?? "").trim();
        if (grp) itemGrpSet.add(grp);
        if (supplier) supplierSet.add(supplier);
    });

    itemGrpSelect.innerHTML = '<option value="all">הכל</option>';
    [...itemGrpSet].sort((a, b) => Number(a) - Number(b)).forEach(value => {
        itemGrpSelect.insertAdjacentHTML("beforeend", `<option value="${value}">${value}</option>`);
    });

    supplierSelect.innerHTML = '<option value="all">הכל</option>';
    [...supplierSet].sort((a, b) => Number(a) - Number(b)).forEach(value => {
        supplierSelect.insertAdjacentHTML("beforeend", `<option value="${value}">${value}</option>`);
    });

    syncAdvancedFilterControls();
}

function syncAdvancedFilterControls() {
    const buyMethodEl = document.getElementById("advBuyMethod");
    const itemGrpEl = document.getElementById("advItemGrpID");
    const supplierEl = document.getElementById("advSupplierID");
    const bodyPlaneEl = document.getElementById("advBodyPlane");
    const lastPOEl = document.getElementById("advLastPODate");

    if (buyMethodEl) buyMethodEl.value = advancedBuyMethod;
    if (itemGrpEl) itemGrpEl.value = advancedItemGrpID;
    if (supplierEl) supplierEl.value = advancedSupplierID;
    if (bodyPlaneEl) bodyPlaneEl.value = advancedBodyPlane;
    if (lastPOEl) lastPOEl.value = advancedLastPODate;
}

window.showItemDetails = function () {
};

window.closeGenericModal = function () {
    const modal = document.getElementById("genericModal");
    if (modal) modal.style.display = "none";
};
