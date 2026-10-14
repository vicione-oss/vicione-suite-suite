interface ChartData {
    t: string[];
    y: (number | null)[][];
}

/**
 * Shows the slot under the pointer: a cursor, a dot on each line and the time on the axis. Raises a bubbling
 * `chart-hover` event with the slot, or `null` once the pointer leaves, for the surrounding component to show its values.
 * Blazor re-renders the element when the data changes; the data is read from its attribute on each move, so it is current.
 */
export function attach(root: HTMLElement): void {
    const plot = root.querySelector<HTMLElement>('.plot');
    if (!plot) {
        return;
    }

    let cachedText = '';
    let cached: ChartData = { t: [], y: [] };
    const data = (): ChartData => {
        const text = root.dataset.chart ?? '';
        if (text !== cachedText) {
            cachedText = text;
            cached = text ? JSON.parse(text) as ChartData : { t: [], y: [] };
        }

        return cached;
    };

    let shown: number | null = null;
    const raise = (slot: number | null): void => {
        if (slot === shown) {
            return;
        }

        shown = slot;
        root.dispatchEvent(new CustomEvent('chart-hover', { bubbles: true, detail: { slot } }));
    };

    const reset = (): void => {
        plot.querySelectorAll<HTMLElement>('.cursor, .dot, .time-box').forEach(element => element.hidden = true);
        raise(null);
    };

    plot.addEventListener('pointermove', event => {
        const current = data();
        const slots = current.t.length;
        if (slots < 2) {
            return;
        }

        const box = plot.getBoundingClientRect();
        const slot = Math.max(0, Math.min(slots - 1, Math.round((event.clientX - box.left) / box.width * (slots - 1))));
        const left = slot / (slots - 1) * 100;

        const cursor = plot.querySelector<HTMLElement>('.cursor');
        if (cursor) {
            cursor.style.left = `${left}%`;
            cursor.hidden = false;
        }

        plot.querySelectorAll<HTMLElement>('.dot').forEach((dot, index) => {
            const y = current.y[index]?.[slot];
            dot.hidden = y === null || y === undefined;
            if (!dot.hidden) {
                dot.style.left = `${left}%`;
                dot.style.top = `${y}%`;
            }
        });

        const timeBox = plot.querySelector<HTMLElement>('.time-box');
        if (timeBox) {
            timeBox.textContent = current.t[slot] ?? '';
            timeBox.style.left = `clamp(16px, ${left}%, calc(100% - 16px))`;
            timeBox.hidden = false;
        }

        raise(slot);
    });

    plot.addEventListener('pointerleave', reset);
}
