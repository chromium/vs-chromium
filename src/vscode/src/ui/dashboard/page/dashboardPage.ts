// Copyright 2026 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

// Entry point of the server dashboard page (bundled into out/media/dashboard.js by
// scripts/bundle-page.js). Builds the DOM from script (the host only provides an empty page with
// the stylesheet media/dashboard.css) and patches it on each update: texts are
// only written when they change, rows are keyed (no flicker, selection and focus are kept). The
// DOM is built with textContent only (no innerHTML). Talks to the host only through the shim
// (hostShim.ts).

import { DashboardAction, DashboardController } from '../dashboardController';
import type { DashboardViewModel, KeyValue, OperationRow, ProjectRow } from '../dashboardModel';
import { createHostShim, ShimWindow } from '../hostShim';
import { HistoryChartView, renderSparkline, StatusTimelineView } from './charts';

function el<K extends keyof HTMLElementTagNameMap>(tag: K, className?: string, text?: string): HTMLElementTagNameMap[K] {
  const e = document.createElement(tag);
  if (className) {
    e.className = className;
  }
  if (text !== undefined) {
    e.textContent = text;
  }
  return e;
}

function setText(e: HTMLElement, text: string) {
  if (e.textContent !== text) {
    e.textContent = text;
  }
}

function setTitle(e: HTMLElement, title: string | undefined) {
  if ((e.title || undefined) !== title) {
    e.title = title ?? '';
  }
}

function setClass(e: Element, className: string) {
  if (e.className !== className) {
    e.className = className;
  }
}

function setDisabled(e: HTMLButtonElement | HTMLInputElement, disabled: boolean) {
  if (e.disabled !== disabled) {
    e.disabled = disabled;
  }
}

/** A checkbox like the ones of the VS Code Settings editor (native input, check mark drawn in CSS). */
function checkbox(label: string): { root: HTMLElement; input: HTMLInputElement } {
  const root = el('span', 'checkbox');
  const input = el('input');
  input.type = 'checkbox';
  input.setAttribute('aria-label', label);
  root.append(input, el('span', 'box'));
  return { root, input };
}

function setChecked(input: HTMLInputElement, checked: boolean, enabled: boolean) {
  if (input.checked !== checked) {
    input.checked = checked;
  }
  setDisabled(input, !enabled);
}

/**
 * Rows keyed by `key`: updated in place when the keys are the same as the last render, rebuilt
 * otherwise (rare: projects or operations added).
 */
class KeyedRows<T extends { key: string }> {
  private keys: string[] | undefined;
  private rows: HTMLElement[] = [];

  constructor(
    private readonly container: HTMLElement,
    private readonly create: (item: T) => HTMLElement,
    private readonly update: (row: HTMLElement, item: T) => void,
    private readonly empty?: () => HTMLElement,
  ) {}

  render(items: T[]) {
    const keys = items.map(item => item.key);
    const last = this.keys;
    if (!last || keys.length !== last.length || keys.some((key, i) => key !== last[i])) {
      this.rows = items.map(item => this.create(item));
      this.container.replaceChildren(...(this.rows.length > 0 || !this.empty ? this.rows : [this.empty()]));
      this.keys = keys;
    }
    items.forEach((item, i) => this.update(this.rows[i], item));
  }
}

