class Position {
    x!: number;
    y!: number;
}

class CanvasConfig {
    width!: number;
    height!: number;
}

class ChartPosition {
    x!: number;
    y!: number;
    width!: number;
    height!: number;
}

class AxisStyles {
    lineWidth!: number;
    lineColor!: string;
    tickLength!: number;
    tickWidth!: number;
    tickColor!: string;
    labelFont!: string;
    labelFontColor!: string;
    labelTextOffset!: number;
    showLabels!: boolean;
}

class AxisConfig {
    maxValue!: number;
    tickInterval!: number;
    labelInterval!: number;
}

class MetricsConfig {
    color!: string;
    minMaxAreaColor!: string;
}

class GaugeStyleScaleLabels {
    left!: string;
    center!: string;
    right!: string;
}

class GaugeStyleScaleLabelOffsets {
    left!: Position;
    center!: Position;
    right!: Position;
}

class GaugeStyle {
    radius!: number;
    startAngleDeg!: number;
    endAngleDeg!: number;
    backgroundArcColor!: string;
    strokeWidth!: number;
    triangleSize!: number;
    triangleOffset!: number;
    labelFont!: string;
    labelFontColor!: string;
    scaleFont!: string;
    scaleFontColor!: string;
    scaleLabels!: GaugeStyleScaleLabels;
    scaleLabelOffsets!: GaugeStyleScaleLabelOffsets;
}

class PopupConfig {
    enabled!: boolean;
    backgroundColor!: string;
    textColor!: string;
    font!: string;
    padding!: number;
    gap!: number;
    borderRadius!: number;
    pointerHeight!: number;
    pointerWidth!: number;
    verticalOffset!: number;
    labelColumnWidth!: number;
    valueColumnWidth!: number;
    rowHeight!: number;
    rowSpacing!: number;
    bottomPadding!: number;
}

class ConfigGridLine {
    lineWidth!: number;
    lineColor!: string;
    showGrid!: boolean;
}

class ConfigGrid {
    x!: ConfigGridLine;
    y!: ConfigGridLine;
}

enum LineType {
    Simple,
    Quadratic,
    Bezier
}

class ConfigChart {
    position!: ChartPosition;
    xAxis!: AxisConfig;
    yAxis!: AxisConfig;
    xAxisStyles!: AxisStyles;
    yAxisStyles!: AxisStyles;
    grid!: ConfigGrid;
    metrics!: Record<string, MetricsConfig>;
    lineType!: LineType;
    backgroundColor!: string;
}

class ConfigGaugeMarkerStyles {
    fillStyle!: string;
}

class ConfigGaugeMarkers {
    min!: ConfigGaugeMarkerStyles;
    max!: ConfigGaugeMarkerStyles;
}

class ConfigGauge {
    style!: GaugeStyle;
    markers!: ConfigGaugeMarkers;
}

class IndicatorLineConfig {
    lineColor!: string;
    lineWidth!: number;
}

class Config {
    canvas!: CanvasConfig;
    chart!: ConfigChart;
    gauge!: ConfigGauge;
    gaugeCenters!: Record<string, Position>;
    popup!: PopupConfig;
    indicatorLine!: IndicatorLineConfig;
}

const minutesPerPoint = 5;

export class Monitoring {

