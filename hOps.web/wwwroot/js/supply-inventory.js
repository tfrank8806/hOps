(function () {
    document.addEventListener('DOMContentLoaded', () => {
        const config = window.supplyInventoryConfig;
        const tableBody = document.getElementById('supplyInventoryRows');
        if (!config || !tableBody) {
            return;
        }

        const templateItems = Array.isArray(config.templateItems) ? config.templateItems : [];
        const addButton = document.querySelector('[data-action="add-item"]');
        const resetButton = document.querySelector('[data-action="reset-template"]');
        const budgetInput = document.querySelector('[data-role="monthly-budget"]');
        const totalOrderCostEl = document.querySelector('[data-role="total-order-cost"]');
        const totalInventoryValueEl = document.querySelector('[data-role="total-inventory-value"]');
        const varianceEl = document.querySelector('[data-role="budget-variance"]');
        const itemCountEl = document.querySelector('[data-role="item-count"]');
        const saveStatusEl = document.querySelector('[data-role="save-status"]');
        const saveSnapshotButton = document.querySelector('[data-action="save-history"]');
        const historyTableWrapper = document.querySelector('[data-role="history-table-wrapper"]');
        const historyEmptyEl = document.querySelector('[data-role="history-empty"]');
        const historyBody = document.querySelector('[data-role="history-rows"]');

        const stateKey = `supplyInventory.state.${config.propertyId ?? 'default'}`;
        const MAX_HISTORY_ENTRIES = 24;
        let state = loadState();

        if (budgetInput) {
            budgetInput.value = formatNumberInput(state.monthlyBudget);
        }

        renderRows();
        updateSummary();
        renderHistory();
        updateSaveStatus('Ready');

        tableBody.addEventListener('input', handleTableInput);
        tableBody.addEventListener('change', handleTableInput);
        tableBody.addEventListener('click', handleTableClick);
        historyBody?.addEventListener('click', handleHistoryClick);

        addButton?.addEventListener('click', event => {
            event.preventDefault();
            state.items.push(buildEmptyItem());
            renderRows();
            updateSummary();
            saveState();
        });

        resetButton?.addEventListener('click', event => {
            event.preventDefault();
            if (!window.confirm('Reset the worksheet to the original supply template?')) {
                return;
            }

            const existingHistory = Array.isArray(state.history) ? state.history : [];
            state = createDefaultState();
            state.history = existingHistory;
            if (budgetInput) {
                budgetInput.value = formatNumberInput(state.monthlyBudget);
            }

            renderRows();
            updateSummary();
            renderHistory();
            saveState();
        });

        budgetInput?.addEventListener('input', () => {
            state.monthlyBudget = normalizeDecimal(budgetInput.value);
            updateSummary();
            saveState();
        });

        saveSnapshotButton?.addEventListener('click', event => {
            event.preventDefault();
            handleSaveSnapshot();
        });

        function createDefaultState() {
            const defaultBudget = typeof config.defaultBudget === 'number' && !Number.isNaN(config.defaultBudget)
                ? config.defaultBudget
                : 0;

            return {
                monthlyBudget: defaultBudget,
                items: templateItems.map(template => ({
                    id: generateId(),
                    item: template?.item ?? '',
                    description: template?.description ?? '',
                    partNumber: template?.partNumber ?? '',
                    price: normalizeDecimal(template?.price),
                    quantityPerCase: normalizeDecimal(template?.quantityPerCase),
                    inventoryCount: 0,
                    orderCaseCount: 0
                })),
                history: []
            };
        }

        function buildEmptyItem() {
            return {
                id: generateId(),
                item: '',
                description: '',
                partNumber: '',
                price: 0,
                quantityPerCase: 0,
                inventoryCount: 0,
                orderCaseCount: 0
            };
        }

        function loadState() {
            if (!supportsLocalStorage()) {
                return createDefaultState();
            }

            try {
                const raw = window.localStorage.getItem(stateKey);
                if (!raw) {
                    return createDefaultState();
                }

                const parsed = JSON.parse(raw);
                const parsedItems = Array.isArray(parsed.items) ? parsed.items : [];
                const parsedHistory = Array.isArray(parsed.history) ? parsed.history : [];

                return {
                    monthlyBudget: normalizeDecimal(parsed.monthlyBudget ?? config.defaultBudget ?? 0),
                    items: parsedItems.map(item => ({
                        id: item?.id || generateId(),
                        item: item?.item ?? '',
                        description: item?.description ?? '',
                        partNumber: item?.partNumber ?? '',
                        price: normalizeDecimal(item?.price),
                        quantityPerCase: normalizeDecimal(item?.quantityPerCase),
                        inventoryCount: normalizeDecimal(item?.inventoryCount),
                        orderCaseCount: normalizeDecimal(item?.orderCaseCount)
                    })),
                    history: parsedHistory.map(entry => ({
                        id: entry?.id || generateId(),
                        savedAt: entry?.savedAt || new Date().toISOString(),
                        monthlyBudget: normalizeDecimal(entry?.monthlyBudget),
                        totalInventoryValue: normalizeDecimal(entry?.totalInventoryValue),
                        totalOrderCost: normalizeDecimal(entry?.totalOrderCost),
                        items: Array.isArray(entry?.items)
                            ? entry.items.map(historyItem => ({
                                id: historyItem?.id || generateId(),
                                item: historyItem?.item ?? '',
                                description: historyItem?.description ?? '',
                                partNumber: historyItem?.partNumber ?? '',
                                price: normalizeDecimal(historyItem?.price),
                                quantityPerCase: normalizeDecimal(historyItem?.quantityPerCase),
                                inventoryCount: normalizeDecimal(historyItem?.inventoryCount),
                                orderCaseCount: normalizeDecimal(historyItem?.orderCaseCount)
                            }))
                            : []
                    }))
                };
            }
            catch (error) {
                console.error('Failed to load supply inventory state', error);
                return createDefaultState();
            }
        }

        function saveState() {
            if (!supportsLocalStorage()) {
                updateSaveStatus('Browser storage unavailable', true);
                return;
            }

            try {
                window.localStorage.setItem(stateKey, JSON.stringify(state));
                const timestamp = new Date().toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' });
                updateSaveStatus(`Saved ${timestamp}`);
            }
            catch (error) {
                console.error('Failed to save supply inventory state', error);
                updateSaveStatus('Unable to save to this browser', true);
            }
        }

        function handleTableInput(event) {
            const target = event.target;
            if (!target || !target.dataset.field) {
                return;
            }

            const row = target.closest('tr[data-item-id]');
            if (!row) {
                return;
            }

            const item = state.items.find(entry => entry.id === row.dataset.itemId);
            if (!item) {
                return;
            }

            const field = target.dataset.field;
            if (field === 'inventoryCount' || field === 'orderCaseCount' || field === 'price' || field === 'quantityPerCase') {
                item[field] = normalizeDecimal(target.value);
            }
            else {
                item[field] = target.value || '';
            }

            updateRowDisplays(row, item);
            updateSummary();
            saveState();
        }

        function handleTableClick(event) {
            const button = event.target.closest('[data-role="remove-item"]');
            if (!button) {
                return;
            }

            event.preventDefault();
            const row = button.closest('tr[data-item-id]');
            if (!row) {
                return;
            }

            state.items = state.items.filter(item => item.id !== row.dataset.itemId);
            renderRows();
            updateSummary();
            saveState();
        }

        function renderRows() {
            tableBody.innerHTML = '';

            if (!state.items.length) {
                const emptyRow = document.createElement('tr');
                const cell = document.createElement('td');
                cell.colSpan = 10;
                cell.className = 'text-center text-muted py-4';
                cell.textContent = 'Add items to begin tracking supply inventory.';
                emptyRow.appendChild(cell);
                tableBody.appendChild(emptyRow);
                updateItemCount();
                return;
            }

            state.items.forEach(item => {
                tableBody.appendChild(buildRow(item));
            });

            updateItemCount();
        }

        function handleSaveSnapshot() {
            if (!Array.isArray(state.items) || state.items.length === 0) {
                window.alert('Add at least one item before saving a snapshot.');
                return;
            }

            const totals = calculateTotals(state.items);
            const snapshot = {
                id: generateId(),
                savedAt: new Date().toISOString(),
                monthlyBudget: state.monthlyBudget || 0,
                totalInventoryValue: totals.totalInventoryValue,
                totalOrderCost: totals.totalOrderCost,
                items: state.items.map(cloneItemForHistory)
            };

            if (!Array.isArray(state.history)) {
                state.history = [];
            }

            state.history.unshift(snapshot);
            if (state.history.length > MAX_HISTORY_ENTRIES) {
                state.history = state.history.slice(0, MAX_HISTORY_ENTRIES);
            }

            saveState();
            renderHistory();
            updateSaveStatus('Saved snapshot');
        }

        function renderHistory() {
            if (!historyBody || !historyEmptyEl || !historyTableWrapper) {
                return;
            }

            const entries = Array.isArray(state.history) ? state.history : [];
            if (!entries.length) {
                historyEmptyEl.classList.remove('d-none');
                historyTableWrapper.classList.add('d-none');
                historyBody.innerHTML = '';
                return;
            }

            historyEmptyEl.classList.add('d-none');
            historyTableWrapper.classList.remove('d-none');
            historyBody.innerHTML = '';

            entries.forEach(entry => {
                const row = document.createElement('tr');

                const savedCell = document.createElement('td');
                savedCell.textContent = formatDateTime(entry.savedAt);
                row.appendChild(savedCell);

                const budgetCell = document.createElement('td');
                budgetCell.className = 'text-end';
                budgetCell.textContent = formatCurrency(entry.monthlyBudget);
                row.appendChild(budgetCell);

                const inventoryCell = document.createElement('td');
                inventoryCell.className = 'text-end';
                inventoryCell.textContent = formatCurrency(entry.totalInventoryValue);
                row.appendChild(inventoryCell);

                const orderCell = document.createElement('td');
                orderCell.className = 'text-end';
                orderCell.textContent = formatCurrency(entry.totalOrderCost);
                row.appendChild(orderCell);

                const actionCell = document.createElement('td');
                actionCell.className = 'text-nowrap';

                const loadButton = document.createElement('button');
                loadButton.type = 'button';
                loadButton.className = 'btn btn-link btn-sm';
                loadButton.dataset.role = 'load-history';
                loadButton.dataset.snapshotId = entry.id;
                loadButton.textContent = 'Load';
                actionCell.appendChild(loadButton);

                const deleteButton = document.createElement('button');
                deleteButton.type = 'button';
                deleteButton.className = 'btn btn-link btn-sm text-danger';
                deleteButton.dataset.role = 'delete-history';
                deleteButton.dataset.snapshotId = entry.id;
                deleteButton.textContent = 'Delete';
                actionCell.appendChild(deleteButton);

                row.appendChild(actionCell);
                historyBody.appendChild(row);
            });
        }

        function handleHistoryClick(event) {
            const button = event.target.closest('button[data-role]');
            if (!button) {
                return;
            }

            const snapshotId = button.dataset.snapshotId;
            if (!snapshotId) {
                return;
            }

            if (button.dataset.role === 'load-history') {
                event.preventDefault();
                loadSnapshot(snapshotId);
            }
            else if (button.dataset.role === 'delete-history') {
                event.preventDefault();
                deleteSnapshot(snapshotId);
            }
        }

        function loadSnapshot(snapshotId) {
            if (!Array.isArray(state.history)) {
                return;
            }

            const snapshot = state.history.find(entry => entry.id === snapshotId);
            if (!snapshot) {
                return;
            }

            state.items = (snapshot.items || []).map(item => ({
                id: item?.id || generateId(),
                item: item?.item ?? '',
                description: item?.description ?? '',
                partNumber: item?.partNumber ?? '',
                price: normalizeDecimal(item?.price),
                quantityPerCase: normalizeDecimal(item?.quantityPerCase),
                inventoryCount: normalizeDecimal(item?.inventoryCount),
                orderCaseCount: normalizeDecimal(item?.orderCaseCount)
            }));

            state.monthlyBudget = normalizeDecimal(snapshot.monthlyBudget);
            if (budgetInput) {
                budgetInput.value = formatNumberInput(state.monthlyBudget);
            }

            renderRows();
            updateSummary();
            saveState();
            updateSaveStatus('Loaded snapshot');
            window.scrollTo({ top: 0, behavior: 'smooth' });
        }

        function deleteSnapshot(snapshotId) {
            if (!Array.isArray(state.history)) {
                return;
            }

            if (!window.confirm('Delete this snapshot from your browser history?')) {
                return;
            }

            state.history = state.history.filter(entry => entry.id !== snapshotId);
            saveState();
            renderHistory();
        }

        function buildRow(item) {
            const row = document.createElement('tr');
            row.dataset.itemId = item.id;

            row.appendChild(createNumberCell('inventoryCount', item.inventoryCount, { width: '110px', min: '0', step: '1' }));
            row.appendChild(createTextCell('item', item.item, { width: '180px', maxLength: 120 }));
            row.appendChild(createTextareaCell('description', item.description));
            row.appendChild(createTextCell('partNumber', item.partNumber, { width: '140px', maxLength: 50 }));
            row.appendChild(createNumberCell('price', item.price, { step: '0.01', min: '0' }));
            row.appendChild(createDisplayCell('inventoryValue', item.inventoryCount * item.price));
            row.appendChild(createNumberCell('quantityPerCase', item.quantityPerCase, { step: '0.01', min: '0' }));
            row.appendChild(createNumberCell('orderCaseCount', item.orderCaseCount, { step: '0.01', min: '0' }));
            row.appendChild(createDisplayCell('orderCost', item.orderCaseCount * item.price));
            row.appendChild(createActionCell());

            return row;
        }

        function createTextCell(field, value, options = {}) {
            const cell = document.createElement('td');
            if (options.width) {
                cell.style.width = options.width;
            }

            const input = document.createElement('input');
            input.type = 'text';
            input.className = 'form-control form-control-sm';
            input.dataset.field = field;
            if (options.maxLength) {
                input.maxLength = options.maxLength;
            }

            input.value = value || '';
            cell.appendChild(input);
            return cell;
        }

        function createTextareaCell(field, value) {
            const cell = document.createElement('td');
            const textarea = document.createElement('textarea');
            textarea.className = 'form-control form-control-sm';
            textarea.rows = 2;
            textarea.dataset.field = field;
            textarea.value = value || '';
            cell.appendChild(textarea);
            return cell;
        }

        function createNumberCell(field, value, options = {}) {
            const cell = document.createElement('td');
            if (options.width) {
                cell.style.width = options.width;
            }

            const input = document.createElement('input');
            input.type = 'number';
            input.className = 'form-control form-control-sm';
            input.dataset.field = field;
            input.inputMode = 'decimal';
            input.step = options.step || '1';
            if (options.min !== undefined) {
                input.min = options.min;
            }

            input.value = value ? value : '';
            cell.appendChild(input);
            return cell;
        }

        function createDisplayCell(displayKey, value) {
            const cell = document.createElement('td');
            cell.className = 'text-end fw-semibold';
            const span = document.createElement('span');
            span.dataset.display = displayKey;
            span.textContent = formatCurrency(value);
            cell.appendChild(span);
            return cell;
        }

        function createActionCell() {
            const cell = document.createElement('td');
            cell.className = 'text-start';
            const button = document.createElement('button');
            button.type = 'button';
            button.className = 'btn btn-link btn-sm text-danger';
            button.dataset.role = 'remove-item';
            button.textContent = 'Remove';
            cell.appendChild(button);
            return cell;
        }

        function updateRowDisplays(row, item) {
            const inventoryDisplay = row.querySelector('[data-display="inventoryValue"]');
            if (inventoryDisplay) {
                inventoryDisplay.textContent = formatCurrency(item.inventoryCount * item.price);
            }

            const orderDisplay = row.querySelector('[data-display="orderCost"]');
            if (orderDisplay) {
                orderDisplay.textContent = formatCurrency(item.orderCaseCount * item.price);
            }
        }

        function updateItemCount() {
            if (!itemCountEl) {
                return;
            }

            const count = state.items.length;
            itemCountEl.textContent = count === 1 ? '1 item' : `${count} items`;
        }

        function updateSummary() {
            const totals = calculateTotals(state.items);
            const totalInventoryValue = totals.totalInventoryValue;
            const totalOrderCost = totals.totalOrderCost;

            const variance = (state.monthlyBudget || 0) - totalOrderCost;

            if (totalInventoryValueEl) {
                totalInventoryValueEl.textContent = formatCurrency(totalInventoryValue);
            }

            if (totalOrderCostEl) {
                totalOrderCostEl.textContent = formatCurrency(totalOrderCost);
            }

            if (varianceEl) {
                varianceEl.textContent = formatCurrency(variance);
                varianceEl.classList.remove('text-danger', 'text-success');
                if (variance < 0) {
                    varianceEl.classList.add('text-danger');
                }
                else if (variance > 0) {
                    varianceEl.classList.add('text-success');
                }
            }
        }

        function calculateTotals(items) {
            if (!Array.isArray(items)) {
                return { totalInventoryValue: 0, totalOrderCost: 0 };
            }

            return items.reduce((acc, item) => {
                const price = Number(item?.price) || 0;
                const inventoryCount = Number(item?.inventoryCount) || 0;
                const orderCaseCount = Number(item?.orderCaseCount) || 0;
                acc.totalInventoryValue += inventoryCount * price;
                acc.totalOrderCost += orderCaseCount * price;
                return acc;
            }, { totalInventoryValue: 0, totalOrderCost: 0 });
        }

        function cloneItemForHistory(item) {
            return {
                id: item?.id || generateId(),
                item: item?.item ?? '',
                description: item?.description ?? '',
                partNumber: item?.partNumber ?? '',
                price: normalizeDecimal(item?.price),
                quantityPerCase: normalizeDecimal(item?.quantityPerCase),
                inventoryCount: normalizeDecimal(item?.inventoryCount),
                orderCaseCount: normalizeDecimal(item?.orderCaseCount)
            };
        }

        function formatDateTime(value) {
            const date = new Date(value);
            if (Number.isNaN(date.getTime())) {
                return 'Unknown';
            }

            return date.toLocaleString(undefined, {
                month: 'short',
                day: 'numeric',
                year: 'numeric',
                hour: 'numeric',
                minute: '2-digit'
            });
        }

        function supportsLocalStorage() {
            try {
                const key = '__supplyInventoryTest';
                window.localStorage.setItem(key, '1');
                window.localStorage.removeItem(key);
                return true;
            }
            catch {
                return false;
            }
        }

        function normalizeDecimal(value) {
            if (typeof value === 'number') {
                return Math.round(value * 100) / 100;
            }

            const parsed = parseFloat(value);
            if (Number.isNaN(parsed)) {
                return 0;
            }

            return Math.round(parsed * 100) / 100;
        }

        function formatNumberInput(value) {
            if (!value) {
                return '';
            }

            return Number(value).toString();
        }

        function formatCurrency(value) {
            const amount = Number(value) || 0;
            return amount.toLocaleString(undefined, { style: 'currency', currency: 'USD', minimumFractionDigits: 2 });
        }

        function updateSaveStatus(message, isError) {
            if (!saveStatusEl) {
                return;
            }

            saveStatusEl.textContent = message;
            saveStatusEl.classList.toggle('text-danger', Boolean(isError));
            saveStatusEl.classList.toggle('text-success', !isError && message.toLowerCase().startsWith('saved'));
        }

        function generateId() {
            if (window.crypto?.randomUUID) {
                return window.crypto.randomUUID();
            }

            return `supply_${Math.random().toString(36).slice(2, 10)}`;
        }
    });
})();
