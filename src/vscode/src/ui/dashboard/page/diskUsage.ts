// Copyright 2026 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

// Drill-down project disk usage explorer view for the server dashboard page. Built with DOM APIs
// and textContent only (no innerHTML, no icon font).

import type { DashboardAction } from '../dashboardController';
import type { DiskUsageBreadcrumb, DiskUsageRowView, DiskUsageViewModel } from '../dashboardModel';

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

function setDisabled(e: HTMLButtonElement, disabled: boolean) {
  if (e.disabled !== disabled) {
    e.disabled = disabled;
  }
}

function setHidden(e: HTMLElement, hidden: boolean) {
  if (e.hidden !== hidden) {
    e.hidden = hidden;
  }
}

function actionButton(className: string, text: string, onClick: () => void, title?: string): HTMLButtonElement {
  const b = el('button', className, text);
  b.type = 'button';
  if (title) {
    b.title = title;
  }
  b.addEventListener('click', onClick);
  return b;
}

interface RowElements {
  tr: HTMLTableRowElement;
  icon: HTMLElement;
  link: HTMLButtonElement;
  name: HTMLElement;
  excludedBadge: HTMLElement;
  sizeCell: HTMLElement;
  bar: HTMLElement;
  barSearchable: HTMLElement;
  barOther: HTMLElement;
  sizeText: HTMLElement;
  measureBtn: HTMLButtonElement;
  remeasureBtn: HTMLButtonElement;
  filesTd: HTMLElement;
  copyBtn: HTMLButtonElement;
  explorerBtn: HTMLButtonElement;
  searchableBtn: HTMLButtonElement;
  current: DiskUsageRowView;
  copiedUntilMs: number;
}

export class DiskUsageView {
  readonly element: HTMLElement;
  private readonly upBtn: HTMLButtonElement;
  private readonly breadcrumbsNav: HTMLElement;
  private breadcrumbKeys: string[] = [];
  private readonly summary: HTMLElement;
  private readonly loadingBadge: HTMLElement;
  private readonly currentStrip: HTMLElement;
  private readonly currentLabel: HTMLElement;
  private readonly copyCurrentBtn: HTMLButtonElement;
  private readonly excludeCurrentExplorerBtn: HTMLButtonElement;
  private readonly excludeCurrentSearchableBtn: HTMLButtonElement;
  private readonly readOnlyNote: HTMLElement;
  private readonly errorBox: HTMLElement;
  private readonly errorText: HTMLElement;
  private readonly tbody: HTMLTableSectionElement;
  private readonly emptyTr: HTMLTableRowElement;
  private readonly emptyTd: HTMLTableCellElement;
  private readonly rowsByKey = new Map<string, RowElements>();
  private renderedKeys: string[] = [];
  private currentVm: DiskUsageViewModel | undefined;
  private currentCopiedUntilMs = 0;

