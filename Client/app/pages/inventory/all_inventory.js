//global variables that store the current state of the Inventory page
//actual rows returned from the server
let fullInventoryData = [];
//pagination and filter state
let currentInventoryPage = 1;
const inventoryPageSize = 100;
let lastLoadedCount = 0;
let knownLastInventoryPage = null;
//main filters
let currentSearch = "";
let currentStockStatus = "all";
let currentPlaneTypeId = "all";
//advanced filters
let advancedBuyMethod = "all";
let advancedItemGrpID = "all";
let advancedSupplierID = "all";
let advancedBodyPlane = "all";
let advancedLastPODateFrom = "";
let advancedLastPODateTo = "";

window.initAllInventory = function () {
    bindInventoryCellTooltips();
    loadInventoryFilterOptions();
    updateAdvancedFiltersBadge();
    loadInventoryPage(1);
};

function bindInventoryCellTooltips() {
    const tbody = document.getElementById("inventory-table-body");
    if (!tbody || tbody.dataset.tooltipBound === "true") return;
    tbody.dataset.tooltipBound = "true";

    tbody.addEventListener("mouseover", function (e) {
        const target = e.target instanceof Element ? e.target.closest(".table-cell-tooltip") : null;
        if (target) showInventoryCellTooltip(target);
    });

    tbody.addEventListener("mouseout", function (e) {
        const target = e.target instanceof Element ? e.target.closest(".table-cell-tooltip") : null;
        if (!target) return;
        const related = e.relatedTarget instanceof Element ? e.relatedTarget.closest(".table-cell-tooltip") : null;
        if (related !== target) hideInventoryCellTooltip();
    });

    tbody.addEventListener("focusin", function (e) {
        const target = e.target instanceof Element ? e.target.closest(".table-cell-tooltip") : null;
        if (target) showInventoryCellTooltip(target);
    });

    tbody.addEventListener("focusout", hideInventoryCellTooltip);
    tbody.addEventListener("mouseover", function (e) {
        const target = e.target instanceof Element ? e.target.closest(".stock-total-tooltip") : null;
        if (target) showStockBreakdownPopover(target);
    });

    tbody.addEventListener("mouseout", function (e) {
        const target = e.target instanceof Element ? e.target.closest(".stock-total-tooltip") : null;
        if (!target) return;
        const related = e.relatedTarget instanceof Element ? e.relatedTarget.closest(".stock-total-tooltip") : null;
        if (related !== target) hideStockBreakdownPopovers();
    });

    tbody.addEventListener("focusin", function (e) {
        const target = e.target instanceof Element ? e.target.closest(".stock-total-tooltip") : null;
        if (target) showStockBreakdownPopover(target);
    });

    tbody.addEventListener("focusout", hideStockBreakdownPopovers);
    tbody.addEventListener("scroll", hideInventoryCellTooltip);
    tbody.addEventListener("scroll", hideStockBreakdownPopovers);
    window.addEventListener("scroll", hideInventoryCellTooltip, true);
    window.addEventListener("scroll", hideStockBreakdownPopovers, true);
    window.addEventListener("resize", hideInventoryCellTooltip);
    window.addEventListener("resize", hideStockBreakdownPopovers);
}

