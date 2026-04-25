const server = "https://localhost:7296/";
let allData = {};
let trackedUnits = [];
let allProductionItems = [];
let allPlaneTypes = [];
let itemPlaneMappings = [];

window.initAddItemToProduction = function () {
    bindFormEvents();
    loadInitialData();
};

function bindFormEvents() {
    $('#item-code-select').off('change').on('change', function () {
        syncSelectedItemName();
    });

    $('#plane-type-select').off('change.itemPlane').on('change.itemPlane', function () {
        renderFilteredPlanes();
    });

    $('#project-select').off('change.projectMeta').on('change.projectMeta', function () {
        syncProjectDueDateVisibility();
        updateProjectDisplayName();
        renderFilteredPlanes();
    });

    $('#project-new').off('input.projectMeta').on('input.projectMeta', function () {
        updateProjectDisplayName();
    });

    $('#single-unit-plane-input').off('input.units change.units').on('input.units change.units', function () { });

    $('#qty-input').off('input').on('input', function () {
        updateUnitsSection();
    });

    $('#tracked-units-body').off('input').on('input', 'input', function () {
        const index = parseInt($(this).data('index'), 10);
        if (!Number.isInteger(index) || index < 0 || index >= trackedUnits.length) {
            return;
        }

        if ($(this).hasClass('tracked-serial-input')) {
            trackedUnits[index].serialNumber = String($(this).val() || '').trim();
        }

        if ($(this).hasClass('tracked-plane-input')) {
            trackedUnits[index].planeNumber = String($(this).val() || '').trim();
        }
    });

    $('#production-form').off('submit').on('submit', function (e) {
        e.preventDefault();
        submitProductionForm();
    });
}

function submitProductionForm() {
    hideFormMessage();

    const form = document.getElementById('production-form');
    if (form && !form.reportValidity()) {
        return;
    }

    const quantity = normalizeQuantity();
    const isNewProject = $('#project-new').is(':visible');
    const projectValue = isNewProject ? $('#project-new').val() : $('#project-select').val();
    const projectPriorityLevel = parseNullableInt($('#project-priority-select').val());

    const basePayload = {
        ProductionItemID: String($('#item-code-select').val() || '').trim(),
        PlaneTypeID: parseNullableInt($('#plane-type-select').val()),
        PriorityID: parseNullableInt($('#priority-select').val()),
        WorkOrderID: String($('#work-order-input').val() || '').trim() || null,
        ProjectName: String(projectValue || '').trim() || null,
        DueDate: $('#due-date-input').val() || null,
        ProjectDueDate: isNewProject ? ($('#project-due-date-input').val() || null) : null,
        ProjectPriorityLevel: isNewProject ? projectPriorityLevel : null,
        Comments: $('#comments-input').val() || null,
        Quantity: quantity
    };

    if (!basePayload.ProductionItemID) {
        showFormMessage('יש לבחור קוד פריט.', true);
        return;
    }

    if (!basePayload.PlaneTypeID || basePayload.PlaneTypeID <= 0) {
        showFormMessage('יש לבחור סוג כטב״ם.', true);
        return;
    }

    let payload;

    if (quantity === 1) {
        const serial = parseNullableInt($('#serial-number-input').val());
        if (!serial || serial <= 0) {
            showFormMessage('יש להזין מספר סריאלי תקין.', true);
            return;
        }

        const singlePlane = String($('#single-unit-plane-input').val() || '').trim();

        payload = {
            ...basePayload,
            SerialNumber: serial,
            PlaneID: singlePlane || null,
            Units: null
        };
    } else {
        const units = collectUnitsForSubmit(quantity);
        if (!units) {
            return;
        }

        payload = {
            ...basePayload,
            SerialNumber: null,
            PlaneID: null,
            Units: units
        };
    }

    const api = server + 'api/ItemsInProduction/InsertItem';
    ajaxCall('POST', api, JSON.stringify(payload),
        function () {
            const successText = quantity > 1 ? 'הפריטים נוספו בהצלחה לייצור' : 'הפריט נוסף בהצלחה לייצור';
            showFormMessage(successText, false, 4500);
            resetProductionForm();
        },
        function (err) {
            console.error('Error saving item:', err);
            const errorText = err?.responseJSON?.error || 'שגיאה כללית';
            showFormMessage('שגיאה בשמירת הנתונים: ' + errorText, true);
        }
    );
}

