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

    const rows = (): HTMLElement | undefined => root.querySelector<HTMLElement>('.rows') ?? undefined;
    const total = (): HTMLElement | undefined => root.querySelector<HTMLElement>('.total') ?? undefined;
    let nowRows: string | undefined;

    const reset = (): void => {
        const list = rows();
        if (list && nowRows !== undefined)
            list.innerHTML = nowRows;

        nowRows = undefined;
        const sum = total();
        if (sum)
            sum.textContent = sum.dataset.now ?? '';

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

        const list = rows();
        if (list) {
            nowRows ??= list.getHTML();
            list.innerHTML = slot.top.map(consumer => row(consumer)).join('');
        }

        const sum = total();
        if (sum)
            sum.textContent = slot.cpu;

    });
}
