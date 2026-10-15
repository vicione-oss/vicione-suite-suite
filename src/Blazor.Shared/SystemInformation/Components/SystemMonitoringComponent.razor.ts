type GaugeValue = {
    share: number | undefined;
    text: string;
};

type HoverSeries = {
    share: Array<number | undefined>;
    text: string[];
    limit: number | undefined;
    now: GaugeValue;
};

type HoverData = {
    series: HoverSeries[];
};

const gaugeCenter = 30;
const gaugeRadius = 24;

/**
The same ring part as the component's Arc: open at the bottom, clockwise over 270°.
*/
function arc(from: number, to: number): string {
    const point = (share: number): string => {
        const angle = Math.PI * (0.75 + (1.5 * share));
        return `${(gaugeCenter + (gaugeRadius * Math.cos(angle))).toFixed(1)} ${(gaugeCenter + (gaugeRadius * Math.sin(angle))).toFixed(1)}`;
    };

    return `M${point(from)} A${gaugeRadius} ${gaugeRadius} 0 ${(to - from) * 270 > 180 ? 1 : 0} 1 ${point(to)}`;
}

/**
The JSON writes a missing value as null; here it stands as undefined.
*/
function fromJson(text: string): HoverData {
    const parsed = JSON.parse(text) as HoverData;
    return {
        series: parsed.series.map(series => ({
            share: series.share.map(share => share ?? undefined),
            text: series.text,
            limit: series.limit ?? undefined,
            now: { share: series.now.share ?? undefined, text: series.now.text }
        }))
    };
}

function showGauge(gauge: Element, series: HoverSeries, value: GaugeValue): void {
    const isOver = value.share !== undefined && series.limit !== undefined && value.share > series.limit;
    const path = gauge.querySelector('path.value');
    path?.setAttribute('d', value.share === undefined ? '' : arc(0, Math.max(value.share, 0.005)));
    path?.classList.toggle('over', isOver);
    gauge.querySelector('path.band')?.setAttribute('visibility', value.share === undefined ? 'hidden' : 'visible');

    const number = gauge.querySelector('text.number');
    if (!number)
        return;

    // Into the text node Blazor rendered, which it keeps writing its own updates into; textContent would replace it.
    if (number.firstChild instanceof Text)
        number.firstChild.data = value.text;

    number.classList.toggle('over', isOver);
    number.classList.toggle('long', value.text.length > 3);
}

/**
Shows the values of the slot the chart reports under the pointer in the gauges, and those of now once it leaves.
Blazor re-renders the element every minute; the data is read from its attribute on each event, so it is always current.
*/
export function attach(root: HTMLElement): void {
    let cachedText = '';
    let cached: HoverData | undefined;
    const data = (): HoverData | undefined => {
        const text = root.dataset.chart ?? '';
        if (text !== cachedText) {
            cachedText = text;
            cached = text === '' ? undefined : fromJson(text);
        }

        return cached;
    };

    const gauges = (show: (series: HoverSeries) => GaugeValue): void => {
        const current = data();
        root.querySelectorAll('.gauge').forEach((gauge, index) => {
            const series = current?.series[index];
            if (series)
                showGauge(gauge, series, show(series));
        });
    };

    root.addEventListener('chart-hover', event => {
        const { slot } = (event as CustomEvent<{ slot: number | undefined }>).detail;
        gauges(series => slot === undefined ? series.now : { share: series.share[slot], text: series.text[slot] ?? '–' });
    });
}
