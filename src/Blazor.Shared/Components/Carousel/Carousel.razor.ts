export async function waitForTransitionEnd(element: HTMLElement | undefined): Promise<void> {
    if (!element)
        return;

    await Promise.allSettled(element.getAnimations().map(async animation => animation.finished));
}
