let fullInventoryData = [];

window.initAllInventory = function () {
    const apiUrl = "https://localhost:7296/api/InventoryItems";

    ajaxCall(
        "GET",
        apiUrl,
        null,
        function (data) {
            fullInventoryData = Array.isArray(data)
                ? data
                : (Array.isArray(data?.$values) ? data.$values : []);
            renderInventoryTable(fullInventoryData);
        },
        function (xhr) {
            console.error("Failed to load inventory items", xhr);
            renderInventoryTable([]);
        }
    );
};

function renderInventoryTable(data) {
    const tbody = document.getElementById("inventory-table-body");
    if (!tbody) return;

    tbody.innerHTML = data.map(item => {
        const inventoryItemID = item.inventoryItemID ?? item.InventoryItemID ?? "";
        const itemName = item.itemName ?? item.ItemName ?? "";
        const itemGrpID = item.itemGrpID ?? item.ItemGrpID ?? "";
        const buyMethod = item.buyMethod ?? item.BuyMethod ?? "";
        const price = item.price ?? item.Price;
        const supplierID = item.supplierID ?? item.SupplierID ?? "";
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
            <td>${displayOrDash(inventoryItemID)}</td>
            <td>${displayOrDash(itemName)}</td>
            <td>${displayOrDash(itemGrpID)}</td>
            <td>${displayOrDash(buyMethod)}</td>
            <td>${displayOrDash(price)}</td>
            <td>${displayOrDash(supplierID)}</td>
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

window.filterInventory = function () {
    renderInventoryTable(fullInventoryData);
};

window.sortInventory = function () {
    renderInventoryTable(fullInventoryData);
};

window.showItemDetails = function () {
};

window.closeGenericModal = function () {
    const modal = document.getElementById("genericModal");
    if (modal) modal.style.display = "none";
};
