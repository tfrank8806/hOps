(() => {
    const templateSelect = document.getElementById('shiftTemplateSelect');
    const addForm = document.getElementById('addShiftForm');
    const shiftNameInput = addForm ? addForm.querySelector('input[name="AssignmentForm.ShiftName"]') : null;
    const shiftStartInput = addForm ? addForm.querySelector('input[name="AssignmentForm.ShiftStartTime"]') : null;
    const shiftEndInput = addForm ? addForm.querySelector('input[name="AssignmentForm.ShiftEndTime"]') : null;
    const shiftColorInput = document.getElementById('shiftColorInput');
    const employeeSelect = addForm ? addForm.querySelector('select[name="AssignmentForm.ScheduleEmployeeId"]') : null;
    const dateInput = addForm ? addForm.querySelector('input[name="AssignmentForm.ShiftDate"]') : null;
    const repeatDayInputs = addForm ? Array.from(addForm.querySelectorAll('[data-repeat-day]')) : [];
    const weekdayNames = ['Sunday', 'Monday', 'Tuesday', 'Wednesday', 'Thursday', 'Friday', 'Saturday'];

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

    const getDayIndexFromIso = (isoDate) => {
        if (!isoDate) {
            return null;
        }
        const parsed = new Date(`${isoDate}T00:00:00`);
        if (Number.isNaN(parsed.getTime())) {
            return null;
        }
        return parsed.getUTCDay();
    };

    const setRepeatDaysForDate = (isoDate) => {
        if (!repeatDayInputs.length || !isoDate) {
            return;
        }
        const dayIndex = getDayIndexFromIso(isoDate);
        if (dayIndex === null || !weekdayNames[dayIndex]) {
            return;
        }
        const targetValue = weekdayNames[dayIndex];
        repeatDayInputs.forEach(input => {
            input.checked = input.value === targetValue;
        });
    };

    const ensureRepeatDaySelection = () => {
        if (!repeatDayInputs.length) {
            return;
        }
        if (repeatDayInputs.some(input => input.checked)) {
            return;
        }
        if (dateInput?.value) {
            setRepeatDaysForDate(dateInput.value);
            return;
        }
        repeatDayInputs[0].checked = true;
    };

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
        if (dateInput?.value) {
            setRepeatDaysForDate(dateInput.value);
        }

        addForm.addEventListener('submit', () => {
            ensureRepeatDaySelection();
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
        setRepeatDaysForDate(isoDate);
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

    const editModalEl = document.getElementById('editShiftModal');
    const editForm = document.getElementById('editShiftForm');
    const editButtons = document.querySelectorAll('.edit-shift-btn');

    if (editModalEl && editForm && editButtons.length) {
        const editAssignmentIdInput = editForm.querySelector('input[name="AssignmentForm.AssignmentId"]');
        const editEmployeeSelect = editForm.querySelector('select[name="AssignmentForm.ScheduleEmployeeId"]');
        const editDateInput = editForm.querySelector('input[name="AssignmentForm.ShiftDate"]');
        const editShiftNameInput = editForm.querySelector('input[name="AssignmentForm.ShiftName"]');
        const editStartInput = editForm.querySelector('input[name="AssignmentForm.ShiftStartTime"]');
        const editEndInput = editForm.querySelector('input[name="AssignmentForm.ShiftEndTime"]');
        const editNotesInput = editForm.querySelector('textarea[name="AssignmentForm.Notes"]');
        const editColorInput = editForm.querySelector('input[name="AssignmentForm.ShiftColorHex"]');
        const editMeta = editModalEl.querySelector('[data-edit-shift-meta]');
        const editModalTitle = document.getElementById('editShiftModalLabel');

        const populateEditForm = (button) => {
            if (!button || !(button instanceof HTMLElement)) {
                return;
            }
            if (editAssignmentIdInput) {
                editAssignmentIdInput.value = button.dataset.assignmentId || '';
            }
            if (editEmployeeSelect) {
                editEmployeeSelect.value = button.dataset.employeeId || '';
            }
            if (editDateInput) {
                editDateInput.value = button.dataset.shiftDate || '';
            }
            if (editShiftNameInput) {
                editShiftNameInput.value = button.dataset.shiftName || '';
            }
            if (editStartInput) {
                editStartInput.value = button.dataset.shiftStart || '';
            }
            if (editEndInput) {
                editEndInput.value = button.dataset.shiftEnd || '';
            }
            if (editNotesInput) {
                editNotesInput.value = button.dataset.notes || '';
            }
            if (editColorInput) {
                editColorInput.value = button.dataset.color || '';
            }

            const shiftName = button.dataset.shiftName || 'Shift';
            const employeeName = button.dataset.employeeName || '';
            const dateLabel = button.dataset.dateLabel || '';
            const metaParts = [];
            if (employeeName) {
                metaParts.push(employeeName);
            }
            if (dateLabel) {
                metaParts.push(dateLabel);
            }
            if (editMeta) {
                editMeta.textContent = metaParts.join(' • ');
            }
            if (editModalTitle) {
                editModalTitle.textContent = shiftName.trim() ? `Edit ${shiftName}` : 'Edit Shift';
            }
        };

        editButtons.forEach(button => {
            button.addEventListener('click', () => populateEditForm(button));
        });

        editModalEl.addEventListener('hidden.bs.modal', () => {
            editForm.reset();
            if (editMeta) {
                editMeta.textContent = '';
            }
            if (editModalTitle) {
                editModalTitle.textContent = 'Edit Shift';
            }
        });
    }

    const scheduleGrid = document.querySelector('[data-schedule-grid="true"]');
    if (scheduleGrid && scheduleGrid.dataset.canReorder === 'true') {
        initEmployeeReorder(scheduleGrid);
    }

    function initEmployeeReorder(grid) {
        const tbody = grid.querySelector('tbody');
        if (!tbody) {
            return;
        }

        const reorderUrl = grid.dataset.reorderUrl || '';
        const scheduleId = Number(grid.dataset.scheduleId || '0');
        const weekStart = grid.dataset.weekStart || '';
        const statusElement = document.getElementById('scheduleReorderStatus');

        const rowSelector = '[data-schedule-row]';
        const rows = () => Array.from(tbody.querySelectorAll(rowSelector));

        let dragRow = null;

        rows().forEach(row => {
            row.addEventListener('dragstart', event => {
                if (!event.target.closest('[data-row-handle]')) {
                    event.preventDefault();
                    return;
                }
                dragRow = row;
                row.classList.add('schedule-row--dragging');
                if (event.dataTransfer) {
                    event.dataTransfer.effectAllowed = 'move';
                    event.dataTransfer.setData('text/plain', row.dataset.scheduleEmployeeId ?? '');
                }
            });

            row.addEventListener('dragend', () => {
                row.classList.remove('schedule-row--dragging');
                dragRow = null;
            });
        });

        tbody.addEventListener('dragover', event => {
            if (!dragRow) {
                return;
            }
            event.preventDefault();
            const targetRow = event.target.closest(rowSelector);
            if (!targetRow || targetRow === dragRow) {
                return;
            }
            const rect = targetRow.getBoundingClientRect();
            const shouldInsertBefore = (event.clientY - rect.top) < rect.height / 2;
            if (shouldInsertBefore) {
                tbody.insertBefore(dragRow, targetRow);
            } else {
                tbody.insertBefore(dragRow, targetRow.nextSibling);
            }
        });

        tbody.addEventListener('drop', event => {
            if (!dragRow) {
                return;
            }
            event.preventDefault();
            dragRow.classList.remove('schedule-row--dragging');
            dragRow = null;
            persistEmployeeOrder();
        });

        const persistEmployeeOrder = debounce(() => {
            saveEmployeeOrder();
        }, 200);

        async function saveEmployeeOrder() {
            if (!reorderUrl || !scheduleId) {
                return;
            }

            const employeeIds = rows()
                .map(row => Number(row.dataset.scheduleEmployeeId))
                .filter(id => Number.isInteger(id) && id > 0);

            if (!employeeIds.length) {
                return;
            }

            setReorderStatus('Saving order…', 'text-muted');

            try {
                const token = getScheduleAntiforgeryToken();
                const response = await fetch(reorderUrl, {
                    method: 'POST',
                    headers: {
                        'Content-Type': 'application/json',
                        'RequestVerificationToken': token || ''
                    },
                    body: JSON.stringify({
                        scheduleId,
                        weekStart,
                        employeeIds
                    })
                });

                if (!response.ok) {
                    throw new Error('Request failed');
                }

                setReorderStatus('Order saved.', 'text-success');
            } catch (error) {
                console.error('Unable to save employee order', error);
                setReorderStatus('Unable to save order. Please refresh and try again.', 'text-danger');
            }
        }

        function setReorderStatus(message, className) {
            if (!statusElement) {
                return;
            }
            statusElement.textContent = message;
            statusElement.classList.remove('d-none', 'text-muted', 'text-success', 'text-danger');
            if (className) {
                statusElement.classList.add(className);
            }
        }
    }

    function getScheduleAntiforgeryToken() {
        const form = document.getElementById('scheduleAntiforgeryForm');
        return form?.querySelector('input[name="__RequestVerificationToken"]')?.value ?? '';
    }

    function debounce(callback, wait) {
        let timerId;
        return function debounced(...args) {
            window.clearTimeout(timerId);
            timerId = window.setTimeout(() => callback.apply(this, args), wait);
        };
    }
})();