//renders the inventory table based on the current page and filters
function loadInventoryPage(page, done) {
    if (page < 1) return;
    if (knownLastInventoryPage !== null && page > knownLastInventoryPage) return;

    //construct query parameters based on current filters and pagination state.
    const params = new URLSearchParams();
    params.set("page", page);
    params.set("pageSize", inventoryPageSize);
    //add filters to the query params only if they are set to a specific value
    if (currentSearch) params.set("search", currentSearch);
    if (currentStockStatus && currentStockStatus !== "all") params.set("stockStatus", currentStockStatus);
    if (currentPlaneTypeId && currentPlaneTypeId !== "all") params.set("planeTypeId", currentPlaneTypeId);
    if (advancedBuyMethod && advancedBuyMethod !== "all") params.set("buyMethod", advancedBuyMethod);
    if (advancedItemGrpID && advancedItemGrpID !== "all") params.set("itemGrpID", advancedItemGrpID);
    if (advancedSupplierID && advancedSupplierID !== "all") params.set("supplierID", advancedSupplierID);
    if (advancedBodyPlane && advancedBodyPlane !== "all") params.set("bodyPlane", advancedBodyPlane);
    if (advancedLastPODateFrom) params.set("lastPODateFrom", advancedLastPODateFrom);
    if (advancedLastPODateTo) params.set("lastPODateTo", advancedLastPODateTo);
    //example for what params might look like: page=1&pageSize=100&buyMethod=B&supplierID=5

    const apiUrl = `https://localhost:7296/api/InventoryItems?${params.toString()}`;

    ajaxCall(
        "GET",
        apiUrl,
        null,
        function (data) {
            //ensures the vaiable is always an array no matter how the data was sent
            fullInventoryData = Array.isArray(data)
                ? data
                : (Array.isArray(data?.$values) ? data.$values : []);
            //pagination logic to know when we are the last page. 
            currentInventoryPage = page;
            lastLoadedCount = fullInventoryData.length;
            if (lastLoadedCount < inventoryPageSize) {
                knownLastInventoryPage = currentInventoryPage;
            } else if (currentInventoryPage === 1) {
                knownLastInventoryPage = null;
            }
            renderInventoryTable(fullInventoryData);
            updateInventoryPager();
            if (typeof done === "function") done();
        },
        function (xhr) {
            console.error("Failed to load inventory items", xhr);
            fullInventoryData = [];
            lastLoadedCount = 0;
            renderInventoryTable([]);
            updateInventoryPager();
            if (typeof done === "function") done();
        }
    );
}

//Renders the inventory items into the HTML table. It maps each item to a table row
function renderInventoryTable(data) {
    const tbody = document.getElementById("inventory-table-body");
    if (!tbody) return;

    //.map loops through each item in the data array and returns a string of HTML for a table row
    tbody.innerHTML = data.map(item => {
        const inventoryItemID = item.inventoryItemID ?? item.InventoryItemID ?? "";
        const itemName = item.itemName ?? item.ItemName ?? "";
        const itemGrpName = item.itemGrpName ?? item.ItemGrpName ?? "";
        const buyMethod = item.buyMethod ?? item.BuyMethod ?? "";
        const price = item.price ?? item.Price;
        const supplierName = item.supplierName ?? item.SupplierName ?? "";
        const whse01 = parseQuantity(item.whse01_QTY ?? item.Whse01_QTY);
        const whse03 = parseQuantity(item.whse03_QTY ?? item.Whse03_QTY);
        const whse90 = parseQuantity(item.whse90_QTY ?? item.Whse90_QTY);
        const totalStock = whse01 + whse03 + whse90;
        const openPurchaseRequestQty = item.openPurchaseRequestQty ?? item.OpenPurchaseRequestQty ?? "";
        const openPurchaseOrderQty = item.openPurchaseOrderQty ?? item.OpenPurchaseOrderQty ?? "";
        const approvedOrderQty = item.approvedOrderQty ?? item.ApprovedOrderQty ?? "";
        const unapprovedOrderQty = item.unapprovedOrderQty ?? item.UnapprovedOrderQty ?? "";
        const bodyPlane = formatBodyPlane(item.bodyPlane ?? item.BodyPlane);
        const lastPODateRaw = item.lastPODate ?? item.LastPODate;
        const lastPODate = lastPODateRaw ? String(lastPODateRaw).split("T")[0] : "";

        return `
        <tr>
            <td class="col-inventory-id">${renderCellWithTooltip(inventoryItemID)}</td>
            <td class="col-item-name">${renderCellWithTooltip(itemName)}</td>
            <td>${renderCellWithTooltip(itemGrpName)}</td>
            <td>${renderCellWithTooltip(buyMethod)}</td>
            <td>${renderCellWithTooltip(price)}</td>
            <td class="col-supplier">${renderCellWithTooltip(supplierName)}</td>
            <td class="col-total-stock">
                <span class="stock-total-tooltip" tabindex="0" aria-label="פירוט מלאי לפי מחסן">
                    ${displayOrDash(totalStock)}
                    <span class="stock-breakdown-popover" role="tooltip">
                        <span class="stock-breakdown-row stock-breakdown-head">
                            <span>מחסן</span>
                            <span>כמות</span>
                        </span>
                        <span class="stock-breakdown-row">
                            <span>01</span>
                            <span>${whse01}</span>
                        </span>
                        <span class="stock-breakdown-row">
                            <span>03</span>
                            <span>${whse03}</span>
                        </span>
                        <span class="stock-breakdown-row">
                            <span>90</span>
                            <span>${whse90}</span>
                        </span>
                    </span>
                </span>
            </td>
            <td>${renderCellWithTooltip(openPurchaseRequestQty)}</td>
            <td>${renderCellWithTooltip(openPurchaseOrderQty)}</td>
            <td>${renderCellWithTooltip(approvedOrderQty)}</td>
            <td>${renderCellWithTooltip(unapprovedOrderQty)}</td>
            <td>${renderCellWithTooltip(bodyPlane)}</td>
            <td>${renderCellWithTooltip(lastPODate)}</td>
        </tr>`;
    }).join("");
}