    private readonly config: Config = {
        canvas: {
            width: 252,
            height: 176
        },
        chart: {
            position: {
                x: 16,
                y: 8,
                width: 220,
                height: 80
            },
            xAxis: {
                maxValue: 24,
                tickInterval: 1,
                labelInterval: 6
            },
            yAxis: {
                maxValue: 100,
                tickInterval: 25,
                labelInterval: 50
            },
            xAxisStyles: {
                lineWidth: 2,
                lineColor: '#dddddd90',
                tickLength: 4,
                tickWidth: 1,
                tickColor: '#dddddd60',
                labelFont: '8px Noto Sans',
                labelFontColor: '#dddddd',
                labelTextOffset: 2,
                showLabels: true
            },
            yAxisStyles: {
                lineWidth: 2,
                lineColor: '#dddddd90',
                tickLength: 4,
                tickWidth: 1,
                tickColor: '#dddddd60',
                labelFont: '8px Noto Sans',
                labelFontColor: '#dddddd',
                labelTextOffset: 2,
                showLabels: false
            },
            grid: {
                x: {
                    lineWidth: 0.5,
                    lineColor: '#dddddd30',
                    showGrid: true
                },
                y: {
                    lineWidth: 0.5,
                    lineColor: '#dddddd30',
                    showGrid: true
                }
            },
            metrics: {
                cpu: {
                    color: '#95ee11',
                    minMaxAreaColor: '#95ee1160'
                },
                ram: {
                    color: '#ffcd00',
                    minMaxAreaColor: '#ffcd0060'
                },
                hdd: {
                    color: '#7394c1',
                    minMaxAreaColor: '#7394c160'
                },
                net: {
                    color: '#ff9600',
                    minMaxAreaColor: '#ff960060'
                }
            },
            lineType: LineType.Simple,
            backgroundColor: '#404040'
        },
        gauge: {
            style: {
                radius: 21,
                startAngleDeg: 45,
                endAngleDeg: 315,
                backgroundArcColor: '#707070',
                strokeWidth: 3,
                triangleSize: 6,
                triangleOffset: 0,
                labelFont: '10px Noto Sans',
                labelFontColor: '#dadada',
                scaleFont: '8px Noto Sans',
                scaleFontColor: '#dadada',
                scaleLabels: {
                    left: '0',
                    center: '%',
                    right: '100'
                },
                scaleLabelOffsets: {
                    left: { x: -16, y: 27 },
                    center: { x: 0, y: 18 },
                    right: { x: 16, y: 27 }
                }
            },
            markers: {
                min: { fillStyle: '#bbbbbb' },
                max: { fillStyle: '#eeeeee' }
            }
        },
        gaugeCenters: {
            cpu: {
                x: 32,
                y: 144
            },
            ram: {
                x: 96,
                y: 144
            },
            hdd: {
                x: 160,
                y: 144
            },
            net: {
                x: 224,
                y: 144
            }
        },
        popup: {
            enabled: true,
            backgroundColor: '#333333',
            textColor: '#ffffff',
            font: '10px Noto Sans',
            padding: 5,
            gap: 10,
            borderRadius: 4,
            pointerHeight: 6,
            pointerWidth: 12,
            verticalOffset: 5,
            labelColumnWidth: 28,
            valueColumnWidth: 28,
            rowHeight: 16,
            rowSpacing: 4,
            bottomPadding: 4
        },
        indicatorLine: {
            lineColor: '#ffffff',
            lineWidth: 1
        }
    };

    private readonly canvas = (() => {
        const el = document.getElementById('dashboardCanvas');
        if (!(el instanceof HTMLCanvasElement))
            throw new Error('Canvas element not found');

        return el;
    })();

    private dpr = window.devicePixelRatio || 1;
    private readonly ctx = this.canvas.getContext('2d', { willReadFrequently: true })!;
    private hoveredGauge: string | undefined = '';
    private hoveredChartDataPosition: number | undefined = undefined;
    private readonly canvasStart = this.parameterToCanvasAngle(this.config.gauge.style.startAngleDeg);
    private canvasEnd = this.parameterToCanvasAngle(this.config.gauge.style.endAngleDeg);
    private cpuData = new Float32Array((288 * 3) + 3);
    private ramData = new Float32Array((288 * 3) + 3);
    private hddData = new Float32Array((288 * 3) + 3);
    private netData = new Float32Array((288 * 3) + 3);
    private readonly tooltipDiv = (() => {
        const tooltipElement = document.getElementById('tooltip');
        if (!(tooltipElement instanceof HTMLDivElement))
            throw new Error('Tooltip element not found');

        return tooltipElement;
    })();

