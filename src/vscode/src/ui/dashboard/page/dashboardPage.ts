// Copyright 2026 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

// Entry point of the server dashboard page (bundled into out/media/dashboard.js by
// scripts/bundle-page.js). Builds the DOM from script (the host only provides an empty page with
// the stylesheet) and patches it on each update: texts are only written when they change, rows are
// keyed (no flicker, selection and focus are kept). The DOM is built with textContent only (no
// innerHTML). Talks to the host only through the shim (hostShim.ts).

import { DashboardAction, DashboardController } from '../dashboardController';
import type { ActionId, DashboardViewModel, KeyValue, OperationRow, ProjectRow } from '../dashboardModel';
import { createHostShim, ShimWindow } from '../hostShim';

const SvgNs = 'http://www.w3.org/2000/svg';

/** Icons (16x16, stroked or filled with currentColor): no icon font in the page. */
const icons: Record<string, string[]> = {
  pause: ['M5 3h2v10H5z', 'M9 3h2v10H9z'],
  resume: ['M5 3l8 5-8 5z'],
  rebuild: ['M13 8a5 5 0 1 1-1.5-3.6', 'M12 2v3h-3'],
  restart: ['M8 2v6', 'M4.5 4.5a5 5 0 1 0 7 0'],
  output: ['M3 4h10', 'M3 7h10', 'M3 10h7', 'M3 13h5'],
  folder: ['M2 4h4l1.5 1.5H14v7.5H2z'],
};

const filledIcons = new Set(['pause', 'resume']);

function icon(name: string): SVGSVGElement {
  const svg = document.createElementNS(SvgNs, 'svg');
  svg.setAttribute('viewBox', '0 0 16 16');
  svg.setAttribute('width', '16');
  svg.setAttribute('height', '16');
  svg.setAttribute('aria-hidden', 'true');
  svg.classList.add('icon', filledIcons.has(name) ? 'filled' : 'stroked');
  for (const d of icons[name] ?? []) {
    const path = document.createElementNS(SvgNs, 'path');
    path.setAttribute('d', d);
    svg.append(path);
  }
  return svg;
}

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

/** A button of the header. */
interface ActionButton {
  button: HTMLButtonElement;
  label: HTMLElement;
  iconName: string;
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
  private readonly facts: HTMLElement;
  private readonly error: HTMLElement;
  private readonly buttons = new Map<string, ActionButton>();
  private readonly settingSwitch: HTMLButtonElement;
  private readonly projects: KeyedRows<ProjectRow>;
  private readonly memory: KeyedRows<KeyValue>;
  private readonly indexCache: KeyedRows<KeyValue>;
  private readonly operations: KeyedRows<OperationRow>;
  private readonly server: KeyedRows<KeyValue>;
  private last: DashboardViewModel | undefined;
  /** The trigram filters switches of the projects are enabled (the server is running). */
  private trigramFiltersEnabled = false;

  constructor(root: HTMLElement, private readonly execute: (action: DashboardAction) => void) {
    // Header
    const header = el('header', 'header');
    const titleRow = el('div', 'title-row');
    titleRow.append(el('h1', 'title', 'VsChromium Server'));
    const toolbar = el('div', 'toolbar');
    toolbar.setAttribute('role', 'toolbar');
    toolbar.append(
      this.button('pauseOrResume', 'pause', 'Pause', 'Pause indexing (file system changes are not processed until resumed)'),
      this.button('rebuild', 'rebuild', 'Rebuild index', 'Scan the file system again and rebuild the index'),
      this.button('restartServer', 'restart', 'Restart server', 'Restart the indexing server'),
      this.button('showOutput', 'output', 'Output', 'Show the output (log) of the extension'),
      this.button('openLogDirectory', 'folder', 'Log folder', 'Open the directory of the log files of the server'),
    );
    titleRow.append(toolbar);
    const statusRow = el('div', 'status-row');
    this.status = el('span', 'status unknown');
    this.facts = el('span', 'facts');
    statusRow.append(this.status, this.facts);
    this.error = el('div', 'error');
    this.error.hidden = true;
    header.append(titleRow, statusRow, this.error);

    // Projects
    const projectsSection = this.section('Projects');
    const settingRow = el('div', 'setting-row');
    this.settingSwitch = this.switch('Trigram filters for all the projects (setting)');
    this.settingSwitch.addEventListener('click', () => this.execute({ action: 'toggleTrigramFiltersSetting' }));
    settingRow.append(this.settingSwitch, el('span', 'setting-label',
      'Trigram filters for all the projects (setting): faster searches, about 5% of the text size in memory'));
    const projectsTable = this.table(['Root', 'Files', 'Searchable', 'Text', 'Trigram filters', 'Cache'], [1, 2, 3]);
    projectsSection.body.append(settingRow, projectsTable.table);
    this.projects = new KeyedRows(projectsTable.body, item => this.createProjectRow(item), (row, item) => this.updateProjectRow(row, item),
      () => this.emptyRow(6, 'No project'));

    // Memory and index cache, side by side.
    const grid = el('div', 'grid');
    const memorySection = this.section('Memory');
    const cacheSection = this.section('Index cache');
    grid.append(memorySection.section, cacheSection.section);
    this.memory = this.keyValues(memorySection.body);
    this.indexCache = this.keyValues(cacheSection.body);

    // Operations
    const operationsSection = this.section('Operations');
    const operationsTable = this.table(['Name', 'Count', 'Last', 'Max', 'Average', 'Total'], [1, 2, 3, 4, 5]);
    operationsSection.body.append(operationsTable.table);
    this.operations = new KeyedRows(operationsTable.body,
      item => this.createCells(6, [1, 2, 3, 4, 5], item.name),
      (row, item) => this.updateCells(row, [item.name, item.count, item.last, item.max, item.average, item.total]),
      () => this.emptyRow(6, 'No operation yet'));

    // Server
    const serverSection = this.section('Server');
    this.server = this.keyValues(serverSection.body);

    root.replaceChildren(header, projectsSection.section, grid, operationsSection.section, serverSection.section);
  }

