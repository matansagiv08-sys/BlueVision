//$(document).ready(function () {
//    console.log("Projects Status: DOM ready.");
//    if (typeof ajaxCall === 'undefined') {
//        $.getScript("../../../JS/ajaxCalls.js").done(() => loadFullProjectsStatus());
//    } else {
//        loadFullProjectsStatus();
//    }
//});

var projectsStatusState = window.projectsStatusState || {
    allProjects: [],
    projectOptions: [],
    planes: [],
    planeTypes: [],
    priorities: [],
    loadToken: 0
};
window.projectsStatusState = projectsStatusState;

window.initProjectsStatus = function () {
    console.log("Projects Status: init.");
    resetProjectStatusView();
    loadProjectStatusOptions();
    loadFullProjectsStatus();
};

function resetProjectStatusView() {
    const onlyUnfinishedEl = document.getElementById("ps-only-unfinished-projects");
    if (onlyUnfinishedEl) onlyUnfinishedEl.checked = false;
    projectsStatusState.loadToken += 1;
    projectsStatusState.allProjects = [];
    setProjectStatusListMessage("טוען פרויקטים...");
}

function loadFullProjectsStatus() {
    const api = server + "api/Projects/full-status";
    const loadToken = projectsStatusState.loadToken;

    ajaxCall("GET", api, null,
        function (data) {
            if (loadToken !== projectsStatusState.loadToken) return;
            console.log("Data received from server:", data);
            let projects = data.$values ? data.$values : data;
            if (!Array.isArray(projects)) projects = [];

            projectsStatusState.allProjects = projects;

            applyProjectStatusFilters();
        },
        function (err) {
            if (loadToken !== projectsStatusState.loadToken) return;
            console.error("Error:", err);
            $("#projects-list").html("<p style='text-align:center; color:red; padding:20px;'>שגיאה בטעינת נתונים מהשרת.</p>");
        }
    );
}

window.applyProjectStatusFilters = function () {
    const onlyUnfinishedProjects = !!document.getElementById("ps-only-unfinished-projects")?.checked;
    const allProjects = projectsStatusState.allProjects || [];
    const visibleProjects = onlyUnfinishedProjects
        ? allProjects.filter(project => !isProjectCompleted(project))
        : allProjects.slice();

    renderProjects(visibleProjects);
};

function isProjectCompleted(project) {
    const planesList = getCollection(project?.planes ?? project?.Planes);
    if (planesList.length === 0) return false;

    return planesList.every(isPlaneCompleted);
}

function isPlaneCompleted(plane) {
    const itemsList = getCollection(plane?.items ?? plane?.Items);
    if (itemsList.length === 0) return false;

    return itemsList.every(isProductionItemCompleted);
}

function isProductionItemCompleted(item) {
    const progress = Number(item?.progress ?? item?.Progress);
    if (!Number.isNaN(progress)) return progress >= 100;

    const stages = getCollection(item?.stages ?? item?.Stages);
    if (stages.length > 0) {
        return stages.every(stageRow => getStatusId(stageRow?.status ?? stageRow?.Status) === 4);
    }

    const currentStage = item?.currentStage ?? item?.CurrentStage;
    return getStatusId(currentStage?.status ?? currentStage?.Status) === 4;
}

function getStatusId(status) {
    return parseInt(status?.productionStatusID ?? status?.ProductionStatusID ?? 0, 10);
}

function getCollection(value) {
    if (!value) return [];
    if (Array.isArray(value)) return value;
    if (Array.isArray(value.$values)) return value.$values;
    return [];
}

function isProjectOverdue(project) {
    return !isProjectCompleted(project) && isDateBeforeToday(project?.dueDate ?? project?.DueDate);
}

function projectHasOverdueItems(project) {
    const planesList = getCollection(project?.planes ?? project?.Planes);
    return planesList.some(plane => getCollection(plane?.items ?? plane?.Items).some(isProductionItemOverdue));
}

function isProductionItemOverdue(item) {
    return !isProductionItemCompleted(item) && isDateBeforeToday(item?.itemDueDate ?? item?.ItemDueDate);
}

