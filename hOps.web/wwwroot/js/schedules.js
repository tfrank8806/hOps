(() => {
    const templateSelect = document.getElementById('shiftTemplateSelect');
    const shiftNameInput = document.querySelector('input[name="AssignmentForm.ShiftName"]');
    const shiftStartInput = document.querySelector('input[name="AssignmentForm.ShiftStartTime"]');
    const shiftEndInput = document.querySelector('input[name="AssignmentForm.ShiftEndTime"]');
    const shiftColorInput = document.getElementById('shiftColorInput');

    const addForm = document.getElementById('addShiftForm');
    const employeeSelect = addForm ? addForm.querySelector('select[name="AssignmentForm.ScheduleEmployeeId"]') : null;
    const dateInput = addForm ? addForm.querySelector('input[name="AssignmentForm.ShiftDate"]') : null;

    const clipboardStatus = document.getElementById('scheduleClipboardStatus');
    const pasteForm = document.getElementById('pasteShiftForm');
    const pasteSourceInput = pasteForm ? pasteForm.querySelector('input[name="sourceAssignmentId"]') : null;
    const pasteEmployeeInput = pasteForm ? pasteForm.querySelector('input[name="targetEmployeeId"]') : null;
    const pasteDateInput = pasteForm ? pasteForm.querySelector('input[name="targetDate"]') : null;

    let clipboard = null;

    function updateClipboardStatus() {
        if (!clipboard || !clipboardStatus) {
            clipboardStatus?.classList.add('d-none');
            return;
        }

        clipboardStatus.classList.remove('d-none');
        clipboardStatus.textContent = `Shift copied: ${clipboard.label}. Click another cell to paste.`;
    }

    function getSelectedTemplateOption(select) {
        if (!select) {
            return null;
        }

        if (select.selectedOptions && select.selectedOptions.length > 0) {
            return select.selectedOptions[0];
        }

        const index = typeof select.selectedIndex === 'number' ? select.selectedIndex : -1;
        return index >= 0 ? select.options[index] : null;
    }

    function applyTemplate(option, force = false) {
        if (!option) {
            if (shiftColorInput) {
                shiftColorInput.value = '';
            }
            return;
        }

        const templateShiftName = option.getAttribute('data-shift-name');
        const templateStart = option.getAttribute('data-start');
        const templateEnd = option.getAttribute('data-end');
        const templateColor = option.getAttribute('data-color');

        if (shiftNameInput && templateShiftName && (force || !shiftNameInput.value.trim())) {
            shiftNameInput.value = templateShiftName;
        }
        if (shiftStartInput && templateStart) {
            shiftStartInput.value = templateStart;
        }
        if (shiftEndInput && templateEnd) {
            shiftEndInput.value = templateEnd;
        }
        if (shiftColorInput && templateColor) {
            shiftColorInput.value = templateColor;
        }
    }

    if (templateSelect) {
        templateSelect.addEventListener('change', () => {
            const option = getSelectedTemplateOption(templateSelect);
            if (option && option.value) {
                applyTemplate(option, true);
            } else if (shiftColorInput) {
                shiftColorInput.value = '';
            }
        });

        if (templateSelect.value) {
            const initialOption = getSelectedTemplateOption(templateSelect);
            if (initialOption) {
                applyTemplate(initialOption);
            }
        }
    }

    if (addForm) {
        addForm.addEventListener('submit', () => {
            const option = getSelectedTemplateOption(templateSelect);
            if (option && option.value && shiftNameInput && !shiftNameInput.value.trim()) {
                applyTemplate(option);
            }
        });
    }

    function setAddFormTarget(employeeId, isoDate) {
        if (!addForm || !employeeSelect || !dateInput) {
            return;
        }

        employeeSelect.value = employeeId;
        employeeSelect.dispatchEvent(new Event('change'));
        dateInput.value = isoDate;
        addForm.scrollIntoView({ behavior: 'smooth', block: 'start' });
        shiftNameInput?.focus();
    }

    document.querySelectorAll('.copy-shift-btn').forEach(button => {
        button.addEventListener('click', event => {
            event.preventDefault();
            clipboard = {
                assignmentId: button.getAttribute('data-assignment-id'),
                label: button.getAttribute('data-shift-label') ?? 'Shift'
            };
            updateClipboardStatus();
        });
    });

    function submitPaste(targetCell) {
        if (!clipboard || !pasteForm || !pasteSourceInput || !pasteEmployeeInput || !pasteDateInput) {
            return;
        }

        const employeeId = targetCell.getAttribute('data-employee-id');
        const targetDate = targetCell.getAttribute('data-date');
        if (!employeeId || !targetDate) {
            return;
        }

        pasteSourceInput.value = clipboard.assignmentId ?? '';
        pasteEmployeeInput.value = employeeId;
        pasteDateInput.value = targetDate;
        pasteForm.submit();
    }

    document.querySelectorAll('[data-schedule-cell="true"]').forEach(cell => {
        cell.addEventListener('click', event => {
            if (!clipboard) {
                return;
            }
            if (event.target.closest('button, form, a')) {
                return;
            }
            submitPaste(cell);
        });

        cell.addEventListener('dblclick', event => {
            if (!addForm) {
                return;
            }
            event.preventDefault();
            event.stopPropagation();
            const employeeId = cell.getAttribute('data-employee-id');
            const targetDate = cell.getAttribute('data-date');
            if (employeeId && targetDate) {
                setAddFormTarget(employeeId, targetDate);
            }
        });
    });

    updateClipboardStatus();
})();
