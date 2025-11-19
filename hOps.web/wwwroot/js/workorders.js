(() => {
    const modalEl = document.getElementById('workOrderDetailsModal');
    if (!modalEl || typeof bootstrap === 'undefined') {
        return;
    }

    const modal = bootstrap.Modal.getOrCreateInstance(modalEl);
    const fields = {
        title: modalEl.querySelector('[data-workorder-field="title"]'),
        meta: modalEl.querySelector('[data-workorder-field="meta"]'),
        issue: modalEl.querySelector('[data-workorder-field="issue"]'),
        details: modalEl.querySelector('[data-workorder-field="details"]'),
        location: modalEl.querySelector('[data-workorder-field="location"]'),
        department: modalEl.querySelector('[data-workorder-field="department"]'),
        dueDate: modalEl.querySelector('[data-workorder-field="dueDate"]'),
        created: modalEl.querySelector('[data-workorder-field="created"]'),
        creator: modalEl.querySelector('[data-workorder-field="creator"]'),
        properties: modalEl.querySelector('[data-workorder-field="properties"]'),
        status: modalEl.querySelector('[data-workorder-field="status"]'),
        type: modalEl.querySelector('[data-workorder-field="type"]')
    };
    const attachmentsSection = modalEl.querySelector('[data-workorder-attachments-section]');
    const attachmentsContainer = modalEl.querySelector('[data-workorder-attachments]');
    const attachmentsEmpty = modalEl.querySelector('[data-workorder-attachments-empty]');

    const rows = document.querySelectorAll('.workorder-row[data-workorder]');
    rows.forEach(row => {
        if (!row.hasAttribute('tabindex')) {
            row.tabIndex = 0;
        }

        row.addEventListener('click', evt => {
            if (isInteractiveTarget(evt.target)) {
                return;
            }
            showRowDetails(row);
        });

        row.addEventListener('keydown', evt => {
            if (evt.key === 'Enter' || evt.key === ' ') {
                evt.preventDefault();
                showRowDetails(row);
            }
        });
    });

    document.querySelectorAll('[data-workorder-trigger]').forEach(trigger => {
        trigger.addEventListener('click', evt => {
            evt.preventDefault();
            evt.stopPropagation();
            const row = trigger.closest('.workorder-row');
            if (row) {
                showRowDetails(row);
            }
        });
    });

    function showRowDetails(row) {
        const payloadRaw = row.dataset.workorder;
        if (!payloadRaw) {
            return;
        }

        let data;
        try {
            data = JSON.parse(payloadRaw);
        } catch {
            return;
        }

        populateModal(data);
        modal.show();
    }

    function populateModal(data) {
        setText(fields.title, data.issue || 'Work Order');

        if (fields.issue) {
            fields.issue.innerHTML = data.issueHtml || escapeHtml(data.issue || 'No issue provided.');
        }

        if (fields.details) {
            if (data.detailsHtml) {
                fields.details.innerHTML = data.detailsHtml;
            } else if (data.details) {
                fields.details.textContent = data.details;
            } else {
                fields.details.innerHTML = '<span class="text-muted">No additional details provided.</span>';
            }
        }

        setText(fields.location, data.location);
        setText(fields.department, data.department);
        setText(fields.dueDate, data.dueDateText);
        setText(fields.created, data.createdAtText);
        setText(fields.creator, data.creator, '');
        setBadge(fields.status, data.status, data.statusColor);
        setBadge(fields.type, data.workOrderType, '', true);

        if (fields.properties) {
            const props = Array.isArray(data.properties) && data.properties.length
                ? data.properties.join(', ')
                : 'Not assigned to a property yet.';
            fields.properties.textContent = props;
        }

        if (fields.meta) {
            const metaParts = [];
            if (data.creator) {
                metaParts.push(`Created by ${data.creator}`);
            }
            if (data.createdAtText) {
                metaParts.push(data.createdAtText);
            }
            fields.meta.textContent = metaParts.join(' • ');
        }

        renderAttachments(Array.isArray(data.attachments) ? data.attachments : []);
    }

    function renderAttachments(attachments) {
        if (!attachmentsContainer) {
            return;
        }

        attachmentsContainer.innerHTML = '';
        const hasAttachments = attachments.length > 0;
        attachmentsSection?.classList.remove('d-none');

        if (!hasAttachments) {
            if (attachmentsEmpty) {
                attachmentsEmpty.classList.remove('d-none');
            }
            return;
        }

        attachmentsEmpty?.classList.add('d-none');

        attachments.forEach(att => {
            const col = document.createElement('div');
            col.className = 'col-12 col-sm-6';

            if (att.isImage && att.url) {
                const figure = document.createElement('figure');
                figure.className = 'mb-0';

                const img = document.createElement('img');
                img.src = att.url;
                img.alt = att.name || 'Attachment preview';
                img.className = 'workorder-attachment-img';
                img.loading = 'lazy';

                figure.appendChild(img);
                col.appendChild(figure);
            } else {
                const fileCard = document.createElement('div');
                fileCard.className = 'file-card border rounded p-3 h-100 d-flex flex-column gap-2';

                const icon = document.createElement('div');
                icon.className = 'text-primary';
                icon.innerHTML = '<svg xmlns="http://www.w3.org/2000/svg" width="20" height="20" viewBox="0 0 16 16" aria-hidden="true"><path fill="currentColor" d="M6.354 13.657a3.5 3.5 0 0 1-4.95-4.95l6.718-6.718a2.5 2.5 0 0 1 3.536 3.536L5.621 11.612a1.5 1.5 0 1 1-2.121-2.121l5.303-5.304a.5.5 0 1 1 .708.708L4.208 10.2a.5.5 0 0 0 .707.707l5.303-5.303a1.5 1.5 0 1 0-2.121-2.121L1.379 10.5a2.5 2.5 0 1 0 3.536 3.536l5.657-5.657a.5.5 0 0 1 .707.708l-5.657 5.657Z"/></svg>';
                fileCard.appendChild(icon);

                const nameEl = document.createElement('div');
                nameEl.className = 'fw-semibold';
                nameEl.textContent = att.name || 'Attachment';
                fileCard.appendChild(nameEl);

                col.appendChild(fileCard);
            }

            if (att.url) {
                const actions = document.createElement('div');
                actions.className = 'mt-2';

                const openLink = document.createElement('a');
                openLink.href = att.url;
                openLink.target = '_blank';
                openLink.rel = 'noopener';
                openLink.className = 'btn btn-sm btn-outline-primary';
                openLink.textContent = 'Open File';

                actions.appendChild(openLink);
                col.appendChild(actions);
            }

            attachmentsContainer.appendChild(col);
        });
    }

    function isInteractiveTarget(target) {
        return Boolean(target.closest('a, button, form, input, textarea, select, label, .workorder-action-group'));
    }

    function setText(el, value, fallback = '—') {
        if (!el) {
            return;
        }
        el.textContent = value ? value : fallback;
    }

    function setBadge(el, text, color, outlineOnly = false) {
        if (!el) {
            return;
        }
        setText(el, text);
        if (outlineOnly) {
            el.classList.add('text-dark', 'bg-light', 'border');
            el.style.backgroundColor = '';
            el.style.color = '';
            return;
        }

        if (color) {
            el.style.backgroundColor = color;
            el.style.color = '#fff';
        } else {
            el.style.backgroundColor = '';
            el.style.color = '';
        }
    }

    function escapeHtml(text) {
        const div = document.createElement('div');
        div.textContent = text || '';
        return div.innerHTML;
    }
})();
