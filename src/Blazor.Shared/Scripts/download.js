(function(window) {

    const ViciOne = window.ViciOne || {};

    /**
     * @class
     * @memberof ViciOne
     */
    const Download = function() {
    };

    /**
     * @param {string} fileName
     * @param {string} url
     */
    Download.triggerFileDownload = function(fileName, url) {
        const anchorElement = document.createElement("a");
        anchorElement.href = url;

        if (fileName) {
            anchorElement.download = fileName;
        }

        anchorElement.click();
        anchorElement.remove();
    };

    Download.fileFromStream = async function(fileName, contentStreamReference) {
        const arrayBuffer = await contentStreamReference.arrayBuffer();
        const blob = new Blob([arrayBuffer]);

        const url = URL.createObjectURL(blob);

        Download.triggerFileDownload(fileName, url);

        URL.revokeObjectURL(url);
    };

    ViciOne.Download = Download;
    window.ViciOne = ViciOne;

})(self);