  render(vm: DashboardViewModel) {
    setText(this.status, vm.status.text);
    const statusClass = `status ${vm.status.kind}`;
    if (this.status.className !== statusClass) {
      this.status.className = statusClass;
    }
    setText(this.facts, vm.facts.join(' \u00b7 '));
    setText(this.error, vm.error ?? '');
    this.error.hidden = vm.error === undefined;

    const pauseOrResume = this.buttons.get('pauseOrResume')!;
    if (this.last?.pauseOrResume !== vm.pauseOrResume) {
      const resume = vm.pauseOrResume === 'resume';
      setText(pauseOrResume.label, resume ? 'Resume' : 'Pause');
      pauseOrResume.button.title = resume ? 'Resume indexing' : 'Pause indexing (file system changes are not processed until resumed)';
      pauseOrResume.button.replaceChild(icon(resume ? 'resume' : 'pause'), pauseOrResume.button.firstChild!);
    }
    this.setEnabled('pauseOrResume', vm.actions[vm.pauseOrResume]);
    for (const id of ['rebuild', 'restartServer', 'showOutput', 'openLogDirectory'] as ActionId[]) {
      this.setEnabled(id, vm.actions[id]);
    }
    this.setSwitch(this.settingSwitch, vm.trigramFiltersSetting, vm.actions.toggleTrigramFilters);
    this.trigramFiltersEnabled = vm.actions.toggleTrigramFilters;

    this.projects.render(vm.projects);
    this.memory.render(vm.memory);
    this.indexCache.render(vm.indexCache);
    this.operations.render(vm.operations);
    this.server.render(vm.server);
    this.last = vm;
  }

  private button(id: string, iconName: string, text: string, title: string): HTMLButtonElement {
    const button = el('button', 'action');
    button.type = 'button';
    button.title = title;
    const label = el('span', 'label', text);
    button.append(icon(iconName), label);
    button.disabled = true;
    button.addEventListener('click', () => {
      if (id === 'pauseOrResume') {
        this.execute({ action: this.last?.pauseOrResume === 'resume' ? 'resume' : 'pause' });
      } else {
        this.execute({ action: id } as DashboardAction);
      }
    });
    this.buttons.set(id, { button, label, iconName });
    return button;
  }

  private setEnabled(id: string, enabled: boolean) {
    const button = this.buttons.get(id)?.button;
    if (button && button.disabled === enabled) {
      button.disabled = !enabled;
    }
  }

  private switch(label: string): HTMLButtonElement {
    const button = el('button', 'switch');
    button.type = 'button';
    button.setAttribute('role', 'switch');
    button.setAttribute('aria-checked', 'false');
    button.setAttribute('aria-label', label);
    button.append(el('span', 'knob'));
    return button;
  }

  private setSwitch(button: HTMLButtonElement, checked: boolean, enabled: boolean) {
    const value = checked ? 'true' : 'false';
    if (button.getAttribute('aria-checked') !== value) {
      button.setAttribute('aria-checked', value);
    }
    if (button.disabled === enabled) {
      button.disabled = !enabled;
    }
  }

  private section(title: string): { section: HTMLElement; body: HTMLElement } {
    const section = el('section', 'card');
    section.append(el('h2', 'card-title', title));
    const body = el('div', 'card-body');
    section.append(body);
    return { section, body };
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
    const row = this.createCells(6, [1, 2, 3]);
    const root = row.children[0] as HTMLElement;
    root.className = 'path';
    root.title = item.rootPath;
    const trigrams = row.children[4] as HTMLElement;
    const toggle = this.switch(`Trigram filters of ${item.rootPath}`);
    toggle.addEventListener('click', () => this.execute({
      action: 'setProjectTrigramFilters',
      projectPath: item.rootPath,
      enabled: toggle.getAttribute('aria-checked') !== 'true',
    }));
    trigrams.className = 'trigrams';
    trigrams.append(toggle, el('span', 'switch-text'));
    return row;
  }

  private updateProjectRow(row: HTMLElement, item: ProjectRow) {
    const cells = row.children;
    setText(cells[0] as HTMLElement, item.rootPath);
    setText(cells[1] as HTMLElement, item.files);
    setText(cells[2] as HTMLElement, item.searchable);
    setText(cells[3] as HTMLElement, item.textSize);
    const trigrams = cells[4] as HTMLElement;
    this.setSwitch(trigrams.children[0] as HTMLButtonElement, item.trigramFiltersEnabled, this.trigramFiltersEnabled);
    setText(trigrams.children[1] as HTMLElement, item.trigramFilters);
    setText(cells[5] as HTMLElement, item.cache);
    setTitle(cells[5] as HTMLElement, item.cacheTitle);
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
  document.addEventListener('visibilitychange', () => controller?.setPageVisible(document.visibilityState !== 'hidden'));
  window.addEventListener('unload', () => controller?.dispose());
  controller.start();
}

main();
