export class JitChat {
    public scrollToBottom() {
        const element = document.querySelector('#jit-chat-message-box');

        if (element !== null)
            element.scroll({ top: element.scrollHeight, behavior: 'smooth' });
    }
}
