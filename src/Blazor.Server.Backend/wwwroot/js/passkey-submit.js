// Adapted from the standard Blazor "Individual Authentication" template
// (dotnet new blazor -au Individual, .NET 10), which ships passkey support.
// Kept intentionally close to the original — treat as generated; see docs/passkeys.md.
// MS guidance: https://learn.microsoft.com/en-us/aspnet/core/security/authentication/passkeys/blazor
const browserSupportsPasskeys =
    typeof navigator.credentials !== 'undefined' &&
    typeof window.PublicKeyCredential !== 'undefined' &&
    typeof window.PublicKeyCredential.parseCreationOptionsFromJSON === 'function' &&
    typeof window.PublicKeyCredential.parseRequestOptionsFromJSON === 'function';

// Passkey helpers below are intentionally duplicated with AddPasskeyControlPanel.razor.js;
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
    return await navigator.credentials.create({ publicKey: options, signal });
}

async function requestCredential(username, mediation, headers, signal) {
    const url = `/account/passkey-request-options?username=${encodeURIComponent(username)}`;
    const optionsResponse = await fetchWithErrorHandling(url, {
        method: 'POST',
        headers,
        signal,
    });
    const optionsJson = await optionsResponse.json();
    const options = PublicKeyCredential.parseRequestOptionsFromJSON(optionsJson);
    return await navigator.credentials.get({ publicKey: options, mediation, signal });
}

customElements.define('passkey-submit', class extends HTMLElement {
    static formAssociated = true;

    connectedCallback() {
        this.internals = this.attachInternals();
        this.attrs = {
            operation: this.getAttribute('operation'),
            name: this.getAttribute('name'),
            usernameField: this.getAttribute('username-field'),
            requestTokenName: this.getAttribute('request-token-name'),
            requestTokenValue: this.getAttribute('request-token-value'),
        };

        this.internals.form.addEventListener('submit', (event) => {
            if (event.submitter?.name === '__passkeySubmit') {
                event.preventDefault();
                this.obtainAndSubmitCredential();
            }
        });

        this.tryAutofillPasskey();
    }

    disconnectedCallback() {
        this.abortController?.abort();
    }

    async obtainCredential(useConditionalMediation, signal) {
        if (!browserSupportsPasskeys) {
            throw new Error('Some passkey features are missing. Please update your browser.');
        }

        const headers = {
            [this.attrs.requestTokenName]: this.attrs.requestTokenValue,
        };

        if (this.attrs.operation === 'Create') {
            return await createCredential(headers, signal);
        } else if (this.attrs.operation === 'Request') {
            const username = new FormData(this.internals.form).get(this.attrs.usernameField);
            const mediation = useConditionalMediation ? 'conditional' : undefined;
            return await requestCredential(username, mediation, headers, signal);
        } else {
            throw new Error(`Unknown passkey operation '${this.attrs.operation}'.`);
        }
    }

    async obtainAndSubmitCredential(useConditionalMediation = false) {
        this.abortController?.abort();
        this.abortController = new AbortController();
        const signal = this.abortController.signal;
        const formData = new FormData();
        try {
            const credential = await this.obtainCredential(useConditionalMediation, signal);
            // Some password managers (1Password, Microsoft Authenticator) don't implement
            // PublicKeyCredential.toJSON correctly, so JSON.stringify(credential) drops
            // clientExtensionResults (required by the server) or throws. Serialize manually instead.
            // Revisit when these tools conform — tracked in
            // https://gitlab.com/vicione-oss/vicione/suite/suite/-/work_items/2817
            const credentialJson = JSON.stringify({
                authenticatorAttachment: credential.authenticatorAttachment,
                clientExtensionResults: credential.getClientExtensionResults(),
                id: credential.id,
                rawId: this.convertToBase64(credential.rawId),
                response: {
                    attestationObject: this.convertToBase64(credential.response.attestationObject),
                    authenticatorData: this.convertToBase64(credential.response.authenticatorData ??
                        credential.response.getAuthenticatorData?.() ?? undefined),
                    clientDataJSON: this.convertToBase64(credential.response.clientDataJSON),
                    publicKey: this.convertToBase64(credential.response.getPublicKey?.() ?? undefined),
                    publicKeyAlgorithm: credential.response.getPublicKeyAlgorithm?.() ?? undefined,
                    transports: credential.response.getTransports?.() ?? undefined,
                    signature: this.convertToBase64(credential.response.signature),
                    userHandle: this.convertToBase64(credential.response.userHandle),
                },
                type: credential.type,
            });
            formData.append(`${this.attrs.name}.CredentialJson`, credentialJson);
        } catch (error) {
            if (error.name === 'AbortError') {
                // The user explicitly canceled the operation - return without error.
                return;
            }
            console.error(error);
            if (useConditionalMediation) {
                // An error occurred during conditional mediation, which is not user-initiated.
                // We log the error in the console but do not relay it to the user.
                return;
            }
            const errorMessage = error.name === 'NotAllowedError'
                ? 'No passkey was provided by the authenticator.'
                : error.message;
            formData.append(`${this.attrs.name}.Error`, errorMessage);
        }
        this.internals.setFormValue(formData);
        this.internals.form.submit();
    }

    async tryAutofillPasskey() {
        if (browserSupportsPasskeys && this.attrs.operation === 'Request' && await PublicKeyCredential.isConditionalMediationAvailable?.()) {
            await this.obtainAndSubmitCredential(/* useConditionalMediation */ true);
        }
    }

    // Encodes a credential field (byte array/ArrayBuffer/Uint8Array/string) as a base64url
    // string. Part of the manual PublicKeyCredential serialization that mitigates the
    // "TypeError: Illegal invocation" thrown by some password managers; see:
    // https://github.com/dotnet/AspNetCore.Docs/blob/main/aspnetcore/security/authentication/passkeys/index.md#mitigate-publickeycredentialtojson-error-typeerror-illegal-invocation
    convertToBase64(o) {
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
});
