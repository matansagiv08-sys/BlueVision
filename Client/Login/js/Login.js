const server = "https://localhost:7296/";
const USER_STORAGE_KEY = "bluevisionUser";

const loginForm = document.getElementById("loginForm");
const changePasswordForm = document.getElementById("changePasswordForm");
const authMessage = document.getElementById("authMessage");
const forgotPasswordLink = document.getElementById("forgotPasswordLink");

let pendingUser = null;
let pendingCurrentPassword = "";

initializeAuthView();

loginForm?.addEventListener("submit", async function (e) {
    e.preventDefault();
    clearMessage();

    const username = document.getElementById("username")?.value?.trim() || "";
    const password = document.getElementById("password")?.value || "";

    if (!username || !password) {
        showMessage("יש להזין שם משתמש וסיסמה", true);
        return;
    }

    try {
        const response = await fetch(`${server}api/Auth/login`, {
            method: "POST",
            headers: {
                "Content-Type": "application/json"
            },
            body: JSON.stringify({
                username,
                password
            })
        });

        const result = await response.json();

        if (!response.ok || !result.success) {
            showMessage(result.message || "ההתחברות נכשלה", true);
            return;
        }

        const userState = mapUserState(result);
        sessionStorage.setItem(USER_STORAGE_KEY, JSON.stringify(userState));

        if (userState.mustChangePassword) {
            pendingUser = userState;
            pendingCurrentPassword = password;
            loginForm.classList.add("hidden-block");
            changePasswordForm.classList.remove("hidden-block");
            showMessage("יש להחליף סיסמה לפני כניסה למערכת", false);
            return;
        }

        window.location.href = "../app/index.html";
    } catch (err) {
        showMessage("שגיאה בתקשורת מול השרת", true);
    }
});

changePasswordForm?.addEventListener("submit", async function (e) {
    e.preventDefault();
    clearMessage();

    if (!pendingUser || !pendingUser.userID) {
        showMessage("יש להתחבר מחדש", true);
        backToLogin();
        return;
    }

    const newPassword = document.getElementById("newPassword")?.value || "";
    const confirmNewPassword = document.getElementById("confirmNewPassword")?.value || "";

    if (!newPassword || newPassword.length < 6) {
        showMessage("הסיסמה החדשה חייבת להכיל לפחות 6 תווים", true);
        return;
    }

    if (newPassword !== confirmNewPassword) {
        showMessage("אימות הסיסמה אינו תואם", true);
        return;
    }

    try {
        const response = await fetch(`${server}api/Auth/change-password`, {
            method: "POST",
            headers: {
                "Content-Type": "application/json"
            },
            body: JSON.stringify({
                userID: pendingUser.userID,
                currentPassword: pendingCurrentPassword,
                newPassword
            })
        });

        const result = await response.json();

        if (!response.ok || !result.success) {
            showMessage(result.message || "עדכון הסיסמה נכשל", true);
            return;
        }

        pendingUser.mustChangePassword = false;
        sessionStorage.setItem(USER_STORAGE_KEY, JSON.stringify(pendingUser));
        showMessage("הסיסמה עודכנה בהצלחה", false);
        window.location.href = "../app/index.html";
    } catch (err) {
        showMessage("שגיאה בתקשורת מול השרת", true);
    }
});

forgotPasswordLink?.addEventListener("click", function (e) {
    e.preventDefault();
    showMessage("יש לפנות למנהל משתמשים לצורך איפוס סיסמה", true);
});

function mapUserState(loginResult) {
    return {
        userID: loginResult.userID,
        username: loginResult.username,
        fullName: loginResult.fullName,
        mustChangePassword: !!loginResult.mustChangePassword,
        canViewProduction: !!loginResult.canViewProduction,
        canViewStock: !!loginResult.canViewStock,
        canManageUsers: !!loginResult.canManageUsers
    };
}

function showMessage(message, isError) {
    authMessage.textContent = message || "";
    authMessage.classList.toggle("error", !!isError);
    authMessage.classList.toggle("success", !isError);
}

function clearMessage() {
    authMessage.textContent = "";
    authMessage.classList.remove("error");
    authMessage.classList.remove("success");
}

function backToLogin() {
    pendingUser = null;
    pendingCurrentPassword = "";
    changePasswordForm.classList.add("hidden-block");
    loginForm.classList.remove("hidden-block");
}

function initializeAuthView() {
    const rawUser = sessionStorage.getItem(USER_STORAGE_KEY);
    if (!rawUser) {
        return;
    }

    try {
        const savedUser = JSON.parse(rawUser);
        if (savedUser && savedUser.userID && !savedUser.mustChangePassword) {
            window.location.href = "../app/index.html";
        }
    } catch (e) {
        sessionStorage.removeItem(USER_STORAGE_KEY);
    }
}
