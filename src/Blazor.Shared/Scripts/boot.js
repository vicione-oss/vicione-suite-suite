Blazor.start({
    circuit: {
        reconnectionOptions: {
            maxRetries: Number.MAX_SAFE_INTEGER,
            retryIntervalMilliseconds: 1000 // moderate
        }
    }
});
