// Copyright 2026 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

// The charts of the dashboard page: hand-written inline SVG (no chart library, nothing loaded),
// colored with the VS Code chart colors (CSS classes, see media/dashboard.css). A chart is only
// redrawn when its data (version) or its width changes; the hover (cursor line and tooltip) only
// moves two elements.

import { ChartFrame, linePath, nearestSample, sparkBars, xOf, yOf } from '../dashboardCharts';
import type { HistoryChart, StatusTimeline } from '../dashboardModel';
import { formatBytes } from '../dashboardModel';

const SvgNs = 'http://www.w3.org/2000/svg';

/** Margins of the plot area in the SVG of the history charts (axis labels). */
export const ChartMargins = { left: 64, right: 8, top: 8, bottom: 20 };

function svg<K extends keyof SVGElementTagNameMap>(tag: K, attributes: Record<string, string | number> = {}, className?: string): SVGElementTagNameMap[K] {
  const e = document.createElementNS(SvgNs, tag);
  for (const [name, value] of Object.entries(attributes)) {
    e.setAttribute(name, String(value));
  }
  if (className) {
    e.setAttribute('class', className);
  }
  return e;
}

function div(className: string, text?: string): HTMLDivElement {
  const e = document.createElement('div');
  e.className = className;
  if (text !== undefined) {
    e.textContent = text;
  }
  return e;
}

function formatValue(unit: HistoryChart['unit'], value: number): string {
  if (value === undefined || isNaN(value)) {
    return '\u2014';
  }
  return unit === 'bytes' ? formatBytes(value) : `${value.toFixed(1)}%`;
}

/** "now", "−12 min", "−1 h 05 min". */
function formatAgo(ms: number): string {
  const minutes = Math.round(ms / 60_000);
  if (minutes <= 0) {
    return 'now';
  }
  return minutes < 60 ? `\u2212${minutes} min` : `\u2212${Math.floor(minutes / 60)} h ${String(minutes % 60).padStart(2, '0')} min`;
}

/** A line chart of the history of the server, with its legend and a hover tooltip. */
export class HistoryChartView {
  readonly element: HTMLElement;
  private readonly legend: HTMLElement;
  private readonly plot: HTMLElement;
  private readonly tooltip: HTMLElement;
  private readonly placeholder: HTMLElement;
  private chart: HistoryChart | undefined;
  private frame: ChartFrame | undefined;
  private drawn = '';
  private cursor: SVGLineElement | undefined;

  constructor(private readonly title: string, private readonly height = 140) {
    this.element = div('chart');
    const header = div('chart-header');
    header.append(div('chart-title', title));
    this.legend = div('chart-legend');
    header.append(this.legend);
    this.plot = div('chart-plot');
    this.tooltip = div('chart-tooltip');
    this.tooltip.hidden = true;
    this.placeholder = div('chart-placeholder', 'Collecting samples\u2026');
    this.element.append(header, this.plot, this.placeholder);
    this.plot.append(this.tooltip);
    this.plot.addEventListener('mousemove', e => this.hover(e.clientX - this.plot.getBoundingClientRect().left, e.target));
    this.plot.addEventListener('mouseleave', () => this.hideHover());
  }

  /** The plot area of the last drawing (to align the status timeline). */
  get plotFrame(): ChartFrame | undefined {
    return this.frame;
  }

  render(chart: HistoryChart | undefined, force = false) {
    this.chart = chart;
    this.placeholder.hidden = chart !== undefined;
    this.plot.hidden = chart === undefined;
    if (!chart) {
      this.legend.replaceChildren();
      this.drawn = '';
      return;
    }
    const width = Math.max(200, this.plot.clientWidth || this.element.clientWidth || 400);
    const key = `${chart.version}:${width}`;
    if (key === this.drawn && !force) {
      return;
    }
    this.drawn = key;
    this.draw(chart, width);
  }

  private draw(chart: HistoryChart, width: number) {
    const frame: ChartFrame = {
      left: ChartMargins.left,
      top: ChartMargins.top,
      width: width - ChartMargins.left - ChartMargins.right,
      height: this.height - ChartMargins.top - ChartMargins.bottom,
      startMs: chart.startMs,
      endMs: chart.endMs,
      yMax: chart.yMax,
    };
    this.frame = frame;
    const root = svg('svg', { width, height: this.height, viewBox: `0 0 ${width} ${this.height}`, role: 'img' }, 'chart-svg');
    root.setAttribute('aria-label', `${this.title} chart, last 2 hours`);
    for (const tick of chart.yTicks) {
      const y = Math.round(yOf(frame, tick.value)) + 0.5;
      root.append(svg('line', { x1: frame.left, x2: frame.left + frame.width, y1: y, y2: y }, tick.value === 0 ? 'axis-line' : 'grid'));
      const label = svg('text', { x: frame.left - 6, y: y + 3.5, 'text-anchor': 'end' }, 'axis-label');
      label.textContent = tick.label;
      root.append(label);
    }
    chart.xTicks.forEach((tick, i) => {
      const x = Math.round(xOf(frame, tick.time)) + 0.5;
      root.append(svg('line', { x1: x, x2: x, y1: frame.top + frame.height, y2: frame.top + frame.height + 3 }, 'axis-line'));
      const anchor = i === 0 ? 'start' : i === chart.xTicks.length - 1 ? 'end' : 'middle';
      const label = svg('text', { x, y: this.height - 5, 'text-anchor': anchor }, 'axis-label');
      label.textContent = tick.label;
      root.append(label);
    });
    for (const series of chart.series) {
      root.append(svg('path', { d: linePath(frame, chart.times, series.values, chart.intervalMs) }, `line series-${series.color}`));
    }
    this.cursor = svg('line', { x1: 0, x2: 0, y1: frame.top, y2: frame.top + frame.height, visibility: 'hidden' }, 'cursor');
    root.append(this.cursor);
    this.plot.replaceChildren(root, this.tooltip);

    // Legend: the series with their last value.
    const last = chart.times.length - 1;
    this.legend.replaceChildren(...chart.series.map(series => {
      const item = div('legend-item');
      item.append(div(`swatch series-${series.color}`), document.createTextNode(`${series.label} ${formatValue(chart.unit, series.values[last])}`));
      return item;
    }));
  }

