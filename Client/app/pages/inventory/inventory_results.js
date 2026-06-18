(() => {
let currentResultsData = [];
let currentReadyToProduceRows = [];
let hasInventoryCalculationResult = false;

window.initInventoryResults = function () {
    setInventoryResultsLoading(true);
    setInventoryResultsInlineError("");

    const payloadText = sessionStorage.getItem("inventoryCheckPayload");
    if (!payloadText) {
        currentReadyToProduceRows = [];
        hasInventoryCalculationResult = false;
        renderResultsTable([]);
        updateSummaryCards([]);
        populatePlatformFilter([]);
        updateShortageSectionsVisibility();
        setInventoryResultsLoading(false);
        return;
    }

    let payload;
    try {
        payload = JSON.parse(payloadText);
    } catch {
        payload = null;
    }

    if (!payload || !Array.isArray(payload.requests) || payload.requests.length === 0) {
        currentReadyToProduceRows = [];
        hasInventoryCalculationResult = false;
        renderResultsTable([]);
        updateSummaryCards([]);
        populatePlatformFilter([]);
        updateShortageSectionsVisibility();
        setInventoryResultsLoading(false);
        return;
    }

    ajaxCall(
        "POST",
        "https://localhost:7296/api/InventoryCheck/calculate",
        JSON.stringify(payload),
        function (data) {
            currentResultsData = Array.isArray(data?.items)
                ? data.items
                : (Array.isArray(data?.Items) ? data.Items : []);
            currentReadyToProduceRows = Array.isArray(data?.readyToProduceRows)
                ? data.readyToProduceRows
                : (Array.isArray(data?.ReadyToProduceRows) ? data.ReadyToProduceRows : []);
            hasInventoryCalculationResult = true;

            const totalShortageItems = data?.totalShortageItems ?? data?.TotalShortageItems ?? currentResultsData.length;
            const totalShortageUnits = data?.totalShortageUnits ?? data?.TotalShortageUnits ?? currentResultsData.reduce((sum, item) => sum + Number(item.shortageQty ?? item.ShortageQty ?? 0), 0);
            const totalEstimatedCost = data?.totalEstimatedCost ?? data?.TotalEstimatedCost ?? currentResultsData.reduce((sum, item) => {
                const shortage = Number(item.shortageQty ?? item.ShortageQty ?? 0);
                const price = Number(item.price ?? item.Price ?? 0);
                return sum + (shortage * price);
            }, 0);

            populatePlatformFilter(currentResultsData);
            const sortSelect = document.getElementById("shortageSortFilter");
            if (sortSelect) sortSelect.value = "default";
            updateShortageSectionsVisibility();
            applyResultsView(totalShortageItems, totalShortageUnits, totalEstimatedCost);
            setInventoryResultsLoading(false);
        },
        function (xhr) {
            console.error("Failed to calculate inventory shortages", xhr);
            currentResultsData = [];
            currentReadyToProduceRows = [];
            hasInventoryCalculationResult = false;
            renderResultsTable([]);
            updateSummaryCards([]);
            populatePlatformFilter([]);
            updateShortageSectionsVisibility();
            setInventoryResultsInlineError("חישוב המלאי נכשל, נסה שוב.");
            setInventoryResultsLoading(false);
        }
    );
};

function setInventoryResultsLoading(isLoading) {
    const content = document.getElementById("stockChildContent");
    const loadingText = document.getElementById("inventoryResultsLoadingText");
    if (content) content.hidden = isLoading;
    if (loadingText) loadingText.hidden = !isLoading;
}

function setInventoryResultsInlineError(message) {
    const errorEl = document.getElementById("inventoryResultsInlineError");
    if (!errorEl) return;
    errorEl.textContent = message || "";
    errorEl.hidden = !message;
}

function renderResultsTable(data) {
    const tbody = document.getElementById("results-table-body");
    if (!tbody) return;

    tbody.innerHTML = data.map(item => {
        const sku = item.inventoryItemID ?? item.InventoryItemID ?? "";
        const desc = item.itemName ?? item.ItemName ?? "";
        const current = item.totalStock ?? item.TotalStock ?? 0;
        const openPurchaseRequestQty = item.openPurchaseRequestQty ?? item.OpenPurchaseRequestQty ?? 0;
        const openPurchaseOrderQty = item.openPurchaseOrderQty ?? item.OpenPurchaseOrderQty ?? 0;
        const approvedOrderQty = item.approvedOrderQty ?? item.ApprovedOrderQty ?? 0;
        const unapprovedOrderQty = item.unapprovedOrderQty ?? item.UnapprovedOrderQty ?? 0;
        const req = item.requiredQty ?? item.RequiredQty ?? 0;
        const shortage = item.shortageQty ?? item.ShortageQty ?? 0;
        const supplier = item.supplierName ?? item.SupplierName ?? "";
        const price = item.price ?? item.Price;
        const measureUnit = formatUnitOfMeasure(item.measureUnit ?? item.MeasureUnit);
        const platforms = item.contributingPlaneTypes ?? item.ContributingPlaneTypes ?? "";
        const shared = (item.isSharedAcrossPlanes ?? item.IsSharedAcrossPlanes) === true;
        const sharedClass = shared ? " shared-shortage-row" : "";
        const sharedBadge = buildSharedBadge(item, shared);

        return `
            <tr class="${sharedClass}">
                <td>${escapeHtml(displayOrDash(sku))}</td>
                <td>${escapeHtml(displayOrDash(desc))} ${sharedBadge}</td>
                <td>${displayNumber(current)}</td>
                <td>${displayNumber(req)}</td>
                <td class="shortage-cell">${displayNumber(shortage)}</td>
                <td>${escapeHtml(measureUnit)}</td>
                <td>${displayNumber(openPurchaseRequestQty)}</td>
                <td>${displayNumber(openPurchaseOrderQty)}</td>
                <td>${displayNumber(approvedOrderQty)}</td>
                <td>${displayNumber(unapprovedOrderQty)}</td>
                <td>${escapeHtml(displayOrDash(supplier))}</td>
                <td>${price === null || price === undefined ? "-" : Number(price).toLocaleString()}</td>
                <td>${escapeHtml(displayOrDash(platforms))}</td>
            </tr>
        `;
    }).join("");
}

function updateSummaryCards(data, totalItemsOverride, totalUnitsOverride, totalCostOverride) {
    const totalShortageCount = totalItemsOverride ?? data.length;
    const totalUnits = totalUnitsOverride ?? data.reduce((sum, item) => sum + Number(item.shortageQty ?? item.ShortageQty ?? 0), 0);
    const totalCost = totalCostOverride ?? data.reduce((sum, item) => {
        const shortage = Number(item.shortageQty ?? item.ShortageQty ?? 0);
        const price = Number(item.price ?? item.Price ?? 0);
        return sum + (shortage * price);
    }, 0);

    const totalShortageEl = document.getElementById("total-shortage");
    const totalItemsCountEl = document.getElementById("total-items-count");
    const totalCostEl = document.getElementById("total-cost");
    const readyEl = document.getElementById("ready-to-prod");

    if (totalShortageEl) totalShortageEl.innerText = String(totalShortageCount);
    if (totalItemsCountEl) totalItemsCountEl.innerText = `${displayNumber(totalUnits)} יחידות`;
    if (totalCostEl) totalCostEl.innerText = `₪ ${Number(totalCost).toLocaleString()}`;
    if (readyEl) renderReadyToProduceRows(readyEl, currentReadyToProduceRows);
}

function renderReadyToProduceRows(target, rows) {
    const readyRows = Array.isArray(rows) ? rows : [];
    if (readyRows.length === 0) {
        target.innerText = "-";
        return;
    }

    target.innerHTML = readyRows.map(row => {
        const readyQtyValue = Number(row.readyQty ?? row.ReadyQty ?? 0);
        const requestedQtyValue = Number(row.requestedQty ?? row.RequestedQty ?? 0);
        const readyQty = displayWholeNumber(readyQtyValue);
        const requestedQty = displayWholeNumber(requestedQtyValue);
        const planeName = row.planeTypeName ?? row.PlaneTypeName ?? row.planeTypeID ?? row.PlaneTypeID ?? "-";
        const status = getReadyStatus(readyQtyValue, requestedQtyValue);
        return `
            <div class="ready-to-prod-line ready-status-${status.key}">
                <span class="ready-to-prod-text">${readyQty}/${requestedQty} ${escapeHtml(displayOrDash(planeName))}</span>
                <span class="ready-status-badge">${status.label}</span>
            </div>
        `;
    }).join("");
}

function getReadyStatus(readyQty, requestedQty) {
    if (requestedQty > 0 && readyQty >= requestedQty) {
        return { key: "full", label: "מוכן" };
    }

    if (readyQty > 0 && readyQty < requestedQty) {
        return { key: "partial", label: "חלקי" };
    }

    return { key: "blocked", label: "לא מוכן" };
}

function updateShortageSectionsVisibility() {
    const hasShortages = Array.isArray(currentResultsData) && currentResultsData.length > 0;
    const filtersSection = document.getElementById("shortageFiltersSection");
    const tableSection = document.getElementById("shortageTableSection");
    const successState = document.getElementById("inventorySuccessState");
    const exportBtn = document.getElementById("inventoryResultsExportBtn");

    setSectionVisible(filtersSection, hasShortages);
    setSectionVisible(tableSection, hasShortages);
    if (successState) successState.hidden = hasShortages || !hasInventoryCalculationResult;
    if (exportBtn) {
        setSectionVisible(exportBtn, hasShortages);
        exportBtn.disabled = !hasShortages;
        exportBtn.title = hasShortages ? "" : "אין חוסרים לייצוא";
    }
}

function setSectionVisible(element, isVisible) {
    if (!element) return;
    element.hidden = !isVisible;
    element.classList.toggle("inventory-results-hidden", !isVisible);
}

function populatePlatformFilter(data) {
    const select = document.getElementById("platformFilter");
    if (!select) return;

    const names = new Set();
    data.forEach(item => {
        const raw = String(item.contributingPlaneTypes ?? item.ContributingPlaneTypes ?? "");
        raw.split(",").map(x => x.trim()).filter(Boolean).forEach(name => names.add(name));
    });

    select.innerHTML = '<option value="all">כל הפלטפורמות</option>';
    [...names].sort((a, b) => a.localeCompare(b)).forEach(name => {
        select.insertAdjacentHTML("beforeend", `<option value="${escapeHtml(name)}">${escapeHtml(name)}</option>`);
    });
}

window.filterResults = function () {
    applyResultsView();
};

function applyResultsView(totalItemsOverride, totalUnitsOverride, totalCostOverride) {
    const searchTerm = (document.getElementById("resultsSearch")?.value || "").toLowerCase().trim();
    const platform = document.getElementById("platformFilter")?.value || "all";
    const sortMode = document.getElementById("shortageSortFilter")?.value || "default";

    const filtered = currentResultsData.filter(item => {
        const sku = String(item.inventoryItemID ?? item.InventoryItemID ?? "").toLowerCase();
        const desc = String(item.itemName ?? item.ItemName ?? "").toLowerCase();
        const platforms = String(item.contributingPlaneTypes ?? item.ContributingPlaneTypes ?? "");

        const matchesSearch = sku.includes(searchTerm) || desc.includes(searchTerm);
        const matchesPlatform = platform === "all" || platforms.split(",").map(x => x.trim()).includes(platform);

        return matchesSearch && matchesPlatform;
    });

    let finalResults = [...filtered];
    if (sortMode === "shortageDesc") {
        finalResults.sort((a, b) => Number(b.shortageQty ?? b.ShortageQty ?? 0) - Number(a.shortageQty ?? a.ShortageQty ?? 0));
    } else if (sortMode === "shortageAsc") {
        finalResults.sort((a, b) => Number(a.shortageQty ?? a.ShortageQty ?? 0) - Number(b.shortageQty ?? b.ShortageQty ?? 0));
    }

    renderResultsTable(finalResults);
    updateSummaryCards(finalResults, totalItemsOverride, totalUnitsOverride, totalCostOverride);
}

window.backToCheck = function () {
    window.location.hash = "/inventory/inventory_check";
};

window.exportToExcel = function () {
    if (!Array.isArray(currentResultsData) || currentResultsData.length === 0) {
        return;
    }

    const headers = [
        "InventoryItemID",
        "ItemName",
        "TotalStock",
        "RequiredQty",
        "ShortageQty",
        "MeasureUnit",
        "OpenPurchaseRequestQty",
        "OpenPurchaseOrderQty",
        "ApprovedOrderQty",
        "UnapprovedOrderQty",
        "SupplierName",
        "Price",
        "ContributingPlaneTypes",
        "IsSharedAcrossPlanes"
    ];

    const lines = [headers.join(",")];
    currentResultsData.forEach(item => {
        const row = [
            item.inventoryItemID ?? item.InventoryItemID ?? "",
            item.itemName ?? item.ItemName ?? "",
            item.totalStock ?? item.TotalStock ?? "",
            item.requiredQty ?? item.RequiredQty ?? "",
            item.shortageQty ?? item.ShortageQty ?? "",
            item.measureUnit ?? item.MeasureUnit ?? "",
            item.openPurchaseRequestQty ?? item.OpenPurchaseRequestQty ?? 0,
            item.openPurchaseOrderQty ?? item.OpenPurchaseOrderQty ?? 0,
            item.approvedOrderQty ?? item.ApprovedOrderQty ?? 0,
            item.unapprovedOrderQty ?? item.UnapprovedOrderQty ?? 0,
            item.supplierName ?? item.SupplierName ?? "",
            item.price ?? item.Price ?? "",
            item.contributingPlaneTypes ?? item.ContributingPlaneTypes ?? "",
            item.isSharedAcrossPlanes ?? item.IsSharedAcrossPlanes ?? false
        ].map(value => `"${String(value).replace(/"/g, '""')}"`);
        lines.push(row.join(","));
    });

    const blob = new Blob(["\uFEFF" + lines.join("\n")], { type: "text/csv;charset=utf-8;" });
    const url = URL.createObjectURL(blob);
    const a = document.createElement("a");
    a.href = url;
    a.download = "inventory_check_shortages.csv";
    document.body.appendChild(a);
    a.click();
    document.body.removeChild(a);
    URL.revokeObjectURL(url);
};

function displayOrDash(value) {
    if (value === null || value === undefined) return "-";
    const text = String(value).trim();
    return text === "" ? "-" : text;
}

function displayNumber(value) {
    const num = Number(value);
    if (!Number.isFinite(num)) return "0";
    return num.toLocaleString(undefined, { maximumFractionDigits: 4 });
}

function displayWholeNumber(value) {
    const num = Number(value);
    if (!Number.isFinite(num)) return "0";
    return Math.trunc(num).toLocaleString();
}

function escapeHtml(value) {    
    return String(value)
        .replace(/&/g, "&amp;")
        .replace(/</g, "&lt;")
        .replace(/>/g, "&gt;")
        .replace(/\"/g, "&quot;")
        .replace(/'/g, "&#39;");
}

function buildSharedBadge(item, isShared) {
    if (!isShared) {
        return "";
    }

    const breakdownLines = getSharedBreakdownLines(item);
    if (breakdownLines.length === 0) {
        return '<span class="shared-badge">משותף</span>';
    }

    const linesHtml = breakdownLines
        .map(line => `<div class="shared-breakdown-line">${escapeHtml(line)}</div>`)
        .join("");

    return `
        <span class="shared-breakdown-wrapper">
            <span class="shared-badge">משותף</span>
            <span class="shared-breakdown-tooltip" role="tooltip" aria-hidden="true">
                ${linesHtml}
            </span>
        </span>
    `;
}

function getSharedBreakdownTooltip(item) {
    return getSharedBreakdownLines(item).join("\n");
}

function getSharedBreakdownLines(item) {
    const breakdown = item.shortageByPlane ?? item.ShortageByPlane;
    if (!breakdown || typeof breakdown !== "object") {
        return [];
    }

    const entries = Object.entries(breakdown);
    if (entries.length === 0) {
        return [];
    }

    return entries.map(([planeName, shortage]) => `${planeName}: חסר ${displayNumber(shortage)}`);
}
})();
