document.addEventListener("DOMContentLoaded", function () {
    const healthCheck = window.location.origin + "/hc";

    // Get the login button
    var buttons = document.getElementById("account").querySelectorAll('button[type=submit]');
    var info = document.getElementById("blazor-disconnect-info");

    const countdownInterval = setInterval(async () => {

        try {
            var response = await fetch(healthCheck);
            [].forEach.call(buttons, function (input) {
                input.disabled = response.ok ? false : true;
            });

            info.style.display = response.ok ? "none" : "block";

        } catch (e) {
            [].forEach.call(buttons, function (input) {
                input.disabled = true;
            });
            info.style.display = "block";
        }
    }, 4000);
});
