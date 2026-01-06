(function () {
    'use strict';

    const storageKey = 'hops.logs.v1';
    const defaultRows = 10;
    const defaultColumns = 6;
    const storageActiveLogKey = `${storageKey}.active`;
    const DEFAULT_USER_NAME = 'Unknown user';
    const MIN_ZOOM = 0.75;
    const MAX_ZOOM = 1.5;
    const ZOOM_STEP = 0.1;
    const MAX_HISTORY_LENGTH = 25;

    const logListEl = document.getElementById('logList');
    const logEmptyStateEl = document.getElementById('logEmptyState');
    const logTitleEl = document.getElementById('logTitle');
    const logSubtitleEl = document.getElementById('logSubtitle');
    const gridElement = document.getElementById('logsGrid');
    const placeholderEl = document.getElementById('spreadsheetPlaceholder');
    const addRowButton = document.getElementById('addRowBtn');
    const insertRowButton = document.getElementById('insertRowBtn');
    const deleteRowButton = document.getElementById('deleteRowBtn');
    const addColumnButton = document.getElementById('addColumnBtn');
    const deleteColumnButton = document.getElementById('deleteColumnBtn');
    const clearLogButton = document.getElementById('clearLogBtn');
    const createLogForm = document.getElementById('createLogForm');
    const logNameInput = document.getElementById('logNameInput');
    const importLogButton = document.getElementById('importLogBtn');
    const importLogInput = document.getElementById('importLogInput');
    const duplicateLogButton = document.getElementById('duplicateLogBtn');
    const deleteLogButton = document.getElementById('deleteLogBtn');
    const renameLogButton = document.getElementById('renameLogBtn');
    const viewAuditLogButton = document.getElementById('viewAuditLogBtn');
    const auditLogModalElement = document.getElementById('auditLogModal');
    const auditLogListElement = document.getElementById('auditLogList');
    const auditLogEmptyStateElement = document.getElementById('auditLogEmptyState');
    const toggleSidebarButton = document.getElementById('toggleSidebarBtn');
    const closeSidebarButton = document.getElementById('closeLogsSidebarBtn');
    const logsLayoutElement = document.getElementById('logsLayout');
    const logsSidebarOverlayElement = document.getElementById('logsSidebarOverlay');
    const exportExcelButton = document.getElementById('exportExcelBtn');
    const undoButton = document.getElementById('undoBtn');
    const zoomInButton = document.getElementById('zoomInBtn');
    const zoomOutButton = document.getElementById('zoomOutBtn');
    const logTabsContainer = document.getElementById('logTabs');
    const logTabsBarElement = document.getElementById('logTabsBar');
    const logTabsEmptyStateElement = document.getElementById('logTabsEmptyState');
    const logTabsScrollElement = document.getElementById('logTabsScroll');
    const addLogTabButton = document.getElementById('addTabBtn');

    let logs = loadLogs();
    let currentLogId = restoreActiveLogId();
    let gridApi = null;
    let gridColumnApi = null;
    let gridInitialized = false;
    let selectionRange = null;
    let zoomLevel = 1;
    let auditLogModalInstance = null;
    const logHistory = new Map();

    initialize();

    function initialize() {
        initializeGrid();
        renderLogList();
        renderLogTabs();
        updateLogManagementButtons();
        wireEventListeners();
        if (logs.length === 0) {
            showPlaceholder();
        } else if (currentLogId) {
            selectLog(currentLogId);
        } else {
            selectLog(logs[0].id);
        }
        handleSidebarResize();
        updateZoomButtonState();
    }

    function wireEventListeners() {
        createLogForm?.addEventListener('submit', handleCreateLog);
        logNameInput?.addEventListener('input', () => logNameInput.classList.remove('is-invalid'));
        importLogButton?.addEventListener('click', () => importLogInput?.click());
        importLogInput?.addEventListener('change', handleImportLogChange);
        addRowButton?.addEventListener('click', addRowToLog);
        insertRowButton?.addEventListener('click', insertRowAboveSelection);
        deleteRowButton?.addEventListener('click', deleteSelectedRows);
        addColumnButton?.addEventListener('click', addColumnToLog);
        deleteColumnButton?.addEventListener('click', deleteSelectedColumns);
        clearLogButton?.addEventListener('click', clearCurrentLog);
        duplicateLogButton?.addEventListener('click', duplicateCurrentLog);
        deleteLogButton?.addEventListener('click', deleteCurrentLog);
        renameLogButton?.addEventListener('click', renameCurrentLog);
        viewAuditLogButton?.addEventListener('click', openAuditLogModal);
        exportExcelButton?.addEventListener('click', exportCurrentLogToExcel);
        undoButton?.addEventListener('click', undoLastChange);
        zoomInButton?.addEventListener('click', () => changeZoom(ZOOM_STEP));
        zoomOutButton?.addEventListener('click', () => changeZoom(-ZOOM_STEP));
        addLogTabButton?.addEventListener('click', handleAddTab);
        toggleSidebarButton?.addEventListener('click', handleSidebarToggle);
        closeSidebarButton?.addEventListener('click', closeSidebar);
        logsSidebarOverlayElement?.addEventListener('click', closeSidebar);
        window.addEventListener('resize', handleSidebarResize);
    }

    function initializeGrid() {
        if (gridInitialized || !gridElement || typeof agGrid === 'undefined') {
            return;
        }
        gridInitialized = true;
        const gridOptions = {
            defaultColDef: {
                editable: true,
                resizable: true,
                sortable: false,
                filter: false,
                suppressHeaderMenuButton: true
            },
            rowSelection: 'single',
            suppressRowClickSelection: true,
            enableRangeSelection: true,
            enableFillHandle: true,
            animateRows: false,
            undoRedoCellEditing: false,
            readOnlyEdit: false,
            getRowId: (params) => String(params.data.__rowIndex),
            onGridSizeChanged: () => gridApi?.sizeColumnsToFit(),
            onCellEditingStarted: () => pushUndoState(),
            onCellEditingStopped: handleCellEditStopped,
            onRangeSelectionChanged: syncSelectionFromGrid,
            onSelectionChanged: syncSelectionFromGrid,
            onFirstDataRendered: () => {
                gridApi?.sizeColumnsToFit();
                focusFirstCell();
            }
        };
        gridApi = agGrid.createGrid(gridElement, gridOptions);
        gridColumnApi = gridApi.columnApi;
    }

    function renderLogList() {
        if (!logListEl) {
            return;
        }
        logListEl.innerHTML = '';
        if (logs.length === 0) {
            logEmptyStateEl?.classList.remove('d-none');
            return;
        }
        logEmptyStateEl?.classList.add('d-none');
        logs.forEach((log) => {
            const item = document.createElement('div');
            item.className = `list-group-item log-item ${log.id === currentLogId ? 'active' : ''}`;
            const button = document.createElement('button');
            button.type = 'button';
            button.className = 'btn btn-link p-0 text-start flex-grow-1 log-item-select';
            button.textContent = log.name;
            button.addEventListener('click', () => selectLog(log.id));
            item.appendChild(button);
            logListEl.appendChild(item);
        });
    }

    function renderLogTabs() {
        if (!logTabsContainer) {
            return;
        }
        logTabsContainer.innerHTML = '';
        if (logs.length === 0) {
            logTabsBarElement?.classList.add('log-tabs-bar--empty');
            logTabsEmptyStateElement?.classList.remove('d-none');
            return;
        }
        logTabsBarElement?.classList.remove('log-tabs-bar--empty');
        logTabsEmptyStateElement?.classList.add('d-none');
        logs.forEach((log, index) => {
            const button = document.createElement('button');
            button.type = 'button';
            button.className = `btn btn-sm log-tab ${log.id === currentLogId ? 'active' : ''}`;
            button.textContent = log.name;
            button.addEventListener('click', () => selectLog(log.id));
            button.addEventListener('keydown', (event) => handleTabKeyNavigation(event, index));
            logTabsContainer.appendChild(button);
        });
        scrollActiveTabIntoView('auto');
    }

    function handleTabKeyNavigation(event, tabIndex) {
        if (!logTabsContainer) {
            return;
        }
        if (event.key === 'ArrowRight') {
            event.preventDefault();
            const nextIndex = (tabIndex + 1) % logTabsContainer.children.length;
            logTabsContainer.children[nextIndex]?.focus();
        } else if (event.key === 'ArrowLeft') {
            event.preventDefault();
            const previousIndex = (tabIndex - 1 + logTabsContainer.children.length) % logTabsContainer.children.length;
            logTabsContainer.children[previousIndex]?.focus();
        }
    }

    function scrollActiveTabIntoView(behavior = 'auto') {
        if (!logTabsContainer || !logTabsScrollElement) {
            return;
        }
        const activeTab = logTabsContainer.querySelector('.log-tab.active');
        if (!activeTab) {
            return;
        }
        const parentRect = logTabsScrollElement.getBoundingClientRect();
        const tabRect = activeTab.getBoundingClientRect();
        if (tabRect.left < parentRect.left) {
            logTabsScrollElement.scrollTo({ left: logTabsScrollElement.scrollLeft - (parentRect.left - tabRect.left) - 12, behavior });
        } else if (tabRect.right > parentRect.right) {
            logTabsScrollElement.scrollTo({ left: logTabsScrollElement.scrollLeft + (tabRect.right - parentRect.right) + 12, behavior });
        }
    }

    function getCurrentLog() {
        if (!currentLogId) {
            return null;
        }
        return logs.find((log) => log.id === currentLogId) ?? null;
    }

    function selectLog(logId) {
        const targetLog = logs.find((log) => log.id === logId);
        if (!targetLog) {
            return;
        }
        currentLogId = targetLog.id;
        persistActiveLogId();
        logTitleEl && (logTitleEl.textContent = targetLog.name);
        logSubtitleEl && (logSubtitleEl.textContent = `${targetLog.data.length} rows, ${targetLog.data[0]?.length ?? defaultColumns} columns`);
        renderGridForLog(targetLog);
        renderLogList();
        renderLogTabs();
        updateLogManagementButtons();
        selectionRange = null;
        updateUndoButtonState();
        scrollActiveTabIntoView('smooth');
    }

    function renderGridForLog(log) {
        if (!gridApi || !gridElement) {
            return;
        }
        const columnDefs = buildColumnDefs(log);
        const rowData = buildRowData(log);
        gridApi.setColumnDefs(columnDefs);
        gridApi.setRowData(rowData);
        gridApi.sizeColumnsToFit();
        gridElement.classList.remove('d-none');
        placeholderEl?.classList.add('d-none');
        requestAnimationFrame(() => {
            focusFirstCell();
            syncSelectionFromGrid();
        });
    }

    function buildColumnDefs(log) {
        const columnCount = log.data[0]?.length ?? defaultColumns;
        const defs = [];
        for (let columnIndex = 0; columnIndex < columnCount; columnIndex++) {
            const fieldKey = getFieldKey(columnIndex);
            defs.push({
                headerName: columnLabel(columnIndex),
                colId: fieldKey,
                field: fieldKey,
                editable: true,
                flex: 1,
                minWidth: 120,
                valueFormatter: (params) => getCellDisplayValue(log, params.node.rowIndex, columnIndex),
                cellClass: (params) => getCellClass(log, params.node.rowIndex, columnIndex)
            });
        }
        return defs;
    }

    function buildRowData(log) {
        const columnCount = log.data[0]?.length ?? defaultColumns;
        return log.data.map((row, rowIndex) => {
            const record = { __rowIndex: rowIndex };
            for (let columnIndex = 0; columnIndex < columnCount; columnIndex++) {
                ensureCellExists(log, rowIndex, columnIndex);
                record[getFieldKey(columnIndex)] = row[columnIndex].value ?? '';
            }
            return record;
        });
    }

    function handleCellEditStopped(event) {
        const log = getCurrentLog();
        if (!log) {
            return;
        }
        const rowIndex = event.node?.rowIndex;
        const columnIndex = getColumnIndexFromField(event.column?.getColId());
        if (rowIndex == null || columnIndex == null) {
            return;
        }
        ensureCellExists(log, rowIndex, columnIndex);
        const rawValue = event.value == null ? '' : String(event.value);
        const cell = log.data[rowIndex][columnIndex];
        if (cell.value === rawValue) {
            refreshGridCells([{ rowIndex, columnIndex }]);
            return;
        }
        const previousValue = cell.value ?? '';
        cell.value = rawValue;
        recordAudit(log, 'Updated cell', `Updated ${formatCellLabel(rowIndex, columnIndex)} from ${formatAuditValue(previousValue)} to ${formatAuditValue(rawValue)}.`);
        persistLogs();
        renderGridForLog(log);
    }

    function refreshGridCells(cells = []) {
        if (!gridApi) {
            return;
        }
        if (!cells.length) {
            gridApi.refreshCells({ force: true });
            return;
        }
        const rowNodes = [];
        const columns = [];
        cells.forEach(({ rowIndex, columnIndex }) => {
            const node = gridApi.getDisplayedRowAtIndex(rowIndex);
            if (node) {
                rowNodes.push(node);
            }
            const fieldKey = getFieldKey(columnIndex);
            if (!columns.includes(fieldKey)) {
                columns.push(fieldKey);
            }
        });
        gridApi.refreshCells({ rowNodes, columns, force: true });
    }

    function syncSelectionFromGrid() {
        if (!gridApi) {
            return;
        }
        const ranges = gridApi.getCellRanges();
        if (!ranges || ranges.length === 0) {
            selectionRange = null;
            updateActionButtons();
            return;
        }
        const latest = ranges[ranges.length - 1];
        const startRow = Math.min(latest.startRow?.rowIndex ?? 0, latest.endRow?.rowIndex ?? 0);
        const endRow = Math.max(latest.startRow?.rowIndex ?? 0, latest.endRow?.rowIndex ?? 0);
        const columns = (latest.columns ?? []).map((col) => getColumnIndexFromField(col.getColId())).filter((index) => index != null);
        const startColumn = Math.min(...columns);
        const endColumn = Math.max(...columns);
        selectionRange = {
            startRow: Math.max(0, startRow),
            endRow: Math.max(0, endRow),
            startColumn: Math.max(0, startColumn),
            endColumn: Math.max(0, endColumn)
        };
        updateActionButtons();
    }

    function focusFirstCell() {
        if (!gridApi) {
            return;
        }
        gridApi.ensureIndexVisible(0);
        gridApi.setFocusedCell(0, getFieldKey(0));
    }

    function setSelection(targetRow, targetColumn) {
        if (!gridApi) {
            return;
        }
        const fieldKey = getFieldKey(targetColumn);
        gridApi.clearRangeSelection();
        gridApi.addCellRange({
            rowStartIndex: targetRow,
            rowEndIndex: targetRow,
            columns: [fieldKey]
        });
        gridApi.setFocusedCell(targetRow, fieldKey);
        syncSelectionFromGrid();
    }

    function getSelectionOrDefault(log) {
        const rowCount = log.data.length;
        const columnCount = log.data[0]?.length ?? defaultColumns;
        if (!selectionRange) {
            return {
                startRow: 0,
                endRow: Math.max(0, rowCount - 1),
                startColumn: 0,
                endColumn: Math.max(0, columnCount - 1)
            };
        }
        return {
            startRow: clamp(selectionRange.startRow, 0, rowCount - 1),
            endRow: clamp(selectionRange.endRow, 0, rowCount - 1),
            startColumn: clamp(selectionRange.startColumn, 0, columnCount - 1),
            endColumn: clamp(selectionRange.endColumn, 0, columnCount - 1)
        };
    }

    function addRowToLog() {
        const log = getCurrentLog();
        if (!log) {
            return;
        }
        pushUndoState();
        const newRow = Array.from({ length: log.data[0]?.length ?? defaultColumns }, () => createCell());
        log.data.push(newRow);
        recordAudit(log, 'Row added', 'Appended a new row to the log.');
        persistLogs();
        renderGridForLog(log);
        setSelection(Math.max(0, log.data.length - 1), 0);
    }

    function insertRowAboveSelection() {
        const log = getCurrentLog();
        if (!log) {
            return;
        }
        const range = getSelectionOrDefault(log);
        pushUndoState();
        const insertIndex = clamp(range.startRow, 0, log.data.length);
        const template = log.data[0]?.length ?? defaultColumns;
        const newRow = Array.from({ length: template }, () => createCell());
        log.data.splice(insertIndex, 0, newRow);
        recordAudit(log, 'Row inserted', `Inserted a new row above row ${insertIndex + 1}.`);
        persistLogs();
        renderGridForLog(log);
        setSelection(insertIndex, 0);
    }

    function deleteSelectedRows() {
        const log = getCurrentLog();
        if (!log || log.data.length <= 1) {
            return;
        }
        const range = getSelectionOrDefault(log);
        const rowCount = range.endRow - range.startRow + 1;
        if (rowCount >= log.data.length) {
            return;
        }
        pushUndoState();
        log.data.splice(range.startRow, rowCount);
        recordAudit(log, 'Rows deleted', `Deleted ${rowCount} row(s).`);
        persistLogs();
        renderGridForLog(log);
        setSelection(Math.max(0, range.startRow - 1), 0);
    }

    function addColumnToLog() {
        const log = getCurrentLog();
        if (!log) {
            return;
        }
        pushUndoState();
        log.data.forEach((row) => row.push(createCell()));
        recordAudit(log, 'Column added', 'Appended a new column to the log.');
        persistLogs();
        renderGridForLog(log);
    }

    function deleteSelectedColumns() {
        const log = getCurrentLog();
        if (!log) {
            return;
        }
        const range = getSelectionOrDefault(log);
        const columnCount = range.endColumn - range.startColumn + 1;
        const totalColumns = log.data[0]?.length ?? defaultColumns;
        if (columnCount >= totalColumns) {
            return;
        }
        pushUndoState();
        log.data.forEach((row) => row.splice(range.startColumn, columnCount));
        recordAudit(log, 'Columns deleted', `Deleted ${columnCount} column(s).`);
        persistLogs();
        renderGridForLog(log);
    }

    function clearCurrentLog() {
        const log = getCurrentLog();
        if (!log) {
            return;
        }
        if (!window.confirm('Clear all cells in this log? This cannot be undone.')) {
            return;
        }
        pushUndoState();
        log.data = createEmptyData();
        recordAudit(log, 'Cleared log', 'All cells were cleared.');
        persistLogs();
        renderGridForLog(log);
        setSelection(0, 0);
    }

    function duplicateCurrentLog() {
        const log = getCurrentLog();
        if (!log) {
            return;
        }
        const clone = {
            id: generateId(),
            name: generateDuplicateLogName(log.name),
            data: cloneLogData(log.data),
            auditTrail: (log.auditTrail ?? []).slice()
        };
        logs.push(clone);
        recordAudit(clone, 'Log created', 'Duplicated from an existing log.');
        persistLogs();
        renderLogList();
        renderLogTabs();
        selectLog(clone.id);
    }

    function deleteCurrentLog() {
        const log = getCurrentLog();
        if (!log) {
            return;
        }
        if (!window.confirm(`Delete "${log.name}" permanently?`)) {
            return;
        }
        logs = logs.filter((item) => item.id !== log.id);
        persistLogs();
        renderLogList();
        renderLogTabs();
        if (logs.length) {
            selectLog(logs[0].id);
        } else {
            currentLogId = null;
            persistActiveLogId();
            showPlaceholder();
            updateLogManagementButtons();
        }
    }

    function renameCurrentLog() {
        const log = getCurrentLog();
        if (!log) {
            return;
        }
        const newName = window.prompt('Rename log', log.name);
        if (!newName) {
            return;
        }
        const trimmed = newName.trim();
        if (!trimmed || trimmed === log.name) {
            return;
        }
        log.name = trimmed;
        recordAudit(log, 'Renamed log', `Renamed to ${trimmed}.`);
        persistLogs();
        renderLogList();
        renderLogTabs();
        logTitleEl && (logTitleEl.textContent = log.name);
    }

    function updateActionButtons() {
        const hasSelection = !!selectionRange;
        deleteRowButton && (deleteRowButton.disabled = !hasSelection);
        insertRowButton && (insertRowButton.disabled = !hasSelection);
        deleteColumnButton && (deleteColumnButton.disabled = !hasSelection);
    }

    function updateLogManagementButtons() {
        const hasLog = logs.length > 0;
        [
            addRowButton,
            insertRowButton,
            deleteRowButton,
            addColumnButton,
            deleteColumnButton,
            clearLogButton,
            duplicateLogButton,
            deleteLogButton,
            renameLogButton,
            viewAuditLogButton,
            exportExcelButton,
            undoButton,
            zoomInButton,
            zoomOutButton
        ].forEach((button) => {
            if (button) {
                button.disabled = !hasLog;
            }
        });
    }

    function pushUndoState() {
        const log = getCurrentLog();
        if (!log) {
            return;
        }
        const history = logHistory.get(log.id) ?? [];
        history.push({
            data: cloneLogData(log.data)
        });
        if (history.length > MAX_HISTORY_LENGTH) {
            history.shift();
        }
        logHistory.set(log.id, history);
        updateUndoButtonState();
    }

    function undoLastChange() {
        const log = getCurrentLog();
        if (!log) {
            return;
        }
        const history = logHistory.get(log.id);
        if (!history || !history.length) {
            return;
        }
        const snapshot = history.pop();
        log.data = cloneLogData(snapshot.data);
        recordAudit(log, 'Undo', 'Reverted the last change.');
        persistLogs();
        renderGridForLog(log);
        updateUndoButtonState();
    }

    function updateUndoButtonState() {
        const log = getCurrentLog();
        if (!log || !undoButton) {
            undoButton && (undoButton.disabled = true);
            return;
        }
        const history = logHistory.get(log.id);
        undoButton.disabled = !(history && history.length);
    }

    function changeZoom(delta) {
        setZoom(zoomLevel + delta);
    }

    function setZoom(level) {
        const next = clamp(Math.round(level * 100) / 100, MIN_ZOOM, MAX_ZOOM);
        if (Math.abs(next - zoomLevel) < 0.01) {
            return;
        }
        zoomLevel = next;
        if (gridElement) {
            gridElement.style.setProperty('--logs-grid-font-size', `${zoomLevel}rem`);
        }
        updateZoomButtonState();
    }

    function updateZoomButtonState() {
        if (zoomInButton) {
            zoomInButton.disabled = zoomLevel >= MAX_ZOOM - 0.01 || !logs.length;
        }
        if (zoomOutButton) {
            zoomOutButton.disabled = zoomLevel <= MIN_ZOOM + 0.01 || !logs.length;
        }
    }

    function handleCreateLog(event) {
        event.preventDefault();
        if (!logNameInput) {
            return;
        }
        const name = logNameInput.value.trim();
        if (!name) {
            logNameInput.classList.add('is-invalid');
            return;
        }
        const log = createLog(name);
        logs.push(log);
        recordAudit(log, 'Log created', 'Created a new custom log.');
        persistLogs();
        renderLogList();
        renderLogTabs();
        selectLog(log.id);
        logNameInput.value = '';
    }

    function handleImportLogChange(event) {
        const input = event.target;
        if (!(input instanceof HTMLInputElement) || !input.files?.length) {
            return;
        }
        const file = input.files[0];
        const reader = new FileReader();
        reader.onload = (loadEvent) => {
            const data = new Uint8Array(loadEvent.target?.result);
            const workbook = XLSX.read(data, { type: 'array' });
            const sheetName = workbook.SheetNames[0];
            const worksheet = workbook.Sheets[sheetName];
            if (!worksheet) {
                return;
            }
            const rows = XLSX.utils.sheet_to_json(worksheet, { header: 1 });
            const log = createLogFromData(generateDefaultLogName(), rows);
            logs.push(log);
            recordAudit(log, 'Log imported', `Imported from ${file.name}.`);
            persistLogs();
            renderLogList();
            renderLogTabs();
            selectLog(log.id);
        };
        reader.readAsArrayBuffer(file);
        input.value = '';
    }

    function exportCurrentLogToExcel() {
        const log = getCurrentLog();
        if (!log) {
            return;
        }
        const sheetData = log.data.map((row) => row.map((cell) => cell.value ?? ''));
        const worksheet = XLSX.utils.aoa_to_sheet(sheetData);
        const workbook = XLSX.utils.book_new();
        XLSX.utils.book_append_sheet(workbook, worksheet, createSheetName(log.name));
        const workbookArray = XLSX.write(workbook, { bookType: 'xlsx', type: 'array' });
        const blob = new Blob([workbookArray], { type: 'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet' });
        const url = URL.createObjectURL(blob);
        const link = document.createElement('a');
        link.href = url;
        link.download = createFileName(log.name);
        link.click();
        URL.revokeObjectURL(url);
    }

    function openAuditLogModal() {
        const log = getCurrentLog();
        if (!log || !auditLogModalElement) {
            return;
        }
        if (!auditLogModalInstance && window.bootstrap) {
            auditLogModalInstance = new window.bootstrap.Modal(auditLogModalElement);
        }
        renderAuditTrail(log);
        auditLogModalInstance?.show();
    }

    function renderAuditTrail(log) {
        if (!auditLogListElement || !auditLogEmptyStateElement) {
            return;
        }
        auditLogListElement.innerHTML = '';
        const trail = ensureAuditTrail(log);
        if (trail.length === 0) {
            auditLogEmptyStateElement.classList.remove('d-none');
            return;
        }
        auditLogEmptyStateElement.classList.add('d-none');
        trail.slice().reverse().forEach((entry) => {
            const item = document.createElement('div');
            item.className = 'list-group-item';
            item.innerHTML = `<div class="fw-semibold">${entry.user}</div><div class="text-muted small">${formatAuditTimestamp(entry.timestamp)}</div><div>${entry.action}${entry.details ? ` — ${entry.details}` : ''}</div>`;
            auditLogListElement.appendChild(item);
        });
    }

    function createLog(name) {
        return {
            id: generateId(),
            name: name.trim(),
            data: createEmptyData(),
            auditTrail: []
        };
    }

    function createLogFromData(name, rows) {
        const normalizedRows = Array.isArray(rows) && rows.length ? rows : [];
        const columnCount = normalizedRows.reduce((max, row) => Math.max(max, Array.isArray(row) ? row.length : 0), defaultColumns);
        const data = Array.from({ length: Math.max(normalizedRows.length, defaultRows) }, (_, rowIndex) => {
            return Array.from({ length: columnCount }, (_, columnIndex) => {
                const row = normalizedRows[rowIndex];
                const value = Array.isArray(row) ? row[columnIndex] ?? '' : '';
                return createCell(String(value ?? ''));
            });
        });
        return {
            id: generateId(),
            name,
            data,
            auditTrail: []
        };
    }

    function createEmptyData(rows = defaultRows, columns = defaultColumns) {
        return Array.from({ length: rows }, () => Array.from({ length: columns }, () => createCell()));
    }

    function createCell(value = '') {
        return { value: value ?? '' };
    }

    function cloneLogData(data) {
        return data.map((row) => row.map((cell) => ({ value: cell.value ?? '' })));
    }

    function ensureCellExists(log, row, column) {
        if (!Array.isArray(log.data[row])) {
            log.data[row] = [];
        }
        if (!log.data[row][column]) {
            log.data[row][column] = createCell();
        }
    }

    function columnLabel(index) {
        let label = '';
        let current = index;
        while (current >= 0) {
            label = String.fromCharCode((current % 26) + 65) + label;
            current = Math.floor(current / 26) - 1;
        }
        return label;
    }

    function getFieldKey(columnIndex) {
        return `col-${columnIndex}`;
    }

    function getColumnIndexFromField(colId) {
        if (!colId || typeof colId !== 'string') {
            return null;
        }
        const match = /^col-(\d+)$/.exec(colId);
        if (!match) {
            return null;
        }
        return Number(match[1]);
    }

    function formatCellLabel(row, column) {
        return `${columnLabel(column)}${row + 1}`;
    }

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

    function persistLogs() {
        try {
            localStorage.setItem(storageKey, JSON.stringify(logs));
        } catch (error) {
            console.warn('Unable to persist logs', error);
        }
    }

    function persistActiveLogId() {
        try {
            if (currentLogId) {
                localStorage.setItem(storageActiveLogKey, currentLogId);
            } else {
                localStorage.removeItem(storageActiveLogKey);
            }
        } catch (error) {
            console.warn('Unable to persist active log id', error);
        }
    }

    function restoreActiveLogId() {
        try {
            const stored = localStorage.getItem(storageActiveLogKey);
            if (!stored) {
                return null;
            }
            const trimmed = stored.trim();
            if (!trimmed) {
                return null;
            }
            return trimmed;
        } catch (error) {
            console.warn('Unable to restore active log id', error);
            return null;
        }
    }

    function normalizeLog(log) {
        if (!log || typeof log !== 'object') {
            return createLog('Log');
        }
        const rows = Array.isArray(log.data) && log.data.length ? log.data.length : defaultRows;
        const columns = Array.isArray(log.data?.[0]) && log.data[0].length ? log.data[0].length : defaultColumns;
        const data = createEmptyData(rows, columns);
        if (Array.isArray(log.data)) {
            log.data.forEach((row, rowIndex) => {
                if (!Array.isArray(row)) {
                    return;
                }
                row.forEach((cell, columnIndex) => {
                    if (rowIndex < rows && columnIndex < columns) {
                        data[rowIndex][columnIndex] = createCell(String(cell?.value ?? cell ?? ''));
                    }
                });
            });
        }
        const auditTrail = Array.isArray(log.auditTrail)
            ? log.auditTrail.map(normalizeAuditEntry).filter(Boolean)
            : [];
        return {
            id: log.id ?? generateId(),
            name: typeof log.name === 'string' && log.name.trim() ? log.name.trim() : 'Untitled Log',
            data,
            auditTrail
        };
    }

    function normalizeAuditEntry(entry) {
        if (!entry || typeof entry !== 'object') {
            return null;
        }
        const id = typeof entry.id === 'string' && entry.id.trim() ? entry.id : generateId('audit');
        const timestampValue = typeof entry.timestamp === 'string' ? entry.timestamp : '';
        const timestamp = new Date(timestampValue);
        const normalizedTimestamp = Number.isNaN(timestamp.getTime()) ? new Date().toISOString() : timestamp.toISOString();
        const userName = typeof entry.user === 'string' && entry.user.trim() ? entry.user.trim() : DEFAULT_USER_NAME;
        const action = typeof entry.action === 'string' && entry.action.trim() ? entry.action.trim() : 'Update';
        const details = typeof entry.details === 'string' ? entry.details : '';
        return { id, timestamp: normalizedTimestamp, user: userName, action, details };
    }

    function ensureAuditTrail(log) {
        if (!log.auditTrail) {
            log.auditTrail = [];
        }
        return log.auditTrail;
    }

    function recordAudit(log, action, details = '') {
        const trail = ensureAuditTrail(log);
        trail.push({
            id: generateId('audit'),
            user: getCurrentUserName(),
            timestamp: new Date().toISOString(),
            action,
            details
        });
    }

    function getCurrentUserName() {
        const user = window.hOps?.currentUser;
        if (user && typeof user === 'object' && typeof user.name === 'string' && user.name.trim()) {
            return user.name.trim();
        }
        return DEFAULT_USER_NAME;
    }

    function formatAuditTimestamp(value) {
        const date = new Date(value);
        if (Number.isNaN(date.getTime())) {
            return '';
        }
        return date.toLocaleString();
    }

    function formatAuditValue(value) {
        if (value == null || value === '') {
            return 'blank';
        }
        return `"${value}"`;
    }

    function getCellDisplayValue(log, row, column) {
        const value = evaluateCellValue(log, row, column, new Set());
        if (value == null) {
            return '';
        }
        if (typeof value === 'number') {
            return formatGeneralNumber(value);
        }
        return value;
    }

    function getCellClass(log, row, column) {
        const rawValue = log.data[row]?.[column]?.value ?? '';
        if (typeof rawValue === 'string' && rawValue.startsWith('=')) {
            const evaluated = evaluateCellValue(log, row, column, new Set());
            if (typeof evaluated === 'string' && evaluated.startsWith('#')) {
                return 'text-danger fw-semibold';
            }
        }
        return '';
    }

    function evaluateCellValue(log, row, column, visited = new Set()) {
        const key = `${row}:${column}`;
        if (visited.has(key)) {
            return '#CYCLE!';
        }
        visited.add(key);
        ensureCellExists(log, row, column);
        const cell = log.data[row][column];
        const value = cell.value ?? '';
        if (typeof value === 'string' && value.startsWith('=')) {
            try {
                const tokens = tokenizeFormula(value.slice(1));
                const parser = createFormulaParser(tokens);
                const ast = parser.parseExpression();
                return evaluateFormulaAst(ast, log, visited);
            } catch (error) {
                return normalizeFormulaError(error);
            }
        }
        visited.delete(key);
        return value;
    }

    function normalizeFormulaError(error) {
        const message = typeof error?.message === 'string' ? error.message : '';
        if (!message) {
            return '#ERROR!';
        }
        if (/divide/i.test(message)) {
            return '#DIV/0!';
        }
        if (/reference/i.test(message)) {
            return '#REF!';
        }
        return '#ERROR!';
    }

    function tokenizeFormula(expression) {
        const tokens = [];
        let index = 0;
        while (index < expression.length) {
            const char = expression[index];
            if (/\s/.test(char)) {
                index += 1;
                continue;
            }
            if (/[0-9.]/.test(char)) {
                let buffer = char;
                index += 1;
                while (index < expression.length && /[0-9.]/.test(expression[index])) {
                    buffer += expression[index];
                    index += 1;
                }
                tokens.push({ type: 'number', value: buffer });
                continue;
            }
            if (/[A-Za-z]/.test(char)) {
                let buffer = char;
                index += 1;
                while (index < expression.length && /[A-Za-z0-9]/.test(expression[index])) {
                    buffer += expression[index];
                    index += 1;
                }
                if (expression[index] === '(') {
                    tokens.push({ type: 'function', value: buffer.toUpperCase() });
                } else {
                    tokens.push({ type: 'cell', value: buffer.toUpperCase() });
                }
                continue;
            }
            if (char === '(' || char === ')' || char === ',' || char === ':' || char === '+' || char === '-' || char === '*' || char === '/' || char === '^') {
                tokens.push({ type: 'operator', value: char });
                index += 1;
                continue;
            }
            throw new Error(`Unexpected character: ${char}`);
        }
        return tokens;
    }

    function createFormulaParser(tokens) {
        let position = 0;
        function peek(offset = 0) {
            return tokens[position + offset];
        }
        function match(type, value) {
            const token = peek();
            if (token && token.type === type && (!value || token.value === value)) {
                position += 1;
                return token;
            }
            return null;
        }
        function expect(type, value) {
            const token = match(type, value);
            if (!token) {
                throw new Error(`Expected ${value ?? type}`);
            }
            return token;
        }
        function parseExpression() {
            let node = parseTerm();
            while (true) {
                const operator = match('operator', '+') || match('operator', '-');
                if (!operator) {
                    break;
                }
                node = { type: 'binary', operator: operator.value, left: node, right: parseTerm() };
            }
            return node;
        }
        function parseTerm() {
            let node = parseFactor();
            while (true) {
                const operator = match('operator', '*') || match('operator', '/') || match('operator', '^');
                if (!operator) {
                    break;
                }
                node = { type: 'binary', operator: operator.value, left: node, right: parseFactor() };
            }
            return node;
        }
        function parseFactor() {
            const unary = match('operator', '+') || match('operator', '-');
            if (unary) {
                return { type: 'unary', operator: unary.value, argument: parseFactor() };
            }
            if (match('operator', '(')) {
                const expression = parseExpression();
                expect('operator', ')');
                return expression;
            }
            const functionToken = match('function');
            if (functionToken) {
                expect('operator', '(');
                const args = [];
                if (!match('operator', ')')) {
                    do {
                        args.push(parseExpression());
                    } while (match('operator', ','));
                    expect('operator', ')');
                }
                return { type: 'function', name: functionToken.value, args };
            }
            const cellToken = match('cell');
            if (cellToken) {
                if (match('operator', ':')) {
                    const endCell = expect('cell');
                    return { type: 'range', start: cellToken.value, end: endCell.value };
                }
                return { type: 'cell', reference: cellToken.value };
            }
            const numberToken = match('number');
            if (numberToken) {
                return { type: 'number', value: Number(numberToken.value) };
            }
            throw new Error('Invalid formula');
        }
        return { parseExpression };
    }

    function evaluateFormulaAst(node, log, visited) {
        switch (node.type) {
            case 'number':
                return node.value;
            case 'cell':
                return evaluateCellReferenceValue(node.reference, log, visited);
            case 'range':
                return evaluateRangeValues(node.start, node.end, log, visited);
            case 'unary': {
                const value = evaluateFormulaAst(node.argument, log, visited);
                if (node.operator === '-') {
                    return -coerceValueToNumber(value);
                }
                return value;
            }
            case 'binary': {
                const left = evaluateFormulaAst(node.left, log, visited);
                const right = evaluateFormulaAst(node.right, log, visited);
                const leftNumber = coerceValueToNumber(left);
                const rightNumber = coerceValueToNumber(right);
                switch (node.operator) {
                    case '+':
                        return leftNumber + rightNumber;
                    case '-':
                        return leftNumber - rightNumber;
                    case '*':
                        return leftNumber * rightNumber;
                    case '/':
                        if (rightNumber === 0) {
                            return '#DIV/0!';
                        }
                        return leftNumber / rightNumber;
                    case '^':
                        return leftNumber ** rightNumber;
                    default:
                        return '#ERROR!';
                }
            }
            case 'function': {
                const args = node.args.map((arg) => evaluateFormulaAst(arg, log, visited));
                switch (node.name) {
                    case 'SUM':
                        return args.reduce((sum, value) => sum + coerceValueToNumber(value), 0);
                    case 'AVERAGE':
                        if (!args.length) {
                            return '#DIV/0!';
                        }
                        return args.reduce((sum, value) => sum + coerceValueToNumber(value), 0) / args.length;
                    case 'MIN':
                        return Math.min(...args.map((value) => coerceValueToNumber(value)));
                    case 'MAX':
                        return Math.max(...args.map((value) => coerceValueToNumber(value)));
                    default:
                        return '#NAME?';
                }
            }
            default:
                return '#ERROR!';
        }
    }

    function evaluateCellReferenceValue(reference, log, visited) {
        const coords = parseCellReference(reference);
        if (!coords) {
            return '#REF!';
        }
        const { row, column } = coords;
        if (row < 0 || column < 0 || row >= log.data.length || column >= (log.data[0]?.length ?? 0)) {
            return '#REF!';
        }
        return evaluateCellValue(log, row, column, visited);
    }

    function evaluateRangeValues(startReference, endReference, log, visited) {
        const start = parseCellReference(startReference);
        const end = parseCellReference(endReference);
        if (!start || !end) {
            return '#REF!';
        }
        const minRow = Math.min(start.row, end.row);
        const maxRow = Math.max(start.row, end.row);
        const minColumn = Math.min(start.column, end.column);
        const maxColumn = Math.max(start.column, end.column);
        const values = [];
        for (let row = minRow; row <= maxRow; row++) {
            for (let column = minColumn; column <= maxColumn; column++) {
                values.push(evaluateCellValue(log, row, column, visited));
            }
        }
        return values.map((value) => coerceValueToNumber(value));
    }

    function parseCellReference(reference) {
        const match = /^([A-Za-z]+)(\d+)$/.exec(reference);
        if (!match) {
            return null;
        }
        const letters = match[1].toUpperCase();
        const row = Number(match[2]) - 1;
        let column = 0;
        for (let i = 0; i < letters.length; i++) {
            column *= 26;
            column += letters.charCodeAt(i) - 64;
        }
        return { row, column: column - 1 };
    }

    function coerceValueToNumber(value) {
        if (Array.isArray(value)) {
            if (!value.length) {
                return 0;
            }
            return coerceValueToNumber(value[0]);
        }
        const number = Number(value);
        if (Number.isNaN(number)) {
            return 0;
        }
        return number;
    }

    function formatGeneralNumber(value) {
        if (!Number.isFinite(value)) {
            return '';
        }
        const rounded = Number(value.toPrecision(12));
        return String(rounded);
    }

    function clamp(value, min, max) {
        return Math.min(Math.max(value, min), max);
    }

    function generateId(prefix = 'log') {
        const randomPart = Math.random().toString(16).slice(2, 10);
        return `${prefix}_${Date.now()}_${randomPart}`;
    }

    function generateDuplicateLogName(baseName) {
        let counter = 2;
        let candidate = `${baseName} ${counter}`;
        const names = new Set(logs.map((log) => log.name));
        while (names.has(candidate)) {
            counter += 1;
            candidate = `${baseName} ${counter}`;
        }
        return candidate;
    }

    function generateDefaultLogName() {
        const base = 'Imported Log';
        const names = new Set(logs.map((log) => log.name));
        if (!names.has(base)) {
            return base;
        }
        let counter = 2;
        let candidate = `${base} ${counter}`;
        while (names.has(candidate)) {
            counter += 1;
            candidate = `${base} ${counter}`;
        }
        return candidate;
    }

    function createSheetName(name) {
        const sanitized = (name ?? '').toString().replace(/[\[\]\\/*:?]/g, ' ').trim();
        return sanitized || 'Sheet1';
    }

    function createFileName(name) {
        const sanitized = (name ?? '').toString().replace(/[\\/:*?"<>|]/g, '_').trim();
        const fallback = sanitized || 'log';
        return `${fallback}.xlsx`;
    }
    function handleAddTab() {
        const name = window.prompt('Name for the new log', generateDefaultLogName());
        if (!name) {
            return;
        }
        const log = createLog(name.trim());
        logs.push(log);
        persistLogs();
        renderLogList();
        renderLogTabs();
        selectLog(log.id);
    }

    function showPlaceholder() {
        placeholderEl?.classList.remove('d-none');
        gridElement?.classList.add('d-none');
    }

    function handleSidebarToggle() {
        if (!logsLayoutElement) {
            return;
        }
        const willCollapse = logsLayoutElement.classList.toggle('sidebar-collapsed');
        logsSidebarOverlayElement && (logsSidebarOverlayElement.style.display = willCollapse ? 'block' : 'none');
    }

    function closeSidebar() {
        if (logsLayoutElement && logsLayoutElement.classList.contains('sidebar-collapsed')) {
            logsLayoutElement.classList.remove('sidebar-collapsed');
        }
        logsSidebarOverlayElement && (logsSidebarOverlayElement.style.display = 'none');
    }

    function handleSidebarResize() {
        if (!logsLayoutElement) {
            return;
        }
        if (window.innerWidth >= 992) {
            logsLayoutElement.classList.remove('sidebar-collapsed');
            logsSidebarOverlayElement && (logsSidebarOverlayElement.style.display = 'none');
        }
    }
})();

