// Copyright 2026 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

// Code Search webview. Talks to the extension (src/ui/codeSearchView.ts) with the messages
// defined in src/ui/codeSearchProtocol.ts. The DOM is built with textContent only (no innerHTML)
// so file contents can never be interpreted as HTML.

(function () {
  // @ts-ignore: provided by VS Code.
  const vscode = acquireVsCodeApi();

  const queryInput = /** @type {HTMLInputElement} */ (document.getElementById('query'));
  const filePathInput = /** @type {HTMLInputElement} */ (document.getElementById('filePathPattern'));
  const statusDiv = /** @type {HTMLElement} */ (document.getElementById('status'));
  const resultsDiv = /** @type {HTMLElement} */ (document.getElementById('results'));
  const toggles = {
    matchCase: /** @type {HTMLButtonElement} */ (document.getElementById('matchCase')),
    matchWholeWord: /** @type {HTMLButtonElement} */ (document.getElementById('matchWholeWord')),
    regex: /** @type {HTMLButtonElement} */ (document.getElementById('regex')),
  };
  const autoSearchDelay = Number(document.body.dataset.autoSearchDelay) || 100;

  let searchId = 0;
  let searchTimer = undefined;
  /** @type {Map<number, HTMLElement>} lines container of each file, by file index */
  let fileLines = new Map();
  /** @type {IntersectionObserver | undefined} */
  let observer;

  // ---------------------------------------------------------------------------
  // State (persisted across webview reloads)

  const saved = vscode.getState() || {};
  queryInput.value = saved.query || '';
  filePathInput.value = saved.filePathPattern || '';
  for (const [name, button] of Object.entries(toggles)) {
    setPressed(button, !!saved[name]);
  }

  function getOptions() {
    return {
      query: queryInput.value,
      filePathPattern: filePathInput.value,
      matchCase: isPressed(toggles.matchCase),
      matchWholeWord: isPressed(toggles.matchWholeWord),
      regex: isPressed(toggles.regex),
    };
  }

  function saveState() {
    vscode.setState(getOptions());
  }

  function isPressed(button) {
    return button.getAttribute('aria-pressed') === 'true';
  }

  function setPressed(button, pressed) {
    button.setAttribute('aria-pressed', pressed ? 'true' : 'false');
  }

  // ---------------------------------------------------------------------------
  // Searching

  function scheduleSearch(delay) {
    clearTimeout(searchTimer);
    searchTimer = setTimeout(startSearch, delay);
  }

  function startSearch() {
    clearTimeout(searchTimer);
    saveState();
    const options = getOptions();
    searchId++;
    if (options.query.trim().length === 0) {
      clearResults();
      setStatus('', false);
      return;
    }
    setStatus('Searching...', true);
    vscode.postMessage({ type: 'search', searchId, options });
  }

  queryInput.addEventListener('input', () => scheduleSearch(autoSearchDelay));
  filePathInput.addEventListener('input', () => scheduleSearch(autoSearchDelay));
  for (const input of [queryInput, filePathInput]) {
    input.addEventListener('keydown', e => {
      if (e.key === 'Enter') {
        startSearch();
      } else if (e.key === 'ArrowDown') {
        e.preventDefault();
        focusRow(firstRow());
      }
    });
  }
  for (const button of Object.values(toggles)) {
    button.addEventListener('click', () => {
      setPressed(button, !isPressed(button));
      startSearch();
    });
  }

  // ---------------------------------------------------------------------------
  // Results

  function clearResults() {
    observer?.disconnect();
    observer = new IntersectionObserver(onFilesVisible, { root: resultsDiv, rootMargin: '200px' });
    fileLines = new Map();
    resultsDiv.replaceChildren();
  }

  function setStatus(text, busy) {
    statusDiv.textContent = text;
    statusDiv.classList.toggle('busy', busy);
  }

  function renderResults(message) {
    clearResults();
    setStatus(message.summary, false);
    const fragment = document.createDocumentFragment();
    for (const file of message.files) {
      const fileDiv = el('div', 'file');
      fileDiv.setAttribute('role', 'treeitem');
      fileDiv.setAttribute('aria-expanded', 'true');

      const header = el('div', 'row file-header');
      header.tabIndex = -1;
      header.dataset.fileIndex = String(file.index);
      header.title = file.fullPath;
      header.append(el('span', 'twistie'), el('span', 'file-name', basename(file.relativePath)));
      const dir = dirname(file.relativePath);
      if (dir) {
        header.append(el('span', 'file-dir', dir));
      }
      header.append(el('span', 'badge', String(file.matchCount)));
      header.addEventListener('click', () => toggleFile(fileDiv));

      const lines = el('div', 'lines');
      lines.setAttribute('role', 'group');
      for (let i = 0; i < file.matchCount; i++) {
        const row = el('div', 'row line pending', 'Loading...');
        row.tabIndex = -1;
        row.dataset.fileIndex = String(file.index);
        row.dataset.positionIndex = String(i);
        row.addEventListener('click', () => openMatch(row, true));
        row.addEventListener('dblclick', () => openMatch(row, false));
        lines.append(row);
      }
      fileLines.set(file.index, lines);
      fileDiv.append(header, lines);
      fragment.append(fileDiv);
      observer.observe(header);
    }
    resultsDiv.append(fragment);
    if (message.files.length === 0) {
      resultsDiv.append(el('div', 'empty', 'No results.'));
    }
  }

  /** Requests text extracts for files scrolled into view (once per file). */
  function onFilesVisible(entries) {
    for (const entry of entries) {
      if (!entry.isIntersecting) {
        continue;
      }
      const header = /** @type {HTMLElement} */ (entry.target);
      observer.unobserve(header);
      vscode.postMessage({ type: 'getExtracts', searchId, fileIndex: Number(header.dataset.fileIndex) });
    }
  }

  function renderExtracts(message) {
    const lines = fileLines.get(message.fileIndex);
    if (!lines) {
      return;
    }
    message.lines.forEach((line, i) => {
      const row = /** @type {HTMLElement} */ (lines.children[i]);
      if (!row || !line) {
        return;
      }
      row.classList.remove('pending');
      row.replaceChildren(
        el('span', 'line-number', `${line.lineNumber}:`),
        el('span', 'text', line.before),
        el('span', 'match', line.match),
        el('span', 'text', line.after));
      row.title = `Line ${line.lineNumber}, column ${line.columnNumber}`;
    });
  }

  function toggleFile(fileDiv) {
    const expanded = fileDiv.getAttribute('aria-expanded') !== 'false';
    fileDiv.setAttribute('aria-expanded', expanded ? 'false' : 'true');
  }

  function openMatch(row, preview) {
    focusRow(row);
    vscode.postMessage({
      type: 'open',
      searchId,
      fileIndex: Number(row.dataset.fileIndex),
      positionIndex: Number(row.dataset.positionIndex),
      preview,
    });
  }

  // ---------------------------------------------------------------------------
  // Keyboard navigation in results

  function visibleRows() {
    return Array.from(resultsDiv.querySelectorAll('.row')).filter(r => r.offsetParent !== null);
  }

  function firstRow() {
    return visibleRows()[0];
  }

  function focusRow(row) {
    if (row) {
      resultsDiv.querySelectorAll('.row.selected').forEach(r => r.classList.remove('selected'));
      row.classList.add('selected');
      row.focus();
      row.scrollIntoView({ block: 'nearest' });
    }
  }

  resultsDiv.addEventListener('keydown', e => {
    const current = /** @type {HTMLElement} */ (document.activeElement);
    if (!current || !current.classList.contains('row')) {
      return;
    }
    const rows = visibleRows();
    const index = rows.indexOf(current);
    const fileDiv = current.closest('.file');
    switch (e.key) {
      case 'ArrowDown':
        focusRow(rows[index + 1]);
        break;
      case 'ArrowUp':
        if (index === 0) {
          queryInput.focus();
        } else {
          focusRow(rows[index - 1]);
        }
        break;
      case 'ArrowLeft':
        if (current.classList.contains('file-header')) {
          fileDiv.setAttribute('aria-expanded', 'false');
        } else {
          focusRow(fileDiv.querySelector('.file-header'));
        }
        break;
      case 'ArrowRight':
        if (current.classList.contains('file-header')) {
          fileDiv.setAttribute('aria-expanded', 'true');
        }
        break;
      case 'Enter':
        if (current.classList.contains('file-header')) {
          toggleFile(fileDiv);
        } else {
          openMatch(current, false);
        }
        break;
      default:
        return;
    }
    e.preventDefault();
  });

  // ---------------------------------------------------------------------------
  // Messages from the extension

  window.addEventListener('message', event => {
    const message = event.data;
    switch (message.type) {
      case 'setQuery':
        queryInput.value = message.query;
        queryInput.focus();
        queryInput.select();
        if (message.search) {
          startSearch();
        }
        break;
      case 'status':
        setStatus(message.text, message.busy);
        break;
      case 'results':
        if (message.searchId === searchId) {
          renderResults(message);
        }
        break;
      case 'error':
        if (message.searchId === searchId) {
          clearResults();
          setStatus(message.message, false);
          statusDiv.classList.add('error');
        }
        break;
      case 'extracts':
        if (message.searchId === searchId) {
          renderExtracts(message);
        }
        break;
    }
    if (message.type !== 'error') {
      statusDiv.classList.remove('error');
    }
  });

  // ---------------------------------------------------------------------------
  // Helpers

  function el(tag, className, text) {
    const e = document.createElement(tag);
    e.className = className;
    if (text !== undefined) {
      e.textContent = text;
    }
    return e;
  }

  function basename(p) {
    const i = Math.max(p.lastIndexOf('\\'), p.lastIndexOf('/'));
    return i >= 0 ? p.substring(i + 1) : p;
  }

  function dirname(p) {
    const i = Math.max(p.lastIndexOf('\\'), p.lastIndexOf('/'));
    return i >= 0 ? p.substring(0, i) : '';
  }

  clearResults();
  vscode.postMessage({ type: 'ready' });
  queryInput.focus();
  if (queryInput.value.trim().length > 0) {
    startSearch();
  }
})();