    constructor(private readonly timeZoneOffsetMinutes: number, private readonly timeSpanHours: number) {
        this.setTimeSpan();
        window.addEventListener('resize', (_ => {
            this.render();
            this.updateTooltip();
        }));

        this.canvas.addEventListener('mousemove', (e: MouseEvent) => {
            const rect = this.canvas.getBoundingClientRect();
            const mouseX = e.clientX - rect.left;
            const mouseY = e.clientY - rect.top;
            const pos = this.config.chart.position;

            const isInsideChart = mouseX >= pos.x && mouseX <= pos.x + pos.width && mouseY >= pos.y && mouseY <= pos.y + pos.height;
            this.hoveredGauge = '';

            if (isInsideChart) {
                const fraction = 1 - ((mouseX - pos.x) / pos.width);
                const nMinutes = this.timeSpanHours * 60;
                const nPoints = this.getPointCount();
                const j = (fraction * nMinutes) / minutesPerPoint;
                this.hoveredChartDataPosition = Math.max(0, Math.min(nPoints - 1, j));
            } else {
                this.hoveredChartDataPosition = undefined;
                for (const metric in this.config.gaugeCenters) { // eslint-disable-line guard-for-in
                    const gaugeCenter = this.config.gaugeCenters[metric];
                    const dx = mouseX - gaugeCenter.x;
                    const dy = mouseY - gaugeCenter.y;
                    if (Math.sqrt((dx * dx) + (dy * dy)) < this.config.gauge.style.radius + 15) {
                        this.hoveredGauge = metric;
                        break;
                    }
                }
            }

            this.render();
            this.updateTooltip();
        });

        this.canvas.addEventListener('mouseout', () => {
            this.hoveredGauge = '';
            this.hoveredChartDataPosition = undefined;
            this.render();
            this.updateTooltip();
        });
    }

    public generateTestData() {
        this.cpuData = this.generateTestDataForMetric('cpu');
        this.ramData = this.generateTestDataForMetric('ram');
        this.hddData = this.generateTestDataForMetric('hdd');
        this.netData = this.generateTestDataForMetric('net');
    }

    public render() {
        this.ctx.clearRect(0, 0, this.config.canvas.width, this.config.canvas.height);
        this.resizeCanvas();
        this.drawChart();
        ['cpu', 'ram', 'hdd', 'net'].forEach(metric => {
            const gaugeVal = this.getGaugeValue(metric);
            this.drawGauge(metric, gaugeVal, this.config.gaugeCenters[metric]);
        });
    }

    public setValue(values: Float32Array, metric: 'cpu' | 'ram' | 'hdd' | 'net') {
        switch (metric) {
            case 'cpu': this.cpuData = values;
                break;
            case 'ram': this.ramData = values;
                break;
            case 'hdd': this.hddData = values;
                break;
            case 'net': this.netData = values;
                break;
        }
    }

    private getPointCount(): number {
        return 1 + ((this.timeSpanHours * 60) / minutesPerPoint);
    }

    private setTimeSpan() {
        this.config.chart.xAxis.maxValue = this.timeSpanHours;

        if (this.timeSpanHours <= 1) {
            this.config.chart.xAxis.tickInterval = 0.25 / 3; // 5min
            this.config.chart.xAxis.labelInterval = 0.25; // 15min
        } else {
            this.config.chart.xAxis.tickInterval = 1;
            this.config.chart.xAxis.labelInterval = 6;
        }

        const nPoints = this.getPointCount();
        const totalLength = nPoints * 3;
        this.cpuData = new Float32Array(totalLength);
        this.ramData = new Float32Array(totalLength);
        this.hddData = new Float32Array(totalLength);
        this.netData = new Float32Array(totalLength);
    }

    private resizeCanvas() {
        this.dpr = window.devicePixelRatio || 1;

        this.canvas.width = this.config.canvas.width * this.dpr;
        this.canvas.height = this.config.canvas.height * this.dpr;

        this.canvas.style.width = `${this.config.canvas.width}px`;
        this.canvas.style.height = `${this.config.canvas.height}px`;
        this.ctx.scale(this.dpr, this.dpr);
        this.ctx.lineJoin = 'round';
        this.ctx.lineCap = 'round';

        if (this.canvasEnd <= this.canvasStart) this.canvasEnd += 2 * Math.PI;
    }

