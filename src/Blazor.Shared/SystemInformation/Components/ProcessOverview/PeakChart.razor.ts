interface Consumer {
    n: string;
    v: string;
    w: number;
    o: boolean;
}

interface Slot {
    cpu: string;
    top: Consumer[];
}

/**
 * Shows the largest consumers of the slot the chart reports under the pointer, and those of now once it leaves.
 * Blazor re-renders the element every minute; the data is read from its attribute on each event, so it is always current.
 */
export function attach(root: HTMLElement): void {
    let cachedText = '';
    let cached: (Slot | null)[] = [];
    const data = (): (Slot | null)[] => {
        const text = root.dataset.slots ?? '';
        if (text !== cachedText) {
            cachedText = text;
            cached = text ? JSON.parse(text) as (Slot | null)[] : [];
        }

        return cached;
    };

    const rows = (): HTMLElement | null => root.querySelector<HTMLElement>('.rows');
    const total = (): HTMLElement | null => root.querySelector<HTMLElement>('.total');
    let nowRows: string | null = null;

    const reset = (): void => {
        const list = rows();
        if (list && nowRows !== null) {
            list.innerHTML = nowRows;
        }
        nowRows = null;
        const sum = total();
        if (sum) {
            sum.textContent = sum.dataset.now ?? '';
        }
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
        const index = (event as CustomEvent<{ slot: number | null }>).detail.slot;
        const slot = index === null ? null : data()[index];
        if (!slot) {
            reset();
            return;
        }

        const list = rows();
        if (list) {
            nowRows ??= list.innerHTML;
            list.innerHTML = slot.top.map(row).join('');
        }

        const sum = total();
        if (sum) {
            sum.textContent = slot.cpu;
        }
    });
}
