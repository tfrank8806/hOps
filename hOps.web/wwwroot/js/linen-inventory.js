(function () {
    document.addEventListener('DOMContentLoaded', () => {
        configureInventoryTable();
        configureCollectionControls();
    });

    function configureInventoryTable() {
        const rows = document.querySelectorAll('.inventory-entry-row');
        const budgetInput = document.querySelector('input[name="Entry.MonthlyBudget"]');
        const summaryBudget = document.querySelector('[data-summary="budget-amount"]');

        const updateSummary = () => {
            let needTotal = 0;
            let orderTotal = 0;

            document.querySelectorAll('[data-role="need-cost"]').forEach(span => {
                const value = parseCurrency(span.textContent);
                needTotal += isNaN(value) ? 0 : value;
            });

            document.querySelectorAll('[data-role="order-cost"]').forEach(span => {
                const value = parseCurrency(span.textContent);
                orderTotal += isNaN(value) ? 0 : value;
            });

            const budgetValue = parseFloat((budgetInput && budgetInput.value) || '0') || 0;

            setCurrency('[data-summary="need-cost"]', needTotal);
            setCurrency('[data-summary="order-cost"]', orderTotal);
            setCurrency('[data-summary="need-cost-display"]', needTotal);
            setCurrency('[data-summary="order-cost-display"]', orderTotal);

            const variance = budgetValue - orderTotal;
            setCurrency('[data-summary="budget-variance"]', variance, true);

            if (summaryBudget) {
                summaryBudget.textContent = budgetValue.toLocaleString(undefined, { minimumFractionDigits: 2, maximumFractionDigits: 2 });
            }
        };

        const updateRow = (row) => {
            const clean = readNumber(row, '[data-field="clean"]');
            const dirty = readNumber(row, '[data-field="dirty"]');
            const storage = readNumber(row, '[data-field="storage"]');
            const carts = readNumber(row, '[data-field="carts"]');
            const casesPurchased = readNumber(row, '[data-field="cases-purchased"]');
            const lastMonth = readNumber(row, '[data-field="last-month"]');

            const totalOnHand = inRooms + clean + dirty + storage + carts;
            const budgetedPar = Number(row.getAttribute('data-budgeted-par')) || 0;
            const caseCount = Number(row.getAttribute('data-case-count')) || 1;
            const casePrice = Number(row.getAttribute('data-case-price')) || 0;
            const inRooms = Number(row.getAttribute('data-in-rooms')) || 0;

            const orderNeed = Math.max(0, budgetedPar - totalOnHand);
            const casesToOrder = caseCount <= 0 ? 0 : orderNeed / caseCount;
            const needCost = casesToOrder * casePrice;
            const orderCost = casesPurchased * casePrice;
            const parDenominator = inRooms > 0 ? inRooms : 1;
            const actToPar = totalOnHand / parDenominator;
            const variance = totalOnHand - lastMonth;

            setInteger(row, '[data-role="total-on-hand"]', totalOnHand);
            setInteger(row, '[data-role="variance"]', variance);
            setInteger(row, '[data-role="order-need"]', orderNeed);
            row.querySelector('[data-role="act-par"]').textContent = `${actToPar.toFixed(2)}x`;
            row.querySelector('[data-role="cases-to-order"]').textContent = casesToOrder.toFixed(2);
            row.querySelector('[data-role="need-cost"]').textContent = formatCurrency(needCost);
            row.querySelector('[data-role="order-cost"]').textContent = formatCurrency(orderCost);
        };

        rows.forEach(row => {
            row.querySelectorAll('.inventory-input').forEach(input => {
                input.addEventListener('input', () => {
                    updateRow(row);
                    updateSummary();
                });
            });

            updateRow(row);
        });

        if (budgetInput) {
            budgetInput.addEventListener('input', () => updateSummary());
        }

        updateSummary();
    }

    function setText(row, selector, value) {
        const target = row.querySelector(selector);
        if (!target) {
            return;
        }
        target.textContent = Number(value).toFixed(2);
    }

    function setInteger(row, selector, value) {
        const target = row.querySelector(selector);
        if (!target) {
            return;
        }
        target.textContent = Math.round(Number(value) || 0).toString();
    }

    function readNumber(row, selector) {
        const input = row.querySelector(selector);
        if (!input) {
            return 0;
        }

        const value = parseFloat(input.value);
        return isNaN(value) ? 0 : value;
    }

    function formatCurrency(value) {
        return `$${(value || 0).toLocaleString(undefined, { minimumFractionDigits: 2, maximumFractionDigits: 2 })}`;
    }

    function parseCurrency(text) {
        if (!text) {
            return 0;
        }
        return parseFloat(String(text).replace(/[^0-9.\-]/g, '')) || 0;
    }

    function setCurrency(selector, value, includeSign = false) {
        const element = document.querySelector(selector);
        if (!element) {
            return;
        }
        const formatted = formatCurrency(value);
        element.textContent = includeSign && value > 0 ? `+${formatted}` : formatted;
    }

    function configureCollectionControls() {
        document.addEventListener('click', (event) => {
            const addButton = event.target.closest('[data-collection-add]');
            if (addButton) {
                event.preventDefault();
                addCollectionRow(addButton);
                return;
            }

            const removeButton = event.target.closest('[data-collection-remove]');
            if (removeButton) {
                event.preventDefault();
                removeCollectionRow(removeButton);
            }
        });
    }

    function addCollectionRow(button) {
        const targetSelector = button.getAttribute('data-collection-target');
        const templateId = button.getAttribute('data-collection-template');
        const target = targetSelector ? document.querySelector(targetSelector) : null;
        const template = templateId ? document.getElementById(templateId) : null;

        if (!target || !template || !template.content) {
            return;
        }

        const index = target.querySelectorAll('[data-collection-item]').length;
        const fragment = template.content.cloneNode(true);

        hydrateTemplate(fragment, index);

        const group = fragment.querySelector('[data-collection-item-group]');
        if (group) {
            while (group.firstElementChild) {
                target.appendChild(group.firstElementChild);
            }
        }
        else {
            target.appendChild(fragment);
        }
    }

    function hydrateTemplate(fragment, index) {
        fragment.querySelectorAll('[data-name-template]').forEach((element) => {
            const templateValue = element.getAttribute('data-name-template');
            if (!templateValue) {
                return;
            }

            const updated = templateValue.replace(/__index__/g, index);
            element.setAttribute('name', updated);
        });

        fragment.querySelectorAll('[data-role="mark-deleted"]').forEach((element) => {
            element.value = 'false';
        });
    }

    function removeCollectionRow(button) {
        const row = button.closest('[data-collection-item]');
        if (!row) {
            return;
        }

        const marker = row.querySelector('[data-role="mark-deleted"]');
        if (marker) {
            marker.value = 'true';
            row.classList.add('d-none');
            const childRow = row.nextElementSibling;
            if (childRow && childRow.hasAttribute('data-collection-item-child')) {
                childRow.classList.add('d-none');
            }
            return;
        }

        const group = row.closest('[data-collection-item-group]');
        if (group) {
            group.remove();
            return;
        }

        const nextRow = row.nextElementSibling;
        row.remove();
        if (nextRow && nextRow.hasAttribute('data-collection-item-child')) {
            nextRow.remove();
        }
    }
})();
