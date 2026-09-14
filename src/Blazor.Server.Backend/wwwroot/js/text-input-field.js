function togglePassword(inputId, button) {
    const input = document.getElementById(inputId);
    if (!input) return;

    const isPassword = input.type === "password";
    const showElement = button.querySelector('.show-icon');
    const hideElement = button.querySelector('.hide-icon');

    input.type = isPassword ? "text" : "password";

    if (showElement) {
        showElement.style.display = isPassword ? 'none' : 'inline-block';
    }

    if (hideElement) {
        hideElement.style.display = isPassword ? 'inline-block' : 'none';
    }
}

function onPasswordToggleClick(event) {
    const button = event.target.closest("[data-toggle-password]");
    if (!button) return;

    togglePassword(button.dataset.togglePassword, button);
}

function attachPasswordToggleOnce() {
    if (window.passwordToggleAttached) return;

    window.passwordToggleAttached = true;
    document.addEventListener("click", onPasswordToggleClick);
}

attachPasswordToggleOnce();
