const usersApiBase = "https://localhost:7296/api/Users";
let usersCache = [];
let pendingDeleteUserID = 0;

window.initAdminUsers = function () {
    bindUsersPageEvents();
    loadUsersList();
};

function bindUsersPageEvents() {
    document.getElementById("createUserBtn")?.addEventListener("click", openCreateUserModal);
    document.getElementById("closeCreateUserModal")?.addEventListener("click", closeCreateUserModal);
    document.getElementById("cancelCreateUserBtn")?.addEventListener("click", closeCreateUserModal);
    document.getElementById("createUserModal")?.addEventListener("click", closeCreateUserModal);
    document.getElementById("createUserForm")?.addEventListener("submit", onCreateUserSubmit);

    document.getElementById("closeManageUserModal")?.addEventListener("click", closeManageUserModal);
    document.getElementById("manageUserModal")?.addEventListener("click", closeManageUserModal);
    document.getElementById("manageUserForm")?.addEventListener("submit", onManageUserSave);
    document.getElementById("resetPasswordBtn")?.addEventListener("click", onResetUserPassword);
    document.getElementById("deleteUserBtn")?.addEventListener("click", onDeleteUser);
    document.getElementById("cancelDeleteInlineBtn")?.addEventListener("click", closeDeletePopover);
    document.getElementById("confirmDeleteInlineBtn")?.addEventListener("click", confirmDeleteUser);
    document.addEventListener("click", handleOutsideDeletePopoverClick);
}

function loadUsersList() {
    ajaxCall(
        "GET",
        usersApiBase,
        null,
        function (data) {
            const users = normalizeUsers(data);
            usersCache = users;
            renderUsersTable(users);
            setUsersAlert("", "");
        },
        function (err) {
            usersCache = [];
            renderUsersTable([]);
            setUsersAlert(readApiError(err, "טעינת המשתמשים נכשלה"), "error");
        }
    );
}

function normalizeUsers(data) {
    if (Array.isArray(data)) return data;
    if (Array.isArray(data?.$values)) return data.$values;
    return [];
}

function renderUsersTable(users) {
    const tbody = document.getElementById("usersTableBody");
    if (!tbody) return;

    if (!users.length) {
        tbody.innerHTML = `<tr><td colspan="7">לא נמצאו משתמשים</td></tr>`;
        return;
    }

    tbody.innerHTML = users.map(user => {
        const userID = valueOf(user, "userID", "UserID");
        const username = valueOf(user, "username", "Username");
        const fullName = valueOf(user, "fullName", "FullName");
        const isActive = boolOf(user, "isActive", "IsActive");
        const canViewProduction = boolOf(user, "canViewProduction", "CanViewProduction");
        const canViewStock = boolOf(user, "canViewStock", "CanViewStock");
        const canManageUsers = boolOf(user, "canManageUsers", "CanManageUsers");
        const mustChangePassword = boolOf(user, "mustChangePassword", "MustChangePassword");

        return `
            <tr data-user-id="${escapeHtml(userID)}">
                <td>${escapeHtml(username || "-")}</td>
                <td>${escapeHtml(fullName || "-")}</td>
                <td>${flagHtml(isActive)}</td>
                <td>${flagHtml(canViewProduction)}</td>
                <td>${flagHtml(canViewStock)}</td>
                <td>${flagHtml(canManageUsers)}</td>
                <td>${flagHtml(mustChangePassword)}</td>
            </tr>
        `;
    }).join("");

    tbody.querySelectorAll("tr[data-user-id]").forEach(row => {
        row.addEventListener("click", function () {
            const userID = parseInt(row.getAttribute("data-user-id"), 10);
            if (userID > 0) {
                openManageUserModal(userID);
            }
        });
    });
}

function flagHtml(value) {
    return `<span class="users-flag ${value ? "on" : "off"}">${value ? "כן" : "לא"}</span>`;
}

