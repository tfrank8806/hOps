(() => {
    const dayInput = document.querySelector('[data-day-source]');
    const dayTarget = document.querySelector('[data-day-target]');
    const dayNames = ["Sunday", "Monday", "Tuesday", "Wednesday", "Thursday", "Friday", "Saturday"];
    const translate = (key, fallback) => {
        if (typeof window !== "undefined" && typeof window.hopsTranslate === "function") {
            return window.hopsTranslate(key, fallback);
        }

        return fallback;
    };

    const parseDateValue = (value) => {
        const parts = value.split("-").map(Number);
        if (parts.length !== 3 || parts.some(Number.isNaN)) {
            return null;
        }

        const [year, month, day] = parts;
        return new Date(year, month - 1, day);
    };

    const updateDayOfWeek = () => {
        if (!dayInput || !dayTarget) {
            return;
        }

        const value = dayInput.value;
        if (!value) {
            dayTarget.value = '';
            return;
        }

        const date = parseDateValue(value);
        if (!date || Number.isNaN(date.getTime())) {
            dayTarget.value = '';
            return;
        }

        const dayKey = dayNames[date.getDay()];
        dayTarget.value = dayKey ? translate(dayKey, dayKey) : '';
    };

    dayInput?.addEventListener('change', updateDayOfWeek);
    updateDayOfWeek();

    const templates = new Map();
    document.querySelectorAll('template[data-collection-template]').forEach(template => {
        const key = template.dataset.collectionTemplate;
        if (key) {
            templates.set(key, template.innerHTML.trim());
        }
    });

    const getContainer = (key) => document.querySelector(`[data-collection="${key}"]`);

    const addRow = (key) => {
        const container = getContainer(key);
        const template = templates.get(key);
        if (!container || !template) {
            return;
        }

        const nextIndex = parseInt(container.dataset.nextIndex ?? container.children.length, 10) || 0;
        container.dataset.nextIndex = (nextIndex + 1).toString();

        const html = template.replace(/__index__/g, nextIndex);
        const wrapper = document.createElement('tbody');
        wrapper.innerHTML = html;
        const row = wrapper.firstElementChild;
        if (row) {
            container.appendChild(row);
        }
    };

    document.addEventListener('click', (event) => {
        const addButton = event.target.closest('[data-collection-add]');
        if (addButton) {
            event.preventDefault();
            addRow(addButton.dataset.collectionAdd);
            return;
        }

        const removeButton = event.target.closest('[data-collection-remove]');
        if (removeButton) {
            event.preventDefault();
            const row = removeButton.closest('[data-collection-row]');
            row?.remove();
        }
    });
})();
