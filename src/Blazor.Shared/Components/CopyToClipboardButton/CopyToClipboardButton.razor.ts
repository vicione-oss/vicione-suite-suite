export function copyTextToClipboard(text: string): void {
    void navigator.clipboard.writeText(text);
}