function collectUnitsForSubmit(quantity) {
    const seenSerials = new Set();
    const units = [];
    const rows = $('#tracked-units-body tr');

    if (rows.length < quantity) {
        showFormMessage('מספר שורות המעקב לא תואם לכמות שהוגדרה.', true);
        return null;
    }

    for (let i = 0; i < quantity; i++) {
        const $row = $(rows[i]);
        const serialRaw = String($row.find('.tracked-serial-input').val() || '').trim();
        const planeRaw = String($row.find('.tracked-plane-input').val() || '').trim();
        const serial = parseNullableInt(serialRaw);

        if (!serial || serial <= 0) {
            showFormMessage(`יש להזין מספר סריאלי תקין בשורה ${i + 1}.`, true);
            return null;
        }

        if (seenSerials.has(serial)) {
            showFormMessage(`מספר סריאלי ${serial} מופיע פעמיים. יש להזין ערך ייחודי לכל שורה.`, true);
            return null;
        }

        seenSerials.add(serial);
        units.push({
            SerialNumber: serial,
            PlaneID: planeRaw || null
        });
    }

    trackedUnits = units.map(u => ({ serialNumber: String(u.SerialNumber), planeNumber: String(u.PlaneID || '') }));
    return units;
}

function showFormMessage(message, isError = false, autoHideMs = 0) {
    const $msg = $('#form-feedback-message');
    if (!$msg.length) return;

    $msg.stop(true, true);
    $msg.text(message);
    $msg.toggleClass('error', !!isError);
    $msg.show();

    if (autoHideMs > 0) {
        setTimeout(() => {
            $msg.fadeOut(250);
        }, autoHideMs);
    }
}

function hideFormMessage() {
    const $msg = $('#form-feedback-message');
    if (!$msg.length) return;
    $msg.stop(true, true).hide().text('').removeClass('error');
}

function resetProductionForm() {
    const form = document.getElementById('production-form');
    if (form) form.reset();

    $('.input-with-action').each(function () {
        const $select = $(this).find('select');
        const $input = $(this).find('input[type="text"]');
        const $btn = $(this).find('.toggle-btn');

        $input.hide().val('').prop('required', false);
        $select.show().val('');
        $btn.text('+').removeClass('active');
    });

    trackedUnits = [];
    $('#qty-input').val(1);
    $('#item-name-input').val('');
    $('#item-code-select').val('');
    $('#plane-type-select').val('');
    $('#project-display-name').val('');
    $('#priority-select').val(2);
    $('#project-priority-select').val(2);
    syncProjectDueDateVisibility();
    renderFilteredPlanes();
    updateUnitsSection();
    syncSelectedItemName();
    $('#item-code-select').focus();
}

function loadInitialData() {
    const api = server + 'api/ItemsInProduction/GetInitialFormData';

    ajaxCall('GET', api, '', function (data) {
        allData = data || {};
        allProductionItems = normalizeProductionItems(allData.productionItems || []);
        allPlaneTypes = (allData.planeTypes || []).map(t => ({
            planeTypeID: parseInt(t.planeTypeID, 10),
            planeTypeName: t.planeTypeName
        })).filter(t => Number.isFinite(t.planeTypeID));
        itemPlaneMappings = normalizeItemPlaneMappings(allData.itemPlaneMappings || allProductionItems);

        renderDatalist('#work-orders-list', allData.existingWorkOrders);
        renderSelect('#priority-select', allData.priorities, 'id', 'name');
        renderSelect('#project-priority-select', allData.priorities, 'id', 'name');
        renderAllItemAndPlaneTypeOptions();

        const $projSelect = $('#project-select');
        $projSelect.empty().append('<option value="">בחר פרויקט...</option>');
        (allData.projects || []).forEach(proj => {
            $projSelect.append(`<option value="${proj.projectName}" data-id="${proj.projectID}">${proj.projectName}</option>`);
        });

        const planeValues = [...new Set((allData.planes || []).map(p => p.planeID).filter(Boolean))];
        renderDatalist('#tracked-plane-list', planeValues);

        $('#priority-select').val(2);
        $('#project-priority-select').val(2);
        $('#qty-input').val(1);

        syncProjectDueDateVisibility();
        updateProjectDisplayName();
        renderFilteredPlanes();
        updateUnitsSection();
    }, err => console.error(err));
}

function renderAllItemAndPlaneTypeOptions() {
    const $itemSelect = $('#item-code-select');
    const $planeTypeSelect = $('#plane-type-select');
    const prevItem = String($itemSelect.val() || '').trim();
    const prevType = String($planeTypeSelect.val() || '').trim();

    setOptions(
        $itemSelect,
        allProductionItems.map(i => ({ value: i.productionItemID, label: i.productionItemID })),
        'בחר מק״ט...',
        prevItem
    );

    setOptions(
        $planeTypeSelect,
        allPlaneTypes.map(t => ({ value: t.planeTypeID, label: t.planeTypeName })),
        'בחר סוג...',
        prevType
    );

    syncSelectedItemName();
}

