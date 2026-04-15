let bomPlaneOptions = [];
let currentBomRows = [];

let currentBomPlaneTypeId = null;
let currentBomPage = 1;
const bomPageSize = 100;
let lastLoadedBomCount = 0;
let knownLastBomPage = null;

let currentBomSearch = "";
let currentBomMeasureUnit = "";
let currentBomWarehouse = "";
let currentBomLevel = "";
let currentBomHasChild = "";
let currentBomBuyMethod = "";
let currentBomBodyPlane = "";

window.initUavBOM = function () {
    loadBomPlaneOptions();
};

function loadBomPlaneOptions() {
    ajaxCall(
        "GET",
        "https://localhost:7296/api/Bom/planes",
        null,
        function (data) {
            bomPlaneOptions = Array.isArray(data)
                ? data
                : (Array.isArray(data?.$values) ? data.$values : []);

            if (bomPlaneOptions.length === 0) {
                currentBomRows = [];
                renderBomPlaneButtons();
                renderBomTable([]);
                updateBomPager();
                return;
            }

            if (!bomPlaneOptions.some(option => String(option.planeTypeID ?? option.PlaneTypeID) === String(currentBomPlaneTypeId))) {
                currentBomPlaneTypeId = String(bomPlaneOptions[0].planeTypeID ?? bomPlaneOptions[0].PlaneTypeID);
            }

            renderBomPlaneButtons();
            loadBomFilterOptions(function () {
                resetBomPaging();
                loadBomPage(1);
            });
        },
        function (xhr) {
            console.error("Failed to load BOM plane options", xhr);
            bomPlaneOptions = [];
            currentBomRows = [];
            renderBomPlaneButtons();
            renderBomTable([]);
            updateBomPager();
        }
    );
}

function loadBomFilterOptions(onDone) {
    const params = new URLSearchParams();
    if (currentBomPlaneTypeId) params.set("planeTypeId", currentBomPlaneTypeId);

    ajaxCall(
        "GET",
        `https://localhost:7296/api/Bom/filter-options?${params.toString()}`,
        null,
        function (data) {
            populateBomFilterOptions(data || {});
            if (typeof onDone === "function") onDone();
        },
        function () {
            populateBomFilterOptions({});
            if (typeof onDone === "function") onDone();
        }
    );
}

function loadBomPage(page) {
    if (!currentBomPlaneTypeId || page < 1) return;
    if (knownLastBomPage !== null && page > knownLastBomPage) return;

    const params = new URLSearchParams();
    params.set("page", page);
    params.set("pageSize", bomPageSize);
    params.set("planeTypeId", currentBomPlaneTypeId);

    if (currentBomSearch) params.set("search", currentBomSearch);
    if (currentBomMeasureUnit) params.set("measureUnit", currentBomMeasureUnit);
    if (currentBomWarehouse) params.set("warehouse", currentBomWarehouse);
    if (currentBomLevel) params.set("bomLevel", currentBomLevel);
    if (currentBomHasChild) params.set("hasChild", currentBomHasChild);
    if (currentBomBuyMethod) params.set("buyMethod", currentBomBuyMethod);
    if (currentBomBodyPlane) params.set("bodyPlane", currentBomBodyPlane);

    ajaxCall(
        "GET",
        `https://localhost:7296/api/Bom?${params.toString()}`,
        null,
        function (data) {
            currentBomRows = Array.isArray(data)
                ? data
                : (Array.isArray(data?.$values) ? data.$values : []);

            currentBomPage = page;
            lastLoadedBomCount = currentBomRows.length;
            if (lastLoadedBomCount < bomPageSize) {
                knownLastBomPage = currentBomPage;
            } else if (currentBomPage === 1) {
                knownLastBomPage = null;
            }

            renderBomTable(currentBomRows);
            updateBomPager();
        },
        function (xhr) {
            console.error("Failed to load BOM rows", xhr);
            currentBomRows = [];
            lastLoadedBomCount = 0;
            renderBomTable([]);
            updateBomPager();
        }
    );
}

function renderBomPlaneButtons() {
    const container = document.getElementById("bomPlaneToggle");
    if (!container) return;

    if (bomPlaneOptions.length === 0) {
        container.innerHTML = "";
        return;
    }

    container.innerHTML = bomPlaneOptions.map(option => {
        const planeTypeId = String(option.planeTypeID ?? option.PlaneTypeID);
        const planeTypeName = (option.planeTypeName ?? option.PlaneTypeName ?? planeTypeId).toString();
        const activeClass = planeTypeId === String(currentBomPlaneTypeId) ? " active" : "";
        return `<button class="slider-btn${activeClass}" onclick="window.selectBomPlane('${escapeHtml(planeTypeId)}', this)">${escapeHtml(planeTypeName)}</button>`;
    }).join("");
}