    private parameterToCanvasAngle(deg: number): number {
        return ((deg + 90) * Math.PI) / 180;
    }

    private percentageToAngle(percent: number): number {
        const p = Math.max(0, Math.min(1, percent / 100));
        return this.canvasStart + (p * (this.canvasEnd - this.canvasStart));
    }

    private getXFromDataPoint(j: number): number { // eslint-disable-line @typescript-eslint/naming-convention
        const nMinutes = this.timeSpanHours * 60;
        const fraction = (j * minutesPerPoint) / nMinutes;
        return this.config.chart.position.x + (this.config.chart.position.width * (1 - fraction));
    }

    private buildPathsForMetric(metricData: Float32Array): { line: Path2D; area: Path2D } {
        const pos = this.config.chart.position;
        const maxPercent = this.config.chart.yAxis.maxValue;
        const nPoints = this.getPointCount();
        const avgPoints: Array<{ x: number; y: number }> = [];
        const minPoints: Array<{ x: number; y: number }> = [];
        const maxPoints: Array<{ x: number; y: number }> = [];

        for (let j = 0; j < nPoints; j++) {
            const idx = j * 3;
            const minVal = metricData[idx];
            const avgVal = metricData[idx + 1];
            const maxVal = metricData[idx + 2];

            const x = this.getXFromDataPoint(j);
            const yAvg = pos.y + pos.height - ((avgVal / maxPercent) * pos.height);
            const yMin = pos.y + pos.height - ((minVal / maxPercent) * pos.height);
            const yMax = pos.y + pos.height - ((maxVal / maxPercent) * pos.height);
            avgPoints.push({ x, y: yAvg });
            minPoints.push({ x, y: yMin });
            maxPoints.push({ x, y: yMax });
        }

        let avgLinePath: Path2D;
        switch (this.config.chart.lineType) {
            case LineType.Simple:
                avgLinePath = new Path2D();
                avgLinePath.moveTo(avgPoints[0].x, avgPoints[0].y);
                for (let i = 1; i < avgPoints.length; i++)
                    avgLinePath.lineTo(avgPoints[i].x, avgPoints[i].y);

                break;
            case LineType.Quadratic:
                avgLinePath = new Path2D();
                if (avgPoints.length > 0) {
                    avgLinePath.moveTo(avgPoints[0].x, avgPoints[0].y);
                    for (let i = 0; i < avgPoints.length - 1; i++) {
                        const cpX = avgPoints[i].x;
                        const cpY = avgPoints[i].y;
                        const midX = (avgPoints[i].x + avgPoints[i + 1].x) / 2;
                        const midY = (avgPoints[i].y + avgPoints[i + 1].y) / 2;
                        avgLinePath.quadraticCurveTo(cpX, cpY, midX, midY);
                    }

                    avgLinePath.lineTo(avgPoints[avgPoints.length - 1].x, avgPoints[avgPoints.length - 1].y);
                }

                break;
            case LineType.Bezier:
                avgLinePath = this.catmullRomToBezier(avgPoints);
                break;
        }

        const areaPath = new Path2D();
        areaPath.moveTo(maxPoints[0].x, maxPoints[0].y);
        for (let i = 1; i < maxPoints.length; i++)
            areaPath.lineTo(maxPoints[i].x, maxPoints[i].y);
        for (let i = minPoints.length - 1; i >= 0; i--)
            areaPath.lineTo(minPoints[i].x, minPoints[i].y);
        areaPath.closePath();

        return { line: avgLinePath, area: areaPath };
    }

