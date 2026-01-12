document.addEventListener("DOMContentLoaded", function () {
    const observer = new MutationObserver(mutationsList => {
        for (let mutation of mutationsList) {
            if (mutation.attributeName === 'class') {
                if (mutation.target.classList.contains("components-reconnect-failed")) {
                    window.Blazor.reconnect()
                }
                else if (mutation.target.classList.contains("components-reconnect-rejected")) {
                    location.reload()
                }
            }
        }
    });

    observer.observe(document.getElementById("components-reconnect-modal"), { attributes: true });
});
