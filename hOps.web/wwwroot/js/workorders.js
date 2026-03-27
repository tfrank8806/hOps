(() => {
    const modalEl = document.getElementById('workOrderDetailsModal');
    if (!modalEl || typeof bootstrap === 'undefined') {
        return;
    }

    const modal = bootstrap.Modal.getOrCreateInstance(modalEl);
    const rowDataCache = new WeakMap();
    const attachmentsModalEl = document.getElementById('workOrderAttachmentsModal');
    const attachmentsModal = attachmentsModalEl ? bootstrap.Modal.getOrCreateInstance(attachmentsModalEl) : null;
    const attachmentsModalBody = attachmentsModalEl?.querySelector('[data-attachments-modal-body]');
    const attachmentsModalEmpty = attachmentsModalEl?.querySelector('[data-attachments-modal-empty]');
    const attachmentsModalTitle = attachmentsModalEl?.querySelector('[data-attachments-modal-title]');
        const fields = {
            title: modalEl.querySelector('[data-workorder-field="title"]'),
            meta: modalEl.querySelector('[data-workorder-field="meta"]'),
            issue: modalEl.querySelector('[data-workorder-field="issue"]'),
            details: modalEl.querySelector('[data-workorder-field="details"]'),
            completionNotes: modalEl.querySelector('[data-workorder-field="completionNotes"]'),
            location: modalEl.querySelector('[data-workorder-field="location"]'),
            department: modalEl.querySelector('[data-workorder-field="department"]'),
            assignee: modalEl.querySelector('[data-workorder-field="assignee"]'),
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
    const completionNotesSection = modalEl.querySelector('[data-completion-notes-container]');

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
            if (!row) {
                return;
            }
            const action = trigger.dataset.workorderTrigger;
            if (action === 'attachments') {
                showAttachmentPreview(row);
            } else {
                showRowDetails(row);
            }
        });
    });

    const detailTriggers = document.querySelectorAll('.js-workorder-details');
    detailTriggers.forEach(trigger => {
        const row = trigger.closest('.workorder-row');
        if (!row) {
            return;
        }

        bootstrap.Popover.getOrCreateInstance(trigger, {
            trigger: 'hover focus',
            html: true,
            container: 'body',
            placement: 'auto',
            customClass: 'workorder-details-popover',
            content: () => buildDetailsPopoverContent(row)
        });
    });

    function showRowDetails(row) {
        const data = getRowData(row);
        if (!data) {
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

        if (fields.completionNotes && completionNotesSection) {
            const notesHtml = data.completionNotesHtml || '';
            const notesText = data.completionNotes || '';
            const hasNotes = Boolean((notesHtml && notesHtml.trim()) || (notesText && notesText.trim()));
            completionNotesSection.classList.toggle('d-none', !hasNotes);
            if (hasNotes) {
                if (notesHtml) {
                    fields.completionNotes.innerHTML = notesHtml;
                } else {
                    fields.completionNotes.textContent = notesText;
                }
            } else {
                fields.completionNotes.textContent = '';
            }
        }

        setText(fields.location, data.location);
        setText(fields.department, data.department, 'Unassigned');
        setText(fields.assignee, data.assignedTo, 'Unassigned');
        setText(fields.dueDate, data.dueDateText);
        setText(fields.created, data.createdAtText);
        setText(fields.creator, data.creator, 'Unknown');
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

    function showAttachmentPreview(row) {
        if (!attachmentsModal || !attachmentsModalBody || !attachmentsModalEmpty) {
            showRowDetails(row);
            return;
        }

        const data = getRowData(row);
        if (!data) {
            return;
        }

        const attachments = Array.isArray(data.attachments) ? data.attachments.filter(att => att && att.url) : [];
        if (attachments.length === 0) {
            showRowDetails(row);
            return;
        }

        attachmentsModalBody.innerHTML = '';
        attachmentsModalTitle.textContent = data.issue || 'Work Order Attachments';

        attachments.forEach(att => {
            const col = document.createElement('div');
            col.className = 'col-12 col-sm-6';

            if (att.isImage) {
                const link = document.createElement('a');
                link.href = att.url;
                link.target = '_blank';
                link.rel = 'noopener';
                link.className = 'd-block';

                const img = document.createElement('img');
                img.src = att.url;
                img.alt = att.name || 'Attachment preview';
                img.className = 'img-fluid rounded shadow-sm';
                img.loading = 'lazy';

                link.appendChild(img);
                col.appendChild(link);
            } else {
                const card = document.createElement('div');
                card.className = 'border rounded p-3 h-100 d-flex flex-column gap-2';

                const title = document.createElement('div');
                title.className = 'fw-semibold';
                title.textContent = att.name || 'Attachment';
                card.appendChild(title);

                const link = document.createElement('a');
                link.href = att.url;
                link.target = '_blank';
                link.rel = 'noopener';
                link.className = 'btn btn-sm btn-outline-primary align-self-start';
                link.textContent = 'Open File';
                card.appendChild(link);

                col.appendChild(card);
            }

            attachmentsModalBody.appendChild(col);
        });

        attachmentsModalEmpty.classList.toggle('d-none', attachmentsModalBody.children.length > 0);
        attachmentsModal.show();
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

    function getRowData(row) {
        if (!row) {
            return null;
        }

        if (rowDataCache.has(row)) {
            return rowDataCache.get(row);
        }

        const payloadRaw = row.dataset.workorder;
        if (!payloadRaw) {
            rowDataCache.set(row, null);
            return null;
        }

        try {
            const data = JSON.parse(payloadRaw);
            rowDataCache.set(row, data);
            return data;
        } catch {
            rowDataCache.set(row, null);
            return null;
        }
    }

    function buildDetailsPopoverContent(row) {
        const data = getRowData(row);
        if (!data) {
            return '<div class="text-muted small">No details available.</div>';
        }

        const html = data.detailsHtml || (data.details ? escapeHtml(data.details) : '');
        if (!html) {
            return '<div class="text-muted small">No details available.</div>';
        }

        return `<div class="workorder-details-popover-content">${html}</div>`;
    }
})();

(() => {
    const containers = document.querySelectorAll('[data-searchable-select]');
    if (!containers.length) {
        return;
    }

    containers.forEach(container => {
        const input = container.querySelector('[data-searchable-select-input]');
        const select = container.querySelector('[data-searchable-select-target]');
        if (!input || !select) {
            return;
        }

        const options = Array.from(select.options).filter(option => option.value);

        const resetVisibility = () => {
            options.forEach(option => {
                option.hidden = false;
            });
        };

        const handleInput = () => {
            const term = (input.value || '').trim().toLowerCase();
            if (!term) {
                resetVisibility();
                return;
            }

            options.forEach(option => {
                option.hidden = !option.text.toLowerCase().includes(term);
            });
        };

        input.addEventListener('input', handleInput);
        input.addEventListener('keydown', event => {
            if (event.key === 'Escape') {
                input.value = '';
                resetVisibility();
            }
        });
        input.addEventListener('blur', () => {
            if (!input.value) {
                resetVisibility();
            }
        });
    });
})();

(() => {
    if (typeof bootstrap === 'undefined') {
        return;
    }

    const completionModalEl = document.getElementById('completeWorkOrderModal');
    if (!completionModalEl) {
        return;
    }

    const completionNotesInput = completionModalEl.querySelector('[data-complete-notes-input]');
    const completionSubmitBtn = completionModalEl.querySelector('[data-complete-submit]');
    const completionModal = bootstrap.Modal.getOrCreateInstance(completionModalEl);
    let pendingCompletionForm = null;
    let pendingCompletionSelect = null;
    let pendingPreviousValue = null;
    let completionConfirmed = false;

    const statusSelects = document.querySelectorAll('.workorder-status-select');
    statusSelects.forEach(select => {
        select.dataset.currentValue = select.value;
        select.addEventListener('change', event => handleStatusChange(event, select));
    });

    document.querySelectorAll('[data-complete-button]').forEach(button => {
        button.addEventListener('click', event => {
            event.preventDefault();
            const form = button.closest('form');
            if (!form) {
                return;
            }
            openCompletionModal(form, null);
        });
    });

    completionSubmitBtn?.addEventListener('click', () => {
        if (!pendingCompletionForm) {
            completionModal.hide();
            return;
        }

        const notesField = pendingCompletionForm.querySelector('[data-completion-notes]');
        if (notesField) {
            notesField.value = completionNotesInput?.value.trim() ?? '';
        }

        completionConfirmed = true;
        if (pendingCompletionSelect) {
            pendingCompletionSelect.dataset.currentValue = pendingCompletionSelect.value;
        }

        const formToSubmit = pendingCompletionForm;
        completionModal.hide();
        pendingCompletionForm = null;
        pendingCompletionSelect = null;
        pendingPreviousValue = null;
        formToSubmit.submit();
    });

    completionModalEl.addEventListener('hidden.bs.modal', () => {
        if (!completionConfirmed && pendingCompletionSelect && pendingPreviousValue !== null) {
            pendingCompletionSelect.value = pendingPreviousValue;
        }

        completionConfirmed = false;
        pendingCompletionForm = null;
        pendingCompletionSelect = null;
        pendingPreviousValue = null;
        if (completionNotesInput) {
            completionNotesInput.value = '';
        }
    });

    function handleStatusChange(event, select) {
        const form = select.closest('form');
        if (!form) {
            return;
        }

        const selectedValue = (select.value || '').toLowerCase();
        if (selectedValue === 'completed') {
            event.preventDefault();
            openCompletionModal(form, select);
        } else {
            select.dataset.currentValue = select.value;
            const notesField = form.querySelector('[data-completion-notes]');
            if (notesField) {
                notesField.value = '';
            }
            form.submit();
        }
    }

    function openCompletionModal(form, select) {
        const hiddenNotes = form.querySelector('[data-completion-notes]');
        if (completionNotesInput) {
            completionNotesInput.value = hiddenNotes?.value || '';
        }
        pendingCompletionForm = form;
        pendingCompletionSelect = select || null;
        pendingPreviousValue = select ? (select.dataset.currentValue || select.value) : null;
        completionModal.show();
    }
})();

(() => {
    const list = document.querySelector('[data-additional-locations]');
    const addButton = document.querySelector('[data-add-location-btn]');
    const template = document.getElementById('additionalLocationTemplate');

    if (!list || !addButton || !template) {
        return;
    }

    const updateInputNames = () => {
        const entries = list.querySelectorAll('.additional-location-entry');
        entries.forEach((entry, index) => {
            const input = entry.querySelector('input');
            if (input) {
                input.name = `Form.AdditionalLocations[${index}]`;
                input.id = `Form_AdditionalLocations_${index}`;
            }
        });
    };

    const createEntryElement = (value = '') => {
        let element = null;
        if (template.content && template.content.firstElementChild) {
            element = template.content.firstElementChild.cloneNode(true);
        } else if (template.firstElementChild) {
            element = template.firstElementChild.cloneNode(true);
        }

        if (!element) {
            return null;
        }

        const input = element.querySelector('input');
        if (input) {
            input.value = value;
        }

        return element;
    };

    const addEntry = (value = '') => {
        const entry = createEntryElement(value);
        if (!entry) {
            return;
        }

        list.appendChild(entry);
        updateInputNames();
        const input = entry.querySelector('input');
        input?.focus();
    };

    list.addEventListener('click', event => {
        const removeButton = event.target.closest('[data-remove-additional-location]');
        if (!removeButton) {
            return;
        }

        event.preventDefault();
        const entry = removeButton.closest('.additional-location-entry');
        entry?.remove();
        updateInputNames();
    });

    addButton.addEventListener('click', event => {
        event.preventDefault();
        addEntry();
    });
})();