    private catmullRomToBezier(points: Array<{ x: number; y: number }>): Path2D {
        const path = new Path2D();
        if (points.length < 2) return path;
        path.moveTo(points[0].x, points[0].y);
        for (let i = 0; i < points.length - 1; i++) {
            const p0 = i === 0 ? points[0] : points[i - 1];
            const p1 = points[i];
            const p2 = points[i + 1];
            const p3 = i + 2 < points.length ? points[i + 2] : p2;
            const cp1x = p1.x + ((p2.x - p0.x) / 6);
            const cp1y = p1.y + ((p2.y - p0.y) / 6);
            const cp2x = p2.x - ((p3.x - p1.x) / 6);
            const cp2y = p2.y - ((p3.y - p1.y) / 6);
            path.bezierCurveTo(cp1x, cp1y, cp2x, cp2y, p2.x, p2.y);
        }

        return path;
    }

    private generateTestDataForMetric(metric: string): Float32Array {
        const nPoints = 1 + 288;
        const base = 50;
        const amplitude = 50;
        const offset = 5;
        let phase = 0;

        switch (metric) {
            case 'cpu': phase = 0;
                break;
            case 'ram': phase = Math.PI / 2;
                break;
            case 'hdd': phase = Math.PI;
                break;
            case 'net': phase = 3 * Math.PI / 2;
                break;
            default: phase = 0;
        }

        const totalValues = nPoints * 3;
        const arr = new Float32Array(totalValues);
        for (let j = 0; j < nPoints; j++) {
            const t = (j / (nPoints - 1)) * (2 * Math.PI);
            let avg = base + (amplitude * Math.sin(t + phase));
            avg = Math.max(0, Math.min(100, avg));
            const minVal = Math.max(0, avg - offset);
            const maxVal = Math.min(100, avg + offset);
            const idx = j * 3;
            arr[idx] = minVal;
            arr[idx + 1] = avg;
            arr[idx + 2] = maxVal;
        }

        return arr;
    }

    private drawXAxis() { // eslint-disable-line @typescript-eslint/naming-convention
        const pos = this.config.chart.position;
        const style = this.config.chart.xAxisStyles;
        this.ctx.letterSpacing = '0.06em';
        this.ctx.beginPath();
        this.ctx.strokeStyle = style.lineColor;
        this.ctx.lineWidth = style.lineWidth;
        this.ctx.moveTo(pos.x, pos.y + pos.height);
        this.ctx.lineTo(pos.x + pos.width, pos.y + pos.height);
        this.ctx.stroke();

        const intervalMs = this.config.chart.xAxis.tickInterval * 3600000;
        const labelIntervalMs = this.config.chart.xAxis.labelInterval * 3600000;
        const spanMs = this.timeSpanHours * 3600000;

        const now = new Date();
        const minTimeMs = now.getTime() - spanMs;
        let tickTimeMs = Math.ceil(minTimeMs / intervalMs) * intervalMs;
        while (tickTimeMs <= now.getTime()) {
            const tickTime = new Date(tickTimeMs);
            const minutesOffset = (now.getTime() - tickTimeMs) / 60000;
            const xTick = this.getXFromDataPoint(minutesOffset / 5);
            const isLabelTick = style.showLabels && (tickTimeMs % labelIntervalMs === 0);

            if (this.config.chart.grid.x.showGrid) {
                this.ctx.beginPath();
                this.ctx.strokeStyle = this.config.chart.grid.x.lineColor;
                this.ctx.lineWidth = isLabelTick ? style.tickWidth : this.config.chart.grid.x.lineWidth;
                this.ctx.moveTo(xTick, pos.y);
                this.ctx.lineTo(xTick, pos.y + pos.height);
                this.ctx.stroke();
            }

            this.ctx.beginPath();
            this.ctx.strokeStyle = style.tickColor;
            this.ctx.lineWidth = style.tickWidth;
            this.ctx.moveTo(xTick, pos.y + pos.height);
            this.ctx.lineTo(xTick, pos.y + pos.height + style.tickLength);
            this.ctx.stroke();

            if (isLabelTick) {
                const localizedTickTime = new Date(tickTime.getTime() + (this.timeZoneOffsetMinutes * 60 * 1000));

                let hours = localizedTickTime.getUTCHours().toString();
                if (hours.length < 2) hours = '0' + hours;
                let minutes = localizedTickTime.getUTCMinutes().toString();
                if (minutes.length < 2) minutes = '0' + minutes;
                const labelStr = `${hours}:${minutes}`;
                this.ctx.font = style.labelFont;
                this.ctx.fillStyle = style.labelFontColor;
                this.ctx.textAlign = 'center';
                this.ctx.textBaseline = 'top';
                this.ctx.fillText(labelStr, xTick, pos.y + pos.height + style.tickLength + style.labelTextOffset);
            }

            tickTimeMs += intervalMs;
        }
    }