function renderCellWithTooltip(value) {
    const displayValue = displayOrDash(value);
    const safeValue = escapeHtml(displayValue);
    if (displayValue === "-") return safeValue;
    return `<span class="table-cell-tooltip" tabindex="0" data-tooltip="${safeValue}">${safeValue}</span>`;
}

function getInventoryCellTooltip() {
    let tooltip = document.getElementById("inventoryCellTooltip");
    if (tooltip) return tooltip;

    tooltip = document.createElement("div");
    tooltip.id = "inventoryCellTooltip";
    tooltip.className = "inventory-cell-tooltip-popover";
    tooltip.setAttribute("role", "tooltip");
    document.body.appendChild(tooltip);
    return tooltip;
}

function showInventoryCellTooltip(target) {
    const text = target?.dataset?.tooltip;
    if (!text || !isInventoryCellTextTruncated(target)) {
        hideInventoryCellTooltip();
        return;
    }

    const tooltip = getInventoryCellTooltip();
    tooltip.textContent = text;
    tooltip.style.display = "block";

    const targetRect = target.getBoundingClientRect();
    const tooltipRect = tooltip.getBoundingClientRect();
    const viewportPadding = 12;
    let top = targetRect.top - tooltipRect.height - 8;
    let left = targetRect.right - tooltipRect.width;

    if (top < viewportPadding) {
        top = targetRect.bottom + 8;
    }

    left = Math.max(viewportPadding, Math.min(left, window.innerWidth - tooltipRect.width - viewportPadding));
    tooltip.style.top = `${top}px`;
    tooltip.style.left = `${left}px`;
}

function hideInventoryCellTooltip() {
    const tooltip = document.getElementById("inventoryCellTooltip");
    if (tooltip) tooltip.style.display = "none";
}

