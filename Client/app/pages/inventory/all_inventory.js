let fullInventoryData = [];
let currentInventoryPage = 1;
const inventoryPageSize = 100;
let lastLoadedCount = 0;
let knownLastInventoryPage = null;
let currentSearch = "";
let currentStockStatus = "all";
let currentPlaneTypeId = "all";
let advancedBuyMethod = "all";
let advancedItemGrpID = "all";
let advancedSupplierID = "all";
let advancedBodyPlane = "all";
let advancedLastPODate = "";

window.initAllInventory = function () {
    loadInventoryFilterOptions();
    updateAdvancedFiltersBadge();
    loadInventoryPage(1);
};

function loadInventoryPage(page) {
    if (page < 1) return;
    if (knownLastInventoryPage !== null && page > knownLastInventoryPage) return;

    const params = new URLSearchParams();
    params.set("page", page);
    params.set("pageSize", inventoryPageSize);
    if (currentSearch) params.set("search", currentSearch);
    if (currentStockStatus && currentStockStatus !== "all") params.set("stockStatus", currentStockStatus);
    if (currentPlaneTypeId && currentPlaneTypeId !== "all") params.set("planeTypeId", currentPlaneTypeId);
    if (advancedBuyMethod && advancedBuyMethod !== "all") params.set("buyMethod", advancedBuyMethod);
    if (advancedItemGrpID && advancedItemGrpID !== "all") params.set("itemGrpID", advancedItemGrpID);
    if (advancedSupplierID && advancedSupplierID !== "all") params.set("supplierID", advancedSupplierID);
    if (advancedBodyPlane && advancedBodyPlane !== "all") params.set("bodyPlane", advancedBodyPlane);
    if (advancedLastPODate) params.set("lastPODate", advancedLastPODate);

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
            if (lastLoadedCount < inventoryPageSize) {
                knownLastInventoryPage = currentInventoryPage;
            } else if (currentInventoryPage === 1) {
                knownLastInventoryPage = null;
            }
            renderInventoryTable(fullInventoryData);
            updateInventoryPager();
        },
        function (xhr) {
            console.error("Failed to load inventory items", xhr);
            fullInventoryData = [];
            lastLoadedCount = 0;
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
    knownLastInventoryPage = null;
    loadInventoryPage(1);
};

window.sortInventory = function () {
    renderInventoryTable(fullInventoryData);
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
    const lastPageForUi = knownLastInventoryPage ?? (hasNext ? currentInventoryPage + 1 : currentInventoryPage);

    if (prevBtn) prevBtn.disabled = currentInventoryPage <= 1;
    if (nextBtn) nextBtn.disabled = knownLastInventoryPage !== null ? currentInventoryPage >= knownLastInventoryPage : !hasNext;

    if (!numbersWrap) return;

    const pages = new Set();
    pages.add(1);
    for (let page = currentInventoryPage - 2; page <= currentInventoryPage + 2; page++) {
        if (page >= 1 && page <= lastPageForUi) {
            pages.add(page);
        }
    }
    pages.add(lastPageForUi);

    const sortedPages = [...pages].sort((a, b) => a - b);
    let html = "";
    let previous = 0;

    sortedPages.forEach(page => {
        if (page - previous > 1) {
            html += '<span class="inventory-page-ellipsis">...</span>';
        }
        const isCurrent = page === currentInventoryPage;
        const activeClass = isCurrent ? " is-active" : "";

        html += `<button class="inventory-page-number${activeClass}" onclick="window.goToInventoryPage(${page})">${page}</button>`;
        previous = page;
    });

    numbersWrap.innerHTML = html;
}

window.goToInventoryPage = function (page) {
    if (page < 1 || page === currentInventoryPage) return;
    if (knownLastInventoryPage !== null && page > knownLastInventoryPage) return;
    loadInventoryPage(page);
};

