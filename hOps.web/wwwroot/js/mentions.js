(function () {
    const selector = '[data-enable-mentions="true"]';
    const inputs = Array.from(document.querySelectorAll(selector));
    if (!inputs.length) {
        return;
    }

    const START_MARKER = '\u200D';
    const END_MARKER = '\u200E';
    const ZERO_WIDTH_ZERO = '\u200B';
    const ZERO_WIDTH_ONE = '\u200C';
    const state = new WeakMap();

    inputs.forEach(initMentionInput);

    function initMentionInput(input) {
        input.setAttribute('autocomplete', 'off');
        input.addEventListener('input', () => handleInput(input));
        input.addEventListener('keydown', (event) => handleKeyDown(event, input));
        input.addEventListener('blur', () => setTimeout(() => hideSuggestions(input), 120));
        state.set(input, {
            anchor: null,
            container: createSuggestionContainer(),
            items: []
        });
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

    function handleInput(input) {
        const mention = getMentionContext(input);
        if (!mention) {
            hideSuggestions(input);
            return;
        }

        const info = state.get(input);
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
        const container = info?.container;
        if (!container || container.classList.contains('d-none')) {
            return;
        }

        const items = info.items;
        if (!items.length) {
            return;
        }

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
        info.container.classList.add('d-none');
        info.items = [];
        info.anchor = null;
    }

    function positionSuggestions(input, container) {
        const rect = input.getBoundingClientRect();
        const top = rect.bottom + window.scrollY + 6;
        const left = rect.left + window.scrollX;
        container.style.top = top + 'px';
        container.style.left = left + 'px';
        container.style.width = rect.width + 'px';
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

    function selectMention(item, input) {
        const info = state.get(input);
        if (!info?.anchor) {
            return;
        }

        const displayName = item.dataset.mentionDisplay;
        const userId = item.dataset.mentionUser;
        if (!displayName || !userId) {
            return;
        }

        const value = input.value;
        const start = info.anchor.start;
        const end = info.anchor.end;
        const before = value.slice(0, start);
        const after = value.slice(end);
        const encodedId = encodeIdentifier(userId);
        const mentionText = '@' + displayName + START_MARKER + encodedId + END_MARKER + ' ';

        input.value = before + mentionText + after;
        input.focus();
        const caretPosition = before.length + mentionText.length; // keep caret past hidden markers
        input.setSelectionRange(caretPosition, caretPosition);
        input.dispatchEvent(new Event('input', { bubbles: true }));
        hideSuggestions(input);
    }

    function getMentionContext(input) {
        const value = input.value;
        const cursor = input.selectionStart ?? value.length;
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

        if (!term.length) {
            return null;
        }

        if (/[^a-zA-Z0-9_.-]/.test(term)) {
            return null;
        }

        if (term.length > 30) {
            return null;
        }

        return {
            start: atIndex,
            end: atIndex + 1 + term.length,
            term
        };
    }

    function escapeHtml(value) {
        const element = document.createElement('textarea');
        element.textContent = value ?? '';
        return element.innerHTML;
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

    window.addEventListener('scroll', () => {
        inputs.forEach((input) => {
            const info = state.get(input);
            if (info && !info.container.classList.contains('d-none')) {
                positionSuggestions(input, info.container);
            }
        });
    }, { passive: true });

    window.addEventListener('resize', () => {
        inputs.forEach((input) => {
            const info = state.get(input);
            if (info && !info.container.classList.contains('d-none')) {
                positionSuggestions(input, info.container);
            }
        });
    });
})();
