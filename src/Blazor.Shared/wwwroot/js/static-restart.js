function getQueryParam(param) {
    const urlParams = new URLSearchParams(window.location.search);
    return urlParams.get(param);
}

document.addEventListener("DOMContentLoaded", function () {
    const title = getQueryParam("title");
    const restartMessage = getQueryParam("restartMessage");
    const connectionMessage = getQueryParam("connectionMessage");
    const waitMessage = getQueryParam("waitMessage");
    const countdownOffset = parseInt(getQueryParam("offset"));
    const version = getQueryParam("version");

    document.getElementById("title").textContent = title;
    document.getElementById("heading").textContent = title;
    document.getElementById("restartMessage").textContent = restartMessage;
    document.getElementById("connectionMessage").textContent = connectionMessage;
    document.getElementById("waitMessage").textContent = waitMessage;

    const d = new Date();
    let year = d.getUTCFullYear();
    const companyElement = document.getElementById("company");
    companyElement.textContent = companyElement.textContent + " " + year;

    const versionElement = document.getElementById("version");
    versionElement.textContent = versionElement.textContent + " " + version;
    versionElement.title = version;

    let counter = 5 + countdownOffset;
    const healthCheck = window.location.origin + "/hc"

    const countdownInterval = setInterval(async () => {
        counter--;

        if (counter == 0) {

            try {
                var response = await fetch(healthCheck);

                if (response.ok) {
                    clearInterval(countdownInterval);
                    window.location.href = window.location.origin;
                }
                else {
                    counter = 5;
                }
            }
            catch (e) {
                counter = 5;
            }
        }
    }, 1000);
});
