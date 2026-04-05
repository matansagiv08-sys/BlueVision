$(document).ready(function () {
    console.log("Projects Status: DOM ready.");
    if (typeof ajaxCall === 'undefined') {
        $.getScript("../../../JS/ajaxCalls.js").done(() => loadFullProjectsStatus());
    } else {
        loadFullProjectsStatus();
    }
});

function loadFullProjectsStatus() {
    const api = server + "api/Projects/full-status";

    ajaxCall("GET", api, null,
        function (data) {
            console.log("Data received from server:", data);
            let projects = data.$values ? data.$values : data;
            if (!Array.isArray(projects)) projects = [];

            renderProjects(projects);
        },
        function (err) {
            console.error("Error:", err);
            $("#projects-list").html("<p style='text-align:center; color:red; padding:20px;'>שגיאה בטעינת נתונים מהשרת.</p>");
        }
    );
}

function renderProjects(projects) {
    let str = "";

    projects.forEach(project => {
        const pProgress = Math.round(project.progress || 0);
        const dueDate = project.dueDate ? new Date(project.dueDate).toLocaleDateString('he-IL') : "אין תאריך";

        const planesList = project.planes && project.planes.$values ? project.planes.$values : (project.planes || []);
        const planeCount = planesList.length;

        str += `
<div class="ps-project-card" id="project-${project.projectID}">
    <button class="ps-project-head" onclick="toggleProject(${project.projectID})">
        <span class="ps-chev">▼</span>
        <div class="ps-project-title">
            <span class="ps-project-name">${project.projectName}</span>
            <span class="ps-counter">${planeCount} כטב"מים</span> 
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

    <div class="ps-project-body" style="display:none;"> 
        <div class="ps-uav-list">
            ${renderPlanes(planesList)}
        </div>
    </div>
</div>`;
    });

    $("#projects-list").html(str);
}

function renderPlanes(planes) {
    if (!planes || planes.length === 0) return "<div class='ps-uav-row'>אין מטוסים רשומים לפרויקט זה</div>";

    return planes.map(plane => {
        const plProgress = Math.round(plane.progress || 0);
        const typeName = (plane.type && plane.type.planeTypeName) ? plane.type.planeTypeName : "UAV";
        const itemsList = plane.items && plane.items.$values ? plane.items.$values : (plane.items || []);

        return `
        <div class="ps-uav-container">
            <div class="ps-uav-row" onclick="togglePlane(this)">
                <span class="ps-chev">▼</span>
                <div class="ps-uav-right">
                    <span class="ps-tag">${typeName}</span> 
                    <span class="ps-uav-id">מטוס ${plane.planeID}</span>
                </div>
                <div class="ps-uav-left">
                    <div class="ps-uav-progress">
                        <div class="ps-uav-progress-bar" style="width: ${plProgress}%"></div>
                    </div>
                    <span class="ps-uav-pct">${plProgress}%</span>
                </div>
            </div>
            
            <div class="ps-uav-body" style="display:none;">
                <div class="ps-parts-tablewrap">
                    <table class="ps-parts-table">
                        <thead>
                            <tr>
                                <th>מספר סידורי</th>
                                <th>מק"ט פריט</th>
                                <th>מספר פק"ע</th>
                                <th>תיאור פריט</th>
                                <th>כמות מתוכננת</th>
                                <th>תחנה נוכחית</th>
                                <th>סטטוס</th>
                            </tr>
                        </thead>
                        <tbody>
                            ${renderItems(itemsList)}
                        </tbody>
                    </table>
                </div>
            </div>
        </div>`;
    }).join("");
}

function renderItems(itemsArray) {
    if (!itemsArray || itemsArray.length === 0) return "<tr><td colspan='7' style='text-align:center;'>אין פריטים בייצור</td></tr>";

    return itemsArray.map(item => {
        const current = item.currentStage;
        const stageName = (current && current.stage) ? current.stage.productionStageName : "-";
        const statusName = (current && current.status) ? current.status.productionStatusName : "-";
        const statusID = (current && current.status) ? current.status.productionStatusID : 0;

        const itemDescription = (item.productionItem && item.productionItem.itemName)
            ? item.productionItem.itemName
            : (item.comments || "-");

        let statusClass = `status-${statusID}`;

        return `
        <tr>
            <td class="ps-col-sn">${item.serialNumber}</td>
            <td>${item.productionItem ? item.productionItem.productionItemID : '-'}</td>
            <td>${item.workOrderID || '-'}</td>
            <td class="ps-col-desc" title="${itemDescription}">${itemDescription}</td>
            <td>${item.plannedQty}</td>
            <td><span class="ps-stage-name">${stageName}</span></td>
            <td><span class="status-pill ${statusClass}">${statusName}</span></td>
        </tr>`;
    }).join("");
}

function toggleProject(projectId) {
    const currentCard = $(`#project-${projectId}`);
    const currentBody = currentCard.find('.ps-project-body');
    const isOpening = !currentBody.is(':visible');
    $('.ps-project-body').slideUp();
    $('.ps-project-card').removeClass('highlight-blue');
    $('.ps-chev').removeClass('up');
    if (isOpening) {
        currentBody.slideDown();
        currentCard.addClass('highlight-blue');
        currentCard.find('.ps-chev').addClass('up');
    }
}

function togglePlane(element) {
    const $row = $(element);
    const $body = $row.next('.ps-uav-body');
    const $container = $row.closest('.ps-project-body');
    if (!$body.is(':visible')) {
        $container.find('.ps-uav-body').slideUp();
        $container.find('.ps-uav-row').removeClass('active');
    }
    $body.slideToggle();
    $row.toggleClass('active');
}