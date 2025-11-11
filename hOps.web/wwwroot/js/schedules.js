(() => {
    const templateSelect = document.getElementById('shiftTemplateSelect');
    if (!templateSelect) {
        return;
    }

    const shiftNameInput = document.querySelector('input[name="AssignmentForm.ShiftName"]');
    const shiftStartInput = document.querySelector('input[name="AssignmentForm.ShiftStartTime"]');
    const shiftEndInput = document.querySelector('input[name="AssignmentForm.ShiftEndTime"]');

    templateSelect.addEventListener('change', () => {
        const selected = templateSelect.selectedOptions[0];
        if (!selected) {
            return;
        }

        const templateName = selected.getAttribute('data-name') ?? '';
        const templateStart = selected.getAttribute('data-start');
        const templateEnd = selected.getAttribute('data-end');

        if (shiftNameInput) {
            shiftNameInput.value = templateName;
        }
        if (shiftStartInput && templateStart) {
            shiftStartInput.value = templateStart;
        }
        if (shiftEndInput && templateEnd) {
            shiftEndInput.value = templateEnd;
        }
    });
})();