class DashboardView {
  private readonly status: HTMLElement;
  private readonly statusText: HTMLElement;
  private readonly facts: HTMLElement;
  private readonly progress: HTMLElement;
  private readonly error: HTMLElement;
  private readonly errorText: HTMLElement;
  private readonly pauseOrResume: HTMLButtonElement;
  private readonly pauseOrResumeLabel: HTMLElement;
  private readonly rebuild: HTMLButtonElement;
  private readonly restart: HTMLButtonElement;
  private readonly showOutput: HTMLButtonElement;
  private readonly openLogDirectory: HTMLButtonElement;
  private readonly setting: HTMLInputElement;
  private readonly projectCount: HTMLElement;
  private readonly operationCount: HTMLElement;
  private readonly projects: KeyedRows<ProjectRow>;
  private readonly memory: KeyedRows<KeyValue>;
  private readonly indexCache: KeyedRows<KeyValue>;
  private readonly operations: KeyedRows<OperationRow>;
  private readonly server: KeyedRows<KeyValue>;
  private last: DashboardViewModel | undefined;
  private readonly memoryChart = new HistoryChartView('Memory Usage');
  private readonly statusTimeline = new StatusTimelineView();
  private readonly cpuChart = new HistoryChartView('CPU', 110);
  private readonly cacheChart = new HistoryChartView('Size on Disk', 110);
  private readonly clearCache: HTMLButtonElement;
  private readonly cacheEnabled: HTMLInputElement;
  private readonly cacheEnabledText: HTMLElement;
  private readonly cacheDirectory: HTMLElement;
  private readonly cacheRestart: HTMLElement;
  private readonly cacheSettings: HTMLElement;
  private readonly openCacheSettings: HTMLButtonElement;
  /** The trigram filters checkboxes of the projects are enabled (the server is running). */
  private trigramFiltersEnabled = false;
  /** The "Clear Cache" links of the projects are enabled. */
  private clearCacheEnabled = false;