    private drawYAxis() { // eslint-disable-line @typescript-eslint/naming-convention
        const pos = this.config.chart.position;
        const yAxisCfg = this.config.chart.yAxis;
        const style = this.config.chart.yAxisStyles;
        this.ctx.letterSpacing = '0.06em';
        this.ctx.beginPath();
        this.ctx.strokeStyle = style.lineColor;
        this.ctx.lineWidth = style.lineWidth;
        this.ctx.moveTo(pos.x, pos.y);
        this.ctx.lineTo(pos.x, pos.y + pos.height);
        this.ctx.stroke();

        for (let v = 0; v <= yAxisCfg.maxValue; v += yAxisCfg.tickInterval) {
            const yVal = pos.y + pos.height - ((v / yAxisCfg.maxValue) * pos.height);
            this.ctx.beginPath();
            this.ctx.strokeStyle = style.tickColor;
            this.ctx.lineWidth = style.tickWidth;
            this.ctx.moveTo(pos.x - style.tickLength, yVal);
            this.ctx.lineTo(pos.x, yVal);
            this.ctx.stroke();

            if (style.showLabels && v % yAxisCfg.labelInterval === 0) {
                this.ctx.font = style.labelFont;
                this.ctx.fillStyle = style.labelFontColor;
                this.ctx.textAlign = 'end';
                this.ctx.textBaseline = 'middle';
                this.ctx.fillText(v + '%', pos.x - style.tickLength - style.labelTextOffset, yVal);
            }

            if (this.config.chart.grid.y.showGrid) {
                this.ctx.beginPath();
                this.ctx.strokeStyle = this.config.chart.grid.y.lineColor;
                this.ctx.lineWidth = this.config.chart.grid.y.lineWidth;
                this.ctx.moveTo(pos.x, yVal);
                this.ctx.lineTo(pos.x + pos.width, yVal);
                this.ctx.stroke();
            }
        }
    }

    private drawChart() {
        const pos = this.config.chart.position;
        const bgColor = this.config.chart.backgroundColor;
        this.ctx.letterSpacing = '0.06em';
        this.ctx.fillStyle = bgColor;
        this.ctx.fillRect(pos.x, pos.y, pos.width, pos.height);
        this.drawXAxis();
        this.drawYAxis();
        ['cpu', 'ram', 'hdd', 'net'].forEach(metric => {
            let dataArray: Float32Array | undefined;
            switch (metric) {
                case 'cpu': dataArray = this.cpuData;
                    break;
                case 'ram': dataArray = this.ramData;
                    break;
                case 'hdd': dataArray = this.hddData;
                    break;
                default: dataArray = this.netData;
                    break;
            }

            const paths = this.buildPathsForMetric(dataArray);
            if (this.hoveredGauge === metric && paths.area) {
                this.ctx.fillStyle = this.config.chart.metrics[metric].minMaxAreaColor;
                this.ctx.fill(paths.area);
            }

            this.ctx.strokeStyle = this.config.chart.metrics[metric].color;
            this.ctx.lineWidth = 2;
            this.ctx.stroke(new Path2D(paths.line));
        });
        this.drawIndicatorLine();
    }

    private drawIndicatorLine() {
        if (this.hoveredChartDataPosition === undefined) return;

        const pos = this.config.chart.position;
        const x = this.getXFromDataPoint(this.hoveredChartDataPosition);

        this.ctx.beginPath();
        this.ctx.strokeStyle = this.config.indicatorLine.lineColor;
        this.ctx.lineWidth = this.config.indicatorLine.lineWidth;
        this.ctx.moveTo(x, pos.y);
        this.ctx.lineTo(x, pos.y + pos.height);
        this.ctx.stroke();
    }