function loadInventoryFilterOptions() {
    ajaxCall(
        "GET",
        "https://localhost:7296/api/InventoryItems/filter-options",
        null,
        function (data) {
            populateFilterOptions(data || {});
        },
        function () {
            populateFilterOptions({});
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

    updateAdvancedFiltersBadge();
    knownLastInventoryPage = null;
    loadInventoryPage(1);
    window.closeAdvancedInventoryFilters();
};

window.resetAdvancedInventoryFilters = function () {
    advancedBuyMethod = "all";
    advancedItemGrpID = "all";
    advancedSupplierID = "all";
    advancedBodyPlane = "all";
    advancedLastPODate = "";

    syncAdvancedFilterControls();
    updateAdvancedFiltersBadge();
    knownLastInventoryPage = null;
    loadInventoryPage(1);
};

window.clearAllInventoryFilters = function () {
    currentSearch = "";
    currentStockStatus = "all";
    currentPlaneTypeId = "all";
    advancedBuyMethod = "all";
    advancedItemGrpID = "all";
    advancedSupplierID = "all";
    advancedBodyPlane = "all";
    advancedLastPODate = "";
    knownLastInventoryPage = null;

    const searchEl = document.getElementById("inventorySearch");
    const stockEl = document.getElementById("stockStatusFilter");
    const platformEl = document.getElementById("platformFilter");
    if (searchEl) searchEl.value = "";
    if (stockEl) stockEl.value = "all";
    if (platformEl) platformEl.value = "all";

    syncAdvancedFilterControls();
    updateAdvancedFiltersBadge();
    loadInventoryPage(1);
};

function populateFilterOptions(options) {
    const platformSelect = document.getElementById("platformFilter");
    const buyMethodSelect = document.getElementById("advBuyMethod");
    const itemGrpSelect = document.getElementById("advItemGrpID");
    const supplierSelect = document.getElementById("advSupplierID");
    const bodyPlaneSelect = document.getElementById("advBodyPlane");
    if (!platformSelect || !buyMethodSelect || !itemGrpSelect || !supplierSelect || !bodyPlaneSelect) return;

    const platforms = Array.isArray(options.platforms) ? options.platforms : (Array.isArray(options.Platforms) ? options.Platforms : []);
    const buyMethods = Array.isArray(options.buyMethods) ? options.buyMethods : (Array.isArray(options.BuyMethods) ? options.BuyMethods : []);
    const groups = Array.isArray(options.groups) ? options.groups : (Array.isArray(options.Groups) ? options.Groups : []);
    const suppliers = Array.isArray(options.suppliers) ? options.suppliers : (Array.isArray(options.Suppliers) ? options.Suppliers : []);
    const bodyPlanes = Array.isArray(options.bodyPlanes) ? options.bodyPlanes : (Array.isArray(options.BodyPlanes) ? options.BodyPlanes : []);

    platformSelect.innerHTML = '<option value="all">כל הפלטפורמות</option>';
    platforms.forEach(platform => {
        const id = platform.planeTypeID ?? platform.PlaneTypeID;
        const name = (platform.planeTypeName ?? platform.PlaneTypeName ?? "").toString().trim();
        if (id === undefined || id === null || name === "") return;
        platformSelect.insertAdjacentHTML("beforeend", `<option value="${id}">${escapeHtml(name)}</option>`);
    });

    buyMethodSelect.innerHTML = '<option value="all">הכל</option>';
    buyMethods
        .map(v => String(v ?? "").trim())
        .filter(v => v !== "")
        .forEach(value => {
            buyMethodSelect.insertAdjacentHTML("beforeend", `<option value="${escapeHtml(value)}">${escapeHtml(value)}</option>`);
        });

    itemGrpSelect.innerHTML = '<option value="all">הכל</option>';
    groups.forEach(group => {
        const id = group.itemGrpID ?? group.ItemGrpID;
        const name = (group.itemGrpName ?? group.ItemGrpName ?? "").toString().trim();
        if (id === undefined || id === null) return;
        itemGrpSelect.insertAdjacentHTML("beforeend", `<option value="${id}">${escapeHtml(name || String(id))}</option>`);
    });

    supplierSelect.innerHTML = '<option value="all">הכל</option>';
    suppliers.forEach(supplier => {
        const id = supplier.supplierID ?? supplier.SupplierID;
        const name = (supplier.supplierName ?? supplier.SupplierName ?? "").toString().trim();
        if (id === undefined || id === null) return;
        supplierSelect.insertAdjacentHTML("beforeend", `<option value="${id}">${escapeHtml(name || String(id))}</option>`);
    });

    bodyPlaneSelect.innerHTML = '<option value="all">הכל</option>';
    bodyPlanes
        .map(v => String(v ?? "").trim())
        .filter(v => v !== "")
        .forEach(value => {
            bodyPlaneSelect.insertAdjacentHTML("beforeend", `<option value="${escapeHtml(value)}">${escapeHtml(value)}</option>`);
    });

    syncAdvancedFilterControls();
    if (platformSelect) platformSelect.value = currentPlaneTypeId;
}

function getAdvancedFiltersCount() {
    let count = 0;
    if (advancedBuyMethod !== "all") count++;
    if (advancedItemGrpID !== "all") count++;
    if (advancedSupplierID !== "all") count++;
    if (advancedBodyPlane !== "all") count++;
    if (advancedLastPODate) count++;
    return count;
}

function updateAdvancedFiltersBadge() {
    const badge = document.getElementById("advancedFiltersCount");
    if (!badge) return;

    const count = getAdvancedFiltersCount();
    badge.textContent = String(count);
    badge.hidden = count === 0;
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
