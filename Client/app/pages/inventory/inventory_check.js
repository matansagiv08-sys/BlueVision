let currentCheckMode = "uav";
let bomPlaneTypeOptions = [];

window.initInventoryCheck = function () {
    const app = document.getElementById("app");
    if (app) {
        app.style.background = "transparent";
        app.style.boxShadow = "none";
    }

    loadBomPlaneTypes();
};

function loadBomPlaneTypes() {
    ajaxCall(
        "GET",
        "https://localhost:7296/api/Bom/planes",
        null,
        function (data) {
            bomPlaneTypeOptions = Array.isArray(data)
                ? data
                : (Array.isArray(data?.$values) ? data.$values : []);

            resetCheckPage();
        },
        function (xhr) {
            console.error("Failed to load BOM plane types", xhr);
            bomPlaneTypeOptions = [];
            resetCheckPage();
        }
    );
}

function resetCheckPage() {
    const container = document.getElementById("check-rows-container");
    if (container) {
        container.innerHTML = "";
        window.addNewCheckRow();
    }
    updateCalculateButton();
}

window.switchCheckMode = function (mode) {
    currentCheckMode = mode;

    document.querySelectorAll(".slider-btn").forEach(btn => btn.classList.remove("active"));
    document.getElementById(`mode-${mode}`)?.classList.add("active");

    updateAddRowButtonText();
};

function updateAddRowButtonText() {
    const addBtn = document.querySelector(".btn-add-row");
    if (!addBtn) return;
    addBtn.innerText = currentCheckMode === "uav" ? "הוסף כטב\"ם נוסף" : "הוסף גוף נוסף";
}

window.addNewCheckRow = function () {
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

            <button class="btn-delete-row" onclick="window.removeRow('${rowId}')" title="מחק שורה">
                🗑️
            </button>
        </div>
    `;

    container.insertAdjacentHTML("beforeend", html);
    updateAddRowButtonText();
    updateCalculateButton();
};

window.removeRow = function (id) {
    document.getElementById(id)?.remove();
    updateCalculateButton();
};

window.validateRow = function (rowId) {
    const row = document.getElementById(rowId);
    if (!row) return;

    const planeValue = row.querySelector(".check-plane")?.value || "";
    const qtyRaw = row.querySelector(".check-qty")?.value || "";
    const qty = Number(qtyRaw);

    const isComplete = planeValue !== "" && Number.isInteger(qty) && qty > 0;
    row.classList.toggle("row-complete", isComplete);
    updateCalculateButton();
};

function updateCalculateButton() {
    const btn = document.getElementById("btn-calculate");
    const rows = document.querySelectorAll(".check-row-card");
    const completeRows = document.querySelectorAll(".check-row-card.row-complete");

    if (btn) {
        btn.disabled = !(rows.length > 0 && rows.length === completeRows.length);
    }
}

window.navigateToResults = function () {
    const rows = document.querySelectorAll(".check-row-card");
    const requests = [];

    rows.forEach(row => {
        const planeTypeID = Number(row.querySelector(".check-plane")?.value || 0);
        const quantity = Number(row.querySelector(".check-qty")?.value || 0);

        if (planeTypeID > 0 && Number.isInteger(quantity) && quantity > 0) {
            requests.push({ planeTypeID, quantity });
        }
    });

    const payload = {
        mode: currentCheckMode,
        requests
    };

    sessionStorage.setItem("inventoryCheckPayload", JSON.stringify(payload));
    window.location.hash = "/inventory/inventory_check/results";
};

function escapeHtml(value) {
    return String(value)
        .replace(/&/g, "&amp;")
        .replace(/</g, "&lt;")
        .replace(/>/g, "&gt;")
        .replace(/\"/g, "&quot;")
        .replace(/'/g, "&#39;");
}
