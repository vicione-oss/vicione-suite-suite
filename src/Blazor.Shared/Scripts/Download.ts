const viciOneDownload = {
    triggerFileDownload(fileName: string | undefined, url: string) {
        const anchorElement = document.createElement('a');
        anchorElement.href = url;

        const downloadName = fileName ?? '';
        if (downloadName !== '')
            anchorElement.download = downloadName;

        anchorElement.click();
        anchorElement.remove();
    },
    async fileFromStream(fileName: string | undefined, contentStreamReference: { arrayBuffer(): Promise<ArrayBuffer> }) {
        const arrayBuffer = await contentStreamReference.arrayBuffer();
        const url = URL.createObjectURL(new Blob([arrayBuffer]));

        viciOneDownload.triggerFileDownload(fileName, url);

        URL.revokeObjectURL(url);
    }
};

// eslint-disable-next-line unicorn/no-global-object-property-assignment -- JsInterop.cs invokes ViciOne.Download.* by this global name.
globalThis.ViciOne ??= {};
globalThis.ViciOne.Download = viciOneDownload;
