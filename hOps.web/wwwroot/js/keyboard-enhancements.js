/* Ensure Enter inserts newline in plain <textarea> inputs without submitting forms */
(function () {
    const SELECTORS = ['textarea'];

    const processed = new WeakSet();

    function handleTextareaEnter(event) {
        if (event.defaultPrevented) {
            return;
        }
        if (event.key !== 'Enter' || event.shiftKey || event.altKey || event.metaKey) {
            return;
        }

        const textarea = event.currentTarget;
        if (!(textarea instanceof HTMLTextAreaElement)) {
            return;
        }

        const { selectionStart, selectionEnd, value } = textarea;
        event.preventDefault();

        const before = value.slice(0, selectionStart);
        const after = value.slice(selectionEnd);
        textarea.value = `${before}\n${after}`;
        const newCaret = selectionStart + 1;
        textarea.selectionStart = textarea.selectionEnd = newCaret;

        textarea.dispatchEvent(new InputEvent('input', { bubbles: true }));
    }

    function attachHandlers(element) {
        if (processed.has(element)) {
            return;
        }

        if (element instanceof HTMLTextAreaElement) {
            element.addEventListener('keydown', handleTextareaEnter);
        }

        processed.add(element);
    }

    function scanAndAttach(root) {
        const elements = root.querySelectorAll
            ? root.querySelectorAll(SELECTORS.join(','))
            : [];

        if (root instanceof HTMLElement && SELECTORS.some(selector => root.matches(selector))) {
            attachHandlers(root);
        }

        elements.forEach(attachHandlers);
    }

    const observer = new MutationObserver((mutations) => {
        mutations.forEach(mutation => {
            if (mutation.type === 'childList') {
                mutation.addedNodes.forEach(node => {
                    if (node instanceof HTMLElement) {
                        scanAndAttach(node);
                    }
                });
            } else if (mutation.type === 'attributes' && mutation.target instanceof HTMLElement) {
                if (SELECTORS.some(selector => mutation.target.matches(selector))) {
                    attachHandlers(mutation.target);
                }
            }
        });
    });

    observer.observe(document.documentElement, {
        childList: true,
        subtree: true,
        attributes: false
    });

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', () => scanAndAttach(document), { once: true });
    } else {
        scanAndAttach(document);
    }
})();
