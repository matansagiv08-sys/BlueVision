let allTasks = [];

window.initWorkOrder = function () {
    // נתוני מוק לדוגמה
    allTasks = [
        { po: "100245", sku: "WB-BODY-01", desc: "גוף WanderB G2", sn: "SN-9921", status: "active", urgency: "critical", deadline: "24/02", station: "plating" },
        { po: "100246", sku: "WB-WING-02", desc: "כנף ימין", sn: "SN-9922", status: "waiting", urgency: "high", deadline: "25/02", station: "layup" }
    ];

    renderTasks(allTasks);
};

function renderTasks(data) {
    const tbody = document.getElementById("tasks-table-body");
    if (!tbody) return;

    tbody.innerHTML = data.map(task => {
        // קובעים איזה צבע לתת לבועה בהתאם לסטטוס
        const colorClass = task.status === 'active' ? 'pill-blue' : 'pill-yellow';
        const statusText = task.status === 'active' ? 'בביצוע' : 'ממתין';

        return `
            <tr onclick="window.openTaskDetails('${task.po}')">
                <td style="font-weight:700;">${task.po}</td>
                <td>${task.sku}</td>
                <td>${task.desc}</td>
                <td>${task.sn}</td>
                <td><span class="status-pill ${colorClass}">${statusText}</span></td>
                <td class="${task.urgency === 'critical' ? 'urgency-critical' : ''}">
                    ${task.urgency === 'critical' ? 'קריטי 🔥' : 'גבוה'}
                </td>
                <td>${task.deadline}</td>
            </tr>
        `;
    }).join('');
}

window.filterByStation = function (station, btn) {
    // עדכון כפתורי הסליידר
    const container = btn.closest('.custom-slider-toggle');
    container.querySelectorAll('.slider-btn').forEach(b => b.classList.remove('active'));
    btn.classList.add('active');

    // סינון והצגה
    const filtered = allTasks.filter(t => station === 'all' || t.station === station);
    renderTasks(filtered);
};