    private drawTriangleForGauge(angle: number, center: Position, size: number, style: { fillStyle: string }) {
        const { strokeWidth } = this.config.gauge.style;
        const { triangleOffset } = this.config.gauge.style;
        const r = this.config.gauge.style.radius + (strokeWidth / 2) + triangleOffset;
        const a = {
            x: center.x + (r * Math.cos(angle)),
            y: center.y + (r * Math.sin(angle))
        };
        const h = size * Math.sqrt(3) / 2;
        const baseMid = {
            x: a.x + (h * Math.cos(angle)),
            y: a.y + (h * Math.sin(angle))
        };
        const perp = { x: -Math.sin(angle), y: Math.cos(angle) };
        const baseLeft = {
            x: baseMid.x + ((size / 2) * perp.x),
            y: baseMid.y + ((size / 2) * perp.y)
        };
        const baseRight = {
            x: baseMid.x - ((size / 2) * perp.x),
            y: baseMid.y - ((size / 2) * perp.y)
        };
        this.ctx.beginPath();
        this.ctx.moveTo(a.x, a.y);
        this.ctx.lineTo(baseLeft.x, baseLeft.y);
        this.ctx.lineTo(baseRight.x, baseRight.y);
        this.ctx.closePath();
        this.ctx.fillStyle = style.fillStyle;
        this.ctx.fill();
    }

    private drawGauge(metricName: string, gaugeVal: { min: number; avg: number; max: number }, center: Position) {
        const gaugeStyle = this.config.gauge.style;
        this.ctx.beginPath();
        this.ctx.lineWidth = gaugeStyle.strokeWidth;
        this.ctx.strokeStyle = gaugeStyle.backgroundArcColor;
        this.ctx.arc(center.x, center.y, gaugeStyle.radius, this.parameterToCanvasAngle(gaugeStyle.startAngleDeg), this.parameterToCanvasAngle(gaugeStyle.endAngleDeg), false);
        this.ctx.stroke();
        const avgAngle = this.percentageToAngle(gaugeVal.avg);
        this.ctx.beginPath();
        this.ctx.strokeStyle = this.config.chart.metrics[metricName].color;
        this.ctx.lineWidth = gaugeStyle.strokeWidth;
        this.ctx.arc(center.x, center.y, gaugeStyle.radius, this.parameterToCanvasAngle(gaugeStyle.startAngleDeg), avgAngle, false);
        this.ctx.stroke();
        this.ctx.letterSpacing = '0.06em';
        this.ctx.font = gaugeStyle.labelFont;
        this.ctx.fillStyle = gaugeStyle.labelFontColor;
        this.ctx.textAlign = 'center';
        this.ctx.textBaseline = 'middle';
        this.ctx.fillText(metricName.toUpperCase(), center.x, center.y);
        this.ctx.font = gaugeStyle.scaleFont;
        this.ctx.fillStyle = gaugeStyle.scaleFontColor;
        this.ctx.fillText(gaugeStyle.scaleLabels.left, center.x + gaugeStyle.scaleLabelOffsets.left.x, center.y + gaugeStyle.scaleLabelOffsets.left.y);
        this.ctx.fillText(gaugeStyle.scaleLabels.center, center.x + gaugeStyle.scaleLabelOffsets.center.x, center.y + gaugeStyle.scaleLabelOffsets.center.y);
        this.ctx.fillText(gaugeStyle.scaleLabels.right, center.x + gaugeStyle.scaleLabelOffsets.right.x, center.y + gaugeStyle.scaleLabelOffsets.right.y);
        const minAngle = this.percentageToAngle(gaugeVal.min);
        this.drawTriangleForGauge(minAngle, center, gaugeStyle.triangleSize, this.config.gauge.markers.min);
        const maxAngle = this.percentageToAngle(gaugeVal.max);
        this.drawTriangleForGauge(maxAngle, center, gaugeStyle.triangleSize, this.config.gauge.markers.max);
    }

