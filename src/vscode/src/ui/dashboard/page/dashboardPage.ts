// Copyright 2026 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

// Entry point of the server dashboard page (bundled into out/media/dashboard.js by
// scripts/bundle-page.js). Builds the DOM from script (the host only provides an empty page with
// the stylesheet media/dashboard.css) and patches it on each update: texts are
// only written when they change, rows are keyed (no flicker, selection and focus are kept). The
// DOM is built with textContent only (no innerHTML). Talks to the host only through the shim
// (hostShim.ts).
//
// Layout: the header (process status and actions), and either the overview sections ("Process",
// "Projects" with clickable project paths, and collapsible "Details") or the drill-down "Disk Usage"
// explorer for a selected project.

import { DashboardAction, DashboardController } from '../dashboardController';
import type { DashboardViewModel, KeyValue, OperationRow, ProjectBlock, ProjectOperationRow } from '../dashboardModel';
import { createHostShim, ShimWindow } from '../hostShim';
import { applyThemeVariables } from '../theme';
import { HistoryChartView, renderSparkline, StatusTimelineView } from './charts';
import { DiskUsageView } from './diskUsage';

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

function setHidden(e: HTMLElement, hidden: boolean) {
  if (e.hidden !== hidden) {
    e.hidden = hidden;
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

function button(className: string, text: string, onClick: () => void, title?: string): HTMLButtonElement {
  const b = el('button', className, text);
  b.type = 'button';
  if (title) {
    b.title = title;
  }
  b.disabled = true;
  b.addEventListener('click', onClick);
  return b;
}

/** A table with its header; numeric columns right aligned. */
function table(headers: string[], numericColumns: number[], className = 'table'): { table: HTMLTableElement; body: HTMLTableSectionElement } {
  const t = el('table', className);
  const head = el('thead');
  const row = el('tr');
  headers.forEach((header, i) => row.append(el('th', numericColumns.includes(i) ? 'num' : undefined, header)));
  head.append(row);
  const body = el('tbody');
  t.append(head, body);
  return { table: t, body };
}

function cells(count: number, numericColumns: number[]): HTMLTableRowElement {
  const row = el('tr');
  for (let i = 0; i < count; i++) {
    row.append(el('td', numericColumns.includes(i) ? 'num' : undefined));
  }
  return row;
}

function updateCells(row: HTMLElement, texts: string[]) {
  texts.forEach((text, i) => setText(row.children[i] as HTMLElement, text));
}

function emptyRow(columns: number, text: string): HTMLElement {
  const row = el('tr', 'empty');
  const cell = el('td', undefined, text);
  cell.colSpan = columns;
  row.append(cell);
  return row;
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

/** A key/value list (label column, value column). */
function keyValues(container: HTMLElement): KeyedRows<KeyValue> {
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

/** A section with a title and a count badge, optionally collapsible (twistie in the title). */
function section(title: string, collapsible?: { expanded: boolean }): { section: HTMLElement; badge: HTMLElement; body: HTMLElement } {
  const s = el('section', 'section');
  const heading = el('h2', 'section-title');
  const badge = el('span', 'badge');
  const body = el('div', 'section-body');
  if (collapsible) {
    const toggle = el('button', 'twistie-button');
    toggle.type = 'button';
    toggle.append(el('span', 'twistie'), el('span', undefined, title));
    const apply = (expanded: boolean) => {
      toggle.setAttribute('aria-expanded', String(expanded));
      body.hidden = !expanded;
    };
    toggle.addEventListener('click', () => apply(toggle.getAttribute('aria-expanded') !== 'true'));
    apply(collapsible.expanded);
    heading.append(toggle, badge);
  } else {
    heading.append(el('span', undefined, title), badge);
  }
  s.append(heading, body);
  return { section: s, badge, body };
}

/** The elements of the block of a project. */
interface ProjectElements {
  root: HTMLElement;
  toggle: HTMLButtonElement;
  path: HTMLButtonElement;
  summary: HTMLElement;
  body: HTMLElement;
  diskUsage: HTMLButtonElement;
  rebuild: HTMLButtonElement;
  clearCache: HTMLButtonElement;
  index: HTMLElement;
  memory: HTMLElement;
  cacheEnabled: HTMLInputElement;
  cacheState: HTMLElement;
  cacheDirectory: HTMLElement;
  openFolder: HTMLButtonElement;
  readOnly: HTMLElement;
  trigramFilters: HTMLInputElement;
  trigramFiltersText: HTMLElement;
  externalLinksWarning: HTMLElement;
  watchExternalLine: HTMLElement;
  watchExternal: HTMLInputElement;
  watchExternalText: HTMLElement;
  operations: KeyedRows<ProjectOperationRow>;
}

class DashboardView {
  private readonly status: HTMLElement;
  private readonly statusText: HTMLElement;
  private readonly facts: HTMLElement;
  private readonly progress: HTMLElement;
  private readonly error: HTMLElement;
  private readonly errorText: HTMLElement;
  private readonly pauseOrResume: HTMLButtonElement;
  private readonly restart: HTMLButtonElement;
  private readonly showOutput: HTMLButtonElement;
  private readonly openLogDirectory: HTMLButtonElement;
  private readonly processSection: HTMLElement;
  private readonly projectsSection: HTMLElement;
  private readonly detailsSection: HTMLElement;
  private readonly diskUsageView: DiskUsageView;
  private readonly processMemory: KeyedRows<KeyValue>;
  private readonly processCache: KeyedRows<KeyValue>;
  private readonly cacheRestart: HTMLElement;
  private readonly openCacheSettings: HTMLButtonElement;
  private readonly clearAllCaches: HTMLButtonElement;
  private readonly trigramFiltersDefault: HTMLInputElement;
  private readonly trigramFiltersDefaultText: HTMLElement;
  private readonly projectCount: HTMLElement;
  private readonly projects: KeyedRows<ProjectBlock>;
  private readonly projectElements = new WeakMap<HTMLElement, ProjectElements>();
  /** The projects expanded or collapsed by the user (otherwise: their default). */
  private readonly expanded = new Map<string, boolean>();
  private readonly operationCount: HTMLElement;
  private readonly operations: KeyedRows<OperationRow>;
  private readonly server: KeyedRows<KeyValue>;
  private readonly memoryChart = new HistoryChartView('Memory Usage');
  private readonly statusTimeline = new StatusTimelineView();
  private readonly cpuChart = new HistoryChartView('CPU');
  private readonly cacheChart = new HistoryChartView('Index Cache Size on Disk (All Projects)', 110);
  private last: DashboardViewModel | undefined;

  constructor(root: HTMLElement, private readonly execute: (action: DashboardAction) => void) {
    // Header: the process status and the process actions.
    const header = el('header', 'header');
    const titleRow = el('div', 'title-row');
    titleRow.append(el('h1', 'title', 'VsChromium Server'));
    const toolbar = el('div', 'toolbar');
    toolbar.setAttribute('role', 'toolbar');
    this.pauseOrResume = button('button primary', 'Pause Indexing',
      () => this.execute({ action: this.last?.pauseOrResume === 'resume' ? 'resume' : 'pause' }));
    this.restart = button('button secondary', 'Restart Server', () => this.execute({ action: 'restartServer' }),
      'Restart the indexing server');
    toolbar.append(this.pauseOrResume, this.restart);
    titleRow.append(toolbar);

    const statusRow = el('div', 'status-row');
    this.statusText = el('span');
    this.status = el('span', 'status unknown');
    this.status.setAttribute('role', 'status');
    const dot = el('span', 'status-dot');
    dot.setAttribute('aria-hidden', 'true');
    this.status.append(dot, this.statusText);
    this.facts = el('span', 'facts');
    this.showOutput = button('link', 'Show Output', () => this.execute({ action: 'showOutput' }),
      'Show the output (log) of the extension');
    this.openLogDirectory = button('link', 'Open Log Folder', () => this.execute({ action: 'openLogDirectory' }),
      'Open the directory of the log files of the server');
    statusRow.append(this.status, this.facts, this.showOutput, this.openLogDirectory);

    this.error = el('div', 'error');
    this.error.setAttribute('role', 'alert');
    this.errorText = el('span');
    this.error.append(this.errorText);
    this.error.hidden = true;
    this.progress = el('div', 'progress');
    this.progress.hidden = true;
    header.append(titleRow, statusRow, this.error, this.progress);

    // Process: memory, charts, index cache root, defaults.
    const process = section('Process');
    this.processSection = process.section;
    this.processMemory = keyValues(process.body);
    const charts = el('div', 'charts');
    const memoryColumn = el('div', 'chart-column');
    memoryColumn.append(this.memoryChart.element, this.statusTimeline.element);
    const cpuColumn = el('div', 'chart-column');
    cpuColumn.append(this.cpuChart.element);
    charts.append(memoryColumn, cpuColumn);
    process.body.append(charts);

    const cacheGroup = el('div', 'group');
    cacheGroup.append(el('h3', 'group-title', 'Index Cache'));
    this.processCache = keyValues(cacheGroup);
    this.cacheRestart = el('div', 'restart-needed', 'The cache directory setting changed: applied when the server restarts.');
    this.cacheRestart.hidden = true;
    const cacheActions = el('div', 'group-actions');
    this.openCacheSettings = button('link', 'Change\u2026', () => this.execute({ action: 'openCacheSettings' }),
      'Change the cache directory (settings)');
    this.clearAllCaches = button('button secondary', 'Clear All Caches', () => this.execute({ action: 'clearCache' }),
      'Delete the index cache of all the projects (the current index stays in use)');
    cacheActions.append(this.clearAllCaches, this.openCacheSettings);
    cacheGroup.append(this.cacheRestart, cacheActions);

    const defaults = el('div', 'setting');
    const defaultsTitle = el('div', 'setting-title');
    defaultsTitle.append(el('span', 'setting-category', 'VsChromium: '), el('span', 'setting-name', 'Trigram Filters'));
    const defaultCheckbox = checkbox('Trigram filters (default)');
    this.trigramFiltersDefault = defaultCheckbox.input;
    this.trigramFiltersDefault.addEventListener('change', () => this.execute({ action: 'toggleTrigramFiltersSetting' }));
    const defaultValue = el('label', 'setting-value');
    // Note: The wording (where the setting is stored) is supplied by the host (vm.labels).
    this.trigramFiltersDefaultText = el('span');
    defaultValue.append(defaultCheckbox.root, this.trigramFiltersDefaultText);
    defaults.append(defaultsTitle, defaultValue);
    process.body.append(cacheGroup, defaults);

    // Projects: a block per project.
    const projectsSection = section('Projects');
    this.projectsSection = projectsSection.section;
    this.projectCount = projectsSection.badge;
    const projectList = el('div', 'projects');
    projectsSection.body.append(projectList);
    this.projects = new KeyedRows(projectList, item => this.createProject(item), (row, item) => this.updateProject(row, item),
      () => el('div', 'kv-empty', 'No project'));

    // Details (collapsed): raw operation timings, server, cache size chart.
    const details = section('Details', { expanded: false });
    this.detailsSection = details.section;
    this.operationCount = details.badge;
    const operationsGroup = el('div', 'group');
    operationsGroup.append(el('h3', 'group-title', 'Operation Timings'));
    const operationsTable = table(['Name', 'Count', 'Last', 'Max', 'Average', 'Total', 'Recent'], [1, 2, 3, 4, 5]);
    operationsGroup.append(operationsTable.table);
    this.operations = new KeyedRows(operationsTable.body,
      item => {
        const row = cells(7, [1, 2, 3, 4, 5]);
        row.title = item.name;
        return row;
      },
      (row, item) => {
        updateCells(row, [item.name, item.count, item.last, item.max, item.average, item.total]);
        const recent = row.children[6] as HTMLElement;
        renderSparkline(recent, item.recentMs);
        setTitle(recent, item.recentTitle || undefined);
      },
      () => emptyRow(7, 'No operation yet'));
    const serverGroup = el('div', 'group');
    serverGroup.append(el('h3', 'group-title', 'Server'));
    this.server = keyValues(serverGroup);
    details.body.append(operationsGroup, serverGroup, this.cacheChart.element);
    // Note: The charts of a collapsed section are drawn when it is expanded (their width is known).
    details.section.querySelector('.twistie-button')!.addEventListener('click', () => this.renderCharts());

    this.diskUsageView = new DiskUsageView(this.execute);

    root.replaceChildren(header, process.section, projectsSection.section, details.section, this.diskUsageView.element);
  }

  render(vm: DashboardViewModel) {
    const wasDiskUsage = this.last?.view === 'diskUsage';
    const isDiskUsage = vm.view === 'diskUsage';

    setClass(this.status, `status ${vm.status.kind}`);
    setText(this.statusText, vm.status.text);
    setText(this.facts, vm.facts.join(' \u00b7 '));
    setHidden(this.progress, vm.status.kind !== 'busy');
    setText(this.errorText, vm.error ?? '');
    setHidden(this.error, vm.error === undefined);

    const resume = vm.pauseOrResume === 'resume';
    setText(this.pauseOrResume, resume ? 'Resume Indexing' : 'Pause Indexing');
    setTitle(this.pauseOrResume, resume
      ? 'Resume indexing'
      : 'Pause indexing (file system changes are not processed until resumed)');
    setDisabled(this.pauseOrResume, !vm.actions[vm.pauseOrResume]);
    setDisabled(this.restart, !vm.actions.restartServer);
    setDisabled(this.showOutput, !vm.actions.showOutput);
    setDisabled(this.openLogDirectory, !vm.actions.openLogDirectory);

    setHidden(this.processSection, isDiskUsage);
    setHidden(this.projectsSection, isDiskUsage);
    setHidden(this.detailsSection, isDiskUsage);

    this.processMemory.render(vm.memory);
    this.processCache.render(vm.indexCache);
    setHidden(this.cacheRestart, !vm.cacheSettings?.restartNeeded);
    setDisabled(this.openCacheSettings, !vm.actions.openCacheSettings);
    setDisabled(this.clearAllCaches, !vm.actions.clearCache);
    setChecked(this.trigramFiltersDefault, vm.trigramFiltersSetting, vm.actions.toggleTrigramFilters);
    setText(this.trigramFiltersDefaultText, vm.labels.trigramFiltersDefault);

    this.last = vm;
    setText(this.projectCount, vm.projects.length > 0 ? String(vm.projects.length) : '');
    this.projects.render(vm.projects);
    setText(this.operationCount, '');
    this.operations.render(vm.operations);
    this.server.render(vm.server);
    this.diskUsageView.render(vm.diskUsage);
    if (!isDiskUsage) {
      this.renderCharts(vm, wasDiskUsage);
    }
  }

  /** Draws the charts (only those whose data or width changed, unless `force`). */
  renderCharts(vm: DashboardViewModel | undefined = this.last, force = true) {
    if (!vm || vm.view === 'diskUsage') {
      return;
    }
    this.memoryChart.render(vm.memoryChart, force);
    this.statusTimeline.render(vm.statusTimeline, this.memoryChart.plotFrame, this.memoryChart.element.clientWidth, force);
    this.cpuChart.render(vm.cpuChart, force);
    setHidden(this.cacheChart.element, vm.cacheChart === undefined);
    if (this.cacheChart.element.offsetParent !== null) {
      this.cacheChart.render(vm.cacheChart, force);
    }
  }

  private createProject(item: ProjectBlock): HTMLElement {
    const root = el('div', 'project');
    const header = el('div', 'project-header');
    const titleGroup = el('div', 'project-title-group');
    const toggle = el('button', 'twistie-button project-twistie');
    toggle.type = 'button';
    toggle.setAttribute('aria-label', `Expand or collapse ${item.rootPath}`);
    toggle.append(el('span', 'twistie'));
    const path = button('link project-path', item.rootPath,
      () => this.execute({ action: 'openDiskUsage', projectPath: item.rootPath }),
      'Explore directory and file sizes on disk to find exclusions');
    const summary = el('span', 'project-summary');
    titleGroup.append(toggle, path, summary);
    const actions = el('div', 'project-actions');
    const diskUsage = button('button secondary small', 'Disk Usage',
      () => this.execute({ action: 'openDiskUsage', projectPath: item.rootPath }),
      'Explore directory and file sizes on disk and add ignore patterns');
    const rebuild = button('button secondary small', 'Rebuild Index',
      () => this.execute({ action: 'rebuildProject', projectPath: item.rootPath }),
      'Delete the cache of the project, scan and read its files again');
    const clearCache = button('button secondary small', 'Clear Cache',
      () => this.execute({ action: 'clearCache', projectPath: item.rootPath }),
      'Delete the index cache of the project (its current index stays in use)');
    actions.append(diskUsage, rebuild, clearCache);
    header.append(titleGroup, actions);

    const body = el('dl', 'project-body');
    const group = (label: string, ...content: HTMLElement[]) => {
      const row = el('div', 'kv-row');
      const dd = el('dd');
      dd.append(...content);
      row.append(el('dt', undefined, label), dd);
      body.append(row);
    };
    const index = el('div', 'line');
    const memory = el('div', 'line');
    group('Index', index);
    group('Memory', memory);

    const cacheLine = el('label', 'line control-line');
    const cacheCheckbox = checkbox(`Index cache of ${item.rootPath}`);
    cacheCheckbox.input.addEventListener('change', () => this.execute({
      action: 'setProjectCacheEnabled', projectPath: item.rootPath, enabled: cacheCheckbox.input.checked,
    }));
    const cacheState = el('span');
    cacheLine.append(cacheCheckbox.root, el('span', 'control-label', 'Enabled'), cacheState);
    const directoryLine = el('div', 'line');
    const cacheDirectory = el('span', 'path');
    const openFolder = button('link', 'Open Folder', () => this.execute({ action: 'openProjectCacheFolder', projectPath: item.rootPath }),
      'Open the cache directory of the project');
    directoryLine.append(cacheDirectory, openFolder);
    const readOnly = el('div', 'line restart-needed', 'Read-only: this cache is written by another server (e.g. another window).');
    group('Cache', cacheLine, directoryLine, readOnly);

    const optionsLine = el('label', 'line control-line');
    const trigramCheckbox = checkbox(`Trigram filters of ${item.rootPath}`);
    trigramCheckbox.input.addEventListener('change', () => this.execute({
      action: 'setProjectTrigramFilters', projectPath: item.rootPath, enabled: trigramCheckbox.input.checked,
    }));
    const trigramText = el('span', 'description');
    optionsLine.append(trigramCheckbox.root, el('span', 'control-label', 'Trigram filters'), trigramText);
    // The symbolic links to targets outside of the project (shown if any).
    const externalLinksWarning = el('div', 'line restart-needed');
    const watchExternalLine = el('label', 'line control-line');
    const watchExternalCheckbox = checkbox(`Watch the targets outside of ${item.rootPath}`);
    watchExternalCheckbox.input.addEventListener('change', () => this.execute({
      action: 'setProjectWatchExternalLinkTargets', projectPath: item.rootPath, enabled: watchExternalCheckbox.input.checked,
    }));
    const watchExternalText = el('span', 'description');
    watchExternalLine.append(watchExternalCheckbox.root, el('span', 'control-label', 'Watch the targets outside the project'), watchExternalText);
    group('Options', optionsLine, externalLinksWarning, watchExternalLine);

    const operationsTable = table(['Operation', 'Count', 'Min', 'Avg', 'Max', 'Last', ''], [1, 2, 3, 4, 5], 'table compact');
    group('Operations', operationsTable.table);
    const operations = new KeyedRows<ProjectOperationRow>(operationsTable.body,
      () => cells(7, [1, 2, 3, 4, 5]),
      (row, op) => {
        updateCells(row, [op.name, op.count, op.min, op.avg, op.max, op.last, op.note]);
        (row.children[6] as HTMLElement).className = 'description';
      });

    root.append(header, body);
    const toggleExpand = () => {
      const expanded = toggle.getAttribute('aria-expanded') !== 'true';
      this.expanded.set(item.rootPath, expanded);
      this.applyExpanded(elements, expanded);
    };
    toggle.addEventListener('click', toggleExpand);
    summary.addEventListener('click', toggleExpand);
    const elements: ProjectElements = {
      root, toggle, path, summary, body, diskUsage, rebuild, clearCache, index, memory,
      cacheEnabled: cacheCheckbox.input, cacheState, cacheDirectory, openFolder, readOnly,
      trigramFilters: trigramCheckbox.input, trigramFiltersText: trigramText,
      externalLinksWarning, watchExternalLine, watchExternal: watchExternalCheckbox.input, watchExternalText, operations,
    };
    this.projectElements.set(root, elements);
    this.applyExpanded(elements, this.expanded.get(item.rootPath) ?? item.defaultExpanded);
    return root;
  }

  private applyExpanded(elements: ProjectElements, expanded: boolean) {
    elements.toggle.setAttribute('aria-expanded', String(expanded));
    setHidden(elements.body, !expanded);
    setHidden(elements.summary, expanded);
  }

  private updateProject(row: HTMLElement, item: ProjectBlock) {
    const e = this.projectElements.get(row)!;
    const vm = this.last!;
    setText(e.path, item.rootPath);
    setTitle(e.path, `Explore directory and file sizes of ${item.rootPath}`);
    setDisabled(e.path, !vm.actions.toggleTrigramFilters);
    setDisabled(e.diskUsage, !vm.actions.toggleTrigramFilters);
    setText(e.summary, item.summary);
    this.applyExpanded(e, this.expanded.get(item.rootPath) ?? item.defaultExpanded);
    setDisabled(e.rebuild, !vm.actions.rebuild);
    setDisabled(e.clearCache, !(vm.actions.clearCache && item.cache.enabled && item.cache.writable));
    setText(e.index, item.index.map(kv => (kv.key === 'updated' ? `updated ${kv.value}` : kv.value)).join(' \u00b7 '));
    setText(e.memory, item.memory.map(kv => `${kv.label.toLowerCase()} ${kv.value}`).join(' \u00b7 '));
    setChecked(e.cacheEnabled, item.cache.enabled, vm.actions.toggleTrigramFilters);
    setText(e.cacheState, item.cache.enabled ? `\u00b7 ${item.cache.state}` : '');
    setText(e.cacheDirectory, item.cache.directory ?? '');
    setHidden(e.cacheDirectory.parentElement!, !item.cache.directory);
    setDisabled(e.openFolder, !vm.actions.toggleTrigramFilters || !item.cache.directory);
    setHidden(e.readOnly, !item.cache.enabled || item.cache.writable);
    setChecked(e.trigramFilters, item.trigramFiltersEnabled, vm.actions.toggleTrigramFilters);
    setText(e.trigramFiltersText, item.trigramFiltersEnabled ? `\u00b7 ${item.trigramFilters}` : '');
    const external = item.externalLinks;
    setText(e.externalLinksWarning, external?.warning ?? '');
    setHidden(e.externalLinksWarning, !external?.warning);
    setHidden(e.watchExternalLine, !external);
    setChecked(e.watchExternal, external?.watched === true, vm.actions.toggleTrigramFilters);
    setText(e.watchExternalText, external?.state ?? '');
    setTitle(e.watchExternalLine, external?.title);
    e.operations.render(item.operations);
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
  // The theme of hosts other than VS Code (which defines the "--vscode-*" variables itself).
  let themeVariables = new Set<string>();
  host.onMessage(message => {
    if (message.type === 'theme') {
      themeVariables = applyThemeVariables(document.documentElement.style, message.variables, themeVariables);
    }
  });
  controller.start();
}

main();
