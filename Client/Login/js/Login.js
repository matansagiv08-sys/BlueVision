document.getElementById('loginForm').addEventListener('submit', function (e) {
    e.preventDefault(); // מונע מהדף להתרענן

    const user = document.getElementById('username').value;
    const pass = document.getElementById('password').value;

    console.log("ניסיון התחברות עבור:", user);

    // כאן תוכל להוסיף קריאת API לשרת שלך
    alert('נשלח ניסיון התחברות עבור המשתמש: ' + user);
});