  constructor(private readonly execute: (action: DashboardAction) => void) {
    this.element = el('section', 'section disk-usage');
    this.element.hidden = true;

    const navBar = el('div', 'explorer-nav');
    const backBtn = actionButton(
      'button secondary small',
      '\u2190 Back to Server',
      () => this.execute({ action: 'closeDiskUsage' }),
      'Return to the server overview',
    );
    this.upBtn = actionButton(
      'button secondary small',
      '\u2191 Up',
      () => {
        if (this.currentVm && this.currentVm.parentRelativePath !== undefined) {
          this.execute({
            action: 'openDiskUsage',
            projectPath: this.currentVm.projectPath,
            relativePath: this.currentVm.parentRelativePath,
          });
        }
      },
      'Go to parent directory',
    );
    this.breadcrumbsNav = el('nav', 'breadcrumbs');
    this.breadcrumbsNav.setAttribute('aria-label', 'Directory path');
    this.loadingBadge = el('span', 'explorer-loading', 'Loading\u2026');
    this.loadingBadge.hidden = true;
    navBar.append(backBtn, this.upBtn, this.breadcrumbsNav, this.loadingBadge);

    const subheader = el('div', 'explorer-subheader');
    this.summary = el('div', 'explorer-summary');
    const legend = el('div', 'chart-legend explorer-legend');
    const searchableItem = el('span', 'legend-item');
    searchableItem.append(el('span', 'swatch series-0'), el('span', undefined, 'Searchable text'));
    const otherItem = el('span', 'legend-item');
    otherItem.append(el('span', 'swatch swatch-other'), el('span', undefined, 'Not searchable / excluded'));
    legend.append(searchableItem, otherItem);
    subheader.append(this.summary, legend);

    this.currentStrip = el('div', 'explorer-current-actions');
    this.currentStrip.hidden = true;
    this.currentLabel = el('span', 'explorer-current-label');
    this.copyCurrentBtn = actionButton('button secondary small', 'Copy Pattern', () => {
      const pattern = this.currentVm?.currentDirectoryPattern;
      if (pattern) {
        this.execute({ action: 'copyPattern', pattern });
        this.flashCurrentCopied();
      }
    });
    this.excludeCurrentExplorerBtn = actionButton('button secondary small', '+ SourceExplorer.ignore', () => {
      const vm = this.currentVm;
      if (vm?.currentDirectoryPattern) {
        this.execute({
          action: 'addIgnorePattern',
          projectPath: vm.projectPath,
          section: 'sourceExplorerIgnore',
          pattern: vm.currentDirectoryPattern,
        });
      }
    });
    this.excludeCurrentSearchableBtn = actionButton('button secondary small', '+ SearchableFiles.ignore', () => {
      const vm = this.currentVm;
      if (vm?.currentDirectoryPattern) {
        this.execute({
          action: 'addIgnorePattern',
          projectPath: vm.projectPath,
          section: 'searchableFilesIgnore',
          pattern: vm.currentDirectoryPattern,
        });
      }
    });
    this.currentStrip.append(
      this.currentLabel,
      this.copyCurrentBtn,
      this.excludeCurrentExplorerBtn,
      this.excludeCurrentSearchableBtn,
    );

    this.readOnlyNote = el('div', 'restart-needed explorer-note');
    this.readOnlyNote.hidden = true;

    this.errorBox = el('div', 'error');
    this.errorBox.setAttribute('role', 'alert');
    this.errorText = el('span');
    this.errorBox.append(this.errorText);
    this.errorBox.hidden = true;

    const tableWrap = el('div', 'explorer-table-wrap');
    const tbl = el('table', 'table disk-usage-table');
    const thead = el('thead');
    const headTr = el('tr');
    headTr.append(
      el('th', undefined, 'Name'),
      el('th', undefined, 'Size on Disk (searchable / total)'),
      el('th', 'num', 'Files'),
      el('th', undefined, 'Ignore Rules'),
    );
    thead.append(headTr);
    this.tbody = el('tbody');
    tbl.append(thead, this.tbody);
    tableWrap.append(tbl);

    this.emptyTr = el('tr', 'empty');
    this.emptyTd = el('td', undefined, 'Empty directory');
    this.emptyTd.colSpan = 4;
    this.emptyTr.append(this.emptyTd);

    this.element.append(navBar, subheader, this.currentStrip, this.readOnlyNote, this.errorBox, tableWrap);
  }

