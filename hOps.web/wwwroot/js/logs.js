(function () {
    const storageKey = 'hops.logs.v1';
    const defaultRows = 10;
    const defaultColumns = 6;

    const logListEl = document.getElementById('logList');
    const logEmptyStateEl = document.getElementById('logEmptyState');
    const logTitleEl = document.getElementById('logTitle');
    const logSubtitleEl = document.getElementById('logSubtitle');
    const spreadsheetContainerEl = document.getElementById('spreadsheetContainer');
    const spreadsheetWrapperEl = document.getElementById('spreadsheetWrapper');
    const spreadsheetTableEl = document.getElementById('spreadsheetTable');
    const spreadsheetPlaceholderEl = document.getElementById('spreadsheetPlaceholder');
    const addRowButton = document.getElementById('addRowBtn');
    const addColumnButton = document.getElementById('addColumnBtn');
    const clearLogButton = document.getElementById('clearLogBtn');
    const createLogForm = document.getElementById('createLogForm');
    const logNameInput = document.getElementById('logNameInput');
    const importLogButton = document.getElementById('importLogBtn');
    const importLogInput = document.getElementById('importLogInput');
    const duplicateLogButton = document.getElementById('duplicateLogBtn');
    const deleteLogButton = document.getElementById('deleteLogBtn');
    const viewAuditLogButton = document.getElementById('viewAuditLogBtn');
    const auditLogModalElement = document.getElementById('auditLogModal');
    const auditLogListElement = document.getElementById('auditLogList');
    const auditLogEmptyStateElement = document.getElementById('auditLogEmptyState');

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
    const undoButton = document.getElementById('undoBtn');
    const zoomInButton = document.getElementById('zoomInBtn');
    const zoomOutButton = document.getElementById('zoomOutBtn');

    const HEX_COLOR_REGEX = /^#(?:[0-9a-f]{3}){1,2}$/i;
    const SINGLE_CELL_REFERENCE_REGEX = /^([A-Za-z]+)(\d+)$/;
    const FORMULA_FUNCTIONS = {
        SUM: createAggregateFormula((values) => values.reduce((total, value) => total + value, 0), {
            emptyResult: 0
        }),
        AVERAGE: createAggregateFormula((values) => values.reduce((sum, value) => sum + value, 0) / values.length, {
            emptyResult: '#DIV/0!'
        }),
        MIN: createAggregateFormula((values) => values.reduce((min, value) => Math.min(min, value))),
        MAX: createAggregateFormula((values) => values.reduce((max, value) => Math.max(max, value))),
        COUNT: (args) => {
            const extraction = extractNumericValues(args);
            if (extraction.error) {
                return extraction.error;
            }
            return extraction.values.length;
        }
    };
    const MAX_HISTORY_LENGTH = 50;
    const MIN_ZOOM = 0.5;
    const MAX_ZOOM = 2;
    const ZOOM_STEP = 0.1;
    const MAX_AUDIT_ENTRIES = 500;
    const DEFAULT_USER_NAME = 'Unknown user';

    if (!logListEl) {
        return;
    }

    let logs = loadLogs();
    let currentLogId = null;
    let selectionRange = null;
    let anchorCell = null;
    let activeEditingCell = null;
    let zoomLevel = 1;
    let isMouseSelecting = false;
    let selectionDragAnchor = null;
    let auditLogModalInstance = null;
    let fillHandleElement = null;
    let isFillDragging = false;
    let fillDragStartRange = null;
    let fillDragTargetRange = null;
    let fillPreviewCells = [];
    let lastClipboardSnapshot = null;

    const logHistory = new Map();
    const formattingMemory = {
        bold: false,
        italic: false,
        underline: false,
        align: '',
        fontSize: '',
        fontFamily: '',
        textColor: '#000000',
        fillColor: '#FFFFFF'
    };

    function clamp(value, min, max) {
        if (value < min) {
            return min;
        }
        if (value > max) {
            return max;
        }
        return value;
    }

    function modulo(value, modulus) {
        if (!modulus) {
            return 0;
        }
        return ((value % modulus) + modulus) % modulus;
    }

    function cloneFormat(format) {
        if (!format || typeof format !== 'object') {
            return undefined;
        }
        return JSON.parse(JSON.stringify(format));
    }

    function cloneCellData(cell) {
        if (!cell) {
            return createCell();
        }
        const cloned = {
            value: typeof cell.value === 'string' ? cell.value : String(cell.value ?? '')
        };
        const clonedFormat = cloneFormat(cell.format);
        if (clonedFormat) {
            cloned.format = clonedFormat;
        }
        return cloned;
    }

    function cloneLogData(data) {
        return data.map((row) => {
            if (!Array.isArray(row)) {
                return [];
            }
            return row.map((cell) => cloneCellData(cell));
        });
    }

    function pushUndoState() {
        const log = getCurrentLog();
        if (!log) {
            return;
        }
        const snapshot = {
            data: cloneLogData(log.data),
            selectionRange: selectionRange ? { ...selectionRange } : null,
            anchorCell: anchorCell ? { ...anchorCell } : null
        };
        const history = logHistory.get(log.id) ?? [];
        history.push(snapshot);
        if (history.length > MAX_HISTORY_LENGTH) {
            history.shift();
        }
        logHistory.set(log.id, history);
        updateUndoButtonState();
    }

    function updateUndoButtonState() {
        if (!undoButton) {
            return;
        }
        const log = getCurrentLog();
        if (!log) {
            undoButton.disabled = true;
            return;
        }
        const history = logHistory.get(log.id);
        undoButton.disabled = !(history && history.length);
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
        selectionRange = snapshot.selectionRange ? { ...snapshot.selectionRange } : null;
        anchorCell = snapshot.anchorCell ? { ...snapshot.anchorCell } : null;
        recordAudit(log, 'Undo', 'Reverted the most recent change.');
        persistLogs();
        renderSpreadsheet(log);
        if (selectionRange) {
            restoreSelection();
        } else {
            setToolbarEnabled(false);
        }
        updateUndoButtonState();
    }

    function applyZoomLevel() {
        if (!spreadsheetWrapperEl) {
            return;
        }
        spreadsheetWrapperEl.style.setProperty('--spreadsheet-zoom-scale', zoomLevel.toString());
    }

    function updateZoomButtonState() {
        if (!zoomInButton || !zoomOutButton) {
            return;
        }
        const hasLog = !!getCurrentLog();
        zoomInButton.disabled = !hasLog || zoomLevel >= MAX_ZOOM - 0.001;
        zoomOutButton.disabled = !hasLog || zoomLevel <= MIN_ZOOM + 0.001;
    }

    function setZoom(level) {
        const clamped = clamp(level, MIN_ZOOM, MAX_ZOOM);
        const rounded = Math.round(clamped * 100) / 100;
        if (Math.abs(rounded - zoomLevel) < 0.001) {
            return;
        }
        zoomLevel = rounded;
        applyZoomLevel();
        updateZoomButtonState();
    }

    function changeZoom(delta) {
        setZoom(zoomLevel + delta);
    }

    function isPrintableKey(event) {
        if (!event || typeof event.key !== 'string') {
            return false;
        }
        if (event.ctrlKey || event.metaKey || event.altKey) {
            return false;
        }
        if (event.key.length === 1) {
            return event.key >= ' ';
        }
        return false;
    }

    function createCell(value = '', format = {}) {
        return {
            value: typeof value === 'string' ? value : String(value ?? ''),
            format: normalizeFormat(format)
        };
    }

    function normalizeImportedValue(value) {
        if (value == null) {
            return '';
        }
        if (value instanceof Date) {
            return value.toISOString();
        }
        if (typeof value === 'number') {
            if (!Number.isFinite(value)) {
                return '';
            }
            return String(value);
        }
        if (typeof value === 'boolean') {
            return value ? 'TRUE' : 'FALSE';
        }
        return String(value);
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
            data: createEmptyData(),
            auditTrail: []
        };
    }

    function createLogFromData(name, rows) {
        const normalizedRows = Array.isArray(rows)
            ? rows.map((row) => (Array.isArray(row) ? row : []))
            : [];

        const maxImportedColumns = normalizedRows.reduce((max, row) => Math.max(max, row.length), 0);
        const totalRows = Math.max(normalizedRows.length, defaultRows);
        const totalColumns = Math.max(maxImportedColumns, defaultColumns);
        const data = createEmptyData(totalRows, totalColumns);

        normalizedRows.forEach((row, rowIndex) => {
            row.forEach((cellValue, columnIndex) => {
                if (rowIndex < data.length && columnIndex < data[rowIndex].length) {
                    data[rowIndex][columnIndex] = createCell(normalizeImportedValue(cellValue));
                }
            });
        });

        return {
            id: generateId(),
            name,
            data,
            auditTrail: []
        };
    }

    function generateUniqueLogName(baseName) {
        const trimmed = typeof baseName === 'string' ? baseName.trim() : '';
        const defaultName = trimmed.length ? trimmed : 'Imported Log';
        let candidate = defaultName;
        let counter = 2;
        const existingNames = new Set(logs.map((log) => log.name));
        while (existingNames.has(candidate)) {
            candidate = `${defaultName} (${counter})`;
            counter += 1;
        }
        return candidate;
    }

    function generateDuplicateLogName(originalName) {
        const trimmed = typeof originalName === 'string' ? originalName.trim() : '';
        const baseName = trimmed.length ? `${trimmed} (Copy)` : 'Duplicated Log';
        const existingNames = new Set(logs.map((log) => log.name));
        if (!existingNames.has(baseName)) {
            return baseName;
        }
        let counter = 2;
        let candidate = `${baseName} ${counter}`;
        while (existingNames.has(candidate)) {
            counter += 1;
            candidate = `${baseName} ${counter}`;
        }
        return candidate;
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

        const auditTrail = Array.isArray(log.auditTrail)
            ? log.auditTrail.map(normalizeAuditEntry).filter(Boolean)
            : [];

        return {
            id: log.id ?? generateId(),
            name: typeof log.name === 'string' && log.name.trim().length ? log.name.trim() : 'Untitled Log',
            data,
            auditTrail
        };
    }

    function generateId(prefix = 'log') {
        return `${prefix}_${Date.now()}_${Math.random().toString(16).slice(2)}`;
    }

    function normalizeAuditEntry(entry) {
        if (!entry || typeof entry !== 'object') {
            return null;
        }
        const id = typeof entry.id === 'string' && entry.id.trim().length ? entry.id : generateId('audit');
        const timestampValue = typeof entry.timestamp === 'string' ? entry.timestamp : '';
        const timestamp = new Date(timestampValue);
        const sanitizedTimestamp = Number.isNaN(timestamp.getTime())
            ? new Date().toISOString()
            : timestamp.toISOString();
        const userName = typeof entry.user === 'string' && entry.user.trim().length ? entry.user.trim() : DEFAULT_USER_NAME;
        const action = typeof entry.action === 'string' && entry.action.trim().length ? entry.action.trim() : 'Update';
        const details = typeof entry.details === 'string' ? entry.details : '';

        return {
            id,
            timestamp: sanitizedTimestamp,
            user: userName,
            action,
            details
        };
    }

    function ensureAuditTrail(log) {
        if (!log) {
            return [];
        }
        if (!Array.isArray(log.auditTrail)) {
            log.auditTrail = [];
        }
        return log.auditTrail;
    }

    function getCurrentUserName() {
        const user = window.hOps?.currentUser;
        if (user && typeof user === 'object') {
            const name = typeof user.name === 'string' ? user.name.trim() : '';
            if (name.length) {
                return name;
            }
        }
        return DEFAULT_USER_NAME;
    }

    function recordAudit(log, action, details = '') {
        if (!log) {
            return;
        }
        const title = typeof action === 'string' && action.trim().length ? action.trim() : 'Update';
        const message = typeof details === 'string' ? details : '';
        const trail = ensureAuditTrail(log);
        trail.push({
            id: generateId('audit'),
            timestamp: new Date().toISOString(),
            user: getCurrentUserName(),
            action: title,
            details: message
        });
        if (trail.length > MAX_AUDIT_ENTRIES) {
            trail.splice(0, trail.length - MAX_AUDIT_ENTRIES);
        }
        if (log.id === currentLogId) {
            updateLogManagementButtons();
            if (isAuditModalOpen()) {
                renderAuditTrail(log);
            }
        }
    }

    function isAuditModalOpen() {
        return !!auditLogModalElement && auditLogModalElement.classList.contains('show');
    }

    function formatAuditTimestamp(value) {
        const date = value ? new Date(value) : null;
        if (!date || Number.isNaN(date.getTime())) {
            return 'Unknown time';
        }
        return date.toLocaleString(undefined, { dateStyle: 'medium', timeStyle: 'short' });
    }

    function renderAuditTrail(log) {
        if (!auditLogListElement || !auditLogEmptyStateElement) {
            return;
        }
        auditLogListElement.innerHTML = '';
        const entries = [...ensureAuditTrail(log)].sort((a, b) => {
            const aTime = new Date(a.timestamp).getTime();
            const bTime = new Date(b.timestamp).getTime();
            if (Number.isNaN(aTime) && Number.isNaN(bTime)) {
                return 0;
            }
            if (Number.isNaN(aTime)) {
                return 1;
            }
            if (Number.isNaN(bTime)) {
                return -1;
            }
            return bTime - aTime;
        });

        if (!entries.length) {
            auditLogEmptyStateElement.classList.remove('d-none');
            return;
        }

        auditLogEmptyStateElement.classList.add('d-none');

        entries.forEach((entry) => {
            const item = document.createElement('div');
            item.className = 'list-group-item audit-log-entry';

            const header = document.createElement('div');
            header.className = 'd-flex justify-content-between align-items-start flex-wrap gap-2';

            const title = document.createElement('h6');
            title.className = 'mb-1 fw-semibold';
            title.textContent = entry.action;

            const meta = document.createElement('small');
            meta.className = 'text-muted';
            meta.textContent = `${entry.user} • ${formatAuditTimestamp(entry.timestamp)}`;

            header.appendChild(title);
            header.appendChild(meta);
            item.appendChild(header);

            if (entry.details) {
                const detailsEl = document.createElement('p');
                detailsEl.className = 'mb-0 text-muted';
                detailsEl.textContent = entry.details;
                item.appendChild(detailsEl);
            }

            auditLogListElement.appendChild(item);
        });
    }

    function updateLogManagementButtons() {
        const log = getCurrentLog();
        const hasLog = !!log;

        if (duplicateLogButton) {
            duplicateLogButton.disabled = !hasLog;
        }

        if (deleteLogButton) {
            deleteLogButton.disabled = !hasLog;
        }

        if (viewAuditLogButton) {
            if (!hasLog) {
                viewAuditLogButton.disabled = true;
                viewAuditLogButton.textContent = 'View History';
            } else {
                viewAuditLogButton.disabled = false;
                const count = ensureAuditTrail(log).length;
                viewAuditLogButton.textContent = count ? `View History (${count})` : 'View History';
            }
        }
    }

    function persistLogs() {
        localStorage.setItem(storageKey, JSON.stringify(logs));
    }

    function renderLogList() {
        logListEl.querySelectorAll('.log-item').forEach((item) => item.remove());

        if (!logs.length) {
            logEmptyStateEl?.classList.remove('d-none');
            updateLogManagementButtons();
            return;
        }

        logEmptyStateEl?.classList.add('d-none');

        logs.forEach((log) => {
            const isActive = log.id === currentLogId;
            const item = document.createElement('div');
            item.className = 'list-group-item list-group-item-action log-item';
            if (isActive) {
                item.classList.add('active');
            }
            item.dataset.logId = log.id;
            item.tabIndex = 0;

            const selectButton = document.createElement('button');
            selectButton.type = 'button';
            selectButton.className = 'log-item-select';
            selectButton.textContent = log.name;
            selectButton.addEventListener('click', (event) => {
                event.stopPropagation();
                selectLog(log.id);
            });

            item.addEventListener('click', () => selectLog(log.id));
            item.addEventListener('keydown', (event) => {
                if (event.key === 'Enter' || event.key === ' ') {
                    event.preventDefault();
                    selectLog(log.id);
                }
            });

            item.appendChild(selectButton);
            logListEl.appendChild(item);
        });

        updateLogManagementButtons();
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
        if (!logHistory.has(log.id)) {
            logHistory.set(log.id, []);
        }
        logTitleEl.textContent = log.name;
        logSubtitleEl.textContent = 'Changes are saved automatically in this browser.';
        addRowButton.disabled = false;
        addColumnButton.disabled = false;
        clearLogButton.disabled = false;
        if (exportExcelButton) {
            exportExcelButton.disabled = typeof XLSX === 'undefined';
        }
        updateUndoButtonState();
        updateZoomButtonState();
        applyZoomLevel();
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
        updateUndoButtonState();
        updateZoomButtonState();
        updateLogManagementButtons();
        setFillPreview(null);
        updateFillHandlePosition();
    }

    function parseCellReference(reference) {
        if (typeof reference !== 'string') {
            return null;
        }
        const normalized = reference.trim().toUpperCase();
        const match = normalized.match(SINGLE_CELL_REFERENCE_REGEX);
        if (!match) {
            return null;
        }
        const letters = match[1];
        const rowIndex = Number(match[2]) - 1;
        if (!Number.isInteger(rowIndex) || rowIndex < 0) {
            return null;
        }
        let columnIndex = 0;
        for (let i = 0; i < letters.length; i++) {
            columnIndex *= 26;
            columnIndex += letters.charCodeAt(i) - 64;
        }
        columnIndex -= 1;
        if (columnIndex < 0) {
            return null;
        }
        return { row: rowIndex, column: columnIndex };
    }

    function evaluateCellValue(log, row, column, visited = new Set()) {
        if (!log || !Array.isArray(log.data[row])) {
            return '';
        }
        ensureCellExists(log, row, column);
        const cell = log.data[row][column];
        if (!cell) {
            return '';
        }
        const rawValue = typeof cell.value === 'string' ? cell.value : String(cell.value ?? '');
        if (!rawValue.startsWith('=')) {
            return rawValue;
        }

        const key = `${row}:${column}`;
        if (visited.has(key)) {
            return '#CYCLE!';
        }
        visited.add(key);

        const expression = rawValue.slice(1).trim();
        if (!expression.length) {
            visited.delete(key);
            return '';
        }

        const result = evaluateFormulaExpression(log, expression, visited);
        visited.delete(key);
        return result;
    }

    function evaluateFormulaExpression(log, expression, visited) {
        const tokens = tokenizeFormula(expression);
        if (!tokens) {
            return '#ERROR';
        }
        const parser = createFormulaParser(tokens);
        let ast;
        try {
            ast = parser.parseExpression();
            if (parser.hasMoreTokens()) {
                return '#ERROR';
            }
        } catch (error) {
            return normalizeFormulaError(error);
        }

        try {
            const value = evaluateFormulaAst(ast, log, visited);
            if (Array.isArray(value)) {
                return '#VALUE!';
            }
            if (value === true) {
                return 1;
            }
            if (value === false) {
                return 0;
            }
            return value;
        } catch (error) {
            return normalizeFormulaError(error);
        }
    }

    function normalizeFormulaError(error) {
        if (typeof error === 'string' && error.startsWith('#')) {
            return error;
        }
        if (error && typeof error.message === 'string' && error.message.startsWith('#')) {
            return error.message;
        }
        return '#ERROR';
    }

    function tokenizeFormula(expression) {
        const tokens = [];
        let index = 0;
        const length = expression.length;
        while (index < length) {
            const char = expression[index];
            if (/\s/.test(char)) {
                index += 1;
                continue;
            }
            if (char === '(') {
                tokens.push({ type: 'parenOpen' });
                index += 1;
                continue;
            }
            if (char === ')') {
                tokens.push({ type: 'parenClose' });
                index += 1;
                continue;
            }
            if (char === ',') {
                tokens.push({ type: 'comma' });
                index += 1;
                continue;
            }
            if (char === ':') {
                tokens.push({ type: 'colon' });
                index += 1;
                continue;
            }
            if (char === '+' || char === '-' || char === '*' || char === '/') {
                tokens.push({ type: 'operator', value: char });
                index += 1;
                continue;
            }
            if (/[0-9.]/.test(char)) {
                let hasDecimal = false;
                const start = index;
                while (index < length) {
                    const current = expression[index];
                    if (current === '.') {
                        if (hasDecimal) {
                            return null;
                        }
                        hasDecimal = true;
                        index += 1;
                        continue;
                    }
                    if (/[0-9]/.test(current)) {
                        index += 1;
                        continue;
                    }
                    break;
                }
                const numberText = expression.slice(start, index);
                if (!numberText.length || numberText === '.') {
                    return null;
                }
                const numericValue = Number(numberText);
                if (!Number.isFinite(numericValue)) {
                    return null;
                }
                tokens.push({ type: 'number', value: numericValue });
                continue;
            }
            if (/[A-Za-z_]/.test(char)) {
                const start = index;
                index += 1;
                while (index < length && /[A-Za-z0-9_]/.test(expression[index])) {
                    index += 1;
                }
                const text = expression.slice(start, index);
                if (/^[A-Za-z]+\d+$/.test(text)) {
                    tokens.push({ type: 'cell', value: text.toUpperCase() });
                } else {
                    tokens.push({ type: 'identifier', value: text.toUpperCase() });
                }
                continue;
            }
            return null;
        }
        return tokens;
    }

    function createFormulaParser(tokens) {
        let position = 0;

        function peek(offset = 0) {
            return tokens[position + offset] ?? null;
        }

        function match(type, value) {
            const token = tokens[position];
            if (!token || token.type !== type) {
                return null;
            }
            if (value !== undefined && token.value !== value) {
                return null;
            }
            position += 1;
            return token;
        }

        function expect(type, value) {
            const token = match(type, value);
            if (!token) {
                throw '#ERROR';
            }
            return token;
        }

        function check(type, value) {
            const token = tokens[position];
            if (!token || token.type !== type) {
                return false;
            }
            if (value !== undefined && token.value !== value) {
                return false;
            }
            return true;
        }

        function parseExpression() {
            let node = parseTerm();
            while (true) {
                const token = peek();
                if (!token || token.type !== 'operator' || (token.value !== '+' && token.value !== '-')) {
                    break;
                }
                position += 1;
                const right = parseTerm();
                node = { type: 'binary', operator: token.value, left: node, right };
            }
            return node;
        }

        function parseTerm() {
            let node = parseFactor();
            while (true) {
                const token = peek();
                if (!token || token.type !== 'operator' || (token.value !== '*' && token.value !== '/')) {
                    break;
                }
                position += 1;
                const right = parseFactor();
                node = { type: 'binary', operator: token.value, left: node, right };
            }
            return node;
        }

        function parseFactor() {
            const token = peek();
            if (token && token.type === 'operator' && (token.value === '+' || token.value === '-')) {
                position += 1;
                const argument = parseFactor();
                return { type: 'unary', operator: token.value, argument };
            }
            return parsePrimary();
        }

        function parsePrimary() {
            const token = peek();
            if (!token) {
                throw '#ERROR';
            }
            if (token.type === 'number') {
                position += 1;
                return { type: 'number', value: token.value };
            }
            if (token.type === 'cell') {
                position += 1;
                let node = { type: 'cell', reference: token.value };
                if (check('colon')) {
                    position += 1;
                    const endToken = match('cell');
                    if (!endToken) {
                        throw '#REF!';
                    }
                    node = { type: 'range', start: token.value, end: endToken.value };
                }
                return node;
            }
            if (token.type === 'identifier') {
                position += 1;
                expect('parenOpen');
                const args = [];
                if (!check('parenClose')) {
                    do {
                        args.push(parseExpression());
                    } while (match('comma'));
                }
                expect('parenClose');
                return { type: 'function', name: token.value, args };
            }
            if (token.type === 'parenOpen') {
                position += 1;
                const expr = parseExpression();
                expect('parenClose');
                return expr;
            }
            throw '#ERROR';
        }

        return {
            parseExpression,
            hasMoreTokens: () => position < tokens.length
        };
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
                if (isErrorValue(value)) {
                    return value;
                }
                if (Array.isArray(value)) {
                    return '#VALUE!';
                }
                const numeric = coerceValueToNumber(value);
                if (numeric == null) {
                    return '#VALUE!';
                }
                return node.operator === '-' ? -numeric : numeric;
            }
            case 'binary': {
                const left = evaluateFormulaAst(node.left, log, visited);
                if (isErrorValue(left)) {
                    return left;
                }
                const right = evaluateFormulaAst(node.right, log, visited);
                if (isErrorValue(right)) {
                    return right;
                }
                if (Array.isArray(left) || Array.isArray(right)) {
                    return '#VALUE!';
                }
                const leftNumber = coerceValueToNumber(left);
                const rightNumber = coerceValueToNumber(right);
                if (leftNumber == null || rightNumber == null) {
                    return '#VALUE!';
                }
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
                    default:
                        return '#ERROR';
                }
            }
            case 'function': {
                const evaluatedArgs = node.args.map((arg) => evaluateFormulaAst(arg, log, visited));
                for (const value of evaluatedArgs) {
                    if (isErrorValue(value)) {
                        return value;
                    }
                }
                const formulaFn = FORMULA_FUNCTIONS[node.name];
                if (!formulaFn) {
                    return '#NAME?';
                }
                return formulaFn(evaluatedArgs);
            }
            default:
                return '#ERROR';
        }
    }

    function evaluateCellReferenceValue(reference, log, visited) {
        const coordinates = parseCellReference(reference);
        if (!coordinates) {
            return '#REF!';
        }
        return evaluateCellValue(log, coordinates.row, coordinates.column, visited);
    }

    function evaluateRangeValues(startReference, endReference, log, visited) {
        const start = parseCellReference(startReference);
        const end = parseCellReference(endReference);
        if (!start || !end) {
            return '#REF!';
        }
        const values = [];
        const startRow = Math.min(start.row, end.row);
        const endRow = Math.max(start.row, end.row);
        const startColumn = Math.min(start.column, end.column);
        const endColumn = Math.max(start.column, end.column);
        for (let row = startRow; row <= endRow; row++) {
            for (let column = startColumn; column <= endColumn; column++) {
                values.push(evaluateCellValue(log, row, column, visited));
            }
        }
        return values;
    }

    function isErrorValue(value) {
        return typeof value === 'string' && value.startsWith('#');
    }

    function coerceValueToNumber(value) {
        if (value == null) {
            return 0;
        }
        if (typeof value === 'number') {
            return Number.isFinite(value) ? value : null;
        }
        if (typeof value === 'boolean') {
            return value ? 1 : 0;
        }
        if (typeof value === 'string') {
            const trimmed = value.trim();
            if (!trimmed.length) {
                return 0;
            }
            const numeric = Number(trimmed);
            if (Number.isFinite(numeric)) {
                return numeric;
            }
            return null;
        }
        return null;
    }

    function extractNumericValues(values) {
        const collected = [];
        for (const value of values) {
            const extraction = extractNumericValuesFromValue(value);
            if (extraction.error) {
                return { error: extraction.error };
            }
            collected.push(...extraction.values);
        }
        return { values: collected };
    }

    function extractNumericValuesFromValue(value) {
        if (Array.isArray(value)) {
            return extractNumericValues(value);
        }
        if (isErrorValue(value)) {
            return { error: value };
        }
        if (value == null) {
            return { values: [] };
        }
        if (typeof value === 'number') {
            if (Number.isFinite(value)) {
                return { values: [value] };
            }
            return { values: [] };
        }
        if (typeof value === 'boolean') {
            return { values: [value ? 1 : 0] };
        }
        if (typeof value === 'string') {
            const trimmed = value.trim();
            if (!trimmed.length) {
                return { values: [] };
            }
            const numeric = Number(trimmed);
            if (Number.isFinite(numeric)) {
                return { values: [numeric] };
            }
            return { values: [] };
        }
        return { values: [] };
    }

    function createAggregateFormula(reducer, options = {}) {
        return (args) => {
            const extraction = extractNumericValues(args);
            if (extraction.error) {
                return extraction.error;
            }
            const numbers = extraction.values;
            if (!numbers.length) {
                if (Object.prototype.hasOwnProperty.call(options, 'emptyResult')) {
                    return options.emptyResult;
                }
                return '#VALUE!';
            }
            return reducer(numbers);
        };
    }

    function getCellDisplayValue(log, row, column) {
        const value = evaluateCellValue(log, row, column, new Set());
        if (value == null) {
            return '';
        }
        if (typeof value === 'number') {
            const rounded = Number(value.toPrecision(12));
            return Number.isInteger(rounded) ? String(rounded) : String(rounded);
        }
        return value;
    }

    function renderSpreadsheet(log) {
        stopEditingActiveCell({ commit: true });

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
                td.contentEditable = 'false';
                td.dataset.row = String(rowIndex);
                td.dataset.column = String(colIndex);
                td.dataset.editing = 'false';
                td.tabIndex = 0;
                td.textContent = getCellDisplayValue(log, rowIndex, colIndex);
                applyCellFormatting(td, cellData.format);
                td.addEventListener('input', handleCellInput);
                td.addEventListener('mousedown', handleCellMouseDown);
                td.addEventListener('mouseenter', handleCellMouseEnter);
                td.addEventListener('focus', handleCellFocus);
                td.addEventListener('keydown', handleCellKeyDown);
                td.addEventListener('dblclick', handleCellDoubleClick);
                td.addEventListener('blur', handleCellBlur);

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

    function formatCellLabel(row, column) {
        return `${columnLabel(column)}${row + 1}`;
    }

    function describeSelection(range) {
        if (!range) {
            return 'selected cells';
        }
        const startLabel = formatCellLabel(range.startRow, range.startColumn);
        const endLabel = formatCellLabel(range.endRow, range.endColumn);
        if (startLabel === endLabel) {
            return `cell ${startLabel}`;
        }
        return `cells ${startLabel} to ${endLabel}`;
    }

    function formatAuditValue(value) {
        if (value === null || value === undefined) {
            return 'empty';
        }
        const text = value.toString();
        if (!text.trim().length) {
            return 'empty';
        }
        return `"${text}"`;
    }

    function handleCellInput(event) {
        void event;
    }

    function handleCellMouseDown(event) {
        const target = event.target;
        if (!(target instanceof HTMLElement)) {
            return;
        }
        if (target.dataset.editing === 'true') {
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

        const isLeftButton = event.button === 0;
        let dragAnchor = anchorCell ? { ...anchorCell } : null;

        if (event.shiftKey && anchorCell) {
            setSelection(anchorCell.row, anchorCell.column, display.row, display.column);
            dragAnchor = { ...anchorCell };
        } else {
            anchorCell = { row: display.row, column: display.column };
            dragAnchor = { ...anchorCell };
            setSelection(display.row, display.column, display.row, display.column);
        }

        if (isLeftButton && dragAnchor) {
            event.preventDefault();
            beginSelectionDrag(dragAnchor);
        }
    }

    function beginSelectionDrag(anchor) {
        if (!anchor) {
            return;
        }
        selectionDragAnchor = { ...anchor };
        if (!isMouseSelecting) {
            isMouseSelecting = true;
            document.addEventListener('mouseup', handleDocumentMouseUp);
        }
    }

    function handleCellMouseEnter(event) {
        if (!isMouseSelecting || !selectionDragAnchor) {
            return;
        }
        const target = event.target;
        if (!(target instanceof HTMLElement)) {
            return;
        }
        if (target.dataset.editing === 'true') {
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
        setSelection(selectionDragAnchor.row, selectionDragAnchor.column, display.row, display.column, { focus: false });
    }

    function handleDocumentMouseUp() {
        isMouseSelecting = false;
        selectionDragAnchor = null;
        document.removeEventListener('mouseup', handleDocumentMouseUp);
    }

    function handleCellFocus(event) {
        const target = event.target;
        if (!(target instanceof HTMLElement)) {
            return;
        }
        if (target.dataset.editing === 'true') {
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

    function beginEditingCell(cell, options = {}) {
        if (!(cell instanceof HTMLElement)) {
            return;
        }
        if (cell.dataset.editing === 'true') {
            return;
        }
        const selectionMode = options.selectionMode === 'end' ? 'end' : 'all';
        stopEditingActiveCell({ commit: true });
        const rowIndex = Number(cell.dataset.row ?? '-1');
        const columnIndex = Number(cell.dataset.column ?? '-1');
        let originalValue = cell.textContent ?? '';
        const log = getCurrentLog();
        if (log && !Number.isNaN(rowIndex) && !Number.isNaN(columnIndex)) {
            ensureCellExists(log, rowIndex, columnIndex);
            const dataCell = log.data[rowIndex][columnIndex];
            originalValue = dataCell.value ?? '';
            cell.textContent = originalValue;
        }
        cell.dataset.originalValue = originalValue;
        cell.dataset.editing = 'true';
        cell.contentEditable = 'true';
        cell.classList.add('editing-cell');
        activeEditingCell = cell;
        requestAnimationFrame(() => {
            if (typeof cell.focus === 'function') {
                try {
                    cell.focus({ preventScroll: true });
                } catch (error) {
                    cell.focus();
                }
            }
            selectCellContents(cell, selectionMode);
        });
    }

    function selectCellContents(cell, mode = 'all') {
        if (!(cell instanceof HTMLElement) || typeof window.getSelection !== 'function') {
            return;
        }
        const selection = window.getSelection();
        if (!selection) {
            return;
        }
        selection.removeAllRanges();
        const range = document.createRange();
        range.selectNodeContents(cell);
        if (mode === 'end') {
            range.collapse(false);
        }
        selection.addRange(range);
    }

    function stopEditingActiveCell(options = {}) {
        if (!activeEditingCell) {
            return;
        }
        const commit = options.commit !== false;
        const cell = activeEditingCell;
        const rowIndex = Number(cell.dataset.row ?? '-1');
        const columnIndex = Number(cell.dataset.column ?? '-1');
        const originalValue = cell.dataset.originalValue ?? '';
        const log = getCurrentLog();
        let shouldRefresh = false;

        if (log && !Number.isNaN(rowIndex) && !Number.isNaN(columnIndex)) {
            ensureCellExists(log, rowIndex, columnIndex);
            const dataCell = log.data[rowIndex][columnIndex];
            if (commit) {
                const newValue = cell.textContent ?? '';
                const previousValue = dataCell.value ?? '';
                if (newValue !== dataCell.value) {
                    pushUndoState();
                    dataCell.value = newValue;
                    recordAudit(log, 'Updated cell', `Updated ${formatCellLabel(rowIndex, columnIndex)} from ${formatAuditValue(previousValue)} to ${formatAuditValue(newValue)}.`);
                    persistLogs();
                    shouldRefresh = true;
                }
                cell.textContent = getCellDisplayValue(log, rowIndex, columnIndex);
            } else {
                cell.textContent = getCellDisplayValue(log, rowIndex, columnIndex);
            }
        } else if (!commit) {
            cell.textContent = originalValue;
        }

        cell.contentEditable = 'false';
        cell.dataset.editing = 'false';
        cell.classList.remove('editing-cell');
        delete cell.dataset.originalValue;
        if (activeEditingCell === cell) {
            activeEditingCell = null;
        }

        if (shouldRefresh && log) {
            const targetRow = rowIndex;
            const targetColumn = columnIndex;
            requestAnimationFrame(() => {
                const currentLog = getCurrentLog();
                if (!currentLog || currentLog.id !== log.id) {
                    return;
                }
                renderSpreadsheet(currentLog);
                if (selectionRange) {
                    restoreSelection();
                } else if (!Number.isNaN(targetRow) && !Number.isNaN(targetColumn)) {
                    anchorCell = { row: targetRow, column: targetColumn };
                    setSelection(targetRow, targetColumn, targetRow, targetColumn, { focus: false });
                }
            });
        }
    }

    function handleCellDoubleClick(event) {
        const target = event.target;
        if (!(target instanceof HTMLElement)) {
            return;
        }
        event.preventDefault();
        const rowIndex = Number(target.dataset.row ?? '-1');
        const columnIndex = Number(target.dataset.column ?? '-1');
        if (Number.isNaN(rowIndex) || Number.isNaN(columnIndex)) {
            return;
        }
        anchorCell = { row: rowIndex, column: columnIndex };
        beginEditingCell(target, { selectionMode: 'all' });
    }

    function handleCellBlur(event) {
        const target = event.target;
        if (!(target instanceof HTMLElement)) {
            return;
        }
        if (target.dataset.editing === 'true') {
            stopEditingActiveCell({ commit: true });
        }
    }

    function handleCellKeyDown(event) {
        const target = event.target;
        if (!(target instanceof HTMLElement)) {
            return;
        }
        const rowIndex = Number(target.dataset.row ?? '-1');
        const columnIndex = Number(target.dataset.column ?? '-1');
        if (Number.isNaN(rowIndex) || Number.isNaN(columnIndex)) {
            return;
        }
        const log = getCurrentLog();
        if (!log) {
            return;
        }
        const rowCount = log.data.length;
        const columnCount = log.data[0]?.length ?? 0;
        if (!rowCount || !columnCount) {
            return;
        }

        const isEditing = target.dataset.editing === 'true';
        if (isEditing) {
            switch (event.key) {
                case 'Enter':
                    event.preventDefault();
                    stopEditingActiveCell({ commit: true });
                    moveSelection(event.shiftKey ? -1 : 1, 0);
                    break;
                case 'Tab':
                    event.preventDefault();
                    stopEditingActiveCell({ commit: true });
                    moveSelection(0, event.shiftKey ? -1 : 1, { wrap: true });
                    break;
                case 'Escape':
                    event.preventDefault();
                    stopEditingActiveCell({ commit: false });
                    anchorCell = { row: rowIndex, column: columnIndex };
                    setSelection(rowIndex, columnIndex, rowIndex, columnIndex);
                    break;
                default:
                    break;
            }
            return;
        }

        switch (event.key) {
            case 'ArrowUp':
                event.preventDefault();
                navigateSelection(rowIndex, columnIndex, -1, 0, event.shiftKey);
                break;
            case 'ArrowDown':
                event.preventDefault();
                navigateSelection(rowIndex, columnIndex, 1, 0, event.shiftKey);
                break;
            case 'ArrowLeft':
                event.preventDefault();
                navigateSelection(rowIndex, columnIndex, 0, -1, event.shiftKey);
                break;
            case 'ArrowRight':
                event.preventDefault();
                navigateSelection(rowIndex, columnIndex, 0, 1, event.shiftKey);
                break;
            case 'Tab':
                event.preventDefault();
                moveSelection(0, event.shiftKey ? -1 : 1, { wrap: true });
                break;
            case 'Enter':
                event.preventDefault();
                beginEditingCell(target, { selectionMode: 'all' });
                break;
            case 'F2':
                event.preventDefault();
                beginEditingCell(target, { selectionMode: 'end' });
                break;
            case 'Backspace':
            case 'Delete': {
                event.preventDefault();
                const activeLog = getCurrentLog();
                if (!activeLog) {
                    return;
                }
                const rangeToClear = selectionRange ? { ...selectionRange } : {
                    startRow: rowIndex,
                    endRow: rowIndex,
                    startColumn: columnIndex,
                    endColumn: columnIndex
                };
                let hasChanges = false;
                forEachCellInSelection(rangeToClear, ({ cellData }) => {
                    if (cellData.value) {
                        hasChanges = true;
                    }
                }, { includeHidden: true });
                if (!hasChanges) {
                    return;
                }
                pushUndoState();
                forEachCellInSelection(rangeToClear, ({ cellData }) => {
                    cellData.value = '';
                }, { includeHidden: true });
                selectionRange = {
                    startRow: rangeToClear.startRow,
                    endRow: rangeToClear.endRow,
                    startColumn: rangeToClear.startColumn,
                    endColumn: rangeToClear.endColumn
                };
                recordAudit(activeLog, 'Cleared cells', `Cleared ${describeSelection(selectionRange)}.`);
                persistLogs();
                renderSpreadsheet(activeLog);
                anchorCell = { row: rangeToClear.startRow, column: rangeToClear.startColumn };
                setSelection(rangeToClear.startRow, rangeToClear.startColumn, rangeToClear.endRow, rangeToClear.endColumn);
                break;
            }
            default:
                if (isPrintableKey(event)) {
                    event.preventDefault();
                    beginEditingCell(target, { selectionMode: 'all' });
                    requestAnimationFrame(() => {
                        if (target.dataset.editing === 'true') {
                            target.textContent = '';
                            if (event.key.length === 1) {
                                target.textContent = event.key;
                                selectCellContents(target, 'end');
                            }
                        }
                    });
                }
                break;
        }
    }

    function navigateSelection(rowIndex, columnIndex, deltaRow, deltaColumn, extend) {
        const log = getCurrentLog();
        if (!log) {
            return;
        }
        const rowCount = log.data.length;
        const columnCount = log.data[0]?.length ?? 0;
        if (!rowCount || !columnCount) {
            return;
        }
        const targetRow = clamp(rowIndex + deltaRow, 0, rowCount - 1);
        const targetColumn = clamp(columnIndex + deltaColumn, 0, columnCount - 1);
        if (extend) {
            const anchor = anchorCell ?? { row: rowIndex, column: columnIndex };
            if (!anchorCell) {
                anchorCell = { row: anchor.row, column: anchor.column };
            }
            ensureCellExists(log, anchor.row, anchor.column);
            ensureCellExists(log, targetRow, targetColumn);
            setSelection(anchor.row, anchor.column, targetRow, targetColumn);
        } else {
            ensureCellExists(log, targetRow, targetColumn);
            anchorCell = { row: targetRow, column: targetColumn };
            setSelection(targetRow, targetColumn, targetRow, targetColumn);
        }
    }

    function moveSelection(deltaRow, deltaColumn, options = {}) {
        const log = getCurrentLog();
        if (!log) {
            return;
        }
        const rowCount = log.data.length;
        const columnCount = log.data[0]?.length ?? 0;
        if (!rowCount || !columnCount) {
            return;
        }
        const currentRow = selectionRange ? selectionRange.endRow : 0;
        const currentColumn = selectionRange ? selectionRange.endColumn : 0;
        let targetRow = currentRow + deltaRow;
        let targetColumn = currentColumn + deltaColumn;
        if (options.wrap) {
            if (targetColumn > columnCount - 1) {
                targetColumn = 0;
                targetRow = currentRow + 1;
            } else if (targetColumn < 0) {
                targetColumn = columnCount - 1;
                targetRow = currentRow - 1;
            }
        }
        targetRow = clamp(targetRow, 0, rowCount - 1);
        targetColumn = clamp(targetColumn, 0, columnCount - 1);
        ensureCellExists(log, targetRow, targetColumn);
        anchorCell = { row: targetRow, column: targetColumn };
        setSelection(targetRow, targetColumn, targetRow, targetColumn);
    }

    function addRow() {
        const log = getCurrentLog();
        if (!log) {
            return;
        }
        pushUndoState();
        const columns = log.data[0]?.length ?? defaultColumns;
        const newRowIndex = log.data.length;
        log.data.push(Array.from({ length: columns }, () => createCell()));
        recordAudit(log, 'Added row', `Added row ${newRowIndex + 1}.`);
        persistLogs();
        renderSpreadsheet(log);
    }

    function addColumn() {
        const log = getCurrentLog();
        if (!log) {
            return;
        }
        pushUndoState();
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
                row.push(createCell());
            }
        });
        recordAudit(log, 'Added column', `Added column ${columnLabel(newColumnIndex)}.`);
        persistLogs();
        renderSpreadsheet(log);
    }

    function clearCurrentLog() {
        const log = getCurrentLog();
        if (!log) {
            return;
        }
        pushUndoState();
        const rows = log.data.length || defaultRows;
        const cols = log.data[0]?.length || defaultColumns;
        log.data = createEmptyData(rows, cols);
        selectionRange = null;
        anchorCell = null;
        recordAudit(log, 'Cleared log', `Cleared all cells in "${log.name}".`);
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
        logHistory.set(newLog.id, []);
        recordAudit(newLog, 'Log created', 'Log created manually.');
        persistLogs();
        createLogForm.reset();
        const modalElement = document.getElementById('createLogModal');
        if (modalElement) {
            const modal = bootstrap.Modal.getOrCreateInstance(modalElement);
            modal.hide();
        }
        selectLog(newLog.id);
    }

    function triggerImportLogPicker() {
        if (!importLogInput) {
            return;
        }
        importLogInput.click();
    }

    async function handleImportLogInputChange(event) {
        const target = event.target;
        if (!(target instanceof HTMLInputElement)) {
            return;
        }
        const file = target.files?.[0];
        if (!file) {
            return;
        }

        try {
            if (typeof XLSX === 'undefined') {
                throw new Error('XLSX library is not available');
            }

            const extension = (file.name.split('.').pop() || '').toLowerCase();
            let workbook;
            if (extension === 'csv') {
                const textContent = await file.text();
                workbook = XLSX.read(textContent, { type: 'string' });
            } else {
                const buffer = await file.arrayBuffer();
                workbook = XLSX.read(buffer, { type: 'array' });
            }

            if (!workbook.SheetNames?.length) {
                throw new Error('Imported file does not contain any sheets');
            }

            const worksheet = workbook.Sheets[workbook.SheetNames[0]];
            if (!worksheet) {
                throw new Error('Unable to read the first worksheet');
            }

            const rows = XLSX.utils.sheet_to_json(worksheet, {
                header: 1,
                defval: '',
                blankrows: true
            });

            const baseName = file.name.replace(/\.[^/.]+$/, '');
            const logName = generateUniqueLogName(baseName);
            const newLog = createLogFromData(logName, rows);
            logs.push(newLog);
            logHistory.set(newLog.id, []);
            recordAudit(newLog, 'Log imported', `Imported from "${file.name}".`);
            persistLogs();
            selectLog(newLog.id);
        } catch (error) {
            console.error('Failed to import log', error);
            window.alert('Unable to import the selected file. Please ensure it is a valid .xls, .xlsx, or .csv document.');
        } finally {
            target.value = '';
        }
    }

    function duplicateLog(logId = currentLogId) {
        const source = logs.find((log) => log.id === logId);
        if (!source) {
            return;
        }
        const duplicateName = generateDuplicateLogName(source.name);
        const newLog = {
            id: generateId(),
            name: duplicateName,
            data: cloneLogData(source.data),
            auditTrail: []
        };
        logs.push(newLog);
        logHistory.set(newLog.id, []);
        recordAudit(newLog, 'Log duplicated', `Created by duplicating "${source.name}".`);
        recordAudit(source, 'Log duplicated', `Duplicated to create "${newLog.name}".`);
        persistLogs();
        selectLog(newLog.id);
    }

    function deleteLog(logId = currentLogId) {
        if (!logId) {
            return;
        }
        const index = logs.findIndex((log) => log.id === logId);
        if (index === -1) {
            return;
        }
        const log = logs[index];
        const confirmed = window.confirm(`Delete log "${log.name}"? This action cannot be undone.`);
        if (!confirmed) {
            return;
        }
        logs.splice(index, 1);
        logHistory.delete(log.id);
        if (auditLogModalElement && auditLogModalElement.classList.contains('show') && typeof bootstrap !== 'undefined' && bootstrap?.Modal) {
            bootstrap.Modal.getOrCreateInstance(auditLogModalElement).hide();
        }
        let nextLogId = null;
        if (logs.length) {
            nextLogId = logs[index]?.id ?? logs[index - 1]?.id ?? logs[0].id;
        }
        const deletingCurrent = currentLogId === log.id;
        if (deletingCurrent) {
            currentLogId = null;
        }
        persistLogs();
        if (deletingCurrent) {
            if (nextLogId) {
                selectLog(nextLogId);
            } else {
                renderLogList();
                showPlaceholder();
            }
        } else {
            renderLogList();
        }
    }

    function openAuditLogModal() {
        const log = getCurrentLog();
        if (!log || !auditLogModalElement) {
            return;
        }
        renderAuditTrail(log);
        if (typeof bootstrap !== 'undefined' && bootstrap?.Modal) {
            auditLogModalInstance = bootstrap.Modal.getOrCreateInstance(auditLogModalElement);
            auditLogModalInstance.show();
        }
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
        stopEditingActiveCell({ commit: true });
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
        setFillPreview(null);
        if (!selectionRange) {
            updateFillHandlePosition();
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
        updateFillHandlePosition();
    }

    function restoreSelection() {
        if (!selectionRange) {
            setToolbarEnabled(false);
            return;
        }
        highlightSelection();
        updateToolbarState();
    }

    function setFillPreview(range) {
        if (!Array.isArray(fillPreviewCells)) {
            fillPreviewCells = [];
        }
        fillPreviewCells.forEach((cell) => cell.classList.remove('fill-preview'));
        fillPreviewCells = [];
        if (!range) {
            return;
        }
        for (let row = range.startRow; row <= range.endRow; row++) {
            for (let column = range.startColumn; column <= range.endColumn; column++) {
                const cellEl = getCellElement(row, column);
                if (cellEl) {
                    cellEl.classList.add('fill-preview');
                    fillPreviewCells.push(cellEl);
                }
            }
        }
    }

    function ensureFillHandleElement() {
        if (fillHandleElement || !spreadsheetWrapperEl) {
            return;
        }
        fillHandleElement = document.createElement('div');
        fillHandleElement.className = 'fill-handle d-none';
        fillHandleElement.setAttribute('aria-hidden', 'true');
        fillHandleElement.addEventListener('mousedown', handleFillHandleMouseDown);
        spreadsheetWrapperEl.appendChild(fillHandleElement);
    }

    function updateFillHandlePosition() {
        if (!fillHandleElement || !spreadsheetWrapperEl) {
            return;
        }
        if (!selectionRange || spreadsheetWrapperEl.classList.contains('d-none')) {
            fillHandleElement.classList.add('d-none');
            return;
        }
        const log = getCurrentLog();
        if (!log) {
            fillHandleElement.classList.add('d-none');
            return;
        }
        const selectionInfo = getSelectionInfo();
        if (selectionInfo.hasMergedCells || selectionInfo.hasExternalMerge) {
            fillHandleElement.classList.add('d-none');
            return;
        }
        const cellEl = getCellElement(selectionRange.endRow, selectionRange.endColumn);
        if (!cellEl) {
            fillHandleElement.classList.add('d-none');
            return;
        }
        const wrapperRect = spreadsheetWrapperEl.getBoundingClientRect();
        const cellRect = cellEl.getBoundingClientRect();
        const size = fillHandleElement.offsetWidth || 10;
        const left = cellRect.right - wrapperRect.left + spreadsheetWrapperEl.scrollLeft - size / 2;
        const top = cellRect.bottom - wrapperRect.top + spreadsheetWrapperEl.scrollTop - size / 2;
        fillHandleElement.style.left = `${left}px`;
        fillHandleElement.style.top = `${top}px`;
        fillHandleElement.classList.remove('d-none');
    }

    function handleFillHandleMouseDown(event) {
        if (event.button !== 0 || !selectionRange) {
            return;
        }
        event.preventDefault();
        event.stopPropagation();
        fillDragStartRange = { ...selectionRange };
        fillDragTargetRange = null;
        isFillDragging = true;
        document.addEventListener('mousemove', handleFillMouseMove);
        document.addEventListener('mouseup', handleFillMouseUp);
    }

    function handleFillMouseMove(event) {
        if (!isFillDragging || !fillDragStartRange) {
            return;
        }
        const resolved = resolveCellFromPoint(event.clientX, event.clientY);
        if (!resolved) {
            fillDragTargetRange = null;
            setFillPreview(null);
            return;
        }
        const display = getDisplayCellCoordinates(resolved.row, resolved.column);
        if (!display) {
            fillDragTargetRange = null;
            setFillPreview(null);
            return;
        }
        const targetRange = determineFillTargetRange(display.row, display.column);
        fillDragTargetRange = targetRange;
        setFillPreview(targetRange);
    }

    function handleFillMouseUp(event) {
        if (!isFillDragging) {
            return;
        }
        event.preventDefault();
        document.removeEventListener('mousemove', handleFillMouseMove);
        document.removeEventListener('mouseup', handleFillMouseUp);
        isFillDragging = false;
        const sourceRange = fillDragStartRange ? { ...fillDragStartRange } : null;
        const targetRange = fillDragTargetRange ? { ...fillDragTargetRange } : null;
        fillDragStartRange = null;
        fillDragTargetRange = null;
        setFillPreview(null);
        if (!sourceRange || !targetRange) {
            updateFillHandlePosition();
            return;
        }
        fillSelectionIntoRange(sourceRange, targetRange);
    }

    function resolveCellFromPoint(clientX, clientY) {
        const element = document.elementFromPoint(clientX, clientY);
        if (!(element instanceof Element)) {
            return null;
        }
        return resolveCellFromElement(element);
    }

    function resolveCellFromElement(element) {
        let current = element;
        while (current && current !== document.body) {
            if (current instanceof HTMLElement && current.matches('td[data-row][data-column]')) {
                const row = Number(current.dataset.row ?? '-1');
                const column = Number(current.dataset.column ?? '-1');
                if (!Number.isNaN(row) && !Number.isNaN(column)) {
                    return { row, column, element: current };
                }
            }
            current = current.parentElement;
        }
        return null;
    }

    function determineFillTargetRange(targetRow, targetColumn) {
        if (!fillDragStartRange) {
            return null;
        }
        const source = fillDragStartRange;
        const verticalDistance = targetRow < source.startRow ? source.startRow - targetRow :
            targetRow > source.endRow ? targetRow - source.endRow : 0;
        const horizontalDistance = targetColumn < source.startColumn ? source.startColumn - targetColumn :
            targetColumn > source.endColumn ? targetColumn - source.endColumn : 0;

        if (verticalDistance === 0 && horizontalDistance === 0) {
            return null;
        }

        if (verticalDistance >= horizontalDistance) {
            if (targetRow < source.startRow) {
                return {
                    startRow: targetRow,
                    endRow: source.startRow - 1,
                    startColumn: source.startColumn,
                    endColumn: source.endColumn
                };
            }
            return {
                startRow: source.endRow + 1,
                endRow: targetRow,
                startColumn: source.startColumn,
                endColumn: source.endColumn
            };
        }

        if (targetColumn < source.startColumn) {
            return {
                startRow: source.startRow,
                endRow: source.endRow,
                startColumn: targetColumn,
                endColumn: source.startColumn - 1
            };
        }
        return {
            startRow: source.startRow,
            endRow: source.endRow,
            startColumn: source.endColumn + 1,
            endColumn: targetColumn
        };
    }

    function fillSelectionIntoRange(sourceRange, targetRange) {
        if (!selectionRange) {
            return;
        }
        const log = getCurrentLog();
        if (!log) {
            return;
        }
        const selectionInfo = getSelectionInfo();
        if (selectionInfo.hasMergedCells || selectionInfo.hasExternalMerge) {
            updateFillHandlePosition();
            return;
        }
        pushUndoState();
        const sourceHeight = sourceRange.endRow - sourceRange.startRow + 1;
        const sourceWidth = sourceRange.endColumn - sourceRange.startColumn + 1;
        let changed = false;
        for (let row = targetRange.startRow; row <= targetRange.endRow; row++) {
            for (let column = targetRange.startColumn; column <= targetRange.endColumn; column++) {
                const mappedRow = sourceRange.startRow + modulo(row - sourceRange.startRow, sourceHeight);
                const mappedColumn = sourceRange.startColumn + modulo(column - sourceRange.startColumn, sourceWidth);
                const sourceCell = getRawCell(mappedRow, mappedColumn);
                if (!sourceCell) {
                    continue;
                }
                ensureCellExists(log, row, column);
                const targetCell = log.data[row][column];
                const previousValue = targetCell.value ?? '';
                const previousFormat = JSON.stringify(targetCell.format ?? {});
                const cloned = cloneCellData(sourceCell);
                targetCell.value = cloned.value ?? '';
                if (cloned.format) {
                    targetCell.format = cloneFormat(cloned.format) || {};
                } else if (targetCell.format) {
                    delete targetCell.format;
                }
                const nextFormatSnapshot = JSON.stringify(targetCell.format ?? {});
                if (previousValue !== (targetCell.value ?? '') || previousFormat !== nextFormatSnapshot) {
                    changed = true;
                }
            }
        }
        if (!changed) {
            const history = logHistory.get(log.id);
            history?.pop();
            updateUndoButtonState();
            updateFillHandlePosition();
            return;
        }
        const sourceDescription = describeSelection(sourceRange);
        const targetDescription = describeSelection(targetRange);
        persistLogs();
        renderSpreadsheet(log);
        recordAudit(log, 'Filled cells', `Copied ${sourceDescription} into ${targetDescription}.`);
        const combinedRange = {
            startRow: Math.min(sourceRange.startRow, targetRange.startRow),
            endRow: Math.max(sourceRange.endRow, targetRange.endRow),
            startColumn: Math.min(sourceRange.startColumn, targetRange.startColumn),
            endColumn: Math.max(sourceRange.endColumn, targetRange.endColumn)
        };
        anchorCell = { row: sourceRange.startRow, column: sourceRange.startColumn };
        setSelection(combinedRange.startRow, combinedRange.startColumn, combinedRange.endRow, combinedRange.endColumn);
    }

    function isSpreadsheetEventTarget(target) {
        if (!spreadsheetWrapperEl) {
            return false;
        }
        if (target instanceof Node && spreadsheetWrapperEl.contains(target)) {
            return true;
        }
        const active = document.activeElement;
        return active instanceof Node && spreadsheetWrapperEl.contains(active);
    }

    function parseClipboardText(text) {
        if (typeof text !== 'string' || !text.length) {
            return [];
        }
        const sanitized = text.replace(/\r/g, '');
        const lines = sanitized.split('\n');
        if (lines.length && lines[lines.length - 1] === '') {
            lines.pop();
        }
        return lines.map((line) => line.split('\t'));
    }

    function handleCopyEvent(event) {
        if (!selectionRange || activeEditingCell) {
            return;
        }
        if (!isSpreadsheetEventTarget(event.target)) {
            return;
        }
        const log = getCurrentLog();
        if (!log) {
            return;
        }
        const height = selectionRange.endRow - selectionRange.startRow + 1;
        const width = selectionRange.endColumn - selectionRange.startColumn + 1;
        const textRows = [];
        const snapshot = [];
        for (let rowOffset = 0; rowOffset < height; rowOffset++) {
            const textRow = [];
            const snapshotRow = [];
            for (let columnOffset = 0; columnOffset < width; columnOffset++) {
                const row = selectionRange.startRow + rowOffset;
                const column = selectionRange.startColumn + columnOffset;
                const cell = getRawCell(row, column);
                const cloned = cloneCellData(cell);
                const displayValue = getCellDisplayValue(log, row, column);
                textRow.push(displayValue ?? '');
                snapshotRow.push(cloned);
            }
            textRows.push(textRow);
            snapshot.push(snapshotRow);
        }
        lastClipboardSnapshot = {
            width,
            height,
            data: snapshot
        };
        if (event instanceof ClipboardEvent && event.clipboardData) {
            const text = textRows.map((row) => row.join('\t')).join('\n');
            event.clipboardData.setData('text/plain', text);
            try {
                event.clipboardData.setData('application/x-hops-log-cells', JSON.stringify(lastClipboardSnapshot));
            } catch (error) {
                console.warn('Unable to serialize clipboard data', error);
            }
            event.preventDefault();
        }
    }

    function handlePasteEvent(event) {
        if (!selectionRange || activeEditingCell) {
            return;
        }
        if (!isSpreadsheetEventTarget(event.target)) {
            return;
        }
        const log = getCurrentLog();
        if (!log) {
            return;
        }
        let textMatrix = [];
        let customData = null;
        let hasPlainText = false;
        if (event instanceof ClipboardEvent && event.clipboardData) {
            const text = event.clipboardData.getData('text/plain');
            if (typeof text === 'string' && text.length > 0) {
                hasPlainText = true;
            }
            textMatrix = parseClipboardText(text);
            const custom = event.clipboardData.getData('application/x-hops-log-cells');
            if (custom) {
                try {
                    const parsed = JSON.parse(custom);
                    if (parsed && typeof parsed === 'object' && Array.isArray(parsed.data)) {
                        customData = parsed;
                    }
                } catch (error) {
                    console.warn('Unable to parse custom clipboard data', error);
                }
            }
        }
        if (!customData && !hasPlainText && lastClipboardSnapshot) {
            customData = lastClipboardSnapshot;
        }
        if ((!textMatrix || !textMatrix.length) && (!customData || !customData.data?.length)) {
            return;
        }
        event.preventDefault();
        const inferredRowCount = textMatrix?.length ?? 0;
        const inferredColumnCount = textMatrix?.reduce((max, row) => Math.max(max, row.length), 0) ?? 0;
        const customRowCount = customData?.height ?? (customData?.data?.length ?? 0);
        const customColumnCount = customData?.width ?? (customData?.data?.[0]?.length ?? 0);
        const rowCount = Math.max(inferredRowCount, customRowCount);
        const columnCount = Math.max(inferredColumnCount, customColumnCount);
        if (!rowCount || !columnCount) {
            return;
        }
        pushUndoState();
        let changed = false;
        const pasteRange = {
            startRow: selectionRange.startRow,
            endRow: selectionRange.startRow + rowCount - 1,
            startColumn: selectionRange.startColumn,
            endColumn: selectionRange.startColumn + columnCount - 1
        };
        for (let rowOffset = 0; rowOffset < rowCount; rowOffset++) {
            for (let columnOffset = 0; columnOffset < columnCount; columnOffset++) {
                const row = selectionRange.startRow + rowOffset;
                const column = selectionRange.startColumn + columnOffset;
                ensureCellExists(log, row, column);
                const targetCell = log.data[row][column];
                const previousValue = targetCell.value ?? '';
                const previousFormat = JSON.stringify(targetCell.format ?? {});
                let appliedValue = '';
                const customCell = customData?.data?.[rowOffset]?.[columnOffset];
                if (customCell) {
                    appliedValue = customCell.value ?? '';
                    if (customCell.format) {
                        targetCell.format = cloneFormat(customCell.format) || {};
                    } else if (targetCell.format) {
                        delete targetCell.format;
                    }
                } else {
                    appliedValue = textMatrix?.[rowOffset]?.[columnOffset] ?? '';
                }
                targetCell.value = appliedValue;
                const nextFormat = JSON.stringify(targetCell.format ?? {});
                if (previousValue !== (targetCell.value ?? '') || previousFormat !== nextFormat) {
                    changed = true;
                }
            }
        }
        if (!changed) {
            const history = logHistory.get(log.id);
            history?.pop();
            updateUndoButtonState();
            return;
        }
        persistLogs();
        renderSpreadsheet(log);
        recordAudit(log, 'Pasted cells', `Pasted into ${describeSelection(pasteRange)}.`);
        setSelection(pasteRange.startRow, pasteRange.startColumn, pasteRange.endRow, pasteRange.endColumn);
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

        const boldActive = boldState === null ? !!formattingMemory.bold : boldState === undefined ? false : !!boldState;
        const italicActive = italicState === null ? !!formattingMemory.italic : italicState === undefined ? false : !!italicState;
        const underlineActive = underlineState === null ? !!formattingMemory.underline : underlineState === undefined ? false : !!underlineState;

        if (boldState !== undefined && boldState !== null) {
            formattingMemory.bold = !!boldState;
        }
        if (italicState !== undefined && italicState !== null) {
            formattingMemory.italic = !!italicState;
        }
        if (underlineState !== undefined && underlineState !== null) {
            formattingMemory.underline = !!underlineState;
        }

        setToggleState(boldButton, boldActive);
        setToggleState(italicButton, italicActive);
        setToggleState(underlineButton, underlineActive);

        let fontSizeValue = '';
        if (typeof fontSizeState === 'string' && fontSizeState.length) {
            fontSizeValue = fontSizeState;
            formattingMemory.fontSize = fontSizeState;
        } else if (fontSizeState === null) {
            fontSizeValue = formattingMemory.fontSize;
        }
        if (fontSizeSelect) {
            fontSizeSelect.value = fontSizeValue || '';
        }

        let fontFamilyValue = '';
        if (typeof fontFamilyState === 'string' && fontFamilyState.length) {
            fontFamilyValue = fontFamilyState;
            formattingMemory.fontFamily = fontFamilyState;
        } else if (fontFamilyState === null) {
            fontFamilyValue = formattingMemory.fontFamily;
        }
        if (fontFamilySelect) {
            fontFamilySelect.value = fontFamilyValue || '';
        }

        let alignValue = '';
        if (typeof alignState === 'string' && alignState.length) {
            alignValue = alignState;
            formattingMemory.align = alignState;
        } else if (alignState === null) {
            alignValue = formattingMemory.align;
        }

        setToggleState(alignLeftButton, alignValue === 'left');
        setToggleState(alignCenterButton, alignValue === 'center');
        setToggleState(alignRightButton, alignValue === 'right');

        let appliedTextColor;
        if (typeof textColorState === 'string' && textColorState.length) {
            appliedTextColor = normalizeHexColor(textColorState);
            formattingMemory.textColor = appliedTextColor;
        } else if (textColorState === null) {
            appliedTextColor = formattingMemory.textColor;
        }

        let appliedFillColor;
        if (typeof fillColorState === 'string' && fillColorState.length) {
            appliedFillColor = normalizeHexColor(fillColorState);
            formattingMemory.fillColor = appliedFillColor;
        } else if (fillColorState === null) {
            appliedFillColor = formattingMemory.fillColor;
        }

        updateColorInputState(textColorInput, clearTextColorButton, appliedTextColor, '#000000');
        updateColorInputState(fillColorInput, clearFillColorButton, appliedFillColor, '#ffffff');

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
        let hasAnyValue = false;
        forEachCellInSelection(selectionRange, ({ cellData }) => {
            const value = cellData.format?.[key];
            if (value !== undefined) {
                hasAnyValue = true;
            }
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
        if (!hasAnyValue) {
            return null;
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
        const log = getCurrentLog();
        if (!log) {
            return;
        }
        pushUndoState();
        const currentValue = !!getUniformFormatValue(key);
        const targetValue = !currentValue;
        let changed = false;
        forEachCellInSelection(selectionRange, ({ cellData }) => {
            cellData.format = cellData.format || {};
            if (targetValue) {
                if (!cellData.format[key]) {
                    cellData.format[key] = true;
                    changed = true;
                }
            } else if (cellData.format[key]) {
                delete cellData.format[key];
                changed = true;
            }
        });
        if (!changed) {
            const history = logHistory.get(log.id);
            history?.pop();
            updateUndoButtonState();
            return;
        }
        const label = key === 'bold' ? 'bold formatting' : key === 'italic' ? 'italic formatting' : 'underline formatting';
        const actionTitle = targetValue ? `Applied ${label}` : `Removed ${label}`;
        recordAudit(log, actionTitle, `${actionTitle} on ${describeSelection(selectionRange)}.`);
        persistLogs();
        if (Object.prototype.hasOwnProperty.call(formattingMemory, key)) {
            formattingMemory[key] = targetValue;
        }
        updateSelectedCellStyles();
        updateToolbarState();
    }

    function setAlignment(alignment) {
        if (!selectionRange) {
            return;
        }
        const log = getCurrentLog();
        if (!log) {
            return;
        }
        pushUndoState();
        const normalizedAlignment = alignment || '';
        let changed = false;
        forEachCellInSelection(selectionRange, ({ cellData }) => {
            cellData.format = cellData.format || {};
            const current = cellData.format.align || '';
            if (normalizedAlignment) {
                if (current !== normalizedAlignment) {
                    cellData.format.align = normalizedAlignment;
                    changed = true;
                }
            } else if (current) {
                delete cellData.format.align;
                changed = true;
            }
        });
        if (!changed) {
            const history = logHistory.get(log.id);
            history?.pop();
            updateUndoButtonState();
            return;
        }
        const actionTitle = normalizedAlignment ? `Set alignment to ${normalizedAlignment}` : 'Cleared alignment';
        recordAudit(log, actionTitle, `${actionTitle} for ${describeSelection(selectionRange)}.`);
        persistLogs();
        formattingMemory.align = normalizedAlignment;
        updateSelectedCellStyles();
        updateToolbarState();
    }

    function setFontSize(size) {
        if (!selectionRange) {
            return;
        }
        const log = getCurrentLog();
        if (!log) {
            return;
        }
        pushUndoState();
        const normalizedSize = size || '';
        let changed = false;
        forEachCellInSelection(selectionRange, ({ cellData }) => {
            cellData.format = cellData.format || {};
            const current = cellData.format.fontSize || '';
            if (normalizedSize) {
                if (current !== normalizedSize) {
                    cellData.format.fontSize = normalizedSize;
                    changed = true;
                }
            } else if (current) {
                delete cellData.format.fontSize;
                changed = true;
            }
        });
        if (!changed) {
            const history = logHistory.get(log.id);
            history?.pop();
            updateUndoButtonState();
            return;
        }
        const actionTitle = normalizedSize ? `Set font size to ${normalizedSize}` : 'Cleared font size';
        recordAudit(log, actionTitle, `${actionTitle} for ${describeSelection(selectionRange)}.`);
        persistLogs();
        formattingMemory.fontSize = normalizedSize;
        updateSelectedCellStyles();
        updateToolbarState();
    }

    function setFontFamily(family) {
        if (!selectionRange) {
            return;
        }
        const log = getCurrentLog();
        if (!log) {
            return;
        }
        pushUndoState();
        const normalizedFamily = family || '';
        let changed = false;
        forEachCellInSelection(selectionRange, ({ cellData }) => {
            cellData.format = cellData.format || {};
            const current = cellData.format.fontFamily || '';
            if (normalizedFamily) {
                if (current !== normalizedFamily) {
                    cellData.format.fontFamily = normalizedFamily;
                    changed = true;
                }
            } else if (current) {
                delete cellData.format.fontFamily;
                changed = true;
            }
        });
        if (!changed) {
            const history = logHistory.get(log.id);
            history?.pop();
            updateUndoButtonState();
            return;
        }
        const actionTitle = normalizedFamily ? `Set font family to ${normalizedFamily}` : 'Cleared font family';
        recordAudit(log, actionTitle, `${actionTitle} for ${describeSelection(selectionRange)}.`);
        persistLogs();
        formattingMemory.fontFamily = normalizedFamily;
        updateSelectedCellStyles();
        updateToolbarState();
    }

    function applyColorToSelection(key, color) {
        if (!selectionRange) {
            return;
        }
        const log = getCurrentLog();
        if (!log) {
            return;
        }
        pushUndoState();
        const normalized = isValidHexColor(color) ? normalizeHexColor(color) : null;
        let changed = false;
        forEachCellInSelection(selectionRange, ({ cellData }) => {
            cellData.format = cellData.format || {};
            const current = cellData.format[key] || '';
            if (normalized) {
                if (current !== normalized) {
                    cellData.format[key] = normalized;
                    changed = true;
                }
            } else if (current) {
                delete cellData.format[key];
                changed = true;
            }
        });
        if (!changed) {
            const history = logHistory.get(log.id);
            history?.pop();
            updateUndoButtonState();
            return;
        }
        const label = key === 'textColor' ? 'text color' : 'fill color';
        const actionTitle = normalized ? `Set ${label} to ${normalized}` : `Cleared ${label}`;
        recordAudit(log, actionTitle, `${actionTitle} for ${describeSelection(selectionRange)}.`);
        persistLogs();
        if (key === 'textColor') {
            formattingMemory.textColor = normalized || getDefaultColorValue(textColorInput, '#000000');
        } else if (key === 'fillColor') {
            formattingMemory.fillColor = normalized || getDefaultColorValue(fillColorInput, '#ffffff');
        }
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
        const selectionDescription = describeSelection(selectionRange);

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
        pushUndoState();
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

        recordAudit(log, 'Merged cells', `Merged ${selectionDescription} into ${formatCellLabel(masterRow, masterColumn)}.`);
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
        pushUndoState();
        masters.forEach(({ row, column }) => unmergeCell(row, column));
        const selectionDescription = describeSelection(selectionRange);
        recordAudit(log, 'Unmerged cells', `Unmerged ${selectionDescription}.`);
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

    textColorInput?.addEventListener('change', (event) => {
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

    fillColorInput?.addEventListener('change', (event) => {
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
    undoButton?.addEventListener('click', undoLastChange);
    zoomInButton?.addEventListener('click', () => changeZoom(ZOOM_STEP));
    zoomOutButton?.addEventListener('click', () => changeZoom(-ZOOM_STEP));

    createLogForm?.addEventListener('submit', handleCreateLog);
    logNameInput?.addEventListener('input', () => {
        logNameInput?.classList.remove('is-invalid');
    });

    importLogButton?.addEventListener('click', triggerImportLogPicker);
    importLogInput?.addEventListener('change', handleImportLogInputChange);

    duplicateLogButton?.addEventListener('click', () => duplicateLog());
    deleteLogButton?.addEventListener('click', () => deleteLog());
    viewAuditLogButton?.addEventListener('click', openAuditLogModal);

    document.addEventListener('copy', handleCopyEvent);
    document.addEventListener('paste', handlePasteEvent);

    ensureFillHandleElement();
    if (spreadsheetWrapperEl) {
        spreadsheetWrapperEl.addEventListener('scroll', updateFillHandlePosition);
    }
    if (typeof window !== 'undefined') {
        window.addEventListener('resize', updateFillHandlePosition);
    }

    applyZoomLevel();
    updateZoomButtonState();
    updateUndoButtonState();
    renderLogList();
    if (logs.length) {
        selectLog(logs[0].id);
    } else {
        showPlaceholder();
    }
})();
