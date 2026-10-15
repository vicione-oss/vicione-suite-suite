// Account pages hold a circuit only to show the reconnect modal when the server goes away. Firefox drops the
// circuit's WebSocket as soon as a navigation starts, which would show the modal until the next page arrives (#2700).
// A circuit ended on purpose shows no modal.
globalThis.addEventListener('beforeunload', () =>
    (globalThis as typeof globalThis & { Blazor?: { disconnect?: () => void } }).Blazor?.disconnect?.());