  render(vm: DiskUsageViewModel | undefined) {
    this.currentVm = vm;
    setHidden(this.element, vm === undefined);
    if (!vm) {
      return;
    }

    setHidden(this.upBtn, vm.parentRelativePath === undefined);
    setDisabled(this.upBtn, vm.parentRelativePath === undefined);
    setHidden(this.loadingBadge, !vm.loading);
    this.renderBreadcrumbs(vm.projectPath, vm.breadcrumbs);

    setText(this.summary, vm.summary);
    setHidden(this.readOnlyNote, vm.readOnlyReason === undefined);
    setText(this.readOnlyNote, vm.readOnlyReason ?? '');

    setHidden(this.errorBox, vm.error === undefined);
    setText(this.errorText, vm.error ?? '');

    const hasCurrentPattern = vm.currentDirectoryPattern !== undefined;
    setHidden(this.currentStrip, !hasCurrentPattern);
    if (hasCurrentPattern) {
      const p = vm.currentDirectoryPattern!;
      setText(this.currentLabel, `Current directory (${p}):`);
      setText(this.copyCurrentBtn, Date.now() < this.currentCopiedUntilMs ? 'Copied' : 'Copy Pattern');
      setDisabled(this.copyCurrentBtn, !vm.canCopyCurrentPattern);
      setTitle(this.copyCurrentBtn, `Copy "${p}" to the clipboard`);
      setDisabled(this.excludeCurrentExplorerBtn, !vm.canExcludeCurrentExplorer);
      setTitle(
        this.excludeCurrentExplorerBtn,
        vm.canExcludeCurrentExplorer
          ? `Add "${p}" to [SourceExplorer.ignore] (excludes this directory from tree and all searches)`
          : (vm.readOnlyReason ?? 'Cannot modify vs-chromium-project.txt'),
      );
      setDisabled(this.excludeCurrentSearchableBtn, !vm.canExcludeCurrentSearchable);
      setTitle(
        this.excludeCurrentSearchableBtn,
        vm.canExcludeCurrentSearchable
          ? `Add "${p}" to [SearchableFiles.ignore] (keeps file names, excludes contents from code search)`
          : (vm.readOnlyReason ?? 'No searchable text bytes in this directory'),
      );
    }

    this.renderRows(vm);
  }

  private renderBreadcrumbs(projectPath: string, crumbs: DiskUsageBreadcrumb[]) {
    const keys = crumbs.map(c => `${c.key}:${c.current ? '1' : '0'}`);
    const same = keys.length === this.breadcrumbKeys.length && keys.every((k, i) => k === this.breadcrumbKeys[i]);
    if (same) {
      return;
    }
    this.breadcrumbKeys = keys;
    const nodes: HTMLElement[] = [];
    crumbs.forEach((crumb, index) => {
      if (index > 0) {
        nodes.push(el('span', 'breadcrumb-sep', '/'));
      }
      if (crumb.current) {
        const span = el('span', 'breadcrumb-current', crumb.label);
        span.title = crumb.relativePath ? `${projectPath}/${crumb.relativePath}` : projectPath;
        nodes.push(span);
      } else {
        const btn = actionButton(
          'link breadcrumb-link',
          crumb.label,
          () => this.execute({ action: 'openDiskUsage', projectPath, relativePath: crumb.relativePath }),
          crumb.relativePath ? `Open ${crumb.relativePath}` : `Open project root ${projectPath}`,
        );
        nodes.push(btn);
      }
    });
    this.breadcrumbsNav.replaceChildren(...nodes);
  }

  private renderRows(vm: DiskUsageViewModel) {
    if (vm.rows.length === 0) {
      setText(this.emptyTd, vm.loading ? 'Loading directory sizes\u2026' : 'No files or subdirectories');
      if (this.renderedKeys.length > 0 || this.tbody.firstChild !== this.emptyTr) {
        this.tbody.replaceChildren(this.emptyTr);
        this.renderedKeys = [];
      }
      return;
    }

    const nextKeys = vm.rows.map(r => r.key);
    const activeSet = new Set(nextKeys);
    for (const key of [...this.rowsByKey.keys()]) {
      if (!activeSet.has(key)) {
        this.rowsByKey.delete(key);
      }
    }

    const orderChanged =
      nextKeys.length !== this.renderedKeys.length ||
      nextKeys.some((k, i) => k !== this.renderedKeys[i]);

    const rowElements = vm.rows.map(item => {
      let existing = this.rowsByKey.get(item.key);
      if (!existing) {
        existing = this.createRow(item);
        this.rowsByKey.set(item.key, existing);
      }
      this.updateRow(existing, item);
      return existing;
    });

    if (orderChanged) {
      this.tbody.replaceChildren(...rowElements.map(r => r.tr));
      this.renderedKeys = nextKeys;
    }
  }

