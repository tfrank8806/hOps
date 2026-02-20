document.addEventListener('DOMContentLoaded', () => {
    const checklistBody = document.querySelector('[data-checklist-body]');
    if (!checklistBody) {
        return;
    }

    const addButton = document.querySelector('[data-add-checklist-item]');
    const template = document.getElementById('siteVisitRowTemplate');
    let nextIndex = Number(checklistBody.getAttribute('data-next-index')) || checklistBody.querySelectorAll('[data-checklist-row]').length;

    const statusClasses = ['status-compliant', 'status-needs-review', 'status-not-compliant'];

    const updateStatusDecoration = row => {
        const select = row.querySelector('[data-status-select]');
        if (!select) {
            return;
        }

        const value = (select.value || '').toLowerCase();
        let className = 'status-compliant';
        if (value === 'needsreview') {
            className = 'status-needs-review';
        } else if (value === 'notcompliant') {
            className = 'status-not-compliant';
        }

        select.classList.remove(...statusClasses);
        select.classList.add(className);

        const dot = row.querySelector('[data-status-dot]');
        if (dot) {
            dot.classList.remove(...statusClasses);
            dot.classList.add(className);
        }
    };

    const resizeTextArea = textarea => {
        textarea.style.height = 'auto';
        const nextHeight = Math.min(Math.max(textarea.scrollHeight, 48), 320);
        textarea.style.height = `${nextHeight}px`;
    };

    const bindAutoSize = textarea => {
        resizeTextArea(textarea);
        textarea.addEventListener('input', () => resizeTextArea(textarea));
    };

    const removeRow = row => {
        const rowCount = checklistBody.querySelectorAll('[data-checklist-row]').length;
        if (rowCount <= 1) {
            const titleInput = row.querySelector('input[name*=".Title"]');
            const notesInput = row.querySelector('[data-notes-input]');
            if (titleInput) {
                titleInput.value = '';
                titleInput.focus();
            }

            const statusSelect = row.querySelector('[data-status-select]');
            if (statusSelect) {
                statusSelect.value = 'Compliant';
                updateStatusDecoration(row);
            }

            if (notesInput) {
                notesInput.value = '';
                resizeTextArea(notesInput);
            }

            return;
        }

        row.remove();
    };

    const registerRow = row => {
        const select = row.querySelector('[data-status-select]');
        if (select) {
            select.addEventListener('change', () => updateStatusDecoration(row));
            updateStatusDecoration(row);
        }

        const notesInput = row.querySelector('[data-notes-input]');
        if (notesInput) {
            bindAutoSize(notesInput);
        }

        const removeButton = row.querySelector('[data-remove-row]');
        if (removeButton) {
            removeButton.addEventListener('click', event => {
                event.preventDefault();
                removeRow(row);
            });
        }
    };

    const addRow = () => {
        if (!template) {
            return;
        }

        const html = template.innerHTML.replace(/__index__/g, nextIndex.toString());
        nextIndex += 1;

        const wrapper = document.createElement('tbody');
        wrapper.innerHTML = html.trim();
        const row = wrapper.firstElementChild;
        if (!row) {
            return;
        }

        checklistBody.appendChild(row);
        registerRow(row);

        const firstInput = row.querySelector('input[name*=".Title"]');
        if (firstInput) {
            firstInput.focus();
        }
    };

    if (addButton) {
        addButton.addEventListener('click', event => {
            event.preventDefault();
            addRow();
        });
    }

    checklistBody.querySelectorAll('[data-checklist-row]').forEach(row => registerRow(row));
});