  constructor(root: HTMLElement, private readonly execute: (action: DashboardAction) => void) {
    // Header
    const header = el('header', 'header');
    const titleRow = el('div', 'title-row');
    titleRow.append(el('h1', 'title', 'VsChromium Server'));
    const toolbar = el('div', 'toolbar');
    toolbar.setAttribute('role', 'toolbar');
    this.pauseOrResumeLabel = el('span', undefined, 'Pause Indexing');
    this.pauseOrResume = this.button('button primary', [this.pauseOrResumeLabel],
      () => this.execute({ action: this.last?.pauseOrResume === 'resume' ? 'resume' : 'pause' }));
    this.rebuild = this.button('button secondary', [el('span', undefined, 'Rebuild Index')],
      () => this.execute({ action: 'rebuild' }), 'Scan the file system again and rebuild the index');
    this.restart = this.button('button secondary', [el('span', undefined, 'Restart Server')],
      () => this.execute({ action: 'restartServer' }), 'Restart the indexing server');
    toolbar.append(this.pauseOrResume, this.rebuild, this.restart);
    titleRow.append(toolbar);

    const statusRow = el('div', 'status-row');
    this.statusText = el('span');
    this.status = el('span', 'status unknown');
    this.status.setAttribute('role', 'status');
    const dot = el('span', 'status-dot');
    dot.setAttribute('aria-hidden', 'true');
    this.status.append(dot, this.statusText);
    this.facts = el('span', 'facts');
    this.showOutput = this.button('link', [el('span', undefined, 'Show Output')],
      () => this.execute({ action: 'showOutput' }), 'Show the output (log) of the extension');
    this.openLogDirectory = this.button('link', [el('span', undefined, 'Open Log Folder')],
      () => this.execute({ action: 'openLogDirectory' }), 'Open the directory of the log files of the server');
    statusRow.append(this.status, this.facts, this.showOutput, this.openLogDirectory);

    this.error = el('div', 'error');
    this.error.setAttribute('role', 'alert');
    this.errorText = el('span');
    this.error.append(this.errorText);
    this.error.hidden = true;
    this.progress = el('div', 'progress');
    this.progress.hidden = true;
    header.append(titleRow, statusRow, this.error, this.progress);

    // Projects
    const projectsSection = this.section('Projects');
    this.projectCount = projectsSection.badge;
    const setting = el('div', 'setting');
    const title = el('div', 'setting-title');
    title.append(el('span', 'setting-category', 'VsChromium: '), el('span', 'setting-name', 'Trigram Filters'));
    const settingCheckbox = checkbox('Trigram Filters');
    this.setting = settingCheckbox.input;
    this.setting.addEventListener('change', () => this.execute({ action: 'toggleTrigramFiltersSetting' }));
    const value = el('label', 'setting-value');
    value.append(settingCheckbox.root, el('span', undefined,
      'Use trigram filters for all the projects: much faster selective searches, at the cost of memory ' +
      '(about 5% of the size of the indexed text). Can be changed per project below, until the setting changes ' +
      'or the server restarts.'));
    setting.append(title, value);
    const projectsTable = this.table(['Root', 'Files', 'Searchable', 'Text', 'Trigram Filters', 'Cache', ''], [1, 2, 3]);
    projectsSection.section.append(setting, projectsTable.table);
    this.projects = new KeyedRows(projectsTable.body, item => this.createProjectRow(item), (row, item) => this.updateProjectRow(row, item),
      () => this.emptyRow(7, 'No project'));

    // Memory and index cache, side by side.
    const columns = el('div', 'columns');
    const memorySection = this.section('Memory');
    const cacheSection = this.section('Index Cache');
    columns.append(memorySection.section, cacheSection.section);
    this.memory = this.keyValues(memorySection.section);
    memorySection.section.append(this.memoryChart.element, this.statusTimeline.element, this.cpuChart.element);
    this.indexCache = this.keyValues(cacheSection.section);

    // Cache settings (host settings, applied when the server restarts) and actions.
    this.cacheSettings = el('div', 'cache-settings');
    const enabledRow = el('label', 'setting-value');
    const enabledCheckbox = checkbox('Index cache enabled (setting)');
    this.cacheEnabled = enabledCheckbox.input;
    this.cacheEnabled.disabled = true;
    this.cacheEnabledText = el('span');
    enabledRow.append(enabledCheckbox.root, this.cacheEnabledText);
    const directoryRow = el('div', 'setting-value');
    this.cacheDirectory = el('span', 'path');
    directoryRow.append(el('span', undefined, 'Directory:'), this.cacheDirectory);
    const settingsTitle = el('div', 'setting-title');
    settingsTitle.append(el('span', 'setting-category', 'VsChromium: '), el('span', 'setting-name', 'Cache Settings'));
    this.cacheRestart = el('div', 'restart-needed', 'Changed: applied when the server restarts.');
    this.cacheRestart.hidden = true;
    this.openCacheSettings = this.button('link', [el('span', undefined, 'Change\u2026')],
      () => this.execute({ action: 'openCacheSettings' }), 'Open the cache settings');
    this.cacheSettings.append(settingsTitle, enabledRow, directoryRow, this.cacheRestart, this.openCacheSettings);
    this.clearCache = this.button('button secondary', [el('span', undefined, 'Clear Cache')],
      () => this.execute({ action: 'clearCache' }),
      'Delete the index cache of all the projects (the current index stays in use)');
    const cacheActions = el('div', 'section-actions');
    cacheActions.append(this.clearCache);
    cacheSection.section.append(this.cacheChart.element, this.cacheSettings, cacheActions);

    // Operations
    const operationsSection = this.section('Operations');
    this.operationCount = operationsSection.badge;
    const operationsTable = this.table(['Name', 'Count', 'Last', 'Max', 'Average', 'Total', 'Recent'], [1, 2, 3, 4, 5]);
    operationsSection.section.append(operationsTable.table);
    this.operations = new KeyedRows(operationsTable.body,
      item => this.createCells(7, [1, 2, 3, 4, 5], item.name),
      (row, item) => {
        this.updateCells(row, [item.name, item.count, item.last, item.max, item.average, item.total]);
        const recent = row.children[6] as HTMLElement;
        renderSparkline(recent, item.recentMs);
        setTitle(recent, item.recentTitle || undefined);
      },
      () => this.emptyRow(7, 'No operation yet'));

    // Server
    const serverSection = this.section('Server');
    this.server = this.keyValues(serverSection.section);

    root.replaceChildren(header, projectsSection.section, columns, operationsSection.section, serverSection.section);
  }

