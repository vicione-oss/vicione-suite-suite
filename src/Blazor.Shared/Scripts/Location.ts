const acxLocation = {
    getCurrentUri() {
        return globalThis.location.href;
    },
    setUriParam(parameter: string, value: string) {
        const url = new URL(acxLocation.getCurrentUri());
        url.searchParams.set(parameter, value);

        globalThis.history.replaceState('', '', url.href);
    },
    open(url: string) {
        globalThis.open(url);
    }
};

// eslint-disable-next-line unicorn/no-global-object-property-assignment -- The dx repo calls ACX.Location.* by this global name.
globalThis.ACX ??= {};
globalThis.ACX.Location = acxLocation;