  private createRow(initial: DiskUsageRowView): RowElements {
    const tr = el('tr');

    // Name column
    const nameTd = el('td', 'entry-td');
    const nameWrap = el('div', 'entry-cell');
    const icon = el('span', 'entry-icon');
    icon.setAttribute('aria-hidden', 'true');
    const link = actionButton('link entry-link', '', () => {
      const projectPath = this.currentVm?.projectPath;
      if (projectPath && elements.current.drillable) {
        this.execute({ action: 'openDiskUsage', projectPath, relativePath: elements.current.relativePath });
      }
    });
    const name = el('span', 'entry-name');
    const excludedBadge = el('span', 'excluded-badge', 'excluded');
    excludedBadge.title = 'Excluded from the index by [SourceExplorer.ignore]';
    nameWrap.append(icon, link, name, excludedBadge);
    nameTd.append(nameWrap);

    // Size column
    const sizeTd = el('td', 'size-td');
    const sizeCell = el('div', 'size-cell');
    const bar = el('span', 'size-bar');
    const barSearchable = el('span', 'size-bar-searchable');
    const barOther = el('span', 'size-bar-other');
    bar.append(barSearchable, barOther);
    const sizeText = el('span', 'size-text');
    const measureBtn = actionButton('button secondary small measure-btn', 'Measure', () => {
      const projectPath = this.currentVm?.projectPath;
      const row = elements.current;
      if (!projectPath) {
        return;
      }
      if (row.measureAction === 'cancel') {
        this.execute({ action: 'cancelMeasure', projectPath, relativePath: row.relativePath });
      } else if (row.measureAction === 'measure') {
        this.execute({ action: 'measureDirectory', projectPath, relativePath: row.relativePath });
      }
    });
    const remeasureBtn = actionButton('link remeasure-link', 'Re-measure', () => {
      const projectPath = this.currentVm?.projectPath;
      if (projectPath) {
        this.execute({
          action: 'measureDirectory',
          projectPath,
          relativePath: elements.current.relativePath,
          restart: true,
        });
      }
    }, 'Measure directory size on disk again');
    sizeCell.append(bar, sizeText, measureBtn, remeasureBtn);
    sizeTd.append(sizeCell);

    // Files column
    const filesTd = el('td', 'num');

    // Actions column
    const actionsTd = el('td', 'actions-td');
    const actionsWrap = el('div', 'row-actions');
    const copyBtn = actionButton('button secondary small', 'Copy Pattern', () => {
      const pattern = elements.current.pattern;
      if (pattern) {
        this.execute({ action: 'copyPattern', pattern });
        this.flashRowCopied(elements);
      }
    });
    const explorerBtn = actionButton('button secondary small', '+ SourceExplorer.ignore', () => {
      const projectPath = this.currentVm?.projectPath;
      const pattern = elements.current.pattern;
      if (projectPath && pattern) {
        this.execute({ action: 'addIgnorePattern', projectPath, section: 'sourceExplorerIgnore', pattern });
      }
    });
    const searchableBtn = actionButton('button secondary small', '+ SearchableFiles.ignore', () => {
      const projectPath = this.currentVm?.projectPath;
      const pattern = elements.current.pattern;
      if (projectPath && pattern) {
        this.execute({ action: 'addIgnorePattern', projectPath, section: 'searchableFilesIgnore', pattern });
      }
    });
    actionsWrap.append(copyBtn, explorerBtn, searchableBtn);
    actionsTd.append(actionsWrap);

    tr.append(nameTd, sizeTd, filesTd, actionsTd);

    const elements: RowElements = {
      tr,
      icon,
      link,
      name,
      excludedBadge,
      sizeCell,
      bar,
      barSearchable,
      barOther,
      sizeText,
      measureBtn,
      remeasureBtn,
      filesTd,
      copyBtn,
      explorerBtn,
      searchableBtn,
      current: initial,
      copiedUntilMs: 0,
    };
    return elements;
  }