function openCreateUserModal() {
    const form = document.getElementById("createUserForm");
    if (form) form.reset();
    resetCreateResult();

    const activeCheckbox = document.getElementById("newUserIsActive");
    if (activeCheckbox) activeCheckbox.checked = true;

    document.getElementById("createUserModal")?.classList.add("open");
}

function closeCreateUserModal() {
    resetCreateResult();
    document.getElementById("createUserModal")?.classList.remove("open");
}

function onCreateUserSubmit(event) {
    event.preventDefault();

    const currentUser = getCurrentSessionUser();
    const username = (document.getElementById("newUserUsername")?.value || "").trim();
    const fullName = (document.getElementById("newUserFullName")?.value || "").trim();
    const isActive = !!document.getElementById("newUserIsActive")?.checked;
    const canViewProduction = !!document.getElementById("newUserCanViewProduction")?.checked;
    const canViewStock = !!document.getElementById("newUserCanViewStock")?.checked;
    const canManageUsers = !!document.getElementById("newUserCanManageUsers")?.checked;

    const createPayload = {
        username,
        fullName,
        isActive,
        canViewProduction,
        canViewStock,
        canManageUsers,
        createdByUserID: currentUser?.userID || null
    };

    ajaxCall(
        "POST",
        usersApiBase,
        JSON.stringify(createPayload),
        function (createResult) {
            const temporaryPassword = valueOf(createResult, "temporaryPassword", "TemporaryPassword");
            showCreateSuccess(temporaryPassword);
            loadUsersList();
        },
        function (err) {
            resetCreateResult();
            setUsersAlert(readApiError(err, "יצירת המשתמש נכשלה"), "error");
        }
    );
}

function showCreateSuccess(temporaryPassword) {
    const safePassword = escapeHtml(temporaryPassword || "-");
    const message = `
        משתמש נוצר בהצלחה. סיסמה זמנית למסירה למשתמש:
        <span id="tempPasswordValue" class="users-temp-pass">${safePassword}</span>
        <button id="copyTempPasswordBtn" type="button" class="users-copy-btn">העתק</button>
    `;

    setCreateResult(message, true);
    setUsersAlert("המשתמש נוצר בהצלחה", "success");

    const copyBtn = document.getElementById("copyTempPasswordBtn");
    copyBtn?.addEventListener("click", function () {
        copyTextToClipboard(temporaryPassword || "", copyBtn);
    });
}

function openManageUserModal(userID) {
    ajaxCall(
        "GET",
        `${usersApiBase}/${userID}`,
        null,
        function (user) {
            fillManageUserForm(user);
            setManageResult("", false);
            document.getElementById("manageUserModal")?.classList.add("open");
        },
        function (err) {
            setUsersAlert(readApiError(err, "טעינת פרטי משתמש נכשלה"), "error");
        }
    );
}

function fillManageUserForm(user) {
    const userID = valueOf(user, "userID", "UserID");
    const username = valueOf(user, "username", "Username");
    const fullName = valueOf(user, "fullName", "FullName");

    document.getElementById("editUserId").value = String(userID || "");
    document.getElementById("editUsername").value = username || "";
    document.getElementById("editFullName").value = fullName || "";
    document.getElementById("editIsActive").checked = boolOf(user, "isActive", "IsActive");
    document.getElementById("editCanViewProduction").checked = boolOf(user, "canViewProduction", "CanViewProduction");
    document.getElementById("editCanViewStock").checked = boolOf(user, "canViewStock", "CanViewStock");
    document.getElementById("editCanManageUsers").checked = boolOf(user, "canManageUsers", "CanManageUsers");

    updateDeleteButtonState(user);
    setDeleteError("");
}

function updateDeleteButtonState(user) {
    const deleteBtn = document.getElementById("deleteUserBtn");
    if (!deleteBtn) return;
    deleteBtn.disabled = false;
    deleteBtn.removeAttribute("title");
}

function closeManageUserModal() {
    document.getElementById("manageUserModal")?.classList.remove("open");
    closeDeletePopover();
    setDeleteError("");
}