function showStockBreakdownPopover(target) {
    const popover = target?.querySelector(".stock-breakdown-popover");
    if (!popover) return;

    hideStockBreakdownPopovers(popover);
    popover.classList.add("stock-breakdown-open");
    popover.classList.remove("stock-breakdown-below");
    popover.style.visibility = "hidden";
    popover.style.top = "0px";
    popover.style.left = "0px";

    const targetRect = target.getBoundingClientRect();
    const popoverRect = popover.getBoundingClientRect();
    const gap = 10;
    const viewportPadding = 8;
    const shouldOpenBelow = targetRect.top < popoverRect.height + gap + viewportPadding;

    let top = shouldOpenBelow
        ? targetRect.bottom + gap
        : targetRect.top - popoverRect.height - gap;
    let left = targetRect.left + (targetRect.width / 2) - (popoverRect.width / 2);

    left = Math.max(viewportPadding, Math.min(left, window.innerWidth - popoverRect.width - viewportPadding));
    top = Math.max(viewportPadding, Math.min(top, window.innerHeight - popoverRect.height - viewportPadding));

    if (shouldOpenBelow) {
        popover.classList.add("stock-breakdown-below");
    }

    popover.style.top = `${top}px`;
    popover.style.left = `${left}px`;
    popover.style.visibility = "visible";
}

function hideStockBreakdownPopovers(exceptPopover = null) {
    document.querySelectorAll(".stock-breakdown-popover.stock-breakdown-open").forEach(popover => {
        if (popover === exceptPopover) return;
        popover.classList.remove("stock-breakdown-open", "stock-breakdown-below");
        popover.style.top = "";
        popover.style.left = "";
        popover.style.visibility = "";
    });
}

function isInventoryCellTextTruncated(target) {
    if (!target) return false;
    return target.scrollWidth > target.clientWidth + 1;
}

function parseQuantity(value) {
    if (value === null || value === undefined || value === "") return 0;
    const numeric = Number(value);
    return Number.isFinite(numeric) ? numeric : 0;
}

//displays the value or a dash if the value is null, undefined, or an empty string after trimming.
function displayOrDash(value) {
    if (value === null || value === undefined) return "-";
    const text = String(value).trim();
    return text === "" ? "-" : text;
}

function formatBodyPlane(value) {
    const normalized = (value ?? "").toString().trim().toUpperCase();

    switch (normalized) {
        case "B":
            return "גוף";
        case "P":
            return "כטב\"מ";
        case "M":
            return "משותף";
        default:
            return "-";
    }
}

function escapeHtml(value) {
    return String(value)
        .replace(/&/g, "&amp;")
        .replace(/</g, "&lt;")
        .replace(/>/g, "&gt;")
        .replace(/\"/g, "&quot;")
        .replace(/'/g, "&#39;");
}

//Event handlers for filter changes and pagination controls. They update the relevant state variables and reload the inventory data accordingly.
window.filterInventory = function () {
    currentSearch = (document.getElementById("inventorySearch")?.value || "").trim();
    currentStockStatus = document.getElementById("stockStatusFilter")?.value || "all";
    currentPlaneTypeId = document.getElementById("platformFilter")?.value || "all";
    knownLastInventoryPage = null;
    loadInventoryPage(1);
};

//three functions called in html page
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

//Updates the state of the pagination controls (previous/next buttons and page numbers) based on the current page, known last page, and how many items were loaded in the last request.
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

// Loads options for filters from the server and populates the filter dropdowns. Called on page init.
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

//5 window functinos related to actions done in through html
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
    advancedLastPODateFrom = document.getElementById("advLastPODateFrom")?.value || "";
    advancedLastPODateTo = document.getElementById("advLastPODateTo")?.value || "";

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
    advancedLastPODateFrom = "";
    advancedLastPODateTo = "";

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
    advancedLastPODateFrom = "";
    advancedLastPODateTo = "";
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
    //html elements for the filters
    const platformSelect = document.getElementById("platformFilter");
    const buyMethodSelect = document.getElementById("advBuyMethod");
    const itemGrpSelect = document.getElementById("advItemGrpID");
    const supplierSelect = document.getElementById("advSupplierID");
    const bodyPlaneSelect = document.getElementById("advBodyPlane");
    if (!platformSelect || !buyMethodSelect || !itemGrpSelect || !supplierSelect || !bodyPlaneSelect) return;

    //extracting options arrays from the response, with support for different possible property names and formats
    //if a is an array , use it, if not check if A is an array and use it, otherwise default to empty array (a : b : [])
    const platforms = Array.isArray(options.platforms) ? options.platforms : (Array.isArray(options.Platforms) ? options.Platforms : []);
    const buyMethods = Array.isArray(options.buyMethods) ? options.buyMethods : (Array.isArray(options.BuyMethods) ? options.BuyMethods : []);
    const groups = Array.isArray(options.groups) ? options.groups : (Array.isArray(options.Groups) ? options.Groups : []);
    const suppliers = Array.isArray(options.suppliers) ? options.suppliers : (Array.isArray(options.Suppliers) ? options.Suppliers : []);
    const bodyPlanes = Array.isArray(options.bodyPlanes) ? options.bodyPlanes : (Array.isArray(options.BodyPlanes) ? options.BodyPlanes : []);

    //populate platform filter, each time adds another <option>. We start with a default "all" option, then add options from the response.
    platformSelect.innerHTML = '<option value="all">כל הפלטפורמות</option>';
    platforms.forEach(platform => {
        const id = platform.planeTypeID ?? platform.PlaneTypeID;
        const name = (platform.planeTypeName ?? platform.PlaneTypeName ?? "").toString().trim();
        if (id === undefined || id === null || name === "") return;
        //escapeHtml is used to prevent XSS in case the server returns malicious data. It converts special characters to HTML entities.
        platformSelect.insertAdjacentHTML("beforeend", `<option value="${id}">${escapeHtml(name)}</option>`);
    });

    //take buyMethods (which is already an array), clean and normalize each value (map), remove unwanted values (filter), and then loop through 
    //the cleaned results(forEach) to create < option > elements one by one
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
            bodyPlaneSelect.insertAdjacentHTML("beforeend", `<option value="${escapeHtml(value)}">${escapeHtml(formatBodyPlane(value))}</option>`);
    });

    //after user selects options, this is how we set the filter selections
    syncAdvancedFilterControls();
    if (platformSelect) platformSelect.value = currentPlaneTypeId;
}

