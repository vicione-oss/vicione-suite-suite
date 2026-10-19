type LinkResource = {
    id: string;
    rel: string;
    href: string;
    type: string;
    integrity: string;
    crossorigin: string;
    key: string;
};

type ScriptResource = {
    href: string;
    bundle: string;
    integrity: string;
    crossorigin: string;
};

const absoluteUrlResolver = document.createElement('a');

const viciOneInterop = {
    setCookie(name: string, value: string, days: number) {
        const expiry = new Date();
        expiry.setTime(expiry.getTime() + (days * 24 * 60 * 60 * 1000));
        document.cookie = `${name}=${value};expires=${expiry.toUTCString()};path=/`;
    },
    getCookie(name: string) {
        const prefix = `${name}=`;
        for (const cookie of decodeURIComponent(document.cookie).split(';')) {
            const trimmed = cookie.trimStart();
            if (trimmed.startsWith(prefix))
                return trimmed.slice(prefix.length);
        }

        return '';
    },
    updateTitle(title: string) {
        if (document.title !== title)
            document.title = title;
    },
    includeMeta(id: string, attribute: string, name: string, content: string, key: string) {
        let meta = id !== '' && key === 'id'
            ? document.querySelector<HTMLMetaElement>(`#${CSS.escape(id)}`)
            : document.querySelector<HTMLMetaElement>(`meta[${CSS.escape(attribute)}="${CSS.escape(name)}"]`);
        if (meta === null) {
            meta = document.createElement('meta');
            meta.setAttribute(attribute, name);
            if (id !== '')
                meta.id = id;

            meta.content = content;
            document.head.append(meta);
        } else {
            if (id !== '')
                meta.setAttribute('id', id);

            if (meta.content !== content)
                meta.setAttribute('content', content);
        }
    },
    // eslint-disable-next-line max-params -- Mirrors IJsInterop.IncludeLink, a public SDK signature.
    includeLink(id: string, rel: string, href: string, type: string, integrity: string, crossorigin: string, key: string) {
        let link = id !== '' && key === 'id'
            ? document.querySelector<HTMLLinkElement>(`#${CSS.escape(id)}`)
            : document.querySelector<HTMLLinkElement>(`link[href="${CSS.escape(href)}"]`);
        if (link === null) {
            link = document.createElement('link');
            if (id !== '')
                link.id = id;

            link.rel = rel;
            if (type !== '')
                link.type = type;

            link.href = href;
            if (integrity !== '')
                link.integrity = integrity;

            if (crossorigin !== '')
                link.crossOrigin = crossorigin;

            document.head.append(link);
            return;
        }

        if (link.id !== id)
            link.setAttribute('id', id);

        if (link.rel !== rel)
            link.setAttribute('rel', rel);

        if (type === '')
            link.removeAttribute('type');
        else if (link.type !== type)
            link.setAttribute('type', type);

        if (link.href !== viciOneInterop.getAbsoluteUrl(href)) {
            link.removeAttribute('integrity');
            link.removeAttribute('crossorigin');
            link.setAttribute('href', href);
        }

        if (integrity === '')
            link.removeAttribute('integrity');
        else if (link.integrity !== integrity)
            link.setAttribute('integrity', integrity);

        if (crossorigin === '')
            link.removeAttribute('crossorigin');
        else if (link.crossOrigin !== crossorigin)
            link.setAttribute('crossorigin', crossorigin);
    },
    includeLinks(links: LinkResource[]) {
        for (const link of links)
            viciOneInterop.includeLink(link.id, link.rel, link.href, link.type, link.integrity, link.crossorigin, link.key);
    },
    // eslint-disable-next-line max-params -- Mirrors IJsInterop.IncludeScript, a public SDK signature.
    includeScript(id: string, src: string, integrity: string, crossorigin: string, content: string, location: string, key: string) {
        let script = id !== '' && key === 'id'
            ? document.querySelector<HTMLScriptElement>(`#${CSS.escape(id)}`)
            : document.querySelector<HTMLScriptElement>(`script[src="${CSS.escape(src)}"]`);
        if (script === null) {
            script = document.createElement('script');
            if (id !== '')
                script.id = id;

            if (src === '') {
                script.textContent = content;
            } else {
                script.src = src;
                if (integrity !== '')
                    script.integrity = integrity;

                if (crossorigin !== '')
                    script.crossOrigin = crossorigin;
            }

            script.async = false;
            viciOneInterop.addScript(script, location)
                .then(() => {
                    console.log(`${src} loaded`);
                })
                .catch(() => {
                    console.error(`${src} failed`);
                });
            return;
        }

        if (script.id !== id)
            script.setAttribute('id', id);

        if (src === '') {
            if (script.textContent !== content)
                script.textContent = content;

            return;
        }

        if (script.src !== viciOneInterop.getAbsoluteUrl(src)) {
            script.removeAttribute('integrity');
            script.removeAttribute('crossorigin');
            script.src = src;
        }

        if (integrity === '')
            script.removeAttribute('integrity');
        else if (script.integrity !== integrity)
            script.setAttribute('integrity', integrity);

        if (crossorigin === '')
            script.removeAttribute('crossorigin');
        else if (script.crossOrigin !== crossorigin)
            script.setAttribute('crossorigin', crossorigin);
    },
    async addScript(script: HTMLScriptElement, location: string) {
        // An inline script runs while it is appended and fires no load event.
        const loaded = script.src === ''
            ? Promise.resolve()
            : new Promise<void>((resolve, reject) => {
                script.addEventListener('load', () => {
                    resolve();
                });
                script.addEventListener('error', () => {
                    reject(new Error(`${script.src} failed to load`));
                });
            });

        if (location === 'head')
            document.head.append(script);

        else if (location === 'body')
            document.body.append(script);

        return loaded;
    },
    async includeScripts(scripts: ScriptResource[]) {
        for (const script of scripts) {
            if (script.bundle === '')
                script.bundle = script.href;
        }

        const bundles = [...new Set(scripts.map(script => script.bundle))];
        await Promise.all(bundles
            .filter(bundle => !loadjs.isDefined(bundle))
            .map(async bundle => loadjs(scripts.filter(script => script.bundle === bundle).map(script => script.href), bundle, {
                async: false,
                returnPromise: true,
                before(path, element) {
                    for (const script of scripts) {
                        if (path === script.href && script.integrity !== '')
                            element.integrity = script.integrity;

                        if (path === script.href && script.crossorigin !== '')
                            element.crossOrigin = script.crossorigin;
                    }
                }
            })));
    },
    getAbsoluteUrl(url: string) {
        absoluteUrlResolver.href = url;
        return absoluteUrlResolver.href;
    },
    removeElementsById(prefix: string, first: string, last: string) {
        for (const element of document.querySelectorAll(`[id^="${CSS.escape(prefix)}"]`)) {
            if ((first === '' || element.id >= first) && (last === '' || element.id <= last))
                element.remove();
        }
    },
    removeScriptsBySource(prefix: string) {
        for (const element of document.querySelectorAll<HTMLScriptElement>(`[src="${CSS.escape(prefix)}"]`)) {
            if (element.src.endsWith(prefix))
                element.remove();
        }
    },
    getElementByName(name: string) {
        return document.querySelector<HTMLInputElement>(`[name="${CSS.escape(name)}"]`)?.value ?? '';
    },
    submitForm(path: string, fields: Record<string, string>) {
        const form = document.createElement('form');
        form.method = 'post';
        form.action = path;

        for (const [name, value] of Object.entries(fields)) {
            const hiddenField = document.createElement('input');
            hiddenField.type = 'hidden';
            hiddenField.name = name;
            hiddenField.value = value;
            form.append(hiddenField);
        }

        document.body.append(form);
        form.submit();
    },
    signOut(path: string) {
        // Firefox drops the circuit's WebSocket as soon as the navigation starts, which would show the reconnect
        // modal until the next page arrives (#2700). A circuit ended on purpose shows no modal.
        globalThis.Blazor.disconnect?.();
        viciOneInterop.submitForm(path, {});
    },
    redirectToSignIn(url: string) {
        // Ends the circuit first for the same reason as signOut.
        globalThis.Blazor.disconnect?.();
        globalThis.location.assign(url);
    },
    getFiles(id: string) {
        const fileInput = document.querySelector<HTMLInputElement>(`#${CSS.escape(id)}`);
        return [...fileInput?.files ?? []].map(file => file.name);
    },
    uploadFiles(postUrl: string, folder: string, id: string) {
        const fileInput = document.querySelector<HTMLInputElement>(`#${CSS.escape(`${id}FileInput`)}`)!;
        const progressInfo = document.querySelector<HTMLElement>(`#${CSS.escape(`${id}ProgressInfo`)}`)!;
        const progressBar = document.querySelector<HTMLProgressElement>(`#${CSS.escape(`${id}ProgressBar`)}`)!;
        const chunkSize = 1024 * 1024;

        progressInfo.setAttribute('style', 'display: inline;');
        progressBar.setAttribute('style', 'width: 200px; display: inline;');

        for (const file of fileInput.files ?? []) {
            const totalParts = Math.ceil(file.size / chunkSize);

            for (let part = 1; part <= totalParts; part++) {
                const chunk = file.slice((part - 1) * chunkSize, part * chunkSize);
                const chunkName = `${file.name}.part_${part.toString().padStart(3, '0')}_${totalParts.toString().padStart(3, '0')}`;

                const data = new FormData();
                data.append('folder', folder);
                data.append('formfile', chunk, chunkName);
                const request = new XMLHttpRequest();
                request.open('POST', postUrl, true);
                request.upload.addEventListener('loadstart', () => {
                    progressInfo.textContent = `${file.name} 0%`;
                    progressBar.value = 0;
                });
                request.upload.addEventListener('progress', event => {
                    const percent = Math.ceil((event.loaded / event.total) * 100);
                    progressInfo.textContent = `${file.name}[${part}] ${percent}%`;
                    progressBar.value = percent / 100;
                });
                request.upload.addEventListener('loadend', () => {
                    progressInfo.textContent = `${file.name} 100%`;
                    progressBar.value = 1;
                });
                request.send(data);
            }
        }

        fileInput.value = '';
    },
    refreshBrowser(shouldForceGet: boolean, wait: number) {
        setTimeout(() => {
            // Firefox honours the non-standard forceGet argument; other browsers ignore it.
            (globalThis.location as unknown as { reload(shouldForceGet: boolean): void }).reload(shouldForceGet);
        }, wait * 1000);
    },
    redirectBrowser(url: string, wait: number) {
        setTimeout(() => {
            globalThis.location.assign(url);
        }, wait * 1000);
    },
    formValid(form: HTMLFormElement) {
        return form.checkValidity();
    },
    setElementAttribute(id: string, attribute: string, value: string) {
        document.querySelector(`#${CSS.escape(id)}`)?.setAttribute(attribute, value);
    },
    submitExistingForm(form: HTMLFormElement | undefined) {
        form?.submit();
    }
};

// eslint-disable-next-line unicorn/no-global-object-property-assignment -- JsInterop.cs invokes ViciOne.Interop.* by this global name.
globalThis.ViciOne ??= {};
globalThis.ViciOne.Interop = viciOneInterop;