  private hover(offsetX: number, target: EventTarget | null) {
    const chart = this.chart;
    const frame = this.frame;
    if (!chart || !frame || !this.cursor || target === this.tooltip) {
      return;
    }
    const time = frame.startMs + ((offsetX - frame.left) * (frame.endMs - frame.startMs)) / frame.width;
    const i = nearestSample(chart.times, time);
    if (i < 0 || chart.times[i] < frame.startMs || offsetX < frame.left) {
      this.hideHover();
      return;
    }
    const x = Math.round(xOf(frame, chart.times[i])) + 0.5;
    this.cursor.setAttribute('x1', String(x));
    this.cursor.setAttribute('x2', String(x));
    this.cursor.setAttribute('visibility', 'visible');
    const rows = [div('tooltip-time', formatAgo(chart.endMs - chart.times[i]))];
    for (const series of chart.series) {
      const row = div('tooltip-row');
      row.append(div(`swatch series-${series.color}`), document.createTextNode(`${series.label}: ${formatValue(chart.unit, series.values[i])}`));
      rows.push(row);
    }
    this.tooltip.replaceChildren(...rows);
    this.tooltip.hidden = false;
    // Note: Positioned through the CSSOM (allowed by the Content Security Policy).
    const left = x + 12 + 180 > frame.left + frame.width ? x - 12 - this.tooltip.offsetWidth : x + 12;
    this.tooltip.style.left = `${Math.max(0, left)}px`;
    this.tooltip.style.top = `${frame.top}px`;
  }

  private hideHover() {
    this.cursor?.setAttribute('visibility', 'hidden');
    this.tooltip.hidden = true;
  }
}

const statusLabels: Record<string, string> = {
  idle: 'Idle',
  busy: 'Indexing',
  verifying: 'Verifying',
  paused: 'Paused',
  yield: 'Paused by the server',
};

/** The status timeline: a strip colored by the indexing status, aligned with a history chart. */
export class StatusTimelineView {
  readonly element: HTMLElement;
  private readonly strip: HTMLElement;
  private drawn = '';

  constructor(private readonly height = 8) {
    this.element = div('timeline');
    this.strip = div('timeline-strip');
    const legend = div('chart-legend timeline-legend');
    for (const status of ['idle', 'busy', 'verifying', 'paused']) {
      const item = div('legend-item');
      const swatch = svg('svg', { width: 10, height: 8 });
      swatch.append(svg('rect', { width: 10, height: 8 }, `status-${status}`));
      item.append(swatch, document.createTextNode(statusLabels[status]));
      legend.append(item);
    }
    this.element.append(this.strip, legend);
  }

  render(timeline: StatusTimeline | undefined, frame: ChartFrame | undefined, width: number, force = false) {
    this.element.hidden = timeline === undefined || frame === undefined;
    if (!timeline || !frame) {
      this.drawn = '';
      return;
    }
    const key = `${timeline.version}:${width}`;
    if (key === this.drawn && !force) {
      return;
    }
    this.drawn = key;
    const root = svg('svg', { width, height: this.height, viewBox: `0 0 ${width} ${this.height}` }, 'timeline-svg');
    root.setAttribute('aria-label', 'Indexing status, last 2 hours');
    for (const segment of timeline.segments) {
      const x1 = xOf(frame, segment.startMs);
      const x2 = xOf(frame, segment.endMs);
      const rect = svg('rect', { x: x1.toFixed(1), y: 0, width: Math.max(1, x2 - x1).toFixed(1), height: this.height }, `status-${segment.status}`);
      const title = svg('title');
      title.textContent = `${statusLabels[segment.status] ?? segment.status}: ${formatAgo(timeline.endMs - segment.startMs)} to ${formatAgo(timeline.endMs - segment.endMs)}`;
      rect.append(title);
      root.append(rect);
    }
    this.strip.replaceChildren(root);
  }
}

/** A bar sparkline of the last durations of an operation, the last one highlighted. */
export function renderSparkline(container: Element, values: number[], width = 100, height = 16) {
  const key = values.join(',');
  if (container.getAttribute('data-values') === key) {
    return;
  }
  container.setAttribute('data-values', key);
  const root = svg('svg', { width, height, viewBox: `0 0 ${width} ${height}` }, 'sparkline');
  root.setAttribute('aria-hidden', 'true');
  for (const bar of sparkBars(values, width, height)) {
    root.append(svg('rect', { x: bar.x, y: bar.y, width: bar.width, height: bar.height }, bar.last ? 'spark last' : 'spark'));
  }
  container.replaceChildren(root);
}