window.selectBomPlane = function (planeTypeId, btn) {
    currentBomPlaneTypeId = String(planeTypeId);

    const container = btn?.closest(".model-selection-toggle") || document.getElementById("bomPlaneToggle");
    container?.querySelectorAll(".slider-btn").forEach(button => button.classList.remove("active"));
    btn?.classList.add("active");

    resetBomPaging();
    loadBomFilterOptions(function () {
        loadBomPage(1);
    });
};

window.filterBomTable = function () {
    currentBomSearch = (document.getElementById("bomSearch")?.value || "").trim();
    currentBomMeasureUnit = readFilterValue("bomMeasureUnitFilter");
    currentBomWarehouse = readFilterValue("bomWarehouseFilter");
    currentBomLevel = readFilterValue("bomLevelFilter");
    currentBomHasChild = readFilterValue("bomHasChildFilter");
    currentBomBuyMethod = readFilterValue("bomBuyMethodFilter");
    currentBomBodyPlane = readFilterValue("bomBodyPlaneFilter");

    resetBomPaging();
    loadBomPage(1);
};

window.clearBomFilters = function () {
    const searchEl = document.getElementById("bomSearch");
    const measureUnitEl = document.getElementById("bomMeasureUnitFilter");
    const warehouseEl = document.getElementById("bomWarehouseFilter");
    const levelEl = document.getElementById("bomLevelFilter");
    const hasChildEl = document.getElementById("bomHasChildFilter");
    const buyMethodEl = document.getElementById("bomBuyMethodFilter");
    const bodyPlaneEl = document.getElementById("bomBodyPlaneFilter");

    if (searchEl) searchEl.value = "";
    if (measureUnitEl) measureUnitEl.value = "";
    if (warehouseEl) warehouseEl.value = "";
    if (levelEl) levelEl.value = "";
    if (hasChildEl) hasChildEl.value = "";
    if (buyMethodEl) buyMethodEl.value = "";
    if (bodyPlaneEl) bodyPlaneEl.value = "";

    window.filterBomTable();
};

window.prevBomPage = function () {
    if (currentBomPage > 1) {
        loadBomPage(currentBomPage - 1);
    }
};

window.nextBomPage = function () {
    if (lastLoadedBomCount === bomPageSize) {
        loadBomPage(currentBomPage + 1);
    }
};

window.goToBomPage = function (page) {
    if (page < 1 || page === currentBomPage) return;
    if (knownLastBomPage !== null && page > knownLastBomPage) return;
    loadBomPage(page);
};

function updateBomPager() {
    const prevBtn = document.getElementById("prevBomPageBtn");
    const nextBtn = document.getElementById("nextBomPageBtn");
    const numbersWrap = document.getElementById("bomPageNumbers");
    const hasNext = lastLoadedBomCount === bomPageSize;
    const lastPageForUi = knownLastBomPage ?? (hasNext ? currentBomPage + 1 : currentBomPage);

    if (prevBtn) prevBtn.disabled = currentBomPage <= 1;
    if (nextBtn) nextBtn.disabled = knownLastBomPage !== null ? currentBomPage >= knownLastBomPage : !hasNext;

    if (!numbersWrap) return;

    const pages = new Set();
    pages.add(1);
    for (let page = currentBomPage - 2; page <= currentBomPage + 2; page++) {
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
        const isCurrent = page === currentBomPage;
        const activeClass = isCurrent ? " is-active" : "";
        html += `<button class="inventory-page-number${activeClass}" onclick="window.goToBomPage(${page})">${page}</button>`;
        previous = page;
    });

    numbersWrap.innerHTML = html;
}

