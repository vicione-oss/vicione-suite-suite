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

async function obtainAndCreateCredentials(passkeyName, forgeryTokenName, forgeryTokenValue) {
    window.SuitePasskeys.AbortController?.abort();
    window.SuitePasskeys.AbortController = new AbortController();
    const headers = {
        [forgeryTokenName]: forgeryTokenValue
    };

    const credentials = await createCredential(headers, window.SuitePasskeys.AbortController.signal);
    const formData = new FormData();
    formData.append('Name', passkeyName);
    formData.append('CredentialJson', JSON.stringify(credentials));

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