function onManageUserSave(event) {
    event.preventDefault();

    const userID = parseInt(document.getElementById("editUserId")?.value || "0", 10);
    if (userID <= 0) {
        setManageResult("משתמש לא תקין", false);
        return;
    }

    const payload = {
        userID,
        fullName: (document.getElementById("editFullName")?.value || "").trim(),
        isActive: !!document.getElementById("editIsActive")?.checked,
        canViewProduction: !!document.getElementById("editCanViewProduction")?.checked,
        canViewStock: !!document.getElementById("editCanViewStock")?.checked,
        canManageUsers: !!document.getElementById("editCanManageUsers")?.checked
    };

    ajaxCall(
        "PUT",
        `${usersApiBase}/${userID}`,
        JSON.stringify(payload),
        function () {
            setManageResult("המשתמש עודכן בהצלחה", true);
            setUsersAlert("המשתמש עודכן בהצלחה", "success");
            loadUsersList();
        },
        function (err) {
            setManageResult(readApiError(err, "עדכון המשתמש נכשל"), false);
        }
    );
}

function onResetUserPassword() {
    const userID = parseInt(document.getElementById("editUserId")?.value || "0", 10);
    if (userID <= 0) {
        setManageResult("משתמש לא תקין", false);
        return;
    }

    ajaxCall(
        "POST",
        `${usersApiBase}/reset-password`,
        JSON.stringify({ userID }),
        function (result) {
            const temporaryPassword = valueOf(result, "temporaryPassword", "TemporaryPassword");
            const safePassword = escapeHtml(temporaryPassword || "-");
            const html = `
                הסיסמה אופסה. סיסמה זמנית חדשה למסירה למשתמש:
                <span id="resetTempPasswordValue" class="users-temp-pass">${safePassword}</span>
                <button id="copyResetPasswordBtn" type="button" class="users-copy-btn">העתק</button>
            `;
            setManageResult(html, true);
            setUsersAlert("בוצע איפוס סיסמה למשתמש", "success");
            loadUsersList();

            const copyBtn = document.getElementById("copyResetPasswordBtn");
            copyBtn?.addEventListener("click", function () {
                copyTextToClipboard(temporaryPassword || "", copyBtn);
            });
        },
        function (err) {
            setManageResult(readApiError(err, "איפוס הסיסמה נכשל"), false);
        }
    );
}

function onDeleteUser(event) {
    event?.stopPropagation();

    const userID = parseInt(document.getElementById("editUserId")?.value || "0", 10);
    if (userID <= 0) {
        setManageResult("משתמש לא תקין", false);
        return;
    }

    const blockedReason = getDeleteBlockedReason(userID);
    if (blockedReason) {
        closeDeletePopover();
        setDeleteError(blockedReason);
        return;
    }

    setDeleteError("");
    openDeletePopover(userID);
}

function getDeleteBlockedReason(userID) {
    const currentUser = getCurrentSessionUser();
    if (currentUser?.userID === userID) {
        return "לא ניתן למחוק את המשתמש המחובר";
    }

    const isAdmin = !!document.getElementById("editCanManageUsers")?.checked;
    if (!isAdmin) {
        return "";
    }

    let adminsCount = 0;
    usersCache.forEach(u => {
        if (boolOf(u, "canManageUsers", "CanManageUsers")) {
            adminsCount += 1;
        }
    });

    return adminsCount <= 1 ? "לא ניתן למחוק את המנהל האחרון במערכת" : "";
}

function openDeletePopover(userID) {
    pendingDeleteUserID = userID;

    const popover = document.getElementById("deleteConfirmPopover");
    if (popover) {
        popover.hidden = false;
    }
}

function closeDeletePopover() {
    pendingDeleteUserID = 0;
    const popover = document.getElementById("deleteConfirmPopover");
    if (popover) {
        popover.hidden = true;
    }
}

