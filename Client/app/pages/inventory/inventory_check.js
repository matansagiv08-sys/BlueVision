let currentCheckMode = 'uav';

window.initInventoryCheck = function () {
    const app = document.getElementById("app");
    if (app) {
        app.style.background = "transparent";
        app.style.boxShadow = "none";
    }
    resetCheckPage();
};

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

    // עדכון ויזואלי של ה"סליידר"
    document.querySelectorAll('.slider-btn').forEach(btn => btn.classList.remove('active'));
    document.getElementById(`mode-${mode}`).classList.add('active');

    // עדכון כפתור ההוספה
    const addBtn = document.querySelector(".btn-add-row");
    if (addBtn) {
        addBtn.innerText = mode === 'uav' ? "הוסף כטב\"ם נוסף +" : "הוסף גוף נוסף +";
    }

    resetCheckPage();
};

window.addNewCheckRow = function () {
    const container = document.getElementById("check-rows-container");
    if (!container) return;

    const rowId = 'row-' + Math.random().toString(36).substr(2, 9);
    const typeLabel = currentCheckMode === 'uav' ? "סוג כטב\"ם" : "סוג גוף";

    const html = `
        <div class="generic-card check-row-card" id="${rowId}">
            <div class="input-group">
                <label>${typeLabel}</label>
                <select class="check-input" onchange="window.validateRow('${rowId}')">
                    <option value="" disabled selected>בחר פלטפורמה...</option>
                    <option>WanderB</option>
                    <option>ThunderB</option>
                    <option>Puma</option>
                </select>
            </div>

            <div class="input-group">
                <label>כמות מבוקשת</label>
                <input type="number" class="check-input" placeholder="0" min="1" 
                       oninput="window.validateRow('${rowId}')">
            </div>

            <div class="input-group">
                <label>עדיפות</label>
                <select class="check-input" onchange="window.validateRow('${rowId}')">
                    <option value="" disabled selected>בחר...</option>
                    <option value="1">1 (דחוף ביותר)</option>
                    <option value="2">2 (סטנדרטי)</option>
                </select>
            </div>

            <button class="btn-delete-row" onclick="window.removeRow('${rowId}')" title="מחק שורה">
                🗑️
            </button>
        </div>
    `;

    container.insertAdjacentHTML('beforeend', html);
    updateCalculateButton();
};

window.removeRow = function (id) {
    document.getElementById(id)?.remove();
    updateCalculateButton();
};

window.validateRow = function (rowId) {
    const row = document.getElementById(rowId);
    const inputs = row.querySelectorAll('.check-input');
    const isComplete = Array.from(inputs).every(i => i.value !== "");
    if (isComplete) row.classList.add("row-complete");
    else row.classList.remove("row-complete");
    updateCalculateButton();
};

function updateCalculateButton() {
    const btn = document.getElementById("btn-calculate");
    const rows = document.querySelectorAll(".check-row-card");
    const complete = document.querySelectorAll(".row-complete");
    if (btn) btn.disabled = !(rows.length > 0 && rows.length === complete.length);
}

window.navigateToResults = function () {
    // לוגיקה לאיסוף הנתונים...
    window.location.hash = "/inventory/inventory_check/results";
};