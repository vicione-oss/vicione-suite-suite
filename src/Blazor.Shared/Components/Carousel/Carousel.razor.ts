export async function waitForTransitionEnd(element: HTMLElement | null): Promise<void> {
    if (!element)
        return;

    await Promise.allSettled(element.getAnimations().map(animation => animation.finished));
}