  render(vm: DashboardViewModel) {
    setClass(this.status, `status ${vm.status.kind}`);
    setText(this.statusText, vm.status.text);
    setText(this.facts, vm.facts.join(' \u00b7 '));
    this.progress.hidden = vm.status.kind !== 'busy';
    setText(this.errorText, vm.error ?? '');
    this.error.hidden = vm.error === undefined;

    const resume = vm.pauseOrResume === 'resume';
    setText(this.pauseOrResumeLabel, resume ? 'Resume Indexing' : 'Pause Indexing');
    setTitle(this.pauseOrResume, resume
      ? 'Resume indexing'
      : 'Pause indexing (file system changes are not processed until resumed)');
    setDisabled(this.pauseOrResume, !vm.actions[vm.pauseOrResume]);
    setDisabled(this.rebuild, !vm.actions.rebuild);
    setDisabled(this.restart, !vm.actions.restartServer);
    setDisabled(this.showOutput, !vm.actions.showOutput);
    setDisabled(this.openLogDirectory, !vm.actions.openLogDirectory);
    setChecked(this.setting, vm.trigramFiltersSetting, vm.actions.toggleTrigramFilters);
    this.trigramFiltersEnabled = vm.actions.toggleTrigramFilters;
    this.clearCacheEnabled = vm.actions.clearCache;

    setText(this.projectCount, vm.projects.length > 0 ? String(vm.projects.length) : '');
    setText(this.operationCount, vm.operations.length > 0 ? String(vm.operations.length) : '');
    this.projects.render(vm.projects);
    this.memory.render(vm.memory);
    this.indexCache.render(vm.indexCache);
    this.operations.render(vm.operations);
    this.server.render(vm.server);
    this.renderCache(vm);
    this.last = vm;
    this.renderCharts(vm, false);
  }

  /** Draws the charts (only those whose data or width changed, unless `force`). */
  renderCharts(vm: DashboardViewModel | undefined = this.last, force = true) {
    if (!vm) {
      return;
    }
    this.memoryChart.render(vm.memoryChart, force);
    this.statusTimeline.render(vm.statusTimeline, this.memoryChart.plotFrame, this.memoryChart.element.clientWidth, force);
    this.cpuChart.render(vm.cpuChart, force);
    // Note: No cache chart when the server has no cache.
    this.cacheChart.element.hidden = vm.cacheChart === undefined;
    this.cacheChart.render(vm.cacheChart, force);
  }

  private renderCache(vm: DashboardViewModel) {
    setDisabled(this.clearCache, !vm.actions.clearCache);
    const settings = vm.cacheSettings;
    this.cacheSettings.hidden = settings === undefined;
    if (settings) {
      if (this.cacheEnabled.checked !== settings.enabled) {
        this.cacheEnabled.checked = settings.enabled;
      }
      setText(this.cacheEnabledText, settings.enabled ? 'Enabled' : 'Disabled');
      setText(this.cacheDirectory, settings.directory);
      this.cacheRestart.hidden = !settings.restartNeeded;
    }
    setDisabled(this.openCacheSettings, !vm.actions.openCacheSettings);
  }

  private button(className: string, content: HTMLElement[], onClick: () => void, title?: string): HTMLButtonElement {
    const button = el('button', className);
    button.type = 'button';
    if (title) {
      button.title = title;
    }
    button.append(...content);
    button.disabled = true;
    button.addEventListener('click', onClick);
    return button;
  }

  private section(title: string): { section: HTMLElement; badge: HTMLElement } {
    const section = el('section', 'section');
    const heading = el('h2', 'section-title', title);
    const badge = el('span', 'badge');
    heading.append(badge);
    section.append(heading);
    return { section, badge };
  }

  private table(headers: string[], numericColumns: number[]): { table: HTMLTableElement; body: HTMLTableSectionElement } {
    const table = el('table', 'table');
    const head = el('thead');
    const row = el('tr');
    headers.forEach((header, i) => row.append(el('th', numericColumns.includes(i) ? 'num' : undefined, header)));
    head.append(row);
    const body = el('tbody');
    table.append(head, body);
    return { table, body };
  }

  private createCells(count: number, numericColumns: number[], title?: string): HTMLTableRowElement {
    const row = el('tr');
    for (let i = 0; i < count; i++) {
      row.append(el('td', numericColumns.includes(i) ? 'num' : undefined));
    }
    if (title) {
      row.title = title;
    }
    return row;
  }

