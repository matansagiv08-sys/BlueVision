let currentCheckMode = "uav";
let bomPlaneTypeOptions = [];
const INVENTORY_CHECK_STATE_KEY = "inventoryCheckFormState";

//When the page loads, initInventoryCheck() runs, commanded by app.js. It sets up the page and loads the plane types for the dropdowns.
window.initInventoryCheck = function () {
    const app = document.getElementById("app");
    if (app) {
        app.style.background = "transparent";
        app.style.boxShadow = "none";
    }

    loadBomPlaneTypes();
};

//Fetches plane types from the server and stores them for use in dropdowns.
//ajaxCall(method, url, data, successCallback, errorCallback)
function loadBomPlaneTypes() {
    ajaxCall(
        "GET",
        "https://localhost:7296/api/Bom/planes",
        null,
        function (data) {
            bomPlaneTypeOptions = Array.isArray(data)
                ? data
                : (Array.isArray(data?.$values) ? data.$values : []);

            restoreCheckState();
        },
        function (xhr) {
            console.error("Failed to load BOM plane types", xhr);
            bomPlaneTypeOptions = [];
            restoreCheckState();
        }
    );
}

//Clears all rows and creates a fresh starting row.
function resetCheckPage() {
    const container = document.getElementById("check-rows-container");
    if (container) {
        container.innerHTML = "";
        window.addNewCheckRow();
    }
    updateCalculateButton();
}

function restoreCheckState() {
    const savedState = readSavedState();
    const container = document.getElementById("check-rows-container");
    if (!container) return;

    container.innerHTML = "";

    if (savedState?.mode === "uav" || savedState?.mode === "body") {
        currentCheckMode = savedState.mode;
    } else {
        currentCheckMode = "uav";
    }

    document.querySelectorAll(".slider-btn").forEach(btn => btn.classList.remove("active"));
    document.getElementById(`mode-${currentCheckMode}`)?.classList.add("active");

    const rows = Array.isArray(savedState?.rows) && savedState.rows.length > 0
        ? savedState.rows
        : [{ planeTypeID: "", quantity: "" }];

    rows.forEach(row => {
        window.addNewCheckRow({
            planeTypeID: row?.planeTypeID ?? "",
            quantity: row?.quantity ?? "",
            isHighPriority: row?.isHighPriority ?? row?.IsHighPriority ?? false
        });
    });

    updateAddRowButtonText();
    updateCalculateButton();
}

//Switches between UAV/body mode and updates UI accordingly.
window.switchCheckMode = function (mode) {
    currentCheckMode = mode;

    document.querySelectorAll(".slider-btn").forEach(btn => btn.classList.remove("active"));
    document.getElementById(`mode-${mode}`)?.classList.add("active");

    updateAddRowButtonText();
    saveCheckState();
};

//Changes the “add row” button text based on the selected mode.
function updateAddRowButtonText() {
    const addBtn = document.querySelector(".btn-add-row");
    if (!addBtn) return;
    addBtn.innerText = currentCheckMode === "uav" ? "הוסף כטב\"ם נוסף" : "הוסף גוף נוסף";
}

//Creates and adds a new input row (plane type + quantity).
window.addNewCheckRow = function (prefill = null) {
    const container = document.getElementById("check-rows-container");
    if (!container) return;

    const rowId = `row-${Math.random().toString(36).slice(2, 11)}`;
    const typeLabel = currentCheckMode === "uav" ? "סוג כטב\"ם" : "סוג גוף";

    const optionsHtml = bomPlaneTypeOptions
        .map(option => {
            const id = option.planeTypeID ?? option.PlaneTypeID;
            const name = option.planeTypeName ?? option.PlaneTypeName ?? id;
            return `<option value="${id}">${escapeHtml(String(name))}</option>`;
        })
        .join("");

    const html = `
        <div class="generic-card check-row-card" id="${rowId}">
            <div class="input-group">
                <label>${typeLabel}</label>
                <select class="check-input check-plane" onchange="window.validateRow('${rowId}')">
                    <option value="" disabled selected>בחר פלטפורמה...</option>
                    ${optionsHtml}
                </select>
            </div>

            <div class="input-group">
                <label>כמות מבוקשת</label>
                <input type="number" class="check-input check-qty" placeholder="0" min="1" step="1" oninput="window.validateRow('${rowId}')">
            </div>

            <div class="input-group priority-group">
                <label class="priority-checkbox-label">
                    <input type="checkbox" class="check-priority" onchange="window.saveCheckInputsState()">
                    עדיפות גבוהה
                </label>
            </div>

            <button class="btn-delete-row" onclick="window.removeRow('${rowId}')" title="מחק שורה">
                🗑️
            </button>
        </div>
    `;

    container.insertAdjacentHTML("beforeend", html);

    const row = document.getElementById(rowId);
    const planeSelect = row?.querySelector(".check-plane");
    const qtyInput = row?.querySelector(".check-qty");
    const priorityInput = row?.querySelector(".check-priority");
    if (planeSelect && prefill?.planeTypeID) {
        planeSelect.value = String(prefill.planeTypeID);
    }
    if (qtyInput && prefill?.quantity !== undefined && prefill?.quantity !== null && String(prefill.quantity) !== "") {
        qtyInput.value = String(prefill.quantity);
    }
    if (priorityInput) {
        const restoredPriority =
            prefill?.isHighPriority === true
            || prefill?.IsHighPriority === true
            || String(prefill?.isHighPriority ?? prefill?.IsHighPriority ?? "").toLowerCase() === "true";

        priorityInput.checked = restoredPriority;
    }

    window.validateRow(rowId);
    updateAddRowButtonText();
    updateCalculateButton();
    saveCheckState();
};