function isDateBeforeToday(value) {
    const date = parseProjectStatusDateOnly(value);
    if (!date) return false;

    const today = new Date();
    today.setHours(0, 0, 0, 0);
    return date.getTime() < today.getTime();
}

function parseProjectStatusDateOnly(value) {
    if (!value) return null;

    const text = String(value);
    const match = text.match(/^(\d{4})-(\d{2})-(\d{2})/);
    if (match) {
        const year = Number(match[1]);
        if (year <= 1901) return null;
        return new Date(year, Number(match[2]) - 1, Number(match[3]));
    }

    const parsed = new Date(text);
    if (Number.isNaN(parsed.getTime()) || parsed.getFullYear() <= 1901) return null;
    return new Date(parsed.getFullYear(), parsed.getMonth(), parsed.getDate());
}

function renderOverdueIndicator(tooltip, variant) {
    const className = variant === "subtle" ? "ps-overdue-indicator subtle" : "ps-overdue-indicator";
    return `<span class="${className}" title="${escapeHtml(tooltip)}" aria-label="${escapeHtml(tooltip)}">!</span>`;
}

function formatProjectStatusDate(value) {
    const date = parseProjectStatusDateOnly(value);
    if (!date) return "---";
    return date.toLocaleDateString("he-IL");
}

function getPriorityOption(priorityID) {
    const id = parseNullableInt(priorityID);
    if (!id) return null;

    const option = (projectsStatusState.priorities || []).find(priority => {
        const optionID = priority.id ?? priority.ID ?? priority.priorityID ?? priority.PriorityID;
        return String(optionID) === String(id);
    });

    return {
        id,
        label: option ? (option.name ?? option.Name ?? option.priorityName ?? option.PriorityName ?? String(id)) : String(id)
    };
}

function getPriorityClass(priorityID) {
    const id = parseNullableInt(priorityID);
    if (!id) return "";
    if (id <= 1) return "high";
    if (id === 2) return "medium";
    return "low";
}

function renderPriorityBadge(priorityID) {
    const priority = getPriorityOption(priorityID);
    if (!priority) return "---";

    return `<span class="ps-priority-badge ${getPriorityClass(priority.id)}">${escapeHtml(priority.label)}</span>`;
}

