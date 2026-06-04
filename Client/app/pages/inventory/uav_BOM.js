let bomPlaneOptions = [];
let currentBomRows = [];
let currentBomTreeRows = [];
let currentBomTreeRoots = [];
let bomExpandedNodeKeys = new Set();

let currentBomPlaneTypeId = null;
let currentBomPage = 1;
const bomPageSize = 100;
let lastLoadedBomCount = 0;
let knownLastBomPage = null;
let currentBomViewMode = "tree";

let currentBomSearch = "";
let currentBomMeasureUnit = "";
let currentBomWarehouse = "";
let currentBomLevel = "";
let currentBomHasChild = "";
let currentBomBuyMethod = "";
let currentBomBodyPlane = "";

window.initUavBOM = function () {
    checkAndRunInventoryImport(function () {
        loadBomPlaneOptions();
    }, {
        onImportStart: showImportSpinner,
        onImportEnd: hideImportSpinner
    });
};

function loadBomPlaneOptions() {
    ajaxCall("GET", "https://localhost:7296/api/Bom/planes", null,
        function (data) {
            bomPlaneOptions = Array.isArray(data) ? data : (Array.isArray(data?.$values) ? data.$values : []);

            if (bomPlaneOptions.length === 0) {
                currentBomRows = [];
                currentBomTreeRows = [];
                currentBomTreeRoots = [];
                renderBomPlaneButtons();
                renderBomTreeTable();
                return;
            }

            if (!bomPlaneOptions.some(option => String(option.planeTypeID ?? option.PlaneTypeID) === String(currentBomPlaneTypeId))) {
                currentBomPlaneTypeId = String(bomPlaneOptions[0].planeTypeID ?? bomPlaneOptions[0].PlaneTypeID);
            }

            renderBomPlaneButtons();
            loadBomFilterOptions(function () {
                resetBomPaging();
                refreshBomViewData();
            });
        },
        function (xhr) {
            console.error("Failed to load BOM plane options", xhr);
            bomPlaneOptions = [];
            currentBomRows = [];
            currentBomTreeRows = [];
            currentBomTreeRoots = [];
            renderBomPlaneButtons();
            renderBomTreeTable();
        }
    );
}

function refreshBomViewData() {
    loadBomTreeData();
}