//Deletes a specific row from the page.
window.removeRow = function (id) {
    document.getElementById(id)?.remove();
    if (document.querySelectorAll(".check-row-card").length === 0) {
        window.addNewCheckRow();
    }
    updateCalculateButton();
    saveCheckState();
};

//Checks if a row has valid inputs and marks it as complete if valid.
window.validateRow = function (rowId) {
    const row = document.getElementById(rowId);
    if (!row) return;

    const planeValue = row.querySelector(".check-plane")?.value || "";
    const qtyRaw = row.querySelector(".check-qty")?.value || "";
    const qty = Number(qtyRaw);

    const isComplete = planeValue !== "" && Number.isInteger(qty) && qty > 0;
    row.classList.toggle("row-complete", isComplete);
    updateCalculateButton();
    saveCheckState();
};

//Enables the calculate button only if all rows are complete and there is at least one row.
function updateCalculateButton() {
    const btn = document.getElementById("btn-calculate");
    const rows = document.querySelectorAll(".check-row-card");
    const completeRows = document.querySelectorAll(".check-row-card.row-complete");

    if (btn) {
        btn.disabled = !(rows.length > 0 && rows.length === completeRows.length);
    }
}

//Gathers all input data, saves it to sessionStorage, and navigates to the results page.
window.navigateToResults = function () {
    const rows = document.querySelectorAll(".check-row-card");
    const requests = [];

    rows.forEach(row => {
        const planeTypeID = Number(row.querySelector(".check-plane")?.value || 0);
        const quantity = Number(row.querySelector(".check-qty")?.value || 0);
        const isHighPriority = !!row.querySelector(".check-priority")?.checked;

        if (planeTypeID > 0 && Number.isInteger(quantity) && quantity > 0) {
            requests.push({ planeTypeID, quantity, isHighPriority });
        }
    });

    //Wraps everything in a payload, for example mode: Body, requests: [{planeTypeID: 1, quantity: 5}, ...]
    const payload = {
        mode: currentCheckMode,
        requests
    };

    saveCheckState();

    //Saves the payload in sessionStorage as a JSON string so the results page can retrieve and send it to the server.
    sessionStorage.setItem("inventoryCheckPayload", JSON.stringify(payload));
    window.location.hash = "/inventory/inventory_check/results";
};

window.clearInventoryCheck = function () {
    sessionStorage.removeItem(INVENTORY_CHECK_STATE_KEY);
    sessionStorage.removeItem("inventoryCheckPayload");
    currentCheckMode = "uav";
    resetCheckPage();
    document.querySelectorAll(".slider-btn").forEach(btn => btn.classList.remove("active"));
    document.getElementById("mode-uav")?.classList.add("active");
    updateAddRowButtonText();
};

window.saveCheckInputsState = function () {
    saveCheckState();
};

function collectCheckRows() {
    const rows = [];
    document.querySelectorAll(".check-row-card").forEach(row => {
        const planeTypeID = row.querySelector(".check-plane")?.value || "";
        const quantity = row.querySelector(".check-qty")?.value || "";
        const isHighPriority = !!row.querySelector(".check-priority")?.checked;
        rows.push({
            planeTypeID: String(planeTypeID),
            quantity: String(quantity),
            isHighPriority
        });
    });
    return rows;
}

function saveCheckState() {
    const state = {
        mode: currentCheckMode,
        rows: collectCheckRows()
    };
    sessionStorage.setItem(INVENTORY_CHECK_STATE_KEY, JSON.stringify(state));
}

function readSavedState() {
    const raw = sessionStorage.getItem(INVENTORY_CHECK_STATE_KEY);
    if (!raw) return null;

    try {
        return JSON.parse(raw);
    } catch {
        return null;
    }
}

//Simple function to escape HTML special characters to prevent injection issues.
function escapeHtml(value) {
    return String(value)
        .replace(/&/g, "&amp;")
        .replace(/</g, "&lt;")
        .replace(/>/g, "&gt;")
        .replace(/\"/g, "&quot;")
        .replace(/'/g, "&#39;");
}
