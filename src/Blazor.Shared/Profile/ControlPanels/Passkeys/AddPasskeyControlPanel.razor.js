// Adapted from the standard Blazor "Individual Authentication" template
// (dotnet new blazor -au Individual, .NET 10), which ships passkey support.
// Kept intentionally close to the original — treat as generated; see docs/passkeys.md.
// MS guidance: https://learn.microsoft.com/en-us/aspnet/core/security/authentication/passkeys/blazor
//
// Passkey helpers below are intentionally duplicated with passkey-submit.js;
// see docs/passkeys.md. Keep both copies in sync when fixing.
async function fetchWithErrorHandling(url, options = {}) {
    const response = await fetch(url, {
        credentials: 'include',
        ...options
    });
    if (!response.ok) {
        const text = await response.text();
        console.error(text);
        throw new Error(`The server responded with status ${response.status}.`);
    }
    return response;
}

async function createCredential(headers, signal) {
    const optionsResponse = await fetchWithErrorHandling('/account/passkey-creation-options', {
        method: 'POST',
        headers,
        signal,
    });
    const optionsJson = await optionsResponse.json();
    const options = PublicKeyCredential.parseCreationOptionsFromJSON(optionsJson);
    return await navigator.credentials.create({publicKey: options, signal});
}

// Encodes a credential field (byte array/ArrayBuffer/Uint8Array/string) as a base64url
// string. Part of the manual PublicKeyCredential serialization that mitigates the
// "TypeError: Illegal invocation" thrown by some password managers; see:
// https://github.com/dotnet/AspNetCore.Docs/blob/main/aspnetcore/security/authentication/passkeys/index.md#mitigate-publickeycredentialtojson-error-typeerror-illegal-invocation
function convertToBase64(o) {
    if (!o) {
        return undefined;
    }

    if (Array.isArray(o)) {
        o = Uint8Array.from(o);
    }

    if (o instanceof ArrayBuffer) {
        o = new Uint8Array(o);
    }

    if (o instanceof Uint8Array) {
        let str = '';
        for (let i = 0; i < o.byteLength; i++) {
            str += String.fromCharCode(o[i]);
        }
        o = window.btoa(str);
    }

    if (typeof o !== 'string') {
        throw new Error("Could not convert to base64 string");
    }

    // Convert standard base64 to base64url: '+' -> '-', '/' -> '_', and strip '=' padding.
    // e.g. "ab+/c==" -> "ab-_c"
    o = o.replace(/\+/g, "-").replace(/\//g, "_").replace(/=*$/g, "");

    return o;
}

async function obtainAndCreateCredentials(passkeyName, forgeryTokenName, forgeryTokenValue) {
    window.SuitePasskeys.AbortController?.abort();
    window.SuitePasskeys.AbortController = new AbortController();
    const headers = {
        [forgeryTokenName]: forgeryTokenValue
    };

    const credentials = await createCredential(headers, window.SuitePasskeys.AbortController.signal);
    const formData = new FormData();
    formData.append('Name', passkeyName);
    // Some password managers (1Password, Microsoft Authenticator) don't implement
    // PublicKeyCredential.toJSON correctly, so JSON.stringify(credentials) drops
    // clientExtensionResults (required by the server) or throws. Serialize manually instead.
    // Revisit when these tools conform — tracked in
    // https://gitlab.com/vicione-oss/vicione/suite/suite/-/work_items/2817
    formData.append('CredentialJson', JSON.stringify({
        authenticatorAttachment: credentials.authenticatorAttachment,
        clientExtensionResults: credentials.getClientExtensionResults(),
        id: credentials.id,
        rawId: convertToBase64(credentials.rawId),
        response: {
            attestationObject: convertToBase64(credentials.response.attestationObject),
            authenticatorData: convertToBase64(credentials.response.authenticatorData ??
                credentials.response.getAuthenticatorData?.() ?? undefined),
            clientDataJSON: convertToBase64(credentials.response.clientDataJSON),
            publicKey: convertToBase64(credentials.response.getPublicKey?.() ?? undefined),
            publicKeyAlgorithm: credentials.response.getPublicKeyAlgorithm?.() ?? undefined,
            transports: credentials.response.getTransports?.() ?? undefined,
            signature: convertToBase64(credentials.response.signature),
            userHandle: convertToBase64(credentials.response.userHandle),
        },
        type: credentials.type,
    }));

    await fetchWithErrorHandling('/account/add-passkey', {
        method: 'POST',
        body: formData,
        headers,
    });
}

const SuitePasskeys = window.SuitePasskeys || {};

SuitePasskeys.ObtainAndCreateCredentials = obtainAndCreateCredentials;
SuitePasskeys.AbortController = new AbortController();

window.SuitePasskeys = SuitePasskeys;