function loadBomFilterOptions(onDone) {
    const params = new URLSearchParams();
    if (currentBomPlaneTypeId) params.set("planeTypeId", currentBomPlaneTypeId);

    ajaxCall("GET", `https://localhost:7296/api/Bom/filter-options?${params.toString()}`, null,
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

function loadBomPage(page, done) {
    if (!currentBomPlaneTypeId || page < 1) return;
    if (knownLastBomPage !== null && page > knownLastBomPage) return;

    const params = buildBomQueryParams({ page, pageSize: bomPageSize, includeSearch: true, treeMode: false });

    ajaxCall("GET", `https://localhost:7296/api/Bom?${params.toString()}`, null,
        function (data) {
            currentBomRows = Array.isArray(data) ? data : (Array.isArray(data?.$values) ? data.$values : []);
            currentBomPage = page;
            lastLoadedBomCount = currentBomRows.length;
            if (lastLoadedBomCount < bomPageSize) {
                knownLastBomPage = currentBomPage;
            } else if (currentBomPage === 1) {
                knownLastBomPage = null;
            }

            renderBomTable(currentBomRows);
            updateBomPager();
            if (typeof done === "function") done(false);
        },
        function (xhr) {
            console.error("Failed to load BOM rows", xhr);
            currentBomRows = [];
            lastLoadedBomCount = 0;
            renderBomTable([]);
            updateBomPager();
            if (typeof done === "function") done(true);
        }
    );
}

function loadBomTreeData() {
    if (!currentBomPlaneTypeId) return;
    bomExpandedNodeKeys = new Set();

    // In tree mode we fetch full filtered sequence (without text search) to preserve ancestry context.
    const params = buildBomQueryParams({ page: 1, pageSize: 100, includeSearch: false, treeMode: true });

    ajaxCall("GET", `https://localhost:7296/api/Bom?${params.toString()}`, null,
        function (data) {
            const rows = Array.isArray(data) ? data : (Array.isArray(data?.$values) ? data.$values : []);
            currentBomTreeRows = rows.slice().sort((a, b) => getNumeric(a, "RowOrder") - getNumeric(b, "RowOrder"));
            currentBomTreeRoots = buildBomTreeByRowOrderAndLevel(currentBomTreeRows);
            renderBomTreeTable();
        },
        function (xhr) {
            console.error("Failed to load BOM tree rows", xhr);
            currentBomTreeRows = [];
            currentBomTreeRoots = [];
            renderBomTreeTable();
        }
    );
}

function buildBomQueryParams({ page, pageSize, includeSearch, treeMode }) {
    const params = new URLSearchParams();
    params.set("page", String(page));
    params.set("pageSize", String(pageSize));
    params.set("treeMode", treeMode ? "true" : "false");
    params.set("planeTypeId", currentBomPlaneTypeId);

    if (includeSearch && currentBomSearch) params.set("search", currentBomSearch);
    if (currentBomMeasureUnit) params.set("measureUnit", currentBomMeasureUnit);
    if (currentBomWarehouse) params.set("warehouse", currentBomWarehouse);
    if (currentBomLevel) params.set("bomLevel", currentBomLevel);
    if (currentBomHasChild) params.set("hasChild", currentBomHasChild);
    if (currentBomBuyMethod) params.set("buyMethod", currentBomBuyMethod);
    if (currentBomBodyPlane) params.set("bodyPlane", currentBomBodyPlane);
    return params;
}

window.setBomViewMode = function (mode) {
    currentBomViewMode = "tree";

    const treeActions = document.getElementById("bomTreeActions");
    const table = document.getElementById("bomDataTable");
    if (treeActions) treeActions.style.display = "flex";
    table?.classList.add("tree-mode");

    resetBomPaging();
    refreshBomViewData();
};

window.selectBomPlane = function (planeTypeId, btn) {
    currentBomPlaneTypeId = String(planeTypeId);
    const container = btn?.closest(".model-selection-toggle") || document.getElementById("bomPlaneToggle");
    container?.querySelectorAll(".slider-btn").forEach(button => button.classList.remove("active"));
    btn?.classList.add("active");

    resetBomPaging();
    loadBomFilterOptions(function () {
        refreshBomViewData();
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
    refreshBomViewData();
};

window.clearBomFilters = function () {
    const ids = ["bomSearch", "bomMeasureUnitFilter", "bomWarehouseFilter", "bomLevelFilter", "bomHasChildFilter", "bomBuyMethodFilter", "bomBodyPlaneFilter"];
    ids.forEach(id => {
        const el = document.getElementById(id);
        if (!el) return;
        el.value = "";
    });
    window.filterBomTable();
};

window.prevBomPage = function () { if (currentBomPage > 1) loadBomPage(currentBomPage - 1); };
window.nextBomPage = function () { if (lastLoadedBomCount === bomPageSize) loadBomPage(currentBomPage + 1); };
window.goToBomPage = function (page) {
    if (page < 1 || page === currentBomPage) return;
    if (knownLastBomPage !== null && page > knownLastBomPage) return;
    loadBomPage(page);
};

window.toggleBomTreeNode = function (nodeKey) {
    if (bomExpandedNodeKeys.has(nodeKey)) bomExpandedNodeKeys.delete(nodeKey);
    else bomExpandedNodeKeys.add(nodeKey);
    renderBomTreeTable();
};

window.expandAllBomTree = function () {
    const allKeys = [];
    walkTree(currentBomTreeRoots, node => {
        if (node.children.length > 0) allKeys.push(node.key);
    });
    bomExpandedNodeKeys = new Set(allKeys);
    renderBomTreeTable();
};

window.collapseAllBomTree = function () {
    bomExpandedNodeKeys = new Set();
    renderBomTreeTable();
};

function buildBomTreeByRowOrderAndLevel(rows) {
    const roots = [];
    const stack = [];

    rows.forEach((row, idx) => {
        const level = normalizeBomLevel(getNumeric(row, "BomLevel"));
        const node = {
            key: getNodeKey(row, idx),
            row,
            level,
            depth: level,
            originalIndex: idx,
            children: [],
            parentKey: null
        };

        while (stack.length > 0 && stack[stack.length - 1].level >= level) {
            stack.pop();
        }

        if (stack.length === 0) {
            roots.push(node);
        } else {
            const parent = stack[stack.length - 1];
            parent.children.push(node);
            node.parentKey = parent.key;
        }

        stack.push(node);
    });

    return roots;
}

function renderBomTreeTable() {
    const tbody = document.getElementById("bom-table-body");
    if (!tbody) return;
    document.getElementById("bomDataTable")?.classList.add("tree-mode");

    const searchText = currentBomSearch.trim().toLowerCase();
    const includeKeys = collectIncludedTreeKeys(currentBomTreeRoots, searchText);
    const autoExpandedKeys = collectAncestorKeys(currentBomTreeRoots, includeKeys);

    const lines = [];
    flattenVisibleTreeRows(currentBomTreeRoots, lines, includeKeys, autoExpandedKeys);

    tbody.innerHTML = lines.map(({ node }) => {
        const item = node.row;
        const nodeHasChildren = node.children.length > 0;
        const nodeExpanded = bomExpandedNodeKeys.has(node.key) || autoExpandedKeys.has(node.key);
        const indent = Math.max(0, node.level - 1) * 18;
        const arrow = nodeHasChildren ? (nodeExpanded ? "▾" : "▸") : "";
        const childCount = nodeHasChildren ? `<span class="bom-tree-child-count">${node.children.length}</span>` : "";
        const itemId = displayOrDash(readValue(item, "InventoryItemID"));
        const qty = displayOrDash(readValue(item, "Quantity"));
        const unit = displayOrDash(readValue(item, "MeasureUnit"));
        const buyMethod = displayOrDash(readValue(item, "BuyMethod"));
        const warehouse = displayOrDash(readValue(item, "Warehouse"));
        const level = displayOrDash(readValue(item, "BomLevel"));

        return `
            <tr class="${nodeHasChildren ? "bom-tree-parent-row" : "bom-tree-leaf-row"}" data-depth="${node.level}">
                <td class="col-sku bom-tree-chip-cell"><span class="bom-tree-chip">${itemId}</span></td>
                <td>
                    <div class="bom-tree-item-cell" style="--tree-indent:${indent}px;">
                        <span class="bom-tree-branch-line"></span>
                        ${nodeHasChildren
                            ? `<button class="bom-tree-arrow" onclick="window.toggleBomTreeNode('${escapeHtml(node.key)}')">${arrow}</button>`
                            : '<span class="bom-tree-arrow-spacer"></span>'}
                        ${nodeHasChildren
                            ? `<span class="bom-tree-node-icon bom-tree-node-icon-parent" aria-hidden="true">
                                    <svg viewBox="0 0 24 24" fill="none" xmlns="http://www.w3.org/2000/svg">
                                        <path d="M4 6.5H10L12 9H20V17.5H4V6.5Z" stroke="currentColor" stroke-width="1.8" stroke-linejoin="round"/>
                                        <path d="M8 13H16" stroke="currentColor" stroke-width="1.8" stroke-linecap="round"/>
                                        <path d="M12 10V16" stroke="currentColor" stroke-width="1.8" stroke-linecap="round"/>
                                    </svg>
                               </span>`
                            : '<span class="bom-tree-node-icon bom-tree-node-icon-leaf" aria-hidden="true"></span>'}
                        <span class="bom-tree-name" title="${escapeHtml(displayOrDash(readValue(item, "ItemName")))}">${displayOrDash(readValue(item, "ItemName"))}</span>
                        ${childCount}
                    </div>
                </td>
                <td class="bom-tree-chip-cell"><span class="bom-tree-chip">כמות ${qty}</span></td>
                <td class="bom-tree-chip-cell"><span class="bom-tree-chip">${unit}</span></td>
                <td class="bom-tree-chip-cell"><span class="bom-tree-chip">${warehouse}</span></td>
                <td class="bom-tree-chip-cell"><span class="bom-tree-chip">רמה ${level}</span></td>
                <td class="bom-tree-chip-cell"><span class="bom-tree-chip">${buyMethod}</span></td>
                <td class="bom-tree-chip-cell"><span class="bom-tree-chip">${displayOrDash(readValue(item, "BodyPlane"))}</span></td>
            </tr>`;
    }).join("");
}

function flattenVisibleTreeRows(nodes, out, includeKeys, autoExpandedKeys) {
    nodes.forEach(node => {
        if (includeKeys && !includeKeys.has(node.key)) return;
        out.push({ node });
        const isExpanded = bomExpandedNodeKeys.has(node.key) || autoExpandedKeys.has(node.key);
        if (isExpanded && node.children.length > 0) {
            flattenVisibleTreeRows(node.children, out, includeKeys, autoExpandedKeys);
        }
    });
}

function collectIncludedTreeKeys(roots, searchText) {
    if (!searchText) return null;
    const include = new Set();

    const dfs = (node, ancestors) => {
        const row = node.row;
        const searchable = `${readValue(row, "InventoryItemID") || ""} ${readValue(row, "ItemName") || ""}`.toLowerCase();
        const selfMatch = searchable.includes(searchText);
        let childMatch = false;
        node.children.forEach(ch => { if (dfs(ch, [...ancestors, node])) childMatch = true; });

        if (selfMatch || childMatch) {
            include.add(node.key);
            ancestors.forEach(a => include.add(a.key));
            return true;
        }
        return false;
    };

    roots.forEach(root => dfs(root, []));
    return include;
}

function collectAncestorKeys(roots, includeKeys) {
    const autoExpanded = new Set();
    if (!includeKeys) return autoExpanded;
    const dfs = (node) => {
        let hasIncludedDescendant = false;
        node.children.forEach(child => {
            if (dfs(child)) hasIncludedDescendant = true;
        });
        if (hasIncludedDescendant && includeKeys.has(node.key)) autoExpanded.add(node.key);
        return includeKeys.has(node.key);
    };
    roots.forEach(root => dfs(root));
    return autoExpanded;
}

function walkTree(nodes, visitor) {
    nodes.forEach(node => {
        visitor(node);
        if (node.children.length > 0) walkTree(node.children, visitor);
    });
}

function getNodeKey(row, idx) {
    const id = readValue(row, "BomSerialID");
    if (id !== null && id !== undefined && String(id).trim() !== "") return `bom-${String(id)}`;
    return `row-${readValue(row, "InventoryItemID") || ""}-${getNumeric(row, "RowOrder")}-${idx}`;
}

function normalizeBomLevel(level) { return level > 0 ? level : 1; }
function getNumeric(obj, key) {
    const value = readValue(obj, key);
    const num = Number(value);
    return Number.isFinite(num) ? num : 0;
}

function readValue(obj, key) { return obj?.[key.charAt(0).toLowerCase() + key.slice(1)] ?? obj?.[key]; }

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

function updateBomPager() {
    const prevBtn = document.getElementById("prevBomPageBtn");
    const nextBtn = document.getElementById("nextBomPageBtn");
    const numbersWrap = document.getElementById("bomPageNumbers");
    const hasNext = lastLoadedBomCount === bomPageSize;
    const lastPageForUi = knownLastBomPage ?? (hasNext ? currentBomPage + 1 : currentBomPage);

    if (prevBtn) prevBtn.disabled = currentBomPage <= 1;
    if (nextBtn) nextBtn.disabled = knownLastBomPage !== null ? currentBomPage >= knownLastBomPage : !hasNext;
    if (!numbersWrap) return;
    if (currentBomViewMode === "tree") {
        numbersWrap.innerHTML = "";
        return;
    }

    const pages = new Set([1, lastPageForUi]);
    for (let page = currentBomPage - 2; page <= currentBomPage + 2; page++) {
        if (page >= 1 && page <= lastPageForUi) pages.add(page);
    }
    const sortedPages = [...pages].sort((a, b) => a - b);

    let html = "";
    let previous = 0;
    sortedPages.forEach(page => {
        if (page - previous > 1) html += '<span class="inventory-page-ellipsis">...</span>';
        const activeClass = page === currentBomPage ? " is-active" : "";
        html += `<button class="inventory-page-number${activeClass}" onclick="window.goToBomPage(${page})">${page}</button>`;
        previous = page;
    });
    numbersWrap.innerHTML = html;
}

function renderBomTable(data) {
    const tbody = document.getElementById("bom-table-body");
    if (!tbody) return;
    document.getElementById("bomDataTable")?.classList.remove("tree-mode");
    tbody.innerHTML = data.map(item => {
        return `
            <tr>
                <td class="col-sku">${displayOrDash(readValue(item, "InventoryItemID"))}</td>
                <td>${displayOrDash(readValue(item, "ItemName"))}</td>
                <td>${displayOrDash(readValue(item, "Quantity"))}</td>
                <td>${displayOrDash(readValue(item, "MeasureUnit"))}</td>
                <td>${displayOrDash(readValue(item, "Warehouse"))}</td>
                <td>${displayOrDash(readValue(item, "BomLevel"))}</td>
                <td>${displayOrDash(readValue(item, "BuyMethod"))}</td>
                <td>${displayOrDash(readValue(item, "BodyPlane"))}</td>
            </tr>`;
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
    values.map(v => String(v ?? "").trim()).filter(v => v !== "")
        .forEach(value => select.insertAdjacentHTML("beforeend", `<option value="${escapeHtml(value)}">${escapeHtml(value)}</option>`));
    const normalizedValues = values.map(v => String(v ?? "").trim());
    select.value = normalizedValues.includes(selectedValue) ? selectedValue : "";
}

function populateHasChildSelect(values, placeholderText) {
    const select = document.getElementById("bomHasChildFilter");
    if (!select) return;
    select.innerHTML = `<option value="" disabled>${escapeHtml(placeholderText)}</option><option value="all">הכל</option>`;
    const normalized = values.map(v => String(v).toLowerCase())
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
