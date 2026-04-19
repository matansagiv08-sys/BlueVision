const server = "https://localhost:7296/";
let allData = {};

window.initAddItemToProduction = function () {
    //if (typeof ajaxCall === 'undefined') {
    //    $.getScript("../../../JS/ajaxCalls.js").done(() => loadInitialData());
    //} else {
    //    loadInitialData();
    //}

    // 1. סנכרון שם פריט לפי הקלדת מק"ט
    $('#item-code-input').off('input').on('input', function () {
        const val = $(this).val();
        if (allData.productionItems) {
            const item = allData.productionItems.find(i => i.productionItemID === val);
            $('#item-name-input').val(item ? item.itemName : '');
        }
    });

    // 2.  מאזינים לשינוי בשני השדות (סוג כטב"ם ופרויקט
    $('#project-select, #plane-type-select').off('change').on('change', function () {
        renderFilteredPlanes();
    });

    $('#project-select').off('change.projectDueDate').on('change.projectDueDate', function () {
        syncProjectDueDateVisibility();
    });

    $('#production-form').off('submit').on('submit', function (e) {
        e.preventDefault();

        const projectValue = $('#project-select').is(':visible') ? $('#project-select').val() : $('#project-new').val();
        const planeValue = $('#plane-select').is(':visible') ? $('#plane-select').val() : $('#plane-new').val();
        const isNewProject = $('#project-new').is(':visible');
        const projectPriorityLevel = parseNullableInt($('#project-priority-select').val());


        const newItem = {
            ProductionItemID: $('#item-code-input').val(),
            PlaneTypeID: parseInt($('#plane-type-select').val()),
            SerialNumber: parseInt($('#serial-number-input').val()),
            Quantity: parseInt($('#qty-input').val()),
            PriorityID: parseInt($('#priority-select').val()),
            WorkOrderID: $('#work-order-input').val(),
            ProjectName: projectValue,
            PlaneID: planeValue,
            DueDate: $('#due-date-input').val() || null,
            ProjectDueDate: isNewProject ? ($('#project-due-date-input').val() || null) : null,
            ProjectPriorityLevel: isNewProject ? projectPriorityLevel : null,
            Comments: $('#comments-input').val()
        };

        let api = server + "api/ItemsInProduction/InsertItem";

        ajaxCall("POST", api, JSON.stringify(newItem),
            function (response) {
                alert("הפריט נוסף בהצלחה לייצור!");
                $('#production-form')[0].reset();
                $('.input-with-action').each(function () {
                    const $select = $(this).find('select');
                    const $input = $(this).find('input[type="text"]');
                    const $btn = $(this).find('.toggle-btn');

                    $input.hide().val('');
                    $select.show().val('');
                    $btn.text('+').removeClass('active');
                });
                $('#project-due-date-input').val('');
                $('#due-date-input').val('');
                syncProjectDueDateVisibility();
                $('#item-name-input').val('');
                $('#priority-select').val(2);
                $('#item-code-input').focus();
            },
            function (err) {
                console.error("Error saving item:", err);
                alert("שגיאה בשמירת הנתונים: " + (err.responseJSON ? err.responseJSON.error : "שגיאה כללית"));
            }
        );
    });
};

function loadInitialData() {
    let api = server + "api/ItemsInProduction/GetInitialFormData";

    ajaxCall("GET", api, "", function (data) {
        allData = data;
        console.log("Data loaded:", data);

        renderDatalist('#item-code-list', data.productionItems, 'productionItemID');
        renderDatalist('#work-orders-list', data.existingWorkOrders);
        renderSelect('#plane-type-select', data.planeTypes, 'planeTypeID', 'planeTypeName');
        renderSelect('#priority-select', data.priorities, 'id', 'name');
        renderSelect('#project-priority-select', data.priorities, 'id', 'name');

        const $projSelect = $('#project-select');
        $projSelect.empty().append('<option value="">בחר פרויקט...</option>');
        if (data.projects) {
            data.projects.forEach(proj => {
                $projSelect.append(`<option value="${proj.projectName}" data-id="${proj.projectID}">${proj.projectName}</option>`);
            });
        }

        $('#priority-select').val(2);
        $('#project-priority-select').val(2);
        syncProjectDueDateVisibility();
    }, err => console.error(err));
}

function syncProjectDueDateVisibility() {
    const isNewProject = $('#project-new').is(':visible');
    const $group = $('#project-due-date-group');
    const $input = $('#project-due-date-input');
    const $priorityGroup = $('#project-priority-group');
    const $prioritySelect = $('#project-priority-select');

    if (isNewProject) {
        $group.show();
        $input.prop('disabled', false).prop('required', true);
        $priorityGroup.show();
        $prioritySelect.prop('disabled', false).prop('required', true);
        if (!$prioritySelect.val()) {
            $prioritySelect.val(2);
        }
    } else {
        $group.hide();
        $input.prop('disabled', true).prop('required', false).val('');
        $priorityGroup.hide();
        $prioritySelect.prop('disabled', true).prop('required', false).val('');
    }
}

function parseNullableInt(value) {
    const parsed = parseInt(value, 10);
    return Number.isNaN(parsed) ? null : parsed;
}

// פונקציה לסינון מטוסים לפי סוג ופרויקט
function renderFilteredPlanes() {
    const selectedProjectID = $('#project-select').find(':selected').data('id'); 
    const selectedTypeID = $('#plane-type-select').val(); 

    const $el = $('#plane-select');
    $el.empty().append('<option value="">בחר מטוס...</option>');

    if (allData.planes) {
        const filtered = allData.planes.filter(p => {
            const matchType = !selectedTypeID || p.typeID == selectedTypeID;
            const matchProject = !selectedProjectID || p.projectID == selectedProjectID;
            return matchType && matchProject;
        });

        filtered.forEach(p => {
            $el.append(`<option value="${p.planeID}">${p.planeID}</option>`);
        });
    }
}

function renderSelect(selector, list, valField, textField) {
    let $el = $(selector);
    let placeholder = $el.find('option:first').text() || "בחר...";
    $el.empty().append(`<option value="">${placeholder}</option>`);
    if (list) {
        list.forEach(item => {
            $el.append(`<option value="${item[valField]}">${item[textField]}</option>`);
        });
    }
}

function renderDatalist(selector, list, field = null) {
    let $el = $(selector);
    $el.empty();
    if (list) {
        list.forEach(item => {
            let val = field ? item[field] : item;
            $el.append(`<option value="${val}">`);
        });
    }
}

function toggleInput(field) {
    const select = $(`#${field}-select`);
    const input = $(`#${field}-new`);
    const btn = $(event.currentTarget);

    if (select.is(':visible')) {
        select.hide().val('');
        input.show().focus().prop('required', true);
        btn.text('x').addClass('active');
    } else {
        input.hide().val('').prop('required', false);
        select.show();
        btn.text('+').removeClass('active');
    }

    if (field === 'project') {
        syncProjectDueDateVisibility();
    }
}

