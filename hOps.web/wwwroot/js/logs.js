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

    const boldButton = document.getElementById('boldBtn');
    const italicButton = document.getElementById('italicBtn');
    const underlineButton = document.getElementById('underlineBtn');
    const fontSizeSelect = document.getElementById('fontSizeSelect');
    const fontFamilySelect = document.getElementById('fontFamilySelect');
    const alignLeftButton = document.getElementById('alignLeftBtn');
    const alignCenterButton = document.getElementById('alignCenterBtn');
    const alignRightButton = document.getElementById('alignRightBtn');
    const mergeCellsButton = document.getElementById('mergeCellsBtn');
    const unmergeCellsButton = document.getElementById('unmergeCellsBtn');
    const textColorInput = document.getElementById('textColorInput');
    const fillColorInput = document.getElementById('fillColorInput');
    const clearTextColorButton = document.getElementById('clearTextColorBtn');
    const clearFillColorButton = document.getElementById('clearFillColorBtn');
    const exportExcelButton = document.getElementById('exportExcelBtn');

    const HEX_COLOR_REGEX = /^#(?:[0-9a-f]{3}){1,2}$/i;

    if (!logListEl) {
        return;
    }

    let logs = loadLogs();
    let currentLogId = null;
    let selectionRange = null;
    let anchorCell = null;

    function createCell(value = '', format = {}) {
        return {
            value: typeof value === 'string' ? value : String(value ?? ''),
            format: normalizeFormat(format)
        };
    }

    function normalizeFormat(format) {
        if (!format || typeof format !== 'object') {
            return {};
        }
        const normalized = {};
        if (format.bold) {
            normalized.bold = true;
        }
        if (format.italic) {
            normalized.italic = true;
        }
        if (format.underline) {
            normalized.underline = true;
        }
        if (typeof format.fontSize === 'string' && format.fontSize.trim().length) {
            normalized.fontSize = format.fontSize.trim();
        }
        if (typeof format.fontFamily === 'string' && format.fontFamily.trim().length) {
            normalized.fontFamily = format.fontFamily.trim();
        }
        if (typeof format.align === 'string' && ['left', 'center', 'right'].includes(format.align)) {
            normalized.align = format.align;
        }
        if (isValidHexColor(format.textColor)) {
            normalized.textColor = normalizeHexColor(format.textColor);
        }
        if (isValidHexColor(format.fillColor)) {
            normalized.fillColor = normalizeHexColor(format.fillColor);
        }
        if (format.merge && typeof format.merge === 'object') {
            const rowSpan = Number(format.merge.rowSpan);
            const colSpan = Number(format.merge.colSpan);
            if (rowSpan > 0 && colSpan > 0) {
                normalized.merge = {
                    rowSpan,
                    colSpan
                };
            }
        }
        if (format.mergedInto && typeof format.mergedInto === 'object') {
            const row = Number(format.mergedInto.row);
            const column = Number(format.mergedInto.column);
            if (!Number.isNaN(row) && !Number.isNaN(column)) {
                normalized.mergedInto = { row, column };
            }
        }
        return normalized;
    }

    function normalizeCell(cell) {
        if (cell && typeof cell === 'object' && 'value' in cell) {
            return createCell(cell.value, cell.format);
        }
        if (typeof cell === 'string') {
            return createCell(cell, {});
        }
        if (cell == null) {
            return createCell('', {});
        }
        return createCell(String(cell ?? ''), {});
    }

    function isValidHexColor(value) {
        return typeof value === 'string' && HEX_COLOR_REGEX.test(value.trim());
    }

    function normalizeHexColor(value) {
        if (!isValidHexColor(value)) {
            return '';
        }
        const trimmed = value.trim();
        if (trimmed.length === 4) {
            const expanded = trimmed
                .slice(1)
                .split('')
                .map((char) => char + char)
                .join('');
            return `#${expanded.toUpperCase()}`;
        }
        return trimmed.toUpperCase();
    }

    function getDefaultColorValue(input, fallback) {
        if (!input) {
            return fallback;
        }
        const candidate = input.dataset?.defaultColor;
        return isValidHexColor(candidate) ? normalizeHexColor(candidate) : fallback;
    }

    function updateColorInputState(input, clearButton, colorValue, fallback) {
        if (input) {
            const defaultColor = getDefaultColorValue(input, fallback);
            if (isValidHexColor(colorValue)) {
                const normalized = normalizeHexColor(colorValue);
                input.value = normalized;
                input.dataset.appliedColor = normalized;
            } else {
                input.value = defaultColor;
                input.dataset.appliedColor = '';
            }
        }
        if (clearButton) {
            clearButton.disabled = !isValidHexColor(colorValue);
        }
    }

    function createEmptyData(rows = defaultRows, columns = defaultColumns) {
        return Array.from({ length: rows }, () => Array.from({ length: columns }, () => createCell()));
    }

    function createLog(name) {
        return {
            id: generateId(),
            name,
            data: createEmptyData()
        };
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
                        data[rowIndex][colIndex] = normalizeCell(value);
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
        selectionRange = null;
        anchorCell = null;
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
        if (exportExcelButton) {
            exportExcelButton.disabled = typeof XLSX === 'undefined';
        }
        spreadsheetPlaceholderEl.classList.add('d-none');
        spreadsheetWrapperEl.classList.remove('d-none');
        renderSpreadsheet(log);
        focusFirstCell();
    }

    function showPlaceholder() {
        logTitleEl.textContent = 'Select a log';
        logSubtitleEl.textContent = 'Choose or create a log to start editing.';
        addRowButton.disabled = true;
        addColumnButton.disabled = true;
        clearLogButton.disabled = true;
        if (exportExcelButton) {
            exportExcelButton.disabled = true;
        }
        spreadsheetPlaceholderEl.classList.remove('d-none');
        spreadsheetWrapperEl.classList.add('d-none');
        selectionRange = null;
        anchorCell = null;
        setToolbarEnabled(false);
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
                ensureCellExists(log, rowIndex, colIndex);
                const cellData = log.data[rowIndex][colIndex];

                if (cellData.format?.mergedInto) {
                    continue;
                }

                const td = document.createElement('td');
                td.contentEditable = 'true';
                td.dataset.row = String(rowIndex);
                td.dataset.column = String(colIndex);
                td.textContent = cellData.value ?? '';
                applyCellFormatting(td, cellData.format);
                td.addEventListener('input', handleCellInput);
                td.addEventListener('mousedown', handleCellMouseDown);
                td.addEventListener('focus', handleCellFocus);

                const merge = cellData.format?.merge;
                if (merge) {
                    td.rowSpan = merge.rowSpan;
                    td.colSpan = merge.colSpan;
                }

                tr.appendChild(td);
            }

            tbody.appendChild(tr);
        });

        restoreSelection();
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
        ensureCellExists(log, rowIndex, columnIndex);
        const cell = log.data[rowIndex][columnIndex];
        cell.value = target.textContent ?? '';
        persistLogs();
    }

    function handleCellMouseDown(event) {
        const target = event.target;
        if (!(target instanceof HTMLElement)) {
            return;
        }
        const rowIndex = Number(target.dataset.row ?? '-1');
        const columnIndex = Number(target.dataset.column ?? '-1');
        if (Number.isNaN(rowIndex) || Number.isNaN(columnIndex)) {
            return;
        }

        const display = getDisplayCellCoordinates(rowIndex, columnIndex);
        if (!display) {
            return;
        }

        if (event.shiftKey && anchorCell) {
            setSelection(anchorCell.row, anchorCell.column, display.row, display.column);
        } else {
            anchorCell = { row: display.row, column: display.column };
            setSelection(display.row, display.column, display.row, display.column);
        }
    }

    function handleCellFocus(event) {
        const target = event.target;
        if (!(target instanceof HTMLElement)) {
            return;
        }
        const rowIndex = Number(target.dataset.row ?? '-1');
        const columnIndex = Number(target.dataset.column ?? '-1');
        if (Number.isNaN(rowIndex) || Number.isNaN(columnIndex)) {
            return;
        }
        const display = getDisplayCellCoordinates(rowIndex, columnIndex);
        if (!display) {
            return;
        }
        anchorCell = { row: display.row, column: display.column };
        setSelection(display.row, display.column, display.row, display.column, { focus: false });
    }

    function addRow() {
        const log = getCurrentLog();
        if (!log) {
            return;
        }
        const columns = log.data[0]?.length ?? defaultColumns;
        log.data.push(Array.from({ length: columns }, () => createCell()));
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

        log.data.forEach((row) => {
            if (Array.isArray(row)) {
                row.push(createCell());
            }
        });
        persistLogs();
        renderSpreadsheet(log);
    }

    function clearCurrentLog() {
        const log = getCurrentLog();
        if (!log) {
            return;
        }
        const rows = log.data.length || defaultRows;
        const cols = log.data[0]?.length || defaultColumns;
        log.data = createEmptyData(rows, cols);
        selectionRange = null;
        anchorCell = null;
        persistLogs();
        renderSpreadsheet(log);
        focusFirstCell();
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

    function focusFirstCell() {
        const firstCell = spreadsheetTableEl.querySelector('tbody td');
        if (!(firstCell instanceof HTMLElement)) {
            setToolbarEnabled(!!selectionRange);
            return;
        }
        const row = Number(firstCell.dataset.row ?? '-1');
        const column = Number(firstCell.dataset.column ?? '-1');
        if (!Number.isNaN(row) && !Number.isNaN(column)) {
            anchorCell = { row, column };
            setSelection(row, column, row, column);
            firstCell.focus();
        }
    }

    function setSelection(startRow, startColumn, endRow, endColumn, options = {}) {
        const log = getCurrentLog();
        if (!log) {
            return;
        }
        const normalized = {
            startRow: Math.min(startRow, endRow),
            endRow: Math.max(startRow, endRow),
            startColumn: Math.min(startColumn, endColumn),
            endColumn: Math.max(startColumn, endColumn)
        };

        selectionRange = normalized;
        highlightSelection();
        setToolbarEnabled(true);
        updateToolbarState();

        if (options.focus !== false) {
            const focusCell = getCellElement(normalized.endRow, normalized.endColumn) ||
                getCellElement(normalized.startRow, normalized.startColumn);
            focusCell?.focus();
        }
    }

    function highlightSelection() {
        spreadsheetTableEl.querySelectorAll('tbody td.selected-cell').forEach((cell) => cell.classList.remove('selected-cell'));
        if (!selectionRange) {
            return;
        }
        const seen = new Set();
        forEachCellInSelection(selectionRange, ({ row, column }) => {
            const key = `${row}:${column}`;
            if (seen.has(key)) {
                return;
            }
            seen.add(key);
            const cellEl = getCellElement(row, column);
            if (cellEl) {
                cellEl.classList.add('selected-cell');
            }
        });
    }

    function restoreSelection() {
        if (!selectionRange) {
            setToolbarEnabled(false);
            return;
        }
        highlightSelection();
        updateToolbarState();
    }

    function setToolbarEnabled(enabled) {
        const controls = [
            boldButton,
            italicButton,
            underlineButton,
            fontSizeSelect,
            fontFamilySelect,
            alignLeftButton,
            alignCenterButton,
            alignRightButton,
            mergeCellsButton,
            unmergeCellsButton,
            textColorInput,
            fillColorInput,
            clearTextColorButton,
            clearFillColorButton
        ];
        controls.forEach((control) => {
            if (!control) {
                return;
            }
            control.disabled = !enabled;
        });
    }

    function updateToolbarState() {
        if (!selectionRange) {
            setToolbarEnabled(false);
            return;
        }
        const log = getCurrentLog();
        if (!log) {
            setToolbarEnabled(false);
            return;
        }

        const boldState = getUniformFormatValue('bold');
        const italicState = getUniformFormatValue('italic');
        const underlineState = getUniformFormatValue('underline');
        const alignState = getUniformFormatValue('align');
        const fontSizeState = getUniformFormatValue('fontSize');
        const fontFamilyState = getUniformFormatValue('fontFamily');
        const textColorState = getUniformFormatValue('textColor');
        const fillColorState = getUniformFormatValue('fillColor');

        setToggleState(boldButton, !!boldState);
        setToggleState(italicButton, !!italicState);
        setToggleState(underlineButton, !!underlineState);

        if (fontSizeSelect) {
            fontSizeSelect.value = fontSizeState || '';
        }
        if (fontFamilySelect) {
            fontFamilySelect.value = fontFamilyState || '';
        }

        setToggleState(alignLeftButton, alignState === 'left');
        setToggleState(alignCenterButton, alignState === 'center');
        setToggleState(alignRightButton, alignState === 'right');

        updateColorInputState(textColorInput, clearTextColorButton, textColorState, '#000000');
        updateColorInputState(fillColorInput, clearFillColorButton, fillColorState, '#ffffff');

        const selectionInfo = getSelectionInfo();
        if (mergeCellsButton) {
            mergeCellsButton.disabled = selectionInfo.totalCells <= 1 || selectionInfo.hasExternalMerge;
        }
        if (unmergeCellsButton) {
            unmergeCellsButton.disabled = !selectionInfo.hasMergedCells;
        }
    }

    function setToggleState(button, isActive) {
        if (!button) {
            return;
        }
        button.setAttribute('aria-pressed', isActive ? 'true' : 'false');
        button.classList.toggle('active', isActive);
    }

    function getSelectionInfo() {
        if (!selectionRange) {
            return { totalCells: 0, hasMergedCells: false };
        }
        let hasMergedCells = false;
        let hasExternalMerge = false;
        for (let row = selectionRange.startRow; row <= selectionRange.endRow; row++) {
            for (let column = selectionRange.startColumn; column <= selectionRange.endColumn; column++) {
                const cell = getRawCell(row, column);
                if (!cell) {
                    continue;
                }
                if (cell.format?.merge || cell.format?.mergedInto) {
                    hasMergedCells = true;
                }
                if (cell.format?.mergedInto) {
                    const masterRef = cell.format.mergedInto;
                    if (masterRef.row < selectionRange.startRow || masterRef.row > selectionRange.endRow ||
                        masterRef.column < selectionRange.startColumn || masterRef.column > selectionRange.endColumn) {
                        hasExternalMerge = true;
                    }
                }
            }
        }
        const totalCells = (selectionRange.endRow - selectionRange.startRow + 1) *
            (selectionRange.endColumn - selectionRange.startColumn + 1);
        return { totalCells, hasMergedCells, hasExternalMerge };
    }

    function getUniformFormatValue(key) {
        if (!selectionRange) {
            return undefined;
        }
        let initialValue;
        let isFirst = true;
        let isMixed = false;
        forEachCellInSelection(selectionRange, ({ cellData }) => {
            const value = cellData.format?.[key];
            if (isFirst) {
                initialValue = value;
                isFirst = false;
                return;
            }
            if (initialValue !== value) {
                isMixed = true;
            }
        });
        if (isMixed) {
            return undefined;
        }
        return initialValue;
    }

    function applyCellFormatting(td, format = {}) {
        td.style.fontWeight = format.bold ? '700' : '';
        td.style.fontStyle = format.italic ? 'italic' : '';
        td.style.textDecoration = format.underline ? 'underline' : '';
        td.style.fontSize = format.fontSize ?? '';
        td.style.fontFamily = format.fontFamily ?? '';
        td.style.textAlign = format.align ?? '';
        td.style.color = format.textColor ?? '';
        td.style.backgroundColor = format.fillColor ?? '';
    }

    function getCellElement(row, column) {
        return spreadsheetTableEl.querySelector(`tbody td[data-row="${row}"][data-column="${column}"]`);
    }

    function getDisplayCellCoordinates(row, column) {
        const log = getCurrentLog();
        if (!log) {
            return null;
        }
        if (!log.data[row] || !log.data[row][column]) {
            return null;
        }
        const cell = log.data[row][column];
        if (cell.format?.mergedInto) {
            return getDisplayCellCoordinates(cell.format.mergedInto.row, cell.format.mergedInto.column);
        }
        return { row, column };
    }

    function getRawCell(row, column) {
        const log = getCurrentLog();
        if (!log || !Array.isArray(log.data[row])) {
            return null;
        }
        return log.data[row][column] ?? null;
    }

    function forEachCellInSelection(range, callback, options = {}) {
        const log = getCurrentLog();
        if (!log) {
            return;
        }
        const includeHidden = options.includeHidden ?? false;
        const seen = new Set();
        for (let row = range.startRow; row <= range.endRow; row++) {
            for (let column = range.startColumn; column <= range.endColumn; column++) {
                if (!Array.isArray(log.data[row]) || !log.data[row][column]) {
                    continue;
                }
                const rawCell = log.data[row][column];
                if (!includeHidden && rawCell.format?.mergedInto) {
                    const key = `${rawCell.format.mergedInto.row}:${rawCell.format.mergedInto.column}`;
                    if (seen.has(key)) {
                        continue;
                    }
                    seen.add(key);
                    const masterCell = getRawCell(rawCell.format.mergedInto.row, rawCell.format.mergedInto.column);
                    if (!masterCell) {
                        continue;
                    }
                    callback({
                        row: rawCell.format.mergedInto.row,
                        column: rawCell.format.mergedInto.column,
                        cellData: masterCell
                    });
                    continue;
                }

                const key = `${row}:${column}`;
                if (seen.has(key)) {
                    continue;
                }
                seen.add(key);
                callback({ row, column, cellData: rawCell });
            }
        }
    }

    function updateSelectedCellStyles() {
        if (!selectionRange) {
            return;
        }
        forEachCellInSelection(selectionRange, ({ row, column, cellData }) => {
            const cellEl = getCellElement(row, column);
            if (cellEl) {
                applyCellFormatting(cellEl, cellData.format);
            }
        });
    }

    function toggleFormat(key) {
        if (!selectionRange) {
            return;
        }
        const currentValue = !!getUniformFormatValue(key);
        const targetValue = !currentValue;
        forEachCellInSelection(selectionRange, ({ cellData }) => {
            cellData.format = cellData.format || {};
            if (targetValue) {
                cellData.format[key] = true;
            } else {
                delete cellData.format[key];
            }
        });
        persistLogs();
        updateSelectedCellStyles();
        updateToolbarState();
    }

    function setAlignment(alignment) {
        if (!selectionRange) {
            return;
        }
        forEachCellInSelection(selectionRange, ({ cellData }) => {
            cellData.format = cellData.format || {};
            if (alignment) {
                cellData.format.align = alignment;
            } else {
                delete cellData.format.align;
            }
        });
        persistLogs();
        updateSelectedCellStyles();
        updateToolbarState();
    }

    function setFontSize(size) {
        if (!selectionRange) {
            return;
        }
        forEachCellInSelection(selectionRange, ({ cellData }) => {
            cellData.format = cellData.format || {};
            if (size) {
                cellData.format.fontSize = size;
            } else {
                delete cellData.format.fontSize;
            }
        });
        persistLogs();
        updateSelectedCellStyles();
        updateToolbarState();
    }

    function setFontFamily(family) {
        if (!selectionRange) {
            return;
        }
        forEachCellInSelection(selectionRange, ({ cellData }) => {
            cellData.format = cellData.format || {};
            if (family) {
                cellData.format.fontFamily = family;
            } else {
                delete cellData.format.fontFamily;
            }
        });
        persistLogs();
        updateSelectedCellStyles();
        updateToolbarState();
    }

    function applyColorToSelection(key, color) {
        if (!selectionRange) {
            return;
        }
        const normalized = isValidHexColor(color) ? normalizeHexColor(color) : null;
        forEachCellInSelection(selectionRange, ({ cellData }) => {
            cellData.format = cellData.format || {};
            if (normalized) {
                cellData.format[key] = normalized;
            } else {
                delete cellData.format[key];
            }
        });
        persistLogs();
        updateSelectedCellStyles();
        updateToolbarState();
    }

    function setTextColor(color) {
        applyColorToSelection('textColor', color);
    }

    function setFillColor(color) {
        applyColorToSelection('fillColor', color);
    }

    function clearTextColor() {
        applyColorToSelection('textColor', null);
    }

    function clearFillColor() {
        applyColorToSelection('fillColor', null);
    }

    function exportCurrentLogToExcel() {
        const log = getCurrentLog();
        if (!log || typeof XLSX === 'undefined') {
            return;
        }

        const rowCount = Math.max(log.data.length, 1);
        let columnCount = 0;
        for (let row = 0; row < log.data.length; row++) {
            const length = Array.isArray(log.data[row]) ? log.data[row].length : 0;
            if (length > columnCount) {
                columnCount = length;
            }
        }
        columnCount = Math.max(columnCount, 1);

        const sheetData = Array.from({ length: rowCount }, () => Array.from({ length: columnCount }, () => ''));
        const merges = [];

        for (let row = 0; row < rowCount; row++) {
            for (let column = 0; column < columnCount; column++) {
                const cell = log.data[row]?.[column];
                if (!cell) {
                    continue;
                }
                if (cell.format?.mergedInto) {
                    continue;
                }
                sheetData[row][column] = cell.value ?? '';
                if (cell.format?.merge) {
                    const merge = cell.format.merge;
                    merges.push({
                        s: { r: row, c: column },
                        e: { r: row + merge.rowSpan - 1, c: column + merge.colSpan - 1 }
                    });
                }
            }
        }

        const worksheet = XLSX.utils.aoa_to_sheet(sheetData);
        if (merges.length) {
            worksheet['!merges'] = merges;
        }

        const workbook = XLSX.utils.book_new();
        XLSX.utils.book_append_sheet(workbook, worksheet, createSheetName(log.name));
        const workbookArray = XLSX.write(workbook, { bookType: 'xlsx', type: 'array' });
        const blob = new Blob([workbookArray], { type: 'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet' });
        const url = URL.createObjectURL(blob);
        const link = document.createElement('a');
        link.href = url;
        link.download = `${createFileName(log.name)}.xlsx`;
        document.body.appendChild(link);
        link.click();
        document.body.removeChild(link);
        setTimeout(() => URL.revokeObjectURL(url), 0);
    }

    function createSheetName(name) {
        const sanitized = (name ?? '').toString().replace(/[\[\]\\/*:?]/g, ' ').trim();
        if (!sanitized) {
            return 'Log';
        }
        return sanitized.slice(0, 31) || 'Log';
    }

    function createFileName(name) {
        const sanitized = (name ?? '').toString().replace(/[\\/:*?"<>|]/g, '_').trim();
        const fallback = sanitized || 'log';
        return fallback.slice(0, 64);
    }

    function mergeSelectedCells() {
        if (!selectionRange) {
            return;
        }
        const log = getCurrentLog();
        if (!log) {
            return;
        }
        const rows = selectionRange.endRow - selectionRange.startRow + 1;
        const columns = selectionRange.endColumn - selectionRange.startColumn + 1;
        if (rows === 1 && columns === 1) {
            return;
        }

        const masterRow = selectionRange.startRow;
        const masterColumn = selectionRange.startColumn;

        for (let row = selectionRange.startRow; row <= selectionRange.endRow; row++) {
            for (let column = selectionRange.startColumn; column <= selectionRange.endColumn; column++) {
                const cell = getRawCell(row, column);
                if (!cell?.format?.mergedInto) {
                    continue;
                }
                const masterRef = cell.format.mergedInto;
                if (masterRef.row < selectionRange.startRow || masterRef.row > selectionRange.endRow ||
                    masterRef.column < selectionRange.startColumn || masterRef.column > selectionRange.endColumn) {
                    return;
                }
            }
        }

        // Unmerge any existing merges within the selection
        const affectedMasters = [];
        forEachCellInSelection(selectionRange, ({ row, column, cellData }) => {
            if (cellData.format?.merge) {
                affectedMasters.push({ row, column });
            }
        }, { includeHidden: true });
        affectedMasters.forEach(({ row, column }) => unmergeCell(row, column));

        const masterCell = getRawCell(masterRow, masterColumn);
        if (!masterCell) {
            return;
        }

        const values = [];
        for (let row = selectionRange.startRow; row <= selectionRange.endRow; row++) {
            for (let column = selectionRange.startColumn; column <= selectionRange.endColumn; column++) {
                const cell = getRawCell(row, column);
                if (!cell) {
                    continue;
                }
                if (row === masterRow && column === masterColumn) {
                    values.push(cell.value);
                    continue;
                }
                values.push(cell.value);
                cell.value = '';
                cell.format = cell.format || {};
                delete cell.format.merge;
                cell.format.mergedInto = { row: masterRow, column: masterColumn };
            }
        }

        masterCell.value = values.join(' ').trim();
        masterCell.format = masterCell.format || {};
        masterCell.format.merge = {
            rowSpan: rows,
            colSpan: columns
        };
        delete masterCell.format.mergedInto;

        persistLogs();
        renderSpreadsheet(log);
        anchorCell = { row: masterRow, column: masterColumn };
        setSelection(masterRow, masterColumn, masterRow, masterColumn);
    }

    function unmergeSelectedCells() {
        if (!selectionRange) {
            return;
        }
        const log = getCurrentLog();
        if (!log) {
            return;
        }
        const masters = [];
        forEachCellInSelection(selectionRange, ({ row, column, cellData }) => {
            if (cellData.format?.merge) {
                masters.push({ row, column });
            }
        });
        if (!masters.length) {
            const single = getRawCell(selectionRange.startRow, selectionRange.startColumn);
            if (single?.format?.mergedInto) {
                masters.push({ row: single.format.mergedInto.row, column: single.format.mergedInto.column });
            }
        }
        if (!masters.length) {
            return;
        }
        masters.forEach(({ row, column }) => unmergeCell(row, column));
        persistLogs();
        renderSpreadsheet(log);
        const master = masters[0];
        anchorCell = { row: master.row, column: master.column };
        setSelection(master.row, master.column, master.row, master.column);
    }

    function unmergeCell(row, column) {
        const log = getCurrentLog();
        if (!log) {
            return;
        }
        const cell = getRawCell(row, column);
        if (!cell || !cell.format?.merge) {
            return;
        }
        const merge = cell.format.merge;
        delete cell.format.merge;
        for (let r = row; r < row + merge.rowSpan; r++) {
            for (let c = column; c < column + merge.colSpan; c++) {
                if (r === row && c === column) {
                    continue;
                }
                ensureCellExists(log, r, c);
                const target = log.data[r][c];
                if (!target.format) {
                    target.format = {};
                }
                delete target.format.mergedInto;
            }
        }
    }

    addRowButton?.addEventListener('click', addRow);
    addColumnButton?.addEventListener('click', addColumn);
    clearLogButton?.addEventListener('click', clearCurrentLog);

    boldButton?.addEventListener('click', () => toggleFormat('bold'));
    italicButton?.addEventListener('click', () => toggleFormat('italic'));
    underlineButton?.addEventListener('click', () => toggleFormat('underline'));

    alignLeftButton?.addEventListener('click', () => setAlignment('left'));
    alignCenterButton?.addEventListener('click', () => setAlignment('center'));
    alignRightButton?.addEventListener('click', () => setAlignment('right'));

    fontSizeSelect?.addEventListener('change', (event) => {
        const target = event.target;
        if (target instanceof HTMLSelectElement) {
            setFontSize(target.value);
        }
    });
    fontFamilySelect?.addEventListener('change', (event) => {
        const target = event.target;
        if (target instanceof HTMLSelectElement) {
            setFontFamily(target.value);
        }
    });

    textColorInput?.addEventListener('input', (event) => {
        const target = event.target;
        if (target instanceof HTMLInputElement) {
            setTextColor(target.value);
        }
    });

    fillColorInput?.addEventListener('input', (event) => {
        const target = event.target;
        if (target instanceof HTMLInputElement) {
            setFillColor(target.value);
        }
    });

    clearTextColorButton?.addEventListener('click', clearTextColor);
    clearFillColorButton?.addEventListener('click', clearFillColor);

    mergeCellsButton?.addEventListener('click', mergeSelectedCells);
    unmergeCellsButton?.addEventListener('click', unmergeSelectedCells);

    exportExcelButton?.addEventListener('click', exportCurrentLogToExcel);

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
