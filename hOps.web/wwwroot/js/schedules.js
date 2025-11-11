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

    function applyTemplate(option) {
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

        if (shiftNameInput && templateShiftName) {
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
            const option = templateSelect.selectedOptions[0];
            if (option && option.value) {
                applyTemplate(option);
            } else if (shiftColorInput) {
                shiftColorInput.value = '';
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
