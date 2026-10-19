// Globals shared with blazor.web.js and loadjs.min.js, and the namespaces C# and the dx repo call by name.

declare var Blazor: {
    start(options: unknown): Promise<void>;
    disconnect?(): void;
};

declare var loadjs: {
    (paths: string[], bundleId: string, options: {
        async: boolean;
        returnPromise: true;
        before(path: string, element: HTMLScriptElement): void;
    }): Promise<void>;
    isDefined(bundleId: string): boolean;
};

declare var ViciOne: Record<string, unknown> | undefined;

declare var ACX: Record<string, unknown> | undefined;
