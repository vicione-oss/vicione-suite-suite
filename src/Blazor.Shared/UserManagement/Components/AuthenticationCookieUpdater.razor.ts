export class AuthenticationCookieUpdater {

    public async updateAuthenticationCookie(nonce: string) {
        const url = new URL('api/UpdateAuthenticationCookie', document.baseURI);
        url.searchParams.append('nonce', nonce);

        const response = await fetch(url, {
            cache: 'no-store',
            credentials: 'same-origin'
        });

        return response.ok && response.status === 204;
    }
}