function syncSelectedItemName() {
    const selectedItem = String($('#item-code-select').val() || '').trim();
    const itemObj = allProductionItems.find(i => i.productionItemID === selectedItem);
    $('#item-name-input').val(itemObj ? itemObj.itemName : '');
}

function normalizeQuantity() {
    const parsed = parseInt($('#qty-input').val(), 10);
    const safeValue = Number.isInteger(parsed) && parsed > 0 ? parsed : 1;
    $('#qty-input').val(safeValue);
    return safeValue;
}

function ensureTrackedUnitsLength(qty) {
    const defaultPlane = String($('#single-unit-plane-input').val() || '').trim();

    while (trackedUnits.length < qty) {
        trackedUnits.push({ serialNumber: '', planeNumber: defaultPlane });
    }
    if (trackedUnits.length > qty) {
        trackedUnits = trackedUnits.slice(0, qty);
    }
}

function updateUnitsSection() {
    const qty = normalizeQuantity();
    const $single = $('#single-unit-fields');
    const $multi = $('#multi-unit-wrapper');
    const $serialInput = $('#serial-number-input');
    const $singlePlane = $('#single-unit-plane-input');

    if (qty <= 1) {
        if (!$serialInput.val() && trackedUnits[0]?.serialNumber) {
            $serialInput.val(trackedUnits[0].serialNumber);
        }
        if (!$singlePlane.val() && trackedUnits[0]?.planeNumber) {
            $singlePlane.val(trackedUnits[0].planeNumber);
        }

        $single.show();
        $multi.hide();
        $serialInput.prop('required', true);
        return;
    }

    if (trackedUnits.length === 0 && ($serialInput.val() || $singlePlane.val())) {
        trackedUnits.push({
            serialNumber: String($serialInput.val() || '').trim(),
            planeNumber: String($singlePlane.val() || '').trim()
        });
    }

    ensureTrackedUnitsLength(qty);
    renderTrackedUnitsTable();

    $single.hide();
    $multi.show();
    $serialInput.prop('required', false);
}