function renderBomTable(data) {
    const tbody = document.getElementById("bom-table-body");
    if (!tbody) return;

    tbody.innerHTML = data.map(item => {
        const inventoryItemId = item.inventoryItemID ?? item.InventoryItemID ?? "";
        const itemName = item.itemName ?? item.ItemName ?? "";
        const quantity = item.quantity ?? item.Quantity;
        const measureUnit = item.measureUnit ?? item.MeasureUnit ?? "";
        const warehouse = item.warehouse ?? item.Warehouse ?? "";
        const bomLevel = item.bomLevel ?? item.BomLevel;
        const hasChildRaw = item.hasChild ?? item.HasChild;
        const hasChild = hasChildRaw === true ? "כן" : "לא";
        const buyMethod = item.buyMethod ?? item.BuyMethod ?? "";
        const bodyPlane = item.bodyPlane ?? item.BodyPlane ?? "";

        return `
            <tr>
                <td class="col-sku">${displayOrDash(inventoryItemId)}</td>
                <td>${displayOrDash(itemName)}</td>
                <td>${displayOrDash(quantity)}</td>
                <td>${displayOrDash(measureUnit)}</td>
                <td>${displayOrDash(warehouse)}</td>
                <td>${displayOrDash(bomLevel)}</td>
                <td>${displayOrDash(hasChild)}</td>
                <td>${displayOrDash(buyMethod)}</td>
                <td>${displayOrDash(bodyPlane)}</td>
            </tr>
        `;
    }).join("");
}

function populateBomFilterOptions(options) {
    const measureUnits = Array.isArray(options.measureUnits) ? options.measureUnits : (Array.isArray(options.MeasureUnits) ? options.MeasureUnits : []);
    const warehouses = Array.isArray(options.warehouses) ? options.warehouses : (Array.isArray(options.Warehouses) ? options.Warehouses : []);
    const bomLevels = Array.isArray(options.bomLevels) ? options.bomLevels : (Array.isArray(options.BomLevels) ? options.BomLevels : []);
    const hasChildOptions = Array.isArray(options.hasChildOptions) ? options.hasChildOptions : (Array.isArray(options.HasChildOptions) ? options.HasChildOptions : []);
    const buyMethods = Array.isArray(options.buyMethods) ? options.buyMethods : (Array.isArray(options.BuyMethods) ? options.BuyMethods : []);
    const bodyPlanes = Array.isArray(options.bodyPlanes) ? options.bodyPlanes : (Array.isArray(options.BodyPlanes) ? options.BodyPlanes : []);

    populateSelectFromList("bomMeasureUnitFilter", measureUnits, currentBomMeasureUnit, "יחידת מידה");
    populateSelectFromList("bomWarehouseFilter", warehouses, currentBomWarehouse, "מחסן");
    populateSelectFromList("bomLevelFilter", bomLevels.map(v => String(v)), currentBomLevel, "רמת BOM");
    populateHasChildSelect(hasChildOptions, "סטטוס ילדים");
    populateSelectFromList("bomBuyMethodFilter", buyMethods, currentBomBuyMethod, "שיטת רכישה");
    populateSelectFromList("bomBodyPlaneFilter", bodyPlanes, currentBomBodyPlane, "כל גוף/כנף");
}

function populateSelectFromList(selectId, values, selectedValue, placeholderText) {
    const select = document.getElementById(selectId);
    if (!select) return;

    select.innerHTML = `<option value="" disabled>${escapeHtml(placeholderText)}</option><option value="all">הכל</option>`;
    values
        .map(v => String(v ?? "").trim())
        .filter(v => v !== "")
        .forEach(value => {
            select.insertAdjacentHTML("beforeend", `<option value="${escapeHtml(value)}">${escapeHtml(value)}</option>`);
        });

    const normalizedValues = values.map(v => String(v ?? "").trim());
    select.value = normalizedValues.includes(selectedValue) ? selectedValue : "";
}

function populateHasChildSelect(values, placeholderText) {
    const select = document.getElementById("bomHasChildFilter");
    if (!select) return;

    select.innerHTML = `<option value="" disabled>${escapeHtml(placeholderText)}</option><option value="all">הכל</option>`;

    const normalized = values
        .map(v => String(v).toLowerCase())
        .map(v => (v === "true" || v === "1") ? "true" : ((v === "false" || v === "0") ? "false" : ""))
        .filter(v => v !== "");

    const unique = [...new Set(normalized)];
    if (unique.includes("true")) select.insertAdjacentHTML("beforeend", '<option value="true">כן</option>');
    if (unique.includes("false")) select.insertAdjacentHTML("beforeend", '<option value="false">לא</option>');

    select.value = unique.includes(currentBomHasChild) ? currentBomHasChild : "";
    currentBomHasChild = select.value;
}

function readFilterValue(selectId) {
    const select = document.getElementById(selectId);
    if (!select) return "";

    const value = (select.value || "").trim();
    if (value === "" || value.toLowerCase() === "all") {
        select.value = "";
        return "";
    }

    return value;
}

function resetBomPaging() {
    currentBomPage = 1;
    lastLoadedBomCount = 0;
    knownLastBomPage = null;
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
