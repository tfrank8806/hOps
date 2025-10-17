(function () {
    const storageKey = 'hops.logs.v1';
    const defaultRows = 10;
    const defaultColumns = 6;

    const logListEl = document.getElementById('logList');
    const logEmptyStateEl = document.getElementById('logEmptyState');
    const logTitleEl = document.getElementById('logTitle');
    const logSubtitleEl = document.getElementById('logSubtitle');
    const spreadsheetWrapperEl = document.getElementById('spreadsheetWrapper');
    const spreadsheetTableEl = document.getElementById('spreadsheetTable');
    const spreadsheetPlaceholderEl = document.getElementById('spreadsheetPlaceholder');
    const addRowButton = document.getElementById('addRowBtn');
    const addColumnButton = document.getElementById('addColumnBtn');
    const clearLogButton = document.getElementById('clearLogBtn');
    const createLogForm = document.getElementById('createLogForm');
    const logNameInput = document.getElementById('logNameInput');

    if (!logListEl) {
        return;
    }

    let logs = loadLogs();
    let currentLogId = null;

    function loadLogs() {
        try {
            const stored = localStorage.getItem(storageKey);
            if (!stored) {
                return [];
            }
            const parsed = JSON.parse(stored);
            if (Array.isArray(parsed)) {
                return parsed.map(normalizeLog);
            }
        } catch (error) {
            console.warn('Unable to load stored logs', error);
        }
        return [];
    }

    function normalizeLog(log) {
        if (!log || typeof log !== 'object') {
            return createLog('Log');
        }

        const rows = Array.isArray(log.data) && log.data.length > 0 ? log.data.length : defaultRows;
        const firstRow = Array.isArray(log.data) && log.data.length > 0 && Array.isArray(log.data[0]) ? log.data[0] : null;
        const cols = Array.isArray(firstRow) && firstRow.length > 0 ? firstRow.length : defaultColumns;
        const data = createEmptyData(rows, cols);

        if (Array.isArray(log.data)) {
            log.data.forEach((row, rowIndex) => {
                if (!Array.isArray(row)) {
                    return;
                }
                row.forEach((value, colIndex) => {
                    if (rowIndex < data.length && colIndex < data[rowIndex].length) {
                        data[rowIndex][colIndex] = typeof value === 'string' ? value : String(value ?? '');
                    }
                });
            });
        }

        return {
            id: log.id ?? generateId(),
            name: typeof log.name === 'string' && log.name.trim().length ? log.name.trim() : 'Untitled Log',
            data
        };
    }

    function createEmptyData(rows = defaultRows, columns = defaultColumns) {
        return Array.from({ length: rows }, () => Array.from({ length: columns }, () => ''));
    }

    function createLog(name) {
        return {
            id: generateId(),
            name,
            data: createEmptyData()
        };
    }

    function generateId() {
        return `log_${Date.now()}_${Math.random().toString(16).slice(2)}`;
    }

    function persistLogs() {
        localStorage.setItem(storageKey, JSON.stringify(logs));
    }

    function renderLogList() {
        logListEl.querySelectorAll('.log-item').forEach((item) => item.remove());

        if (!logs.length) {
            logEmptyStateEl?.classList.remove('d-none');
            return;
        }

        logEmptyStateEl?.classList.add('d-none');

        logs.forEach((log) => {
            const button = document.createElement('button');
            button.type = 'button';
            button.textContent = log.name;
            button.className = 'list-group-item list-group-item-action log-item';
            if (log.id === currentLogId) {
                button.classList.add('active');
            }
            button.addEventListener('click', () => selectLog(log.id));
            logListEl.appendChild(button);
        });
    }

    function getCurrentLog() {
        return logs.find((log) => log.id === currentLogId) ?? null;
    }

    function selectLog(logId) {
        currentLogId = logId;
        renderLogList();
        const log = getCurrentLog();
        if (!log) {
            showPlaceholder();
            return;
        }
        logTitleEl.textContent = log.name;
        logSubtitleEl.textContent = 'Changes are saved automatically in this browser.';
        addRowButton.disabled = false;
        addColumnButton.disabled = false;
        clearLogButton.disabled = false;
        spreadsheetPlaceholderEl.classList.add('d-none');
        spreadsheetWrapperEl.classList.remove('d-none');
        renderSpreadsheet(log);
    }

    function showPlaceholder() {
        logTitleEl.textContent = 'Select a log';
        logSubtitleEl.textContent = 'Choose or create a log to start editing.';
        addRowButton.disabled = true;
        addColumnButton.disabled = true;
        clearLogButton.disabled = true;
        spreadsheetPlaceholderEl.classList.remove('d-none');
        spreadsheetWrapperEl.classList.add('d-none');
    }

    function renderSpreadsheet(log) {
        const data = log.data;
        const columnCount = data[0]?.length ?? defaultColumns;
        const headerRow = document.createElement('tr');
        headerRow.appendChild(document.createElement('th'));

        for (let colIndex = 0; colIndex < columnCount; colIndex++) {
            const th = document.createElement('th');
            th.scope = 'col';
            th.textContent = columnLabel(colIndex);
            headerRow.appendChild(th);
        }

        const thead = spreadsheetTableEl.querySelector('thead');
        thead.innerHTML = '';
        thead.appendChild(headerRow);

        const tbody = spreadsheetTableEl.querySelector('tbody');
        tbody.innerHTML = '';

        data.forEach((row, rowIndex) => {
            const tr = document.createElement('tr');
            const rowHeader = document.createElement('th');
            rowHeader.scope = 'row';
            rowHeader.textContent = (rowIndex + 1).toString();
            tr.appendChild(rowHeader);

            for (let colIndex = 0; colIndex < columnCount; colIndex++) {
                const td = document.createElement('td');
                td.contentEditable = 'true';
                td.dataset.row = String(rowIndex);
                td.dataset.column = String(colIndex);
                td.textContent = row[colIndex] ?? '';
                td.addEventListener('input', handleCellInput);
                tr.appendChild(td);
            }

            tbody.appendChild(tr);
        });
    }

    function columnLabel(index) {
        const alphabetLength = 26;
        let label = '';
        let current = index;
        do {
            label = String.fromCharCode(65 + (current % alphabetLength)) + label;
            current = Math.floor(current / alphabetLength) - 1;
        } while (current >= 0);
        return label;
    }

    function handleCellInput(event) {
        const target = event.target;
        if (!(target instanceof HTMLElement)) {
            return;
        }
        const rowIndex = Number(target.dataset.row ?? '-1');
        const columnIndex = Number(target.dataset.column ?? '-1');
        const log = getCurrentLog();
        if (!log || Number.isNaN(rowIndex) || Number.isNaN(columnIndex)) {
            return;
        }
        if (!Array.isArray(log.data[rowIndex])) {
            log.data[rowIndex] = [];
        }
        log.data[rowIndex][columnIndex] = target.textContent ?? '';
        persistLogs();
    }

    function addRow() {
        const log = getCurrentLog();
        if (!log) {
            return;
        }
        const columns = log.data[0]?.length ?? defaultColumns;
        log.data.push(Array.from({ length: columns }, () => ''));
        persistLogs();
        renderSpreadsheet(log);
    }

    function addColumn() {
        const log = getCurrentLog();
        if (!log) {
            return;
        }
        let columnCount = defaultColumns;
        if (log.data.length && Array.isArray(log.data[0])) {
            columnCount = log.data[0].length;
        } else if (!log.data.length) {
            log.data = createEmptyData();
            columnCount = log.data[0].length;
        }

        const newColumnIndex = columnCount;

        log.data.forEach((row) => {
            if (Array.isArray(row)) {
                row.push('');
            }
        });
        persistLogs();
        renderSpreadsheet(log);
        // Focus first cell of new column
        const rows = spreadsheetTableEl.querySelectorAll(`tbody tr`);
        if (rows.length) {
            const firstRow = rows[0];
            const cell = firstRow.querySelector(`td[data-column="${newColumnIndex - 1}"]`);
            if (cell instanceof HTMLElement) {
                cell.focus();
            }
        }
    }

    function clearCurrentLog() {
        const log = getCurrentLog();
        if (!log) {
            return;
        }
        const rows = log.data.length || defaultRows;
        const cols = log.data[0]?.length || defaultColumns;
        log.data = createEmptyData(rows, cols);
        persistLogs();
        renderSpreadsheet(log);
    }

    function handleCreateLog(event) {
        event.preventDefault();
        const formData = new FormData(createLogForm);
        const name = (formData.get('logName') || '').toString().trim();
        if (!name) {
            logNameInput?.classList.add('is-invalid');
            return;
        }
        logNameInput?.classList.remove('is-invalid');
        const newLog = createLog(name);
        logs.push(newLog);
        persistLogs();
        createLogForm.reset();
        const modalElement = document.getElementById('createLogModal');
        if (modalElement) {
            const modal = bootstrap.Modal.getOrCreateInstance(modalElement);
            modal.hide();
        }
        selectLog(newLog.id);
        renderLogList();
    }

    addRowButton?.addEventListener('click', addRow);
    addColumnButton?.addEventListener('click', addColumn);
    clearLogButton?.addEventListener('click', clearCurrentLog);

    createLogForm?.addEventListener('submit', handleCreateLog);
    logNameInput?.addEventListener('input', () => {
        logNameInput?.classList.remove('is-invalid');
    });

    renderLogList();
    if (logs.length) {
        selectLog(logs[0].id);
    } else {
        showPlaceholder();
    }
})();
