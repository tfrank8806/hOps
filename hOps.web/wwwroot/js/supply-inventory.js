(function () {
    document.addEventListener('DOMContentLoaded', () => {
        const config = window.supplyInventoryConfig;
        const tableBody = document.getElementById('supplyInventoryRows');
        if (!config || !tableBody) {
            return;
        }

        const translate = typeof window.hopsTranslate === 'function'
            ? window.hopsTranslate
            : (key, fallback) => (typeof fallback === 'string' && fallback.length ? fallback : key);

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

        const propertyId = Number(config.propertyId) || 0;
        const endpoints = config.endpoints || {};
        const MAX_HISTORY_ENTRIES = 24;
        let state = createDefaultState();
        let saveTimeoutId = null;
        let saveInProgress = false;
        let saveQueued = false;

        if (budgetInput) {
            budgetInput.value = formatNumberInput(state.monthlyBudget);
        }

        renderRows();
        updateSummary();
        renderHistory();
        updateSaveStatus(translate('SupplyInventory.Status.Loading', 'Loading saved data...'));
        hydrateFromServer();

        tableBody.addEventListener('input', handleTableInput);
        tableBody.addEventListener('change', handleTableInput);
        tableBody.addEventListener('click', handleTableClick);
        historyBody?.addEventListener('click', handleHistoryClick);

        addButton?.addEventListener('click', event => {
            event.preventDefault();
            state.items.push(buildEmptyItem());
            renderRows();
            updateSummary();
            scheduleSave();
        });

        resetButton?.addEventListener('click', event => {
            event.preventDefault();
            if (!window.confirm(translate('SupplyInventory.Dialog.ResetTemplate', 'Reset the worksheet to the original supply template?'))) {
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
            scheduleSave(true);
        });

        budgetInput?.addEventListener('input', () => {
            state.monthlyBudget = normalizeDecimal(budgetInput.value);
            updateSummary();
            scheduleSave();
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

        async function hydrateFromServer() {
            if (!endpoints.load || !propertyId) {
                updateSaveStatus(translate('SupplyInventory.Status.Ready', 'Ready'));
                return;
            }

            try {
                const response = await fetch(`${endpoints.load}?propertyId=${encodeURIComponent(propertyId)}`, {
                    headers: {
                        'Accept': 'application/json',
                        'X-Requested-With': 'XMLHttpRequest'
                    }
                });

                if (!response.ok) {
                    throw new Error(translate('SupplyInventory.Error.LoadFailed', 'Unable to load saved data.'));
                }

                const payload = await response.json();
                if (payload?.state) {
                    state.monthlyBudget = normalizeDecimal(payload.state.monthlyBudget ?? state.monthlyBudget);
                    state.items = Array.isArray(payload.state.items) && payload.state.items.length
                        ? payload.state.items.map(normalizeServerItem)
                        : state.items;

                    if (budgetInput) {
                        budgetInput.value = formatNumberInput(state.monthlyBudget);
                    }
                }

                const historyEntries = Array.isArray(payload?.history)
                    ? payload.history
                        .map(normalizeSnapshotEntry)
                        .filter(entry => entry !== null)
                    : [];

                state.history = historyEntries;

                renderRows();
                updateSummary();
                renderHistory();
                updateSaveStatus(translate('SupplyInventory.Status.Ready', 'Ready'));
            }
            catch (error) {
                console.error('Unable to load supply inventory state', error);
                updateSaveStatus(translate('SupplyInventory.Error.LoadFailedShort', 'Unable to load saved data'), true);
            }
        }

        function scheduleSave(immediate = false) {
            if (!endpoints.save || !propertyId) {
                return;
            }

            if (immediate) {
                if (saveTimeoutId) {
                    window.clearTimeout(saveTimeoutId);
                    saveTimeoutId = null;
                }
                persistStateToServer();
                return;
            }

            if (saveTimeoutId) {
                window.clearTimeout(saveTimeoutId);
            }

            saveTimeoutId = window.setTimeout(() => {
                saveTimeoutId = null;
                persistStateToServer();
            }, 750);
        }

        async function persistStateToServer() {
            if (saveInProgress) {
                saveQueued = true;
                return;
            }

            saveInProgress = true;
            saveQueued = false;
            updateSaveStatus(translate('SupplyInventory.Status.Saving', 'Saving to server...'));

            try {
                const response = await postJson(endpoints.save, buildSavePayload());
                if (!response?.success) {
                    throw new Error(response?.message || translate('SupplyInventory.Error.SaveFailed', 'Unable to save changes.'));
                }

                const timestamp = new Date().toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' });
                const savedMessage = translate('SupplyInventory.Status.SavedAt', 'Saved {0}').replace('{0}', timestamp);
                updateSaveStatus(savedMessage, false, true);
            }
            catch (error) {
                console.error('Failed to save supply inventory state', error);
                updateSaveStatus(error?.message || translate('SupplyInventory.Error.SaveRefresh', 'Unable to save changes. Please refresh.'), true);
            }
            finally {
                saveInProgress = false;
                if (saveQueued) {
                    persistStateToServer();
                }
            }
        }

        function buildSavePayload() {
            return {
                propertyId,
                monthlyBudget: state.monthlyBudget || 0,
                items: Array.isArray(state.items)
                    ? state.items.map(item => ({
                        id: item?.id || generateId(),
                        item: item?.item ?? '',
                        description: item?.description ?? '',
                        partNumber: item?.partNumber ?? '',
                        price: normalizeDecimal(item?.price),
                        quantityPerCase: normalizeDecimal(item?.quantityPerCase),
                        inventoryCount: normalizeDecimal(item?.inventoryCount),
                        orderCaseCount: normalizeDecimal(item?.orderCaseCount)
                    }))
                    : []
            };
        }

        function normalizeServerItem(item) {
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

        function normalizeSnapshotEntry(entry) {
            if (!entry) {
                return null;
            }

            return {
                id: entry.id?.toString() || generateId(),
                savedAt: entry.savedAt || entry.savedAtUtc || new Date().toISOString(),
                monthlyBudget: normalizeDecimal(entry.monthlyBudget),
                totalInventoryValue: normalizeDecimal(entry.totalInventoryValue),
                totalOrderCost: normalizeDecimal(entry.totalOrderCost),
                items: Array.isArray(entry.items)
                    ? entry.items.map(normalizeServerItem)
                    : []
            };
        }

        function getAntiforgeryToken() {
            return document.querySelector('#supplyInventoryAntiforgery input[name="__RequestVerificationToken"]')?.value ?? '';
        }

        async function postJson(url, payload) {
            if (!url) {
            throw new Error(translate('SupplyInventory.Error.EndpointMissing', 'Endpoint missing.'));
            }

            const response = await fetch(url, {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json',
                    'RequestVerificationToken': getAntiforgeryToken(),
                    'X-Requested-With': 'XMLHttpRequest'
                },
                body: JSON.stringify(payload)
            });

            if (!response.ok) {
                throw new Error(translate('SupplyInventory.Error.RequestFailed', 'Request failed.'));
            }

            return response.json();
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
            scheduleSave();
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
            scheduleSave();
        }

        function renderRows() {
            tableBody.innerHTML = '';

            if (!state.items.length) {
                const emptyRow = document.createElement('tr');
                const cell = document.createElement('td');
                cell.colSpan = 10;
                cell.className = 'text-center text-muted py-4';
                cell.textContent = translate('SupplyInventory.EmptyState.Table', 'Add items to begin tracking supply inventory.');
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

        async function handleSaveSnapshot() {
            if (!Array.isArray(state.items) || state.items.length === 0) {
                window.alert(translate('SupplyInventory.Dialog.SnapshotRequiresItem', 'Add at least one item before saving a snapshot.'));
                return;
            }

            if (!endpoints.snapshot || !propertyId) {
                window.alert(translate('SupplyInventory.Dialog.SnapshotEndpointMissing', 'Snapshot endpoint not configured.'));
                return;
            }

            try {
                const response = await postJson(endpoints.snapshot, buildSavePayload());
                if (!response?.success || !response.snapshot) {
                    throw new Error(response?.message || translate('SupplyInventory.Error.SnapshotSave', 'Unable to save snapshot.'));
                }

                const snapshotEntry = normalizeSnapshotEntry(response.snapshot);
                if (!snapshotEntry) {
                    throw new Error(translate('SupplyInventory.Error.SnapshotInvalid', 'Snapshot response was invalid.'));
                }

                if (!Array.isArray(state.history)) {
                    state.history = [];
                }

                state.history.unshift(snapshotEntry);
                if (state.history.length > MAX_HISTORY_ENTRIES) {
                    state.history = state.history.slice(0, MAX_HISTORY_ENTRIES);
                }

                renderHistory();
                updateSaveStatus(translate('SupplyInventory.Status.SnapshotSaved', 'Snapshot saved'), false, true);
            }
            catch (error) {
                console.error('Unable to save snapshot', error);
                window.alert(error?.message || translate('SupplyInventory.Error.SnapshotSaveRetry', 'Unable to save snapshot. Please try again.'));
            }
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
                loadButton.textContent = translate('SupplyInventory.Button.LoadSnapshot', 'Load');
                actionCell.appendChild(loadButton);

                const deleteButton = document.createElement('button');
                deleteButton.type = 'button';
                deleteButton.className = 'btn btn-link btn-sm text-danger';
                deleteButton.dataset.role = 'delete-history';
                deleteButton.dataset.snapshotId = entry.id;
                deleteButton.textContent = translate('SupplyInventory.Button.DeleteSnapshot', 'Delete');
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

            const snapshot = state.history.find(entry => entry.id?.toString() === snapshotId?.toString());
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
            scheduleSave(true);
            updateSaveStatus(translate('SupplyInventory.Status.SnapshotLoaded', 'Loaded snapshot'), false, true);
            window.scrollTo({ top: 0, behavior: 'smooth' });
        }

        async function deleteSnapshot(snapshotId) {
            if (!Array.isArray(state.history)) {
                return;
            }

            if (!endpoints.deleteSnapshot || !propertyId) {
                return;
            }

            if (!window.confirm(translate('SupplyInventory.Dialog.DeleteSnapshotConfirm', 'Delete this snapshot from history?'))) {
                return;
            }

            try {
                const response = await postJson(endpoints.deleteSnapshot, {
                    propertyId,
                    snapshotId: Number(snapshotId)
                });

                if (!response?.success) {
                    throw new Error(response?.message || translate('SupplyInventory.Error.SnapshotDelete', 'Unable to delete snapshot.'));
                }

                const targetId = snapshotId?.toString() ?? '';
                state.history = state.history.filter(entry => (entry?.id?.toString() ?? '') !== targetId);
                renderHistory();
            }
            catch (error) {
                console.error('Unable to delete snapshot', error);
                window.alert(error?.message || translate('SupplyInventory.Error.SnapshotDeleteRetry', 'Unable to delete snapshot. Please try again.'));
            }
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
            button.textContent = translate('SupplyInventory.Button.RemoveRow', 'Remove');
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
            const itemCountText = count === 1
                ? translate('SupplyInventory.Template.SingleItem', '1 item')
                : translate('SupplyInventory.Template.MultipleItems', '{0} items').replace('{0}', count);
            itemCountEl.textContent = itemCountText;
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

        function formatDateTime(value) {
            const date = new Date(value);
            if (Number.isNaN(date.getTime())) {
                return translate('SupplyInventory.Label.Unknown', 'Unknown');
            }

            return date.toLocaleString(undefined, {
                month: 'short',
                day: 'numeric',
                year: 'numeric',
                hour: 'numeric',
                minute: '2-digit'
            });
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

        function updateSaveStatus(message, isError, isSuccess) {
            if (!saveStatusEl) {
                return;
            }

            saveStatusEl.textContent = message;
            saveStatusEl.classList.toggle('text-danger', Boolean(isError));
            saveStatusEl.classList.toggle('text-success', Boolean(isSuccess));
        }

        function generateId() {
            if (window.crypto?.randomUUID) {
                return window.crypto.randomUUID();
            }

            return `supply_${Math.random().toString(36).slice(2, 10)}`;
        }
    });
})();
