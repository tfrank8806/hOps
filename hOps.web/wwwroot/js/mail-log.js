(() => {
    function showAlert(type, message) {
        const container = document.getElementById('mailLogAlertContainer');
        if (!container) {
            return;
        }

        container.innerHTML = '';

        const alert = document.createElement('div');
        alert.className = `alert alert-${type} alert-dismissible fade show`;
        alert.setAttribute('role', 'alert');
        alert.textContent = message;

        const closeButton = document.createElement('button');
        closeButton.type = 'button';
        closeButton.className = 'btn-close';
        closeButton.setAttribute('data-bs-dismiss', 'alert');
        closeButton.setAttribute('aria-label', 'Close');

        alert.appendChild(closeButton);
        container.appendChild(alert);
    }

    function updateRow(row, payload) {
        if (!row) {
            return;
        }

        const badge = row.querySelector('.delivered-badge');
        if (badge) {
            badge.textContent = payload.delivered ? 'Yes' : 'No';
            badge.classList.remove('text-bg-success', 'text-bg-secondary');
            badge.classList.add(payload.delivered ? 'text-bg-success' : 'text-bg-secondary');
        }

        const deliveredCell = row.querySelector('[data-col="deliveredAt"]');
        if (deliveredCell) {
            deliveredCell.textContent = payload.deliveredAt ?? '-';
        }

        const form = row.querySelector('.mail-log-toggle-form');
        if (form) {
            const hidden = form.querySelector('input[name="delivered"]');
            if (hidden) {
                hidden.value = payload.delivered ? 'false' : 'true';
            }

            const button = form.querySelector('.toggle-delivered-button');
            if (button) {
                button.disabled = false;
                button.textContent = payload.delivered ? 'Mark Pending' : 'Mark Delivered';
                button.classList.remove('btn-outline-success', 'btn-outline-warning');
                button.classList.add(payload.delivered ? 'btn-outline-warning' : 'btn-outline-success');
            }
        }
    }

    function handleError(row, button, error) {
        if (button) {
            button.disabled = false;
            const original = button.dataset.originalText;
            if (original) {
                button.textContent = original;
                delete button.dataset.originalText;
            } else {
                const currentState = button.classList.contains('btn-outline-warning');
                button.textContent = currentState ? 'Mark Pending' : 'Mark Delivered';
            }
        }

        const message = error?.message || 'Unable to update the package status. Please try again.';
        showAlert('danger', message);
    }

    document.addEventListener('DOMContentLoaded', () => {
        const forms = document.querySelectorAll('.mail-log-toggle-form');
        forms.forEach(form => {
            form.addEventListener('submit', async (event) => {
                event.preventDefault();

                const row = form.closest('tr');
                const button = form.querySelector('.toggle-delivered-button');

                if (button) {
                    button.dataset.originalText = button.textContent.trim();
                    button.disabled = true;
                    button.textContent = 'Saving...';
                }

                try {
                    const formData = new FormData(form);
                    const response = await fetch(form.action, {
                        method: 'POST',
                        body: formData,
                        headers: {
                            'X-Requested-With': 'XMLHttpRequest'
                        }
                    });

                    if (!response.ok) {
                        throw new Error('Server returned an unexpected status.');
                    }

                    const payload = await response.json();
                    if (!payload?.success) {
                        throw new Error(payload?.message || 'Unable to update the package status.');
                    }

                    updateRow(row, payload);
                    if (button) {
                        delete button.dataset.originalText;
                    }
                    showAlert('success', payload.message || 'Package status updated.');
                } catch (error) {
                    handleError(row, button, error);
                }
            });
        });
    });
})();
