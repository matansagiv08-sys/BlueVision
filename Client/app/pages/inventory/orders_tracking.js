let allOrders = [];

window.initOrdersTracking = function () {
    checkAndRunInventoryImport(function () {
        // נתוני מוק
        allOrders = [
            { id: "PO-2024-001", vendor: "אולפסון תעשיות", date: "01/02/2024", due: "15/03/2024", status: "sent", value: "₪ 45,000" },
            { id: "PO-2024-002", vendor: "מערכות תעופה", date: "05/02/2024", due: "10/02/2024", status: "delayed", value: "₪ 12,500" },
            { id: "PO-2024-003", vendor: "אלביט", date: "10/02/2024", due: "20/03/2024", status: "partial", value: "₪ 180,000" }
        ];

        renderOrders(allOrders);
    }, {
        onImportStart: showImportSpinner,
        onImportEnd: hideImportSpinner
    });
};

function renderOrders(data) {
    const tbody = document.getElementById("orders-table-body");
    if (!tbody) return;

    tbody.innerHTML = data.map(order => {
        const statusMap = {
            'sent': { text: 'נשלח לספק', class: 'pill-blue' },
            'partial': { text: 'אספקה חלקית', class: 'pill-yellow' },
            'delayed': { text: 'באיחור', class: 'pill-red' }
        };
        const status = statusMap[order.status];

        return `
            <tr onclick="window.openOrderDetails('${order.id}')">
                <td style="font-weight:700;">${order.id}</td>
                <td>${order.vendor}</td>
                <td>${order.date}</td>
                <td>${order.due}</td>
                <td><span class="status-pill ${status.class}">${status.text}</span></td>
                <td>${order.value}</td>
            </tr>
        `;
    }).join('');
}

window.filterOrders = function () {
    const search = document.getElementById("orderSearch").value.toLowerCase();
    const status = document.getElementById("statusFilter").value;

    const filtered = allOrders.filter(o => {
        const matchesSearch = o.id.toLowerCase().includes(search) || o.vendor.toLowerCase().includes(search);
        const matchesStatus = status === 'all' || o.status === status;
        return matchesSearch && matchesStatus;
    });
    renderOrders(filtered);
};
