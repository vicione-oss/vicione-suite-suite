export class AuthenticationCookieUpdater {
    constructor(readonly baseUri: string) {}

    public async updateAuthenticationCookie(nonce: string) {
        const url = new URL('api/UpdateAuthenticationCookie', this.baseUri);
        url.searchParams.append('nonce', nonce);

        const response = await fetch(url, {
            cache: 'no-store',
            credentials: 'same-origin'
        });

        return response.ok && response.status === 204;
    }
}