  private updateCells(row: HTMLElement, texts: string[]) {
    texts.forEach((text, i) => setText(row.children[i] as HTMLElement, text));
  }

  private emptyRow(columns: number, text: string): HTMLElement {
    const row = el('tr', 'empty');
    const cell = el('td', undefined, text);
    cell.colSpan = columns;
    row.append(cell);
    return row;
  }

  private createProjectRow(item: ProjectRow): HTMLElement {
    const row = this.createCells(7, [1, 2, 3]);
    const root = row.children[0] as HTMLElement;
    root.className = 'path';
    root.title = item.rootPath;
    const trigrams = row.children[4] as HTMLElement;
    const { root: box, input } = checkbox(`Trigram filters of ${item.rootPath}`);
    input.addEventListener('change', () => this.execute({
      action: 'setProjectTrigramFilters',
      projectPath: item.rootPath,
      enabled: input.checked,
    }));
    const label = el('label', 'trigrams');
    label.append(box, el('span', 'description'));
    trigrams.append(label);
    const clear = this.button('link', [el('span', undefined, 'Clear Cache')],
      () => this.execute({ action: 'clearCache', projectPath: item.rootPath }),
      `Delete the index cache of ${item.rootPath}`);
    row.children[6].append(clear);
    return row;
  }

  private updateProjectRow(row: HTMLElement, item: ProjectRow) {
    const cells = row.children;
    setText(cells[0] as HTMLElement, item.rootPath);
    setText(cells[1] as HTMLElement, item.files);
    setText(cells[2] as HTMLElement, item.searchable);
    setText(cells[3] as HTMLElement, item.textSize);
    const label = (cells[4] as HTMLElement).firstElementChild as HTMLElement;
    setChecked(label.querySelector('input')!, item.trigramFiltersEnabled, this.trigramFiltersEnabled);
    setText(label.lastElementChild as HTMLElement, item.trigramFilters);
    setText(cells[5] as HTMLElement, item.cache);
    setTitle(cells[5] as HTMLElement, item.cacheTitle);
    setDisabled((cells[6] as HTMLElement).firstElementChild as HTMLButtonElement, !this.clearCacheEnabled);
  }

  private keyValues(container: HTMLElement): KeyedRows<KeyValue> {
    const list = el('dl', 'kv');
    container.append(list);
    return new KeyedRows<KeyValue>(list,
      () => {
        const row = el('div', 'kv-row');
        row.append(el('dt'), el('dd'));
        return row;
      },
      (row, item) => {
        setText(row.children[0] as HTMLElement, item.label);
        setText(row.children[1] as HTMLElement, item.value);
        setTitle(row, item.title);
      },
      () => el('div', 'kv-empty', '\u2014'));
  }
}

/** Calls `render` at most once per animation frame, with the last view model. */
function coalesce(render: (vm: DashboardViewModel) => void): (vm: DashboardViewModel) => void {
  let pending: DashboardViewModel | undefined;
  return vm => {
    if (pending === undefined) {
      requestAnimationFrame(() => {
        const latest = pending!;
        pending = undefined;
        render(latest);
      });
    }
    pending = vm;
  };
}

function main() {
  const root = document.getElementById('app') ?? document.body;
  let controller: DashboardController | undefined;
  const view = new DashboardView(root, action => void controller?.execute(action));
  const host = createHostShim(window as unknown as ShimWindow);
  controller = new DashboardController(host, coalesce(vm => view.render(vm)));
  // The charts are drawn at the width of the page: redrawn when it changes.
  let width = root.clientWidth;
  new ResizeObserver(() => {
    if (root.clientWidth !== width) {
      width = root.clientWidth;
      view.renderCharts();
    }
  }).observe(root);
  document.addEventListener('visibilitychange', () => controller?.setPageVisible(document.visibilityState !== 'hidden'));
  window.addEventListener('unload', () => controller?.dispose());
  controller.start();
}

main();
