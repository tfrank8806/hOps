(function () {
    const storageKey = 'hops.mailLog.v1';
    const form = document.getElementById('mailLogForm');
    const tableBody = document.getElementById('mailLogTableBody');
    const storageMessage = document.getElementById('mailLogStorageMessage');

    if (!form || !tableBody) {
        return;
    }

    const storageEnabled = isStorageAvailable('localStorage');
    if (!storageEnabled && storageMessage) {
        storageMessage.classList.remove('text-muted');
        storageMessage.classList.add('text-warning');
        storageMessage.textContent = 'Local storage is unavailable. Entries will be cleared when you refresh or leave this page.';
    }

    let entries = loadEntries();

    renderEntries();

    form.addEventListener('submit', (event) => {
        event.preventDefault();

        const entry = {
            id: generateId(),
            createdAt: new Date().toISOString(),
            guestName: sanitizeInput(form.guestName?.value),
            courier: sanitizeInput(form.courier?.value),
            trackingNumber: sanitizeInput(form.trackingNumber?.value),
            roomNumber: sanitizeInput(form.roomNumber?.value),
            arrivalDate: sanitizeInput(form.arrivalDate?.value),
            departureDate: sanitizeInput(form.departureDate?.value),
            storageLocation: sanitizeInput(form.storageLocation?.value),
            notes: sanitizeInput(form.notes?.value),
            delivered: false,
            deliveredAt: null
        };

        entries.unshift(entry);
        persistEntries();
        renderEntries();
        form.reset();
        form.querySelector('input, textarea')?.focus();
    });

    tableBody.addEventListener('change', (event) => {
        const target = event.target;
        if (!(target instanceof HTMLInputElement) || target.dataset.role !== 'deliveredToggle') {
            return;
        }

        const entryId = target.dataset.entryId;
        if (!entryId) {
            return;
        }

        updateDeliveryStatus(entryId, target.checked);
    });

    tableBody.addEventListener('click', (event) => {
        const target = event.target;
        if (!(target instanceof HTMLElement)) {
            return;
        }

        const button = target.closest('[data-action="deleteEntry"]');
        if (!(button instanceof HTMLElement)) {
            return;
        }

        const entryId = button.dataset.entryId;
        if (!entryId) {
            return;
        }

        deleteEntry(entryId);
    });

    function loadEntries() {
        if (!storageEnabled) {
            return [];
        }

        try {
            const stored = window.localStorage.getItem(storageKey);
            if (!stored) {
                return [];
            }

            const parsed = JSON.parse(stored);
            if (!Array.isArray(parsed)) {
                return [];
            }

            return parsed
                .map(normalizeEntry)
                .filter((entry) => entry !== null)
                .sort((a, b) => new Date(b.createdAt).getTime() - new Date(a.createdAt).getTime());
        } catch (error) {
            console.warn('Unable to load package log entries from storage', error);
            return [];
        }
    }

    function persistEntries() {
        if (!storageEnabled) {
            return;
        }

        try {
            window.localStorage.setItem(storageKey, JSON.stringify(entries));
        } catch (error) {
            console.warn('Unable to save package log entries', error);
        }
    }

    function normalizeEntry(entry) {
        if (!entry || typeof entry !== 'object') {
            return null;
        }

        const createdAt = typeof entry.createdAt === 'string' && !Number.isNaN(new Date(entry.createdAt).getTime())
            ? entry.createdAt
            : new Date().toISOString();

        const deliveredAt = typeof entry.deliveredAt === 'string' && !Number.isNaN(new Date(entry.deliveredAt).getTime())
            ? entry.deliveredAt
            : null;

        return {
            id: typeof entry.id === 'string' && entry.id.trim().length ? entry.id : generateId(),
            createdAt,
            guestName: sanitizeInput(entry.guestName),
            courier: sanitizeInput(entry.courier),
            trackingNumber: sanitizeInput(entry.trackingNumber),
            roomNumber: sanitizeInput(entry.roomNumber),
            arrivalDate: sanitizeInput(entry.arrivalDate),
            departureDate: sanitizeInput(entry.departureDate),
            storageLocation: sanitizeInput(entry.storageLocation),
            notes: sanitizeInput(entry.notes),
            delivered: Boolean(entry.delivered),
            deliveredAt
        };
    }

    function renderEntries() {
        tableBody.innerHTML = '';

        if (!entries.length) {
            const emptyRow = document.createElement('tr');
            const emptyCell = document.createElement('td');
            emptyCell.colSpan = 12;
            emptyCell.className = 'text-center text-muted py-4';
            emptyCell.textContent = 'No packages logged yet. Use the form above to record the first delivery.';
            emptyRow.appendChild(emptyCell);
            tableBody.appendChild(emptyRow);
            return;
        }

        entries.forEach((entry) => {
            const row = document.createElement('tr');
            row.dataset.entryId = entry.id;

            row.appendChild(createCell(formatDateTime(entry.createdAt)));
            row.appendChild(createCell(entry.guestName));
            row.appendChild(createCell(entry.courier));
            row.appendChild(createCell(entry.trackingNumber));
            row.appendChild(createCell(entry.roomNumber));
            row.appendChild(createCell(formatDate(entry.arrivalDate)));
            row.appendChild(createCell(formatDate(entry.departureDate)));
            row.appendChild(createCell(entry.storageLocation));
            row.appendChild(createCell(entry.notes, 'notes-cell'));

            const deliveredCell = document.createElement('td');
            deliveredCell.className = 'text-center delivered-cell';
            const checkbox = document.createElement('input');
            checkbox.type = 'checkbox';
            checkbox.className = 'form-check-input';
            checkbox.dataset.role = 'deliveredToggle';
            checkbox.dataset.entryId = entry.id;
            checkbox.checked = entry.delivered;
            checkbox.setAttribute('aria-label', 'Mark package as delivered or picked up');
            deliveredCell.appendChild(checkbox);
            row.appendChild(deliveredCell);

            row.appendChild(createCell(entry.delivered ? formatDateTime(entry.deliveredAt) : '—', 'delivered-at-cell'));

            const actionsCell = document.createElement('td');
            actionsCell.className = 'text-end actions-cell';
            const deleteButton = document.createElement('button');
            deleteButton.type = 'button';
            deleteButton.className = 'btn btn-outline-danger btn-sm';
            deleteButton.dataset.action = 'deleteEntry';
            deleteButton.dataset.entryId = entry.id;
            deleteButton.textContent = 'Remove';
            actionsCell.appendChild(deleteButton);
            row.appendChild(actionsCell);

            tableBody.appendChild(row);
        });
    }

    function updateDeliveryStatus(entryId, delivered) {
        const entry = entries.find((item) => item.id === entryId);
        if (!entry) {
            return;
        }

        entry.delivered = delivered;
        entry.deliveredAt = delivered ? new Date().toISOString() : null;
        persistEntries();
        renderEntries();
    }

    function deleteEntry(entryId) {
        const index = entries.findIndex((item) => item.id === entryId);
        if (index === -1) {
            return;
        }

        entries.splice(index, 1);
        persistEntries();
        renderEntries();
    }

    function sanitizeInput(value) {
        if (typeof value !== 'string') {
            return '';
        }

        return value.trim();
    }

    function generateId() {
        return `mail_${Date.now()}_${Math.random().toString(16).slice(2)}`;
    }

    function formatDate(value) {
        if (!value) {
            return '—';
        }

        if (typeof value !== 'string') {
            return '—';
        }

        const parts = value.split('-');
        if (parts.length !== 3) {
            return '—';
        }

        const year = Number(parts[0]);
        const month = Number(parts[1]) - 1;
        const day = Number(parts[2]);

        if ([year, month, day].some((component) => Number.isNaN(component))) {
            return '—';
        }

        const date = new Date(year, month, day);
        if (Number.isNaN(date.getTime())) {
            return '—';
        }

        return date.toLocaleDateString(undefined, { year: 'numeric', month: 'short', day: 'numeric' });
    }

    function formatDateTime(value) {
        if (!value) {
            return '—';
        }

        const date = new Date(value);
        if (Number.isNaN(date.getTime())) {
            return '—';
        }

        return date.toLocaleString(undefined, { dateStyle: 'medium', timeStyle: 'short' });
    }

    function createCell(value, className) {
        const cell = document.createElement('td');
        if (className) {
            cell.className = className;
        }
        cell.textContent = value && value.trim ? value.trim() : value || '—';
        return cell;
    }

    function isStorageAvailable(type) {
        try {
            const storage = window[type];
            const testKey = '__storage_test__';
            storage.setItem(testKey, testKey);
            storage.removeItem(testKey);
            return true;
        } catch (error) {
            return false;
        }
    }
})();
