(function(window) {

    const ACX = window.ACX || {};

    /**
     * @class
     * @memberof ACX
     */
    const Location = function() {
    };

    /**
     * @returns {string}
     */
    Location.getCurrentUri = function() {
        return window.location.href;
    };

    /**
     * @param {string} param
     * @param {string} value
     */
    Location.setUriParam = function (param, value) {
        let url = new URL(Location.getCurrentUri());
        let queryString = url.search;
        let searchParams = new URLSearchParams(queryString);

        searchParams.set(param, value);
        url.search = searchParams.toString();

        window.history.replaceState('', '', url.toString());
    }

     /**
     * @param {string} args
     */
    Location.open = function (args) {
        window.open(args);
    }

    ACX.Location = Location;
    window.ACX = ACX;

})(self);
