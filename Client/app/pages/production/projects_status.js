$(document).ready(function () {
    loadFullProjectsStatus();
});

function loadFullProjectsStatus() {
    const api = `https://localhost:7296/api/Projects/full-status`;

    ajaxCall("GET", api, null,
        function (data) {
            console.log("Success:", data);
            let projects = Array.isArray(data) ? data : (data.$values || []);
            renderProjects(projects);
        },
        function (err) {
            console.error("Error:", err);
        }
    );
}

function renderProjects(projects) {
    let str = "";

    projects.forEach(project => {
        const pProgress = Math.round(project.progress || 0);
        const dueDate = project.dueDate ? new Date(project.dueDate).toLocaleDateString('he-IL') : "אין תאריך";

        str += `
        <div class="ps-project-card">
            <button class="ps-project-head" onclick="$(this).next('.ps-project-body').slideToggle(); $(this).find('.ps-chev').toggleClass('up');">
                <span class="ps-chev">▼</span>
                <div class="ps-project-title">
                    <span class="ps-project-name">${project.projectName}</span>
                    <span class="ps-project-sub">#${project.projectID}</span>
                </div>
                
                <div class="ps-project-meta">
                    <div class="ps-project-meta-right">
                        <span class="ps-deadline">יעד: ${dueDate}</span>
                    </div>
                    <div class="ps-project-meta-left">
                        <div class="ps-progress">
                            <div class="ps-progress-bar" style="width: ${pProgress}%"></div>
                        </div>
                        <span class="ps-percent">${pProgress}%</span>
                    </div>
                </div>
            </button>

            <div class="ps-project-body">
                <div class="ps-uav-list">
                    ${renderPlanes(project.planes || [])}
                </div>
            </div>
        </div>`;
    });

    $("#projects-list").html(str);
}

function renderPlanes(planes) {
    if (planes.length === 0) return "<div class='ps-uav-row'>אין מטוסים רשומים לפרויקט זה</div>";

    return planes.map(plane => {
        const plProgress = Math.round(plane.progress || 0);
        return `
        <div class="ps-uav-container">
            <div class="ps-uav-row" onclick="$(this).next('.ps-uav-body').slideToggle(); $(this).toggleClass('active');">
                <span class="ps-chev">▼</span>
                <div class="ps-uav-right">
                    <span class="ps-tag">UAV</span>
                    <span class="ps-uav-id">מטוס ${plane.planeID}</span>
                </div>
                
                <div class="ps-uav-left">
                    <div class="ps-uav-progress">
                        <div class="ps-uav-progress-bar" style="width: ${plProgress}%"></div>
                    </div>
                    <span class="ps-uav-pct">${plProgress}%</span>
                </div>
            </div>
            
            <div class="ps-uav-body">
                <div class="ps-parts-tablewrap">
                    <table class="ps-parts-table">
                        <thead>
                            <tr>
                                <th>מספר סידורי</th>
                                <th>מק"ט פריט</th>
                                <th>כמות מתוכננת</th>
                                <th>הערות</th>
                                <th>סטטוס נוכחי</th>
                            </tr>
                        </thead>
                        <tbody>
                            ${renderItems(plane.items || [])}
                        </tbody>
                    </table>
                </div>
            </div>
        </div>`;
    }).join("");
}

function renderItems(items) {
    if (items.length === 0) return "<tr><td colspan='5' style='text-align:center;'>אין פריטים בייצור למטוס זה</td></tr>";

    return items.map(item => {
        // מציאת הסטטוס האחרון מתוך רשימת ה-Stages
        const lastStage = item.stages && item.stages.length > 0 ? item.stages[item.stages.length - 1] : null;
        const statusID = lastStage && lastStage.status ? lastStage.status.productionStatusID : 0;

        // בחירת קלאס לצבע הסטטוס (לפי ה-CSS שלך)
        let statusClass = "ps-pill-info";
        let statusText = "בביצוע";

        if (statusID === 4) {
            statusClass = "ps-pill-good";
            statusText = "בוצע";
        }

        return `
        <tr>
            <td>${item.serialNumber}</td>
            <td>${item.productionItem ? item.productionItem.productionItemID : 'N/A'}</td>
            <td>${item.plannedQty}</td>
            <td class="ps-col-desc">${item.comments || '-'}</td>
            <td>
                <span class="ps-pill ${statusClass}">${statusText}</span>
            </td>
        </tr>`;
    }).join("");
}