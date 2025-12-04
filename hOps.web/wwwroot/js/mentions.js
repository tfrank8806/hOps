(function () {
    const selector = '[data-enable-mentions="true"]';
    const trackedInputs = new Set();
    const isContentEditableElement = (element) => element instanceof HTMLElement && element.isContentEditable;
    const isTextualInput = (element) =>
        element instanceof HTMLInputElement || element instanceof HTMLTextAreaElement;
    const shouldSkipElement = (element) => element?.dataset?.mentionsProxy === 'true';

    const START_MARKER = '\u200D';
    const END_MARKER = '\u200E';
    const ZERO_WIDTH_ZERO = '\u200B';
    const ZERO_WIDTH_ONE = '\u200C';
    const CARET_STYLE_PROPERTIES = [
        'direction',
        'boxSizing',
        'width',
        'height',
        'overflowX',
        'overflowY',
        'borderTopWidth',
        'borderRightWidth',
        'borderBottomWidth',
        'borderLeftWidth',
        'paddingTop',
        'paddingRight',
        'paddingBottom',
        'paddingLeft',
        'fontStyle',
        'fontVariant',
        'fontWeight',
        'fontStretch',
        'fontSize',
        'fontSizeAdjust',
        'lineHeight',
        'fontFamily',
        'textAlign',
        'textTransform',
        'textIndent',
        'textDecoration',
        'letterSpacing',
        'wordSpacing',
        'tabSize',
        'MozTabSize'
    ];
    const state = new WeakMap();
    const caretMirror = createCaretMirror();

    document.querySelectorAll(selector).forEach(initMentionInput);

    const observer = new MutationObserver((mutations) => {
        mutations.forEach((mutation) => {
            mutation.addedNodes.forEach((node) => {
                if (!(node instanceof HTMLElement)) {
                    return;
                }

                if (node.matches(selector)) {
                    initMentionInput(node);
                }

                if (typeof node.querySelectorAll === 'function') {
                    node.querySelectorAll(selector).forEach(initMentionInput);
                }
            });

            mutation.removedNodes.forEach((node) => {
                if (!(node instanceof HTMLElement)) {
                    return;
                }

                if (node.matches(selector)) {
                    disposeMentionInput(node);
                }

                if (typeof node.querySelectorAll === 'function') {
                    node.querySelectorAll(selector).forEach(disposeMentionInput);
                }
            });
        });
    });

    if (document.body) {
        observer.observe(document.body, { childList: true, subtree: true });
    }

    function initMentionInput(input) {
        if (!input || trackedInputs.has(input) || shouldSkipElement(input)) {
            return;
        }

        trackedInputs.add(input);
        input.setAttribute('autocomplete', 'off');
        input.addEventListener('input', () => handleInput(input));
        input.addEventListener('keydown', (event) => handleKeyDown(event, input));
        input.addEventListener('blur', () => setTimeout(() => hideSuggestions(input), 120));

        state.set(input, {
            anchor: null,
            container: createSuggestionContainer(),
            items: [],
            kind: isContentEditableElement(input) ? 'editor' : 'text'
        });
    }

    function disposeMentionInput(input) {
        if (!trackedInputs.has(input)) {
            return;
        }

        hideSuggestions(input);
        const info = state.get(input);
        if (info && info.container && info.container.parentElement) {
            info.container.parentElement.removeChild(info.container);
        }

        trackedInputs.delete(input);
        state.delete(input);
    }

    function createSuggestionContainer() {
        const container = document.createElement('div');
        container.className = 'mention-suggestions shadow-sm border bg-white rounded d-none';
        container.style.position = 'absolute';
        container.style.zIndex = '1080';
        container.style.minWidth = '220px';
        container.style.maxHeight = '220px';
        container.style.overflowY = 'auto';
        container.style.fontSize = '0.9rem';
        container.style.padding = '0.25rem 0';
        document.body.appendChild(container);
        return container;
    }

    function createCaretMirror() {
        const mirror = document.createElement('div');
        mirror.style.position = 'absolute';
        mirror.style.visibility = 'hidden';
        mirror.style.whiteSpace = 'pre-wrap';
        mirror.style.wordWrap = 'break-word';
        mirror.style.top = '-9999px';
        mirror.style.left = '-9999px';
        mirror.style.padding = '0';
        mirror.style.border = '0';
        mirror.style.boxSizing = 'border-box';
        document.body.appendChild(mirror);
        return mirror;
    }

    function handleInput(input) {
        const mention = getMentionContext(input);
        if (!mention) {
            hideSuggestions(input);
            return;
        }

        const info = state.get(input);
        if (!info) {
            return;
        }

        info.anchor = mention;

        if (mention.term.length < 2) {
            hideSuggestions(input);
            return;
        }

        fetch('/Mentions/Search?term=' + encodeURIComponent(mention.term))
            .then((response) => (response.ok ? response.json() : []))
            .then((users) => (Array.isArray(users) ? users : []))
            .then((users) => showSuggestions(input, users))
            .catch(() => hideSuggestions(input));
    }

    function handleKeyDown(event, input) {
        const info = state.get(input);
        if (!info || !info.items.length || info.container.classList.contains('d-none')) {
            return;
        }

        const items = info.items;
        const currentIndex = items.findIndex((item) => item.classList.contains('active'));

        if (event.key === 'ArrowDown') {
            event.preventDefault();
            const nextIndex = currentIndex < items.length - 1 ? currentIndex + 1 : 0;
            setActive(items, nextIndex);
        } else if (event.key === 'ArrowUp') {
            event.preventDefault();
            const previousIndex = currentIndex > 0 ? currentIndex - 1 : items.length - 1;
            setActive(items, previousIndex);
        } else if (event.key === 'Enter') {
            if (currentIndex >= 0) {
                event.preventDefault();
                selectMention(items[currentIndex], input);
            }
        } else if (event.key === 'Escape') {
            hideSuggestions(input);
        }
    }

    function showSuggestions(input, users) {
        const info = state.get(input);
        if (!info) {
            return;
        }

        const container = info.container;
        container.innerHTML = '';
        info.items = [];

        if (!users.length) {
            hideSuggestions(input);
            return;
        }

        users.forEach((user, index) => {
            const item = document.createElement('button');
            item.type = 'button';
            item.className = 'dropdown-item text-start';
            if (index === 0) {
                item.classList.add('active');
            }
            item.dataset.mentionUser = user.id;
            item.dataset.mentionDisplay = user.displayName;
            item.textContent = user.displayName;

            item.addEventListener('mousedown', (event) => {
                event.preventDefault();
                selectMention(item, input);
            });

            container.appendChild(item);
            info.items.push(item);
        });

        positionSuggestions(input, container);
        container.classList.remove('d-none');
    }

    function hideSuggestions(input) {
        const info = state.get(input);
        if (!info) {
            return;
        }

        info.anchor = null;
        info.items = [];
        if (info.container) {
            info.container.classList.add('d-none');
        }
    }

    function positionSuggestions(input, container) {
        const rect = input.getBoundingClientRect();
        const caret = getCaretCoordinates(input);
        const width = Math.min(Math.max(rect.width, 220), 360);
        container.style.width = width + 'px';
        if (caret) {
            container.style.left = window.scrollX + rect.left + caret.left + 'px';
            container.style.top = window.scrollY + rect.top + caret.top + caret.height + 6 + 'px';
        } else {
            container.style.left = window.scrollX + rect.left + 'px';
            container.style.top = window.scrollY + rect.bottom + 6 + 'px';
        }
    }

    function setActive(items, index) {
        items.forEach((item, i) => {
            if (i === index) {
                item.classList.add('active');
                item.scrollIntoView({ block: 'nearest' });
            } else {
                item.classList.remove('active');
            }
        });
    }

    function getCaretCoordinates(input) {
        if (!(input instanceof HTMLInputElement) && !(input instanceof HTMLTextAreaElement)) {
            return null;
        }

        const selectionStart = input.selectionStart == null ? input.value.length : input.selectionStart;
        copyCaretStyles(input);

        caretMirror.textContent = input.value.slice(0, selectionStart);
        const marker = document.createElement('span');
        marker.textContent = input.value.slice(selectionStart) || '.';
        marker.style.display = 'inline-block';
        caretMirror.appendChild(marker);

        const markerRect = marker.getBoundingClientRect();
        const mirrorRect = caretMirror.getBoundingClientRect();
        const style = window.getComputedStyle(input);
        const lineHeight = parseFloat(style.lineHeight) || parseFloat(style.fontSize) || 16;

        const top = markerRect.top - mirrorRect.top - input.scrollTop;
        const left = markerRect.left - mirrorRect.left - input.scrollLeft;

        caretMirror.innerHTML = '';
        return { top, left, height: lineHeight };
    }

    function copyCaretStyles(input) {
        const style = window.getComputedStyle(input);
        CARET_STYLE_PROPERTIES.forEach((prop) => {
            caretMirror.style[prop] = style[prop];
        });

        const width = style.width === 'auto' ? input.clientWidth : parseFloat(style.width);
        caretMirror.style.width = (isNaN(width) ? input.clientWidth : width) + 'px';
    }

    function selectMention(item, input) {
        const info = state.get(input);
        if (!info || !info.anchor) {
            return;
        }

        const displayName = item.dataset.mentionDisplay;
        const userId = item.dataset.mentionUser;
        if (!displayName || !userId) {
            return;
        }

        const encodedId = encodeIdentifier(userId);
        const mentionText = '@' + displayName + START_MARKER + encodedId + END_MARKER + ' ';

        if (isContentEditableElement(input) && info.anchor.range) {
            insertMentionIntoEditor(input, mentionText, info.anchor.range);
            hideSuggestions(input);
            return;
        }

        if (!isTextualInput(input)) {
            hideSuggestions(input);
            return;
        }

        const value = input.value;
        const start = info.anchor.start;
        const end = info.anchor.end;
        const before = value.slice(0, start);
        const after = value.slice(end);

        input.value = before + mentionText + after;
        input.focus();
        const caretPosition = before.length + mentionText.length;
        input.setSelectionRange(caretPosition, caretPosition);
        input.dispatchEvent(new Event('input', { bubbles: true }));
        hideSuggestions(input);
    }

    function insertMentionIntoEditor(editor, mentionText, range) {
        const workingRange = range.cloneRange();
        workingRange.deleteContents();
        const textNode = document.createTextNode(mentionText);
        workingRange.insertNode(textNode);

        const selection = window.getSelection();
        if (selection) {
            selection.removeAllRanges();
            const caretRange = document.createRange();
            caretRange.setStart(textNode, textNode.length);
            caretRange.collapse(true);
            selection.addRange(caretRange);
        }

        editor.dispatchEvent(new Event('input', { bubbles: true }));
    }

    function getMentionContext(input) {
        if (isContentEditableElement(input)) {
            return getMentionContextFromEditor(input);
        }

        if (!isTextualInput(input)) {
            return null;
        }

        return getMentionContextFromTextInput(input);
    }

    function getMentionContextFromTextInput(input) {
        const value = input.value;
        const cursor = input.selectionStart == null ? value.length : input.selectionStart;
        const beforeCursor = value.slice(0, cursor);
        const atIndex = beforeCursor.lastIndexOf('@');

        if (atIndex === -1) {
            return null;
        }

        if (atIndex > 0) {
            const charBefore = beforeCursor[atIndex - 1];
            if (!/\s|\(|\[|\{/.test(charBefore)) {
                return null;
            }
        }

        const rawTerm = beforeCursor.slice(atIndex + 1);
        if (
            rawTerm.includes(START_MARKER) ||
            rawTerm.includes(END_MARKER) ||
            rawTerm.includes(ZERO_WIDTH_ZERO) ||
            rawTerm.includes(ZERO_WIDTH_ONE)
        ) {
            return null;
        }

        const termMatch = rawTerm.match(/^[^\s@]*/);
        const term = termMatch ? termMatch[0] : '';

        if (!term.length || /[^a-zA-Z0-9_.-]/.test(term) || term.length > 30) {
            return null;
        }

        return {
            start: atIndex,
            end: atIndex + 1 + term.length,
            term
        };
    }

    function getMentionContextFromEditor(editor) {
        const selection = window.getSelection();
        if (!selection || selection.rangeCount === 0) {
            return null;
        }

        const caretRange = selection.getRangeAt(0);
        if (!editor.contains(caretRange.startContainer)) {
            return null;
        }

        const workingRange = caretRange.cloneRange();
        workingRange.collapse(true);

        const termChars = [];
        while (true) {
            const step = moveRangeStartBackwardOneCharacter(editor, workingRange);
            if (!step) {
                return null;
            }

            const char = step.char;
            if (char === '@') {
                const term = termChars.join('');
                if (!term.length || /[^a-zA-Z0-9_.-]/.test(term)) {
                    return null;
                }
                const mentionRange = workingRange.cloneRange();
                mentionRange.setEnd(caretRange.startContainer, caretRange.startOffset);
                return {
                    term,
                    range: mentionRange
                };
            }

            if (/\s|\(|\[|\{|\n/.test(char) || char === START_MARKER || char === END_MARKER) {
                return null;
            }

            if (char === ZERO_WIDTH_ZERO || char === ZERO_WIDTH_ONE) {
                return null;
            }

            termChars.unshift(char);
            if (termChars.length > 30) {
                return null;
            }
        }
    }

    function encodeIdentifier(id) {
        let bits = '';
        for (let i = 0; i < id.length; i++) {
            const code = id.charCodeAt(i).toString(2).padStart(8, '0');
            bits += code;
        }

        let encoded = '';
        for (let i = 0; i < bits.length; i++) {
            encoded += bits[i] === '0' ? ZERO_WIDTH_ZERO : ZERO_WIDTH_ONE;
        }
        return encoded;
    }

    function moveRangeStartBackwardOneCharacter(root, range) {
        let container = range.startContainer;
        let offset = range.startOffset;

        while (container) {
            if (container.nodeType === Node.TEXT_NODE) {
                if (offset > 0) {
                    const newOffset = offset - 1;
                    range.setStart(container, newOffset);
                    return { char: container.data[newOffset] };
                }

                const parent = container.parentNode;
                if (!parent) {
                    return null;
                }
                offset = Array.prototype.indexOf.call(parent.childNodes, container);
                container = parent;
                continue;
            }

            if (offset > 0) {
                let node = container.childNodes[offset - 1];
                while (node && node.lastChild) {
                    node = node.lastChild;
                }

                if (!node) {
                    return null;
                }

                if (node.nodeType === Node.TEXT_NODE) {
                    container = node;
                    offset = node.data.length;
                    continue;
                }

                if (isLineBreakNode(node)) {
                    range.setStartBefore(node);
                    return { char: '\n' };
                }

                container = node;
                offset = node.childNodes.length;
                continue;
            }

            if (container === root) {
                return null;
            }

            const parent = container.parentNode;
            if (!parent) {
                return null;
            }
            offset = Array.prototype.indexOf.call(parent.childNodes, container);
            container = parent;
        }

        return null;
    }

    function isLineBreakNode(node) {
        return node && node.nodeType === Node.ELEMENT_NODE && node.tagName === 'BR';
    }

    function repositionActiveSuggestions() {
        trackedInputs.forEach((input) => {
            const info = state.get(input);
            if (info && info.container && !info.container.classList.contains('d-none')) {
                positionSuggestions(input, info.container);
            }
        });
    }

    window.addEventListener('scroll', repositionActiveSuggestions, { passive: true });
    window.addEventListener('resize', repositionActiveSuggestions);
})();
