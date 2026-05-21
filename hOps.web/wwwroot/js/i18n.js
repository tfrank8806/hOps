(() => {
    const translations = window.__STATIC_TRANSLATIONS || {};
    const activeLanguage = (window.__HOPS_ACTIVE_LANGUAGE || '').toLowerCase();

    const attributeNames = [
        'aria-label',
        'title',
        'placeholder',
        'data-empty-message',
        'data-bs-original-title',
        'data-tooltip',
        'data-toast-title',
        'data-confirm',
        'data-hover-title'
    ];

    const normalizeKey = (value) => (value || '').trim().replace(/\s+/g, ' ');

    const getTranslation = (value) => {
        const normalized = normalizeKey(value);
        if (!normalized) {
            return null;
        }

        const direct = translations[normalized];
        if (direct) {
            return direct;
        }

        const match = Object.keys(translations).find(
            key => key.localeCompare(normalized, undefined, { sensitivity: 'accent' }) === 0
        );

        return match ? translations[match] : null;
    };

    window.hopsTranslate = (key, fallback) => {
        if (typeof key !== 'string' || key.length === 0) {
            return typeof fallback === 'string' ? fallback : key;
        }

        const translated = getTranslation(key);
        if (translated) {
            return translated;
        }

        if (typeof fallback === 'string' && fallback.length > 0) {
            return fallback;
        }

        return key;
    };

    if (!Object.keys(translations).length || !activeLanguage || activeLanguage === 'en') {
        return;
    }

    const translateTextNode = (node) => {
        if (!node || node.nodeType !== Node.TEXT_NODE) {
            return;
        }

        const original = node.textContent || '';
        const trimmed = normalizeKey(original);
        if (!trimmed) {
            return;
        }

        const translation = getTranslation(trimmed);
        if (!translation || translation === trimmed) {
            return;
        }

        node.textContent = original.replace(trimmed, translation);
    };

    const translateElementAttributes = (element) => {
        attributeNames.forEach(attr => {
            if (!element.hasAttribute(attr)) {
                return;
            }

            const current = element.getAttribute(attr);
            const translation = getTranslation(current);
            if (!translation) {
                return;
            }

            element.setAttribute(attr, translation);
        });

        if (element instanceof HTMLInputElement) {
            const inputType = (element.getAttribute('type') || '').toLowerCase();
            if (inputType === 'button' || inputType === 'submit' || inputType === 'reset') {
                const value = element.value;
                const translation = getTranslation(value);
                if (translation) {
                    element.value = translation;
                }
            }
        }
    };

    const translateElement = (element) => {
        if (!element || element.nodeType !== Node.ELEMENT_NODE) {
            return;
        }

        translateElementAttributes(element);

        const walker = document.createTreeWalker(element, NodeFilter.SHOW_TEXT, null);
        let currentNode = walker.nextNode();
        while (currentNode) {
            translateTextNode(currentNode);
            currentNode = walker.nextNode();
        }
    };

    const translateDocument = () => {
        translateElement(document.body);
    };

    const observer = new MutationObserver(mutations => {
        mutations.forEach(mutation => {
            if (mutation.type === 'characterData' && mutation.target) {
                translateTextNode(mutation.target);
                return;
            }

            mutation.addedNodes.forEach(node => {
                if (node.nodeType === Node.TEXT_NODE) {
                    translateTextNode(node);
                } else if (node.nodeType === Node.ELEMENT_NODE) {
                    translateElement(node);
                }
            });
        });
    });

    const observeDocument = () => {
        observer.observe(document.body, {
            childList: true,
            subtree: true,
            characterData: true
        });
    };

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', () => {
            translateDocument();
            observeDocument();
        });
    } else {
        translateDocument();
        observeDocument();
    }
})();
