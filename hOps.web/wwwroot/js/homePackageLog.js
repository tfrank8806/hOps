(function () {
    const WIDGET_SELECTOR = '[data-package-log-widget]';
    const LIST_SELECTOR = '[data-package-log-list]';
    const EMPTY_SELECTOR = '[data-package-log-empty]';
    const STORAGE_KEY = 'hops.mailLog.v1';
    const MAX_ITEMS = 5;

    const widget = document.querySelector(WIDGET_SELECTOR);
    if (!widget) {
        return;
    }

    const fallbackCount = Number(widget.dataset.fallbackCount ?? '0');
    if (!Number.isFinite(fallbackCount) || fallbackCount > 0) {
        // Server-side data already rendered; nothing to do.
        return;
    }

    if (!isStorageAvailable('localStorage')) {
        return;
    }

    const listElement = widget.querySelector(LIST_SELECTOR);
    const emptyState = widget.querySelector(EMPTY_SELECTOR);

    if (!listElement || !emptyState) {
        return;
    }

    const entries = loadEntries().slice(0, MAX_ITEMS);
    if (!entries.length) {
        emptyState.hidden = false;
        return;
    }

    listElement.innerHTML = '';
    entries.forEach((entry) => {
        listElement.appendChild(renderEntry(entry));
    });

    emptyState.hidden = true;

    function isStorageAvailable(type) {
        try {
            const storage = window[type];
            const testKey = '__storage_test__';
            storage.setItem(testKey, '1');
            storage.removeItem(testKey);
            return true;
        } catch {
            return false;
        }
    }

    function loadEntries() {
        try {
            const raw = window.localStorage.getItem(STORAGE_KEY);
            if (!raw) {
                return [];
            }

            const parsed = JSON.parse(raw);
            if (!Array.isArray(parsed)) {
                return [];
            }

            return parsed
                .map(normalizeEntry)
                .filter((entry) => entry !== null)
                .sort((a, b) => b.createdAt.getTime() - a.createdAt.getTime());
        } catch {
            return [];
        }
    }

    function normalizeEntry(entry) {
        if (typeof entry !== 'object' || entry === null) {
            return null;
        }

        const createdAt = parseDate(entry.createdAt);
        if (!createdAt) {
            return null;
        }

        return {
            createdAt,
            guestName: sanitize(entry.guestName),
            courier: sanitize(entry.courier),
            trackingNumber: sanitize(entry.trackingNumber),
            roomNumber: sanitize(entry.roomNumber),
            storageLocation: sanitize(entry.storageLocation),
            notes: sanitize(entry.notes),
        };
    }

    function sanitize(value) {
        if (typeof value !== 'string') {
            return '';
        }
        return value.trim();
    }

    function parseDate(value) {
        if (typeof value !== 'string') {
            return null;
        }
        const date = new Date(value);
        if (Number.isNaN(date.getTime())) {
            return null;
        }
        return date;
    }

    function renderEntry(entry) {
        const listItem = document.createElement('li');
        listItem.className = 'widget-item';

        const title = document.createElement('span');
        title.className = 'fw-semibold';
        title.textContent = entry.guestName || entry.trackingNumber || entry.courier || 'Package Entry';
        listItem.appendChild(title);

        const metaParts = [];
        if (entry.courier) {
            metaParts.push(entry.courier);
        }
        if (entry.trackingNumber) {
            metaParts.push(entry.trackingNumber);
        }
        if (entry.roomNumber) {
            metaParts.push(`Room ${entry.roomNumber}`);
        }
        if (entry.storageLocation) {
            metaParts.push(entry.storageLocation);
        }

        if (metaParts.length) {
            const meta = document.createElement('div');
            meta.className = 'text-muted small';
            meta.textContent = metaParts.join(' · ');
            listItem.appendChild(meta);
        }

        const loggedText = document.createElement('div');
        loggedText.className = 'text-muted small';
        loggedText.textContent = `Logged ${formatDateTime(entry.createdAt)}`;
        listItem.appendChild(loggedText);

        if (entry.notes) {
            const notes = document.createElement('div');
            notes.className = 'text-muted small';
            notes.textContent = entry.notes;
            listItem.appendChild(notes);
        }

        return listItem;
    }

    function formatDateTime(date) {
        try {
            return new Intl.DateTimeFormat(undefined, {
                month: 'short',
                day: 'numeric',
                year: 'numeric',
                hour: 'numeric',
                minute: '2-digit',
            }).format(date);
        } catch {
            return date.toLocaleString();
        }
    }
})();
