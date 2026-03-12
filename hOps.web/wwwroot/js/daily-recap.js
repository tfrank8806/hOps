(() => {
    const dayInput = document.querySelector('[data-day-source]');
    const dayTarget = document.querySelector('[data-day-target]');

    const updateDayOfWeek = () => {
        if (!dayInput || !dayTarget) {
            return;
        }

        const value = dayInput.value;
        if (!value) {
            dayTarget.value = '';
            return;
        }

        const date = new Date(value);
        if (Number.isNaN(date.getTime())) {
            dayTarget.value = '';
            return;
        }

        dayTarget.value = date.toLocaleDateString(undefined, { weekday: 'long' });
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
