document.addEventListener('DOMContentLoaded', () => {
    const launcher = document.getElementById('messagesLauncher');
    const panel = document.getElementById('messagesFloatingPanel');
    const closeButton = document.getElementById('messagesFloatingClose');
    const backdrop = document.getElementById('messagesFloatingBackdrop');

    if (!launcher || !panel || !closeButton || !backdrop) {
        return;
    }

    let lastFocusedElement = null;

    function openPanel() {
        lastFocusedElement = document.activeElement instanceof HTMLElement ? document.activeElement : null;
        panel.classList.add('messages-floating-panel--open');
        panel.setAttribute('aria-hidden', 'false');
        launcher.setAttribute('aria-expanded', 'true');
        backdrop.classList.add('messages-floating-backdrop--visible');
        closeButton.focus();
    }

    function closePanel() {
        panel.classList.remove('messages-floating-panel--open');
        panel.setAttribute('aria-hidden', 'true');
        launcher.setAttribute('aria-expanded', 'false');
        backdrop.classList.remove('messages-floating-backdrop--visible');
        if (lastFocusedElement && document.body.contains(lastFocusedElement)) {
            lastFocusedElement.focus();
        } else {
            launcher.focus();
        }
    }

    function togglePanel() {
        const isOpen = panel.classList.contains('messages-floating-panel--open');
        if (isOpen) {
            closePanel();
        } else {
            openPanel();
        }
    }

    launcher.addEventListener('click', togglePanel);
    closeButton.addEventListener('click', closePanel);
    backdrop.addEventListener('click', closePanel);

    document.addEventListener('keydown', event => {
        if (event.key === 'Escape' && panel.classList.contains('messages-floating-panel--open')) {
            closePanel();
        }
    });
});

document.addEventListener('DOMContentLoaded', () => {
    const multiselects = document.querySelectorAll('select.js-checkbox-multiselect');
    if (!multiselects.length) {
        return;
    }

    multiselects.forEach(select => {
        if (select.dataset.checkboxMultiselect === 'true') {
            return;
        }

        select.dataset.checkboxMultiselect = 'true';
        select.multiple = true;

        const placeholder = select.dataset.placeholder || 'Select options';
        const allLabel = select.dataset.allLabel || 'All';

        const wrapper = document.createElement('div');
        wrapper.className = 'dropdown filter-multiselect w-100';

        const button = document.createElement('button');
        button.type = 'button';
        button.className = 'filter-multiselect__toggle';
        button.dataset.bsToggle = 'dropdown';
        button.dataset.bsAutoClose = 'outside';
        button.setAttribute('aria-expanded', 'false');

        const labelSpan = document.createElement('span');
        labelSpan.className = 'filter-multiselect__label';
        button.appendChild(labelSpan);

        const caret = document.createElement('span');
        caret.className = 'filter-multiselect__caret';
        caret.setAttribute('aria-hidden', 'true');
        caret.innerHTML = '&#9662;';
        button.appendChild(caret);

        const menu = document.createElement('div');
        menu.className = 'dropdown-menu filter-multiselect__menu';
        menu.addEventListener('click', event => event.stopPropagation());

        const optionList = Array.from(select.options)
            .filter(option => !option.disabled)
            .filter(option => option.value !== '');

        const optionMap = new Map(optionList.map(option => [option.value, option]));

        const selectedValues = new Set(
            optionList
                .filter(option => option.selected)
                .map(option => option.value)
        );

        select.classList.add('d-none');
        select.setAttribute('tabindex', '-1');

        select.parentElement?.insertBefore(wrapper, select);
        wrapper.appendChild(button);
        wrapper.appendChild(menu);
        wrapper.appendChild(select);

        const createCheckbox = (labelText, value, isChecked = false) => {
            const idBase = select.id || `multiselect_${Math.random().toString(36).slice(2, 8)}`;
            const checkboxId = `${idBase}_${Math.random().toString(36).slice(2, 6)}`;

            const formCheck = document.createElement('div');
            formCheck.className = 'form-check';

            const input = document.createElement('input');
            input.type = 'checkbox';
            input.className = 'form-check-input';
            input.id = checkboxId;
            input.value = value;
            input.checked = isChecked;

            const label = document.createElement('label');
            label.className = 'form-check-label';
            label.setAttribute('for', checkboxId);
            label.textContent = labelText;

            formCheck.appendChild(input);
            formCheck.appendChild(label);
            menu.appendChild(formCheck);

            return input;
        };

        const allCheckbox = createCheckbox(allLabel, '__all__', selectedValues.size === 0);
        allCheckbox.dataset.allOption = 'true';

        const optionCheckboxes = optionList.map(option => {
            const isChecked = selectedValues.has(option.value);
            return createCheckbox(option.textContent?.trim() || option.value, option.value, isChecked);
        });

        const updateSelections = () => {
            optionMap.forEach(option => {
                option.selected = false;
            });

            const activeValues = optionCheckboxes
                .filter(checkbox => checkbox.checked)
                .map(checkbox => checkbox.value);

            if (activeValues.length === 0) {
                allCheckbox.checked = true;
            } else {
                allCheckbox.checked = false;
                activeValues.forEach(value => {
                    const option = optionMap.get(value);
                    if (option) {
                        option.selected = true;
                    }
                });
            }

            const selectedLabels = activeValues
                .map(value => optionMap.get(value)?.textContent?.trim())
                .filter(label => Boolean(label));

            if (selectedLabels.length === 0) {
                labelSpan.textContent = placeholder;
                button.title = placeholder;
            } else if (selectedLabels.length === 1) {
                labelSpan.textContent = selectedLabels[0];
                button.title = selectedLabels[0];
            } else {
                labelSpan.textContent = `${selectedLabels.length} selected`;
                button.title = selectedLabels.join(', ');
            }
        };

        allCheckbox.addEventListener('change', () => {
            if (allCheckbox.checked) {
                optionCheckboxes.forEach(checkbox => {
                    checkbox.checked = false;
                });
            }
            updateSelections();
        });

        optionCheckboxes.forEach(checkbox => {
            checkbox.addEventListener('change', () => {
                if (checkbox.checked) {
                    allCheckbox.checked = false;
                } else if (!optionCheckboxes.some(cb => cb.checked)) {
                    allCheckbox.checked = true;
                }
                updateSelections();
            });
        });

        updateSelections();
    });
});

document.addEventListener('DOMContentLoaded', () => {
    const today = new Date();
    const pad = value => value.toString().padStart(2, '0');
    const todayValue = `${today.getFullYear()}-${pad(today.getMonth() + 1)}-${pad(today.getDate())}`;

    document.querySelectorAll('input[type="date"]').forEach(input => {
        if (!input || input.value) {
            return;
        }

        input.value = todayValue;
        input.dispatchEvent(new Event('change', { bubbles: true }));
    });
});
