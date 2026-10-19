// Bundled last into suite.js (BundleSuiteScripts in Blazor.Shared.csproj), so the ViciOne and ACX globals exist before the
// circuit makes its first interop call.
void Blazor.start({
    circuit: {
        reconnectionOptions: {
            maxRetries: Number.MAX_SAFE_INTEGER,
            retryIntervalMilliseconds: 1000
        }
    }
});