function renderProjects(projects) {
    let str = "";

    if (!projects || projects.length === 0) {
        setProjectStatusListMessage("אין פרויקטים להצגה.");
        return;
    }

    projects.forEach(project => {
        const projectID = project.projectID || project.ProjectID;
        const projectName = project.projectName || project.ProjectName || "";
        const pProgress = Math.round(project.progress || 0);
        const projectDueDate = project.dueDate ?? project.DueDate;
        const dueDate = projectDueDate ? formatProjectStatusDate(projectDueDate) : "אין תאריך";
        const projectPriority = project.priorityLevel ?? project.PriorityLevel;

        const planesList = project.planes && project.planes.$values ? project.planes.$values : (project.planes || []);
        const planeCount = planesList.length;
        const projectOverdue = isProjectOverdue(project);
        const hasOverdueItems = !projectOverdue && projectHasOverdueItems(project);
        const projectOverdueIndicator = projectOverdue
            ? renderOverdueIndicator("הפרויקט עבר את תאריך היעד")
            : (hasOverdueItems ? renderOverdueIndicator("קיימים פריטים שעברו את תאריך היעד", "subtle") : "");

        str += `
<div class="ps-project-card" id="project-${projectID}">
    <button class="ps-project-head" onclick="toggleProject(${projectID})">
        <span class="ps-chev">▼</span>
        <div class="ps-project-topline">
            <div class="ps-project-title">
                <span class="ps-project-name">${escapeHtml(projectName)}</span>
                ${projectOverdueIndicator}
                <span class="ps-counter">${planeCount} כטב"מים</span>
            </div>
            <span type="button" class="ps-project-action-btn" onclick="event.stopPropagation(); openProjectStatusNewPlaneModal(${projectID});">+ הוספת כלי</span>
        </div>
        
        <div class="ps-project-meta">
            <div class="ps-project-meta-right">
                <span class="ps-deadline">יעד: ${dueDate}</span>
                ${renderPriorityBadge(projectPriority)}
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

function setProjectStatusListMessage(message) {
    $("#projects-list").html(`<p style='text-align:center; color:#64748b; padding:20px;'>${escapeHtml(message)}</p>`);
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
                                <th>תאריך יעד</th>
                                <th>דחיפות</th>
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
    if (!itemsArray || itemsArray.length === 0) return "<tr><td colspan='9' style='text-align:center;'>אין פריטים בייצור</td></tr>";

    return itemsArray.map(item => {
        const current = item.currentStage;
        const stageName = (current && current.stage) ? current.stage.productionStageName : "-";
        const statusName = (current && current.status) ? current.status.productionStatusName : "-";
        const statusID = (current && current.status) ? current.status.productionStatusID : 0;
        const itemOverdueIndicator = isProductionItemOverdue(item)
            ? renderOverdueIndicator("הפריט עבר את תאריך היעד")
            : "";
        const itemDueDate = item.itemDueDate ?? item.ItemDueDate;
        const itemPriority = item.priorityLevel ?? item.PriorityLevel;

        const itemDescription = (item.productionItem && item.productionItem.itemName)
            ? item.productionItem.itemName
            : (item.comments || "-");

        let statusClass = `status-${statusID}`;

        return `
        <tr>
            <td class="ps-col-sn">${item.serialNumber}</td>
            <td>${item.productionItem ? item.productionItem.productionItemID : '-'}</td>
            <td>${item.workOrderID || '-'}</td>
            <td class="ps-col-desc" title="${escapeHtml(itemDescription)}"><span class="ps-item-desc-wrap"><span>${escapeHtml(itemDescription)}</span>${itemOverdueIndicator}</span></td>
            <td>${item.plannedQty}</td>
            <td class="ps-col-date">${formatProjectStatusDate(itemDueDate)}</td>
            <td class="ps-col-priority">${renderPriorityBadge(itemPriority)}</td>
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
    $('.ps-project-head .ps-chev').removeClass('up');
    if (isOpening) {
        currentBody.slideDown();
        currentCard.addClass('highlight-blue');
        currentCard.find('.ps-project-head .ps-chev').addClass('up');
    }
}

function togglePlane(element) {
    const $row = $(element);
    const $body = $row.next('.ps-uav-body');
    const $container = $row.closest('.ps-project-body');
    const isOpening = !$body.is(':visible');
    if (!$body.is(':visible')) {
        $container.find('.ps-uav-body').slideUp();
        $container.find('.ps-uav-row').removeClass('active');
        $container.find('.ps-uav-row .ps-chev').removeClass('up');
    }
    $body.slideToggle();
    $row.toggleClass('active', isOpening);
    $row.find('.ps-chev').toggleClass('up', isOpening);
}

function loadProjectStatusOptions(afterLoad) {
    ajaxCall("GET", server + "api/ItemsInProduction/GetInitialFormData", "",
        function (data) {
            projectsStatusState.planeTypes = normalizePlaneTypes(data?.planeTypes || []);
            projectsStatusState.priorities = data?.priorities || [];
            projectsStatusState.planes = data?.planes || [];
            if (Array.isArray(data?.projects)) {
                projectsStatusState.projectOptions = data.projects;
            }
            if ((projectsStatusState.allProjects || []).length > 0) {
                applyProjectStatusFilters();
            }
            if (typeof afterLoad === "function") afterLoad(data);
        },
        function (err) {
            console.error("Error loading project status options:", err);
        }
    );
}

function normalizePlaneTypes(types) {
    return (types || []).map(type => ({
        planeTypeID: parseNullableInt(type.planeTypeID ?? type.PlaneTypeID),
        planeTypeName: type.planeTypeName ?? type.PlaneTypeName ?? ""
    })).filter(type => type.planeTypeID);
}

window.openProjectStatusNewProjectModal = function () {
    clearProjectStatusModalMessage("#psNewProjectMessage");
    $("#psNewProjectName").val("");
    $("#psNewProjectDueDate").val("");
    renderProjectStatusSelect(
        document.getElementById("psNewProjectPriority"),
        (projectsStatusState.priorities || []).map(priority => ({ value: priority.id ?? priority.ID, label: priority.name ?? priority.Name })),
        "בחר עדיפות...",
        2
    );
    $("#psNewProjectModal").css("display", "flex");
    setTimeout(() => $("#psNewProjectName").focus(), 0);
};

window.closeProjectStatusNewProjectModal = function () {
    $("#psNewProjectModal").hide();
    clearProjectStatusModalMessage("#psNewProjectMessage");
};

window.saveProjectStatusNewProject = function () {
    const projectName = String($("#psNewProjectName").val() || "").trim();
    const dueDate = $("#psNewProjectDueDate").val() || null;
    const priorityLevel = parseNullableInt($("#psNewProjectPriority").val()) || 2;

    if (!projectName) {
        showProjectStatusModalMessage("#psNewProjectMessage", "שם פרויקט הוא שדה חובה.", true);
        return;
    }

    if (projectNameExists(projectName)) {
        showProjectStatusModalMessage("#psNewProjectMessage", "פרויקט בשם זה כבר קיים.", true);
        return;
    }

    setProjectStatusSaving("#psSaveNewProjectBtn", true);
    ajaxCall("POST", server + "api/Projects", JSON.stringify({ ProjectName: projectName, DueDate: dueDate, PriorityLevel: priorityLevel }),
        function () {
            loadProjectStatusOptions(function () {
                loadFullProjectsStatus();
                closeProjectStatusNewProjectModal();
                showProjectStatusToast("הפרויקט נוסף בהצלחה.", false);
                setProjectStatusSaving("#psSaveNewProjectBtn", false);
            });
        },
        function (err) {
            setProjectStatusSaving("#psSaveNewProjectBtn", false);
            showProjectStatusModalMessage("#psNewProjectMessage", getProjectStatusErrorText(err, "שגיאה ביצירת הפרויקט."), true);
        }
    );
};

window.openProjectStatusNewPlaneModal = function (projectID) {
    clearProjectStatusModalMessage("#psNewPlaneMessage");
    $("#psNewPlaneID").val("");
    renderProjectStatusSelect(
        document.getElementById("psNewPlaneProject"),
        getProjectOptions().map(project => ({ value: project.projectID, label: project.projectName })),
        "בחר פרויקט...",
        projectID || ""
    );
    renderProjectStatusSelect(
        document.getElementById("psNewPlaneType"),
        projectsStatusState.planeTypes.map(type => ({ value: type.planeTypeID, label: type.planeTypeName })),
        "בחר סוג...",
        ""
    );

    if (projectID && !$("#psNewPlaneProject").val()) {
        const project = getProjectOptions().find(option => String(option.projectID) === String(projectID));
        const option = document.createElement("option");
        option.value = String(projectID);
        option.textContent = project?.projectName || String(projectID);
        document.getElementById("psNewPlaneProject").appendChild(option);
        $("#psNewPlaneProject").val(String(projectID));
    }

    $("#psNewPlaneModal").css("display", "flex");
    setTimeout(() => $("#psNewPlaneID").focus(), 0);
};

window.closeProjectStatusNewPlaneModal = function () {
    $("#psNewPlaneModal").hide();
    clearProjectStatusModalMessage("#psNewPlaneMessage");
};

window.saveProjectStatusNewPlane = function () {
    const projectID = parseNullableInt($("#psNewPlaneProject").val());
    const planeID = String($("#psNewPlaneID").val() || "").trim();
    const planeTypeID = parseNullableInt($("#psNewPlaneType").val());

    if (!projectID) {
        showProjectStatusModalMessage("#psNewPlaneMessage", "יש לבחור פרויקט.", true);
        return;
    }

    if (!planeID) {
        showProjectStatusModalMessage("#psNewPlaneMessage", "מספר / מזהה כלי הוא שדה חובה.", true);
        return;
    }

    if (!planeTypeID) {
        showProjectStatusModalMessage("#psNewPlaneMessage", "יש לבחור סוג כטב״ם.", true);
        return;
    }

    if (planeExistsForProject(projectID, planeID)) {
        showProjectStatusModalMessage("#psNewPlaneMessage", "כלי זה כבר קיים בפרויקט הנבחר.", true);
        return;
    }

    setProjectStatusSaving("#psSaveNewPlaneBtn", true);
    ajaxCall("POST", server + "api/Planes", JSON.stringify({ ProjectID: projectID, PlaneID: planeID, PlaneTypeID: planeTypeID }),
        function () {
            loadProjectStatusOptions(function () {
                loadFullProjectsStatus();
                closeProjectStatusNewPlaneModal();
                showProjectStatusToast("הכלי נוסף לפרויקט בהצלחה.", false);
                setProjectStatusSaving("#psSaveNewPlaneBtn", false);
            });
        },
        function (err) {
            setProjectStatusSaving("#psSaveNewPlaneBtn", false);
            showProjectStatusModalMessage("#psNewPlaneMessage", getProjectStatusErrorText(err, "שגיאה ביצירת הכלי."), true);
        }
    );
};

function getProjectOptions() {
    const map = new Map();
    [...(projectsStatusState.projectOptions || []), ...(projectsStatusState.allProjects || [])].forEach(project => {
        const id = project.projectID ?? project.ProjectID;
        const name = project.projectName ?? project.ProjectName;
        if (id && name && !map.has(String(id))) {
            map.set(String(id), { projectID: id, projectName: name });
        }
    });
    return Array.from(map.values());
}

function renderProjectStatusSelect(select, options, placeholder, selectedValue) {
    if (!select) return;
    select.innerHTML = `<option value="">${escapeHtml(placeholder)}</option>`;
    (options || []).forEach(option => {
        if (option.value === undefined || option.value === null || option.value === "") return;
        const optionEl = document.createElement("option");
        optionEl.value = String(option.value);
        optionEl.textContent = option.label || option.value;
        select.appendChild(optionEl);
    });
    if (selectedValue !== undefined && selectedValue !== null && selectedValue !== "") {
        select.value = String(selectedValue);
    }
}

function projectNameExists(projectName) {
    const normalized = projectName.trim().toLowerCase();
    return getProjectOptions().some(project => String(project.projectName || "").trim().toLowerCase() === normalized);
}

function planeExistsForProject(projectID, planeID) {
    const normalized = planeID.trim().toLowerCase();
    return (projectsStatusState.planes || []).some(plane => {
        const existingPlaneID = String(plane.planeID ?? plane.PlaneID ?? "").trim().toLowerCase();
        const existingProjectID = parseNullableInt(plane.projectID ?? plane.ProjectID);
        return existingProjectID === projectID && existingPlaneID === normalized;
    });
}

function parseNullableInt(value) {
    const parsed = parseInt(value, 10);
    return Number.isNaN(parsed) ? null : parsed;
}

function showProjectStatusModalMessage(selector, message, isError) {
    $(selector).text(message).toggleClass("error", !!isError).show();
}

function clearProjectStatusModalMessage(selector) {
    $(selector).hide().text("").removeClass("error");
}

function setProjectStatusSaving(selector, saving) {
    const $btn = $(selector);
    $btn.prop("disabled", !!saving);
    if (saving) {
        $btn.data("original-text", $btn.text()).text("שומר...");
    } else if ($btn.data("original-text")) {
        $btn.text($btn.data("original-text"));
    }
}

function showProjectStatusToast(message, isError) {
    showAppMessage(message, { title: isError ? "שגיאה" : "הצלחה" });
}

function getProjectStatusErrorText(err, fallback) {
    return err?.responseJSON?.error || err?.responseText || err?.statusText || fallback;
}

function escapeHtml(value) {
    return String(value)
        .replace(/&/g, "&amp;")
        .replace(/</g, "&lt;")
        .replace(/>/g, "&gt;")
        .replace(/\"/g, "&quot;")
        .replace(/'/g, "&#39;");
}