function renderTrackedUnitsTable() {
    const $tbody = $('#tracked-units-body');
    $tbody.empty();

    trackedUnits.forEach((row, index) => {
        const serialVal = String(row.serialNumber || '');
        const planeVal = String(row.planeNumber || '');
        $tbody.append(`
            <tr>
                <td class="row-index-cell">${index + 1}</td>
                <td><input type="number" class="tracked-serial-input" data-index="${index}" min="1" value="${serialVal}" placeholder="מספר סריאלי"></td>
                <td><input type="text" class="tracked-plane-input" data-index="${index}" list="tracked-plane-list" value="${planeVal}" placeholder="מספר מטוס"></td>
            </tr>
        `);
    });
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

function updateProjectDisplayName() {
    const isNewProject = $('#project-new').is(':visible');
    const projectName = isNewProject
        ? String($('#project-new').val() || '').trim()
        : String($('#project-select option:selected').text() || '').trim();

    $('#project-display-name').val(projectName && projectName !== 'בחר פרויקט...' ? projectName : '');
}

function parseNullableInt(value) {
    const parsed = parseInt(value, 10);
    return Number.isNaN(parsed) ? null : parsed;
}

function renderFilteredPlanes() {
    const selectedProjectID = $('#project-select').find(':selected').data('id');
    const selectedTypeID = $('#plane-type-select').val();
    const existingSinglePlane = String($('#single-unit-plane-input').val() || '').trim();

    (allData.planes || [])
        .filter(p => {
            const matchType = !selectedTypeID || p.typeID == selectedTypeID;
            const matchProject = !selectedProjectID || p.projectID == selectedProjectID;
            return matchType && matchProject;
        })
        .map(p => p.planeID)
        .filter(Boolean);

    const uniquePlaneValues = [...new Set(filteredPlanes)];
    renderDatalist('#tracked-plane-list', uniquePlaneValues);

    if (existingSinglePlane && !uniquePlaneValues.includes(existingSinglePlane)) {
        $('#single-unit-plane-input').val('');
    }
}

function renderSelect(selector, list, valField, textField) {
    const $el = $(selector);
    const placeholder = $el.find('option:first').text() || 'בחר...';
    $el.empty().append(`<option value="">${placeholder}</option>`);
    (list || []).forEach(item => {
        $el.append(`<option value="${item[valField]}">${item[textField]}</option>`);
    });
}

function normalizeProductionItems(items) {
    const map = new Map();
    (items || []).forEach(item => {
        const id = String(item.productionItemID || item.ProductionItemID || '').trim();
        const name = String(item.itemName || item.ItemName || '').trim();
        if (!id) return;
        if (!map.has(id)) {
            map.set(id, { productionItemID: id, itemName: name });
        }
    });
    return Array.from(map.values());
}

function normalizeItemPlaneMappings(rawMappings) {
    const pairSet = new Set();
    const normalized = [];

    (rawMappings || []).forEach(row => {
        const itemId = String(row.productionItemID || row.ProductionItemID || row.itemID || row.ItemID || '').trim();
        const planeTypeID = parseNullableInt(row.planeTypeID ?? row.PlaneTypeID);
        if (!itemId || !planeTypeID || planeTypeID <= 0) {
            return;
        }

        const key = `${itemId}|${planeTypeID}`;
        if (pairSet.has(key)) {
            return;
        }

        pairSet.add(key);
        normalized.push({ productionItemID: itemId, planeTypeID });
    });

    return normalized;
}

function setOptions($select, options, placeholder, previousValue) {
    $select.empty().append(`<option value="">${placeholder}</option>`);
    options.forEach(opt => {
        $select.append(`<option value="${opt.value}">${opt.label}</option>`);
    });

    if (previousValue && options.some(o => String(o.value) === String(previousValue))) {
        $select.val(String(previousValue));
    }
}

function applyItemPlaneFilters(source) {
    const $itemSelect = $('#item-code-select');
    const $planeTypeSelect = $('#plane-type-select');

    const selectedItem = String($itemSelect.val() || '').trim();
    const selectedPlaneType = parseNullableInt($planeTypeSelect.val());

    if (!itemPlaneMappings.length) {
        setOptions(
            $itemSelect,
            allProductionItems.map(i => ({ value: i.productionItemID, label: i.productionItemID })),
            'בחר מק״ט...',
            selectedItem
        );
        setOptions(
            $planeTypeSelect,
            allPlaneTypes.map(t => ({ value: t.planeTypeID, label: t.planeTypeName })),
            'בחר סוג...',
            selectedPlaneType
        );
        const itemObj = allProductionItems.find(i => i.productionItemID === String($itemSelect.val() || '').trim());
        $('#item-name-input').val(itemObj ? itemObj.itemName : '');
        return;
    }

    let allowedItems = allProductionItems;
    if (selectedPlaneType) {
        const allowedIds = new Set(itemPlaneMappings
            .filter(m => m.planeTypeID === selectedPlaneType)
            .map(m => m.productionItemID));
        allowedItems = allProductionItems.filter(i => allowedIds.has(i.productionItemID));
    }

    let allowedPlaneTypes = allPlaneTypes;
    if (selectedItem) {
        const allowedTypeIds = new Set(itemPlaneMappings
            .filter(m => m.productionItemID === selectedItem)
            .map(m => m.planeTypeID));
        allowedPlaneTypes = allPlaneTypes.filter(t => allowedTypeIds.has(t.planeTypeID));
    }

    setOptions(
        $itemSelect,
        allowedItems.map(i => ({ value: i.productionItemID, label: i.productionItemID })),
        'בחר מק״ט...',
        selectedItem
    );

    setOptions(
        $planeTypeSelect,
        allowedPlaneTypes.map(t => ({ value: t.planeTypeID, label: t.planeTypeName })),
        'בחר סוג...',
        selectedPlaneType
    );

    const currentItem = String($itemSelect.val() || '').trim();
    const currentType = parseNullableInt($planeTypeSelect.val());

    if (source === 'planeType' && selectedItem && !currentItem) {
        $('#item-name-input').val('');
    }

    if (source === 'item' && selectedPlaneType && !currentType) {
        // the previously selected plane type became invalid and was cleared
    }

    const itemObj = allProductionItems.find(i => i.productionItemID === currentItem);
    $('#item-name-input').val(itemObj ? itemObj.itemName : '');
}

function renderDatalist(selector, list, field = null) {
    const $el = $(selector);
    $el.empty();
    (list || []).forEach(item => {
        const val = field ? item[field] : item;
        if (val !== undefined && val !== null && String(val).trim() !== '') {
            $el.append(`<option value="${val}">`);
        }
    });
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
        updateProjectDisplayName();
    }

}
