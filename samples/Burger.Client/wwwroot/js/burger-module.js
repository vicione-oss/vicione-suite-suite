
/* A example to load global styles into head section */
(function () {
    function onReady(fn) {
        if (document.readyState !== "loading") {
            fn();
        } else {
            document.addEventListener("DOMContentLoaded", fn);
        }
    }

    onReady(function () {
        console.log("Global Burger js loaded!");
    });
})();