//Counts how many advanced filters are currently active (not set to "all" or empty) and returns that count. Used for the badge on the "Advanced Filters" button.
function getAdvancedFiltersCount() {
    let count = 0;
    if (advancedBuyMethod !== "all") count++;
    if (advancedItemGrpID !== "all") count++;
    if (advancedSupplierID !== "all") count++;
    if (advancedBodyPlane !== "all") count++;
    if (advancedLastPODateFrom || advancedLastPODateTo) count++;
    return count;
}

//Updates the badge on the "Advanced Filters" button to show how many advanced filters are currently active
function updateAdvancedFiltersBadge() {
    const badge = document.getElementById("advancedFiltersCount");
    if (!badge) return;

    const count = getAdvancedFiltersCount();
    badge.textContent = String(count);
    badge.hidden = count === 0;
}

//shows selected advanced filters in the modal when opened, by setting the value of each control to the corresponding state variable
function syncAdvancedFilterControls() {
    const buyMethodEl = document.getElementById("advBuyMethod");
    const itemGrpEl = document.getElementById("advItemGrpID");
    const supplierEl = document.getElementById("advSupplierID");
    const bodyPlaneEl = document.getElementById("advBodyPlane");
    const lastPOFromEl = document.getElementById("advLastPODateFrom");
    const lastPOToEl = document.getElementById("advLastPODateTo");

    if (buyMethodEl) buyMethodEl.value = advancedBuyMethod;
    if (itemGrpEl) itemGrpEl.value = advancedItemGrpID;
    if (supplierEl) supplierEl.value = advancedSupplierID;
    if (bodyPlaneEl) bodyPlaneEl.value = advancedBodyPlane;
    if (lastPOFromEl) lastPOFromEl.value = advancedLastPODateFrom;
    if (lastPOToEl) lastPOToEl.value = advancedLastPODateTo;
}

window.showItemDetails = function () {
};

window.closeGenericModal = function () {
    const modal = document.getElementById("genericModal");
    if (modal) modal.style.display = "none";
};