function handleOutsideDeletePopoverClick(event) {
    const popover = document.getElementById("deleteConfirmPopover");
    const deleteBtn = document.getElementById("deleteUserBtn");
    if (!popover || popover.hidden) return;

    const target = event.target;
    if (popover.contains(target) || deleteBtn?.contains(target)) {
        return;
    }

    closeDeletePopover();
}

function confirmDeleteUser() {
    const userID = pendingDeleteUserID;
    if (userID <= 0) {
        closeDeletePopover();
        return;
    }

    const currentUser = getCurrentSessionUser();
    const currentUserID = currentUser?.userID || 0;

    ajaxCall(
        "DELETE",
        `${usersApiBase}/${userID}?currentUserID=${currentUserID}`,
        null,
        function () {
            closeDeletePopover();
            closeManageUserModal();
            setUsersAlert("המשתמש נמחק בהצלחה", "success");
            loadUsersList();
        },
        function (err) {
            const errorMsg = readApiError(err, "מחיקת המשתמש נכשלה");
            closeDeletePopover();
            setDeleteError(errorMsg);
        }
    );
}

function setCreateResult(message, isSuccess) {
    const resultBox = document.getElementById("createUserResult");
    if (!resultBox) return;

    if (!message) {
        resultBox.style.display = "none";
        resultBox.classList.remove("success");
        resultBox.innerHTML = "";
        return;
    }

    resultBox.style.display = "block";

    resultBox.classList.toggle("success", !!isSuccess);
    resultBox.innerHTML = message || "";
}

function resetCreateResult() {
    setCreateResult("", false);
}

function setManageResult(message, isSuccess) {
    const resultBox = document.getElementById("manageUserResult");
    if (!resultBox) return;

    if (!message) {
        resultBox.style.display = "none";
        resultBox.classList.remove("success");
        resultBox.innerHTML = "";
        return;
    }

    resultBox.style.display = "block";
    resultBox.classList.toggle("success", !!isSuccess);
    resultBox.innerHTML = message || "";
}

function setDeleteError(message) {
    const errorEl = document.getElementById("manageDeleteError");
    if (!errorEl) return;

    if (!message) {
        errorEl.hidden = true;
        errorEl.style.display = "none";
        errorEl.textContent = "";
        return;
    }

    errorEl.textContent = message;
    errorEl.hidden = false;
    errorEl.style.display = "block";
}

function setUsersAlert(message, type) {
    const alertEl = document.getElementById("usersAlert");
    if (!alertEl) return;

    alertEl.classList.remove("success", "error");
    if (type) {
        alertEl.classList.add(type);
    }
    alertEl.textContent = message || "";
}

function getCurrentSessionUser() {
    const raw = sessionStorage.getItem("bluevisionUser");
    if (!raw) return null;
    try {
        return JSON.parse(raw);
    } catch (e) {
        return null;
    }
}

function copyTextToClipboard(value, btnElement) {
    navigator.clipboard.writeText(value).then(function () {
        if (btnElement) {
            btnElement.textContent = "הועתק";
            setTimeout(() => { btnElement.textContent = "העתק"; }, 1500);
        }
    });
}

function valueOf(obj, camelName, pascalName) {
    if (!obj) return "";
    if (obj[camelName] !== undefined && obj[camelName] !== null) return obj[camelName];
    if (obj[pascalName] !== undefined && obj[pascalName] !== null) return obj[pascalName];
    return "";
}

function boolOf(obj, camelName, pascalName) {
    const value = valueOf(obj, camelName, pascalName);
    return value === true || value === "true" || value === 1;
}

function readApiError(err, fallbackMessage) {
    return err?.responseJSON?.message
        || err?.responseJSON?.error
        || err?.responseText
        || fallbackMessage;
}

function escapeHtml(value) {
    return String(value)
        .replace(/&/g, "&amp;")
        .replace(/</g, "&lt;")
        .replace(/>/g, "&gt;")
        .replace(/\"/g, "&quot;")
        .replace(/'/g, "&#39;");
}
