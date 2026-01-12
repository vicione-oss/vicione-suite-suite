class JitChat {
    public scrollToBottom() {
        const element = document.getElementById('jit-chat-message-box');

        if (element !== null)
            element.scroll({ top: element.scrollHeight, behavior: 'smooth' });
    }
}

export function init() {
    return new JitChat();
}