  private updateRow(e: RowElements, item: DiskUsageRowView) {
    e.current = item;
    setClass(e.tr, item.excluded ? 'excluded-row' : '');
    setClass(e.icon, `entry-icon ${item.isDirectory ? 'folder' : 'file'}`);

    setHidden(e.link, !item.drillable);
    setHidden(e.name, item.drillable);
    if (item.drillable) {
      setText(e.link, item.displayName);
      setTitle(e.link, `Open ${item.relativePath}`);
    } else {
      setText(e.name, item.displayName);
      setTitle(e.name, item.relativePath);
    }
    setHidden(e.excludedBadge, !item.excluded);

    setHidden(e.bar, !item.showBar);
    if (item.showBar) {
      e.barSearchable.style.width = `${item.searchableBarPercent.toFixed(2)}%`;
      e.barOther.style.width = `${item.notSearchableBarPercent.toFixed(2)}%`;
      setClass(e.barSearchable, item.searchableBarPercent > 0 ? 'size-bar-searchable non-zero' : 'size-bar-searchable');
      setClass(e.barOther, item.notSearchableBarPercent > 0 ? 'size-bar-other non-zero' : 'size-bar-other');
    }
    setText(e.sizeText, item.sizeText);
    setClass(e.sizeText, item.excluded && item.measureState !== 'done' ? 'size-text muted' : 'size-text');
    setTitle(e.sizeCell, item.sizeTitle);

    const showMeasureBtn = item.measureAction === 'measure' || item.measureAction === 'cancel';
    setHidden(e.measureBtn, !showMeasureBtn);
    if (showMeasureBtn) {
      const isCancel = item.measureAction === 'cancel';
      setText(e.measureBtn, isCancel ? 'Cancel' : 'Measure');
      setTitle(e.measureBtn, isCancel
        ? `Cancel size measurement of ${item.relativePath}`
        : `Enumerate files on disk in ${item.relativePath} to measure its total size`);
    }
    setHidden(e.remeasureBtn, item.measureAction !== 'remeasure');

    setText(e.filesTd, item.filesText);
    setTitle(e.filesTd, item.filesTitle);

    setText(e.copyBtn, Date.now() < e.copiedUntilMs ? 'Copied' : 'Copy Pattern');
    setDisabled(e.copyBtn, !item.canCopyPattern);
    setTitle(e.copyBtn, item.copyPatternTitle);

    setDisabled(e.explorerBtn, !item.canAddExplorerIgnore);
    setTitle(e.explorerBtn, item.explorerIgnoreTitle);

    setDisabled(e.searchableBtn, !item.canAddSearchableIgnore);
    setTitle(e.searchableBtn, item.searchableIgnoreTitle);
  }

  private flashRowCopied(e: RowElements) {
    e.copiedUntilMs = Date.now() + 1200;
    setText(e.copyBtn, 'Copied');
    setTimeout(() => {
      if (Date.now() >= e.copiedUntilMs) {
        setText(e.copyBtn, 'Copy Pattern');
      }
    }, 1250);
  }

  private flashCurrentCopied() {
    this.currentCopiedUntilMs = Date.now() + 1200;
    setText(this.copyCurrentBtn, 'Copied');
    setTimeout(() => {
      if (Date.now() >= this.currentCopiedUntilMs) {
        setText(this.copyCurrentBtn, 'Copy Pattern');
      }
    }, 1250);
  }
}