    private getDataArray(metric: string): Float32Array {
        switch (metric) {
            case 'cpu': return this.cpuData;
            case 'ram': return this.ramData;
            case 'hdd': return this.hddData;
            default: return this.netData;
        }
    }

    private getValuesAtIndex(data: Float32Array, j: number): { min: number; avg: number; max: number } {
        const idx = j * 3;
        return { min: data[idx], avg: data[idx + 1], max: data[idx + 2] };
    }

    private getGaugeValue(metric: string) {
        const data = this.getDataArray(metric);
        const index = Math.round(this.hoveredChartDataPosition ?? 0);
        return this.getValuesAtIndex(data, index);
    }

    private updateTooltip() {
        if (!this.config.popup.enabled) {
            this.tooltipDiv.style.display = 'none';
            return;
        }

        if (this.hoveredGauge) {
            const gaugeVal = this.getGaugeValue(this.hoveredGauge);
            const gaugeCenter = this.config.gaugeCenters[this.hoveredGauge];
            const tooltipTop = this.config.canvas.height + this.config.popup.verticalOffset;
            this.tooltipDiv.querySelector('.tooltip-header')!.textContent = this.hoveredGauge.toUpperCase();
            const rows = this.tooltipDiv.querySelectorAll('.tooltip-table tr');
            rows[0].querySelector('.value')!.textContent = Math.round(gaugeVal.min ?? 0) + ' %';
            rows[1].querySelector('.value')!.textContent = Math.round(gaugeVal.avg ?? 0) + ' %';
            rows[2].querySelector('.value')!.textContent = Math.round(gaugeVal.max ?? 0) + ' %';
            this.tooltipDiv.querySelectorAll('.tooltip-table .label').forEach(cell => {
                if (cell instanceof HTMLElement)
                    cell.style.width = this.config.popup.labelColumnWidth + 'px';
            });
            this.tooltipDiv.querySelectorAll('.tooltip-table .value').forEach(cell => {
                if (cell instanceof HTMLElement)
                    cell.style.width = this.config.popup.valueColumnWidth + 'px';
            });
            const totalWidth = this.config.popup.labelColumnWidth
                + this.config.popup.gap
                + this.config.popup.valueColumnWidth
                + (2 * this.config.popup.padding);
            const rowCount = 3;
            const totalHeight = (rowCount * this.config.popup.rowHeight)
                + ((rowCount) * this.config.popup.rowSpacing)
                + (2 * this.config.popup.padding)
                + this.config.popup.bottomPadding;
            this.tooltipDiv.style.display = 'block';
            this.tooltipDiv.style.width = totalWidth + 'px';
            this.tooltipDiv.style.height = totalHeight + 'px';
            const tooltipLeft = gaugeCenter.x - (totalWidth / 2);
            this.tooltipDiv.style.left = tooltipLeft + 'px';
            this.tooltipDiv.style.top = tooltipTop + 'px';
            const pointerLeft = (totalWidth / 2) - (this.config.popup.pointerWidth / 2);
            const pointerEl = this.tooltipDiv.querySelector('.tooltip-pointer');
            if (pointerEl instanceof HTMLElement)
                pointerEl.style.left = pointerLeft + 'px';
            this.tooltipDiv.querySelectorAll('.tooltip-table tr').forEach(row => {
                if (row instanceof HTMLElement) {
                    row.style.height = this.config.popup.rowHeight + 'px';
                    row.style.lineHeight = this.config.popup.rowHeight + 'px';
                    row.style.marginBottom = this.config.popup.rowSpacing + 'px';
                }
            });
        } else {
            this.tooltipDiv.style.display = 'none';
        }
    }
}

export function init(timeZoneOffsetMinutes: number, timeSpanHours: number) {
    return new Monitoring(timeZoneOffsetMinutes, timeSpanHours);
}
