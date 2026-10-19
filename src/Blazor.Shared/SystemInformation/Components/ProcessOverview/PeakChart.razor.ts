type Consumer = {
    n: string;
    v: string;
    w: number;
    o: boolean;
};

type Slot = {
    cpu: string;
    top: Consumer[];
};

/**
Shows the largest consumers of the slot the chart reports under the pointer, and those of now once it leaves.
Blazor re-renders the element every minute; the data is read from its attribute on each event, so it is always current.
*/
export function attach(root: HTMLElement): void {
    // A chart removed before the call arrives is handed over as null.
    if (!(root instanceof HTMLElement))
        return;

    let cachedText = '';
    let cached: Array<Slot | undefined> = [];
    const data = (): Array<Slot | undefined> => {
        const text = root.dataset.slots ?? '';
        if (text !== cachedText) {
            cachedText = text;
            cached = text === '' ? [] : (JSON.parse(text) as unknown[]).map(entry => (entry ?? undefined) as Slot | undefined);
        }

        return cached;
    };

    // The rows and the total of now are Blazor's, which keeps writing into the nodes it rendered; the hover has its own
    // and only switches which are shown.
    const hoverRows = (): HTMLElement | undefined => root.querySelector<HTMLElement>('.rows[data-hover]') ?? undefined;
    const hoverTotal = (): HTMLElement | undefined => root.querySelector<HTMLElement>('.total[data-hover]') ?? undefined;

    const showHover = (isHovering: boolean): void => {
        for (const element of root.querySelectorAll<HTMLElement>('[data-now]'))
            element.hidden = isHovering;

        for (const element of root.querySelectorAll<HTMLElement>('[data-hover]'))
            element.hidden = !isHovering;
    };

    const reset = (): void => {
        showHover(false);
    };

    const row = (consumer: Consumer): string => {
        const name = document.createElement('span');
        name.className = 'name';
        name.textContent = consumer.n;
        const value = document.createElement('span');
        value.className = 'value';
        value.textContent = consumer.v;
        return `${name.outerHTML}<span class="bar"><span class="${consumer.o ? 'over' : ''}" style="width: ${consumer.w}%"></span></span>${value.outerHTML}`;
    };

    root.addEventListener('chart-hover', event => {
        const index = (event as CustomEvent<{ slot: number | undefined }>).detail.slot;
        const slot = index === undefined ? undefined : data()[index];
        if (slot === undefined) {
            reset();
            return;
        }

        const rows = hoverRows();
        if (rows)
            rows.innerHTML = slot.top.map(consumer => row(consumer)).join('');

        const total = hoverTotal();
        if (total)
            total.textContent = slot.cpu;

        showHover(true);
    });
}
