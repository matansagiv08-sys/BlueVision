let currentResultsData = [];

window.initInventoryResults = function () {
    const payloadText = sessionStorage.getItem("inventoryCheckPayload");
    if (!payloadText) {
        renderResultsTable([]);
        updateSummaryCards([]);
        populatePlatformFilter([]);
        return;
    }

    let payload;
    try {
        payload = JSON.parse(payloadText);
    } catch {
        payload = null;
    }

    if (!payload || !Array.isArray(payload.requests) || payload.requests.length === 0) {
        renderResultsTable([]);
        updateSummaryCards([]);
        populatePlatformFilter([]);
        return;
    }

    ajaxCall(
        "POST",
        "https://localhost:7296/api/InventoryCheck/calculate",
        JSON.stringify(payload),
        //success callback
        function (data) {
            currentResultsData = Array.isArray(data?.items)
                ? data.items
                : (Array.isArray(data?.Items) ? data.Items : []);

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
            applyResultsView(totalShortageItems, totalShortageUnits, totalEstimatedCost);
        },
        function (xhr) {
            console.error("Failed to calculate inventory shortages", xhr);
            currentResultsData = [];
            renderResultsTable([]);
            updateSummaryCards([]);
            populatePlatformFilter([]);
        }
    );
};

function renderResultsTable(data) {
    const tbody = document.getElementById("results-table-body");
    if (!tbody) return;

    tbody.innerHTML = data.map(item => {
        const sku = item.inventoryItemID ?? item.InventoryItemID ?? "";
        const desc = item.itemName ?? item.ItemName ?? "";
        const current = item.totalStock ?? item.TotalStock ?? 0;
        const req = item.requiredQty ?? item.RequiredQty ?? 0;
        const shortage = item.shortageQty ?? item.ShortageQty ?? 0;
        const supplier = item.supplierName ?? item.SupplierName ?? "";
        const price = item.price ?? item.Price;
        const measureUnitRaw = item.measureUnit ?? item.MeasureUnit ?? "";
        const measureUnit = String(measureUnitRaw).trim() === "" ? "each" : measureUnitRaw;
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
                <td>${escapeHtml(displayOrDash(measureUnit))}</td>
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
    if (readyEl) readyEl.innerText = "-";
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
