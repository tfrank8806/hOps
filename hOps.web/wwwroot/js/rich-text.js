(function () {
    const selector = '[data-rich-text="true"]';
    const elements = Array.from(document.querySelectorAll(selector));
    if (!elements.length) {
        return;
    }

    const colorOptions = [
        { value: 'default', label: 'Default', previewColor: '' },
        { value: 'red', label: 'Red', previewColor: '#b42318' },
        { value: 'orange', label: 'Orange', previewColor: '#c2410c' },
        { value: 'yellow', label: 'Yellow', previewColor: '#b58105' },
        { value: 'green', label: 'Green', previewColor: '#15803d' },
        { value: 'teal', label: 'Teal', previewColor: '#0f766e' },
        { value: 'blue', label: 'Blue', previewColor: '#2563eb' },
        { value: 'purple', label: 'Purple', previewColor: '#7c3aed' },
        { value: 'pink', label: 'Pink', previewColor: '#db2777' },
        { value: 'gray', label: 'Gray', previewColor: '#334155' }
    ];

    const colorHexMap = {
        red: '#b42318',
        orange: '#c2410c',
        yellow: '#b58105',
        green: '#15803d',
        teal: '#0f766e',
        blue: '#2563eb',
        purple: '#7c3aed',
        pink: '#db2777',
        gray: '#334155'
    };

    const MENTION_START = '\u200D';
    const MENTION_END = '\u200E';
    const ZERO_WIDTH_ZERO = '\u200B';
    const ZERO_WIDTH_ONE = '\u200C';
    const mentionPattern = new RegExp(
        `@([^${MENTION_START}${MENTION_END}]+)${MENTION_START}[${ZERO_WIDTH_ZERO}${ZERO_WIDTH_ONE}]+${MENTION_END}`,
        'g'
    );

    const DEFAULT_TEXT_COLOR = '#212529';
    const HIGHLIGHT_COLOR = '#fff2a8';
    const contexts = new WeakMap();
    let richTextIdCounter = 0;

    elements.forEach(initializeEditor);

    function initializeEditor(textarea) {
        if (textarea.dataset.richTextInitialized === 'true') {
            return;
        }

        textarea.dataset.richTextInitialized = 'true';
        textarea.classList.add('rich-text-input');

        const wrapper = document.createElement('div');
        wrapper.className = 'rich-text-editor-container';

        const editor = document.createElement('div');
        editor.className = 'rich-text-editor form-control';
        editor.setAttribute('contenteditable', 'true');
        editor.setAttribute('role', 'textbox');
        editor.setAttribute('aria-multiline', 'true');

        if (textarea.dataset.enableMentions === 'true') {
            editor.dataset.enableMentions = 'true';
            editor.dataset.richTextEditor = 'true';
            textarea.dataset.mentionsProxy = 'true';
            if (!textarea.id) {
                textarea.id = `richTextField_${++richTextIdCounter}`;
            }
            editor.dataset.mentionsSource = textarea.id;
        }

        if (textarea.placeholder) {
            editor.dataset.placeholder = textarea.placeholder;
        }

        const context = {
            textarea,
            editor,
            wrapper
        };

        const toolbar = buildToolbar(context);
        wrapper.appendChild(toolbar);
        wrapper.appendChild(editor);

        const parent = textarea.parentNode;
        if (parent) {
            parent.insertBefore(wrapper, textarea);
        }
        wrapper.appendChild(textarea);

        contexts.set(textarea, context);
        contexts.set(editor, context);

        populateEditorFromMarkup(context);
        syncToTextarea(context);

        editor.addEventListener('input', () => syncToTextarea(context));
        editor.addEventListener('blur', () => syncToTextarea(context));
        editor.addEventListener('paste', (event) => handlePaste(event, context));
        editor.addEventListener('keydown', (event) => handleKeydown(event, context));

        const form = textarea.form;
        if (form) {
            form.addEventListener('submit', () => {
                syncToTextarea(context);
            });
        }
    }

    function buildToolbar(context) {
        const toolbar = document.createElement('div');
        toolbar.className = 'rich-text-toolbar btn-toolbar mb-2';

        const emphasisGroup = createGroup(toolbar);
        addButton(emphasisGroup, {
            label: 'B',
            title: 'Bold (Ctrl+B)',
            action: () => applyInlineCommand(context, 'bold')
        });
        addButton(emphasisGroup, {
            label: 'I',
            title: 'Italic (Ctrl+I)',
            action: () => applyInlineCommand(context, 'italic')
        });
        addButton(emphasisGroup, {
            label: 'U',
            title: 'Underline (Ctrl+U)',
            action: () => applyInlineCommand(context, 'underline')
        });
        addButton(emphasisGroup, {
            label: 'S',
            title: 'Strikethrough',
            action: () => applyInlineCommand(context, 'strikeThrough')
        });
        addButton(emphasisGroup, {
            label: 'HL',
            title: 'Highlight',
            action: () => toggleHighlight(context)
        });

        const listGroup = createGroup(toolbar);
        addButton(listGroup, {
            label: '&bull;',
            title: 'Bulleted list',
            action: () => applyListCommand(context, 'insertUnorderedList')
        });
        addButton(listGroup, {
            label: '1.',
            title: 'Numbered list',
            action: () => applyListCommand(context, 'insertOrderedList')
        });
        addButton(listGroup, {
            label: '❝',
            title: 'Quote',
            action: () => applyBlockFormat(context, 'blockquote')
        });

        createColorPicker(toolbar, context);

        return toolbar;
    }

    function createGroup(toolbar) {
        const group = document.createElement('div');
        group.className = 'btn-group btn-group-sm me-2';
        toolbar.appendChild(group);
        return group;
    }

    function addButton(group, options) {
        const button = document.createElement('button');
        button.type = 'button';
        button.className = 'btn btn-outline-secondary';
        button.innerHTML = options.label;
        button.title = options.title;
        button.addEventListener('click', (event) => {
            event.preventDefault();
            options.action();
        });
        group.appendChild(button);
    }

    function applyInlineCommand(context, command) {
        focusEditor(context);
        document.execCommand(command, false, null);
        syncToTextarea(context);
    }

    function applyListCommand(context, command) {
        focusEditor(context);
        document.execCommand(command, false, null);
        syncToTextarea(context);
    }

    function applyBlockFormat(context, format) {
        focusEditor(context);
        document.execCommand('formatBlock', false, format);
        syncToTextarea(context);
    }

    function toggleHighlight(context) {
        focusEditor(context);
        const current = normalizeColor(document.queryCommandValue('HiliteColor'));
        if (current && colorsEqual(current, HIGHLIGHT_COLOR)) {
            document.execCommand('hiliteColor', false, 'transparent');
        } else {
            document.execCommand('hiliteColor', false, HIGHLIGHT_COLOR);
        }
        syncToTextarea(context);
    }

    function createColorPicker(toolbar, context) {
        const colorGroup = createGroup(toolbar);
        colorGroup.classList.add('rich-text-toolbar__color-group');

        const toggle = document.createElement('button');
        toggle.type = 'button';
        toggle.className = 'btn btn-outline-secondary rich-text-color-toggle';
        toggle.textContent = 'Color';
        toggle.setAttribute('aria-haspopup', 'true');
        toggle.setAttribute('aria-expanded', 'false');
        colorGroup.appendChild(toggle);

        const menu = document.createElement('div');
        menu.className = 'rich-text-color-menu';
        colorGroup.appendChild(menu);

        colorOptions.forEach(option => {
            const item = document.createElement('button');
            item.type = 'button';
            item.className = 'rich-text-color-menu__item';
            item.dataset.colorValue = option.value;

            if (option.value !== 'default') {
                const swatch = document.createElement('span');
                swatch.className = 'rich-text-color-menu__swatch';
                swatch.style.backgroundColor = option.previewColor;
                item.appendChild(swatch);
            } else {
                item.classList.add('rich-text-color-menu__item--default');
            }

            const label = document.createElement('span');
            label.className = 'rich-text-color-menu__label';
            label.textContent = option.label;
            item.appendChild(label);

            item.addEventListener('click', (event) => {
                event.preventDefault();
                applyColor(context, option.value);
                hideMenu();
            });

            menu.appendChild(item);
        });

        toggle.addEventListener('click', (event) => {
            event.preventDefault();
            const willOpen = !menu.classList.contains('show');
            closeOtherColorMenus(menu);
            if (willOpen) {
                menu.classList.add('show');
                toggle.setAttribute('aria-expanded', 'true');
            } else {
                hideMenu();
            }
        });

        document.addEventListener('click', (event) => {
            if (!menu.classList.contains('show')) {
                return;
            }
            if (colorGroup.contains(event.target)) {
                return;
            }
            hideMenu();
        });

        function hideMenu() {
            menu.classList.remove('show');
            toggle.setAttribute('aria-expanded', 'false');
        }
    }

    function closeOtherColorMenus(currentMenu) {
        document
            .querySelectorAll('.rich-text-color-menu.show')
            .forEach(menu => {
                if (menu === currentMenu) {
                    return;
                }
                menu.classList.remove('show');
                const toggle = menu.previousElementSibling;
                if (toggle && toggle.classList.contains('rich-text-color-toggle')) {
                    toggle.setAttribute('aria-expanded', 'false');
                }
            });
    }

    function applyColor(context, value) {
        focusEditor(context);
        if (value === 'default') {
            document.execCommand('foreColor', false, DEFAULT_TEXT_COLOR);
        } else {
            const hex = colorHexMap[value];
            document.execCommand('foreColor', false, hex);
        }
        syncToTextarea(context);
    }

    function handlePaste(event, context) {
        event.preventDefault();
        const text = (event.clipboardData || window.clipboardData).getData('text/plain');
        focusEditor(context);
        document.execCommand('insertText', false, text);
        syncToTextarea(context);
    }

    function handleKeydown(event, context) {
        if (event.key === 'Enter' && !event.shiftKey && !event.altKey && !event.metaKey) {
            event.preventDefault();
            insertParagraph(context);
            syncToTextarea(context);
            return;
        }

        if (!(event.ctrlKey || event.metaKey)) {
            return;
        }

        const key = event.key.toLowerCase();
        if (key === 'b') {
            event.preventDefault();
            applyInlineCommand(context, 'bold');
        } else if (key === 'i') {
            event.preventDefault();
            applyInlineCommand(context, 'italic');
        } else if (key === 'u') {
            event.preventDefault();
            applyInlineCommand(context, 'underline');
        }
    }

    function insertParagraph(context) {
        const editor = context.editor;

        if (typeof document.execCommand === 'function' && document.queryCommandSupported('insertParagraph')) {
            document.execCommand('insertParagraph', false, null);
            editor.dispatchEvent(new InputEvent('input', { bubbles: true }));
            return;
        }

        const selection = window.getSelection();
        if (!selection || !selection.rangeCount) {
            const block = document.createElement('div');
            block.appendChild(document.createElement('br'));
            editor.appendChild(block);
            editor.dispatchEvent(new InputEvent('input', { bubbles: true }));
            return;
        }

        const range = selection.getRangeAt(0);
        range.deleteContents();

        const block = document.createElement('div');
        block.appendChild(document.createElement('br'));

        range.insertNode(block);

        range.setStart(block, block.childNodes.length);
        range.collapse(true);
        selection.removeAllRanges();
        selection.addRange(range);

        editor.dispatchEvent(new InputEvent('input', { bubbles: true }));
    }

    function focusEditor(context) {
        const editor = context.editor;
        if (document.activeElement !== editor) {
            editor.focus();
        }
    }

    function populateEditorFromMarkup(context) {
        const { textarea, editor } = context;
        let initialHtml = textarea.dataset.initialHtml;
        if (initialHtml) {
            editor.innerHTML = initialHtml;
            textarea.dataset.initialHtml = '';
        } else {
            const html = markupToHtml(textarea.value || '');
            editor.innerHTML = html || '';
        }
        normalizeEditorDom(editor);
        ensureEditorHasContent(editor);
        decorateMentions(editor);
    }

    function ensureEditorHasContent(editor) {
        if (!editor.textContent.trim()) {
            editor.innerHTML = '';
        }
    }

    function decorateMentions(root) {
        if (!root) {
            return;
        }
        const walker = document.createTreeWalker(root, NodeFilter.SHOW_TEXT, null);
        const targets = [];
        while (walker.nextNode()) {
            const node = walker.currentNode;
            if (!node.nodeValue) {
                continue;
            }
            if (node.parentElement && node.parentElement.closest('.rich-text-mention')) {
                continue;
            }
            mentionPattern.lastIndex = 0;
            if (mentionPattern.test(node.nodeValue)) {
                targets.push(node);
            }
        }

        targets.forEach(textNode => wrapMentionTextNode(textNode));
        mentionPattern.lastIndex = 0;
    }

    function wrapMentionTextNode(textNode) {
        const text = textNode.nodeValue || '';
        mentionPattern.lastIndex = 0;
        let lastIndex = 0;
        let match;
        const fragment = document.createDocumentFragment();
        let replaced = false;

        while ((match = mentionPattern.exec(text)) !== null) {
            const before = text.slice(lastIndex, match.index);
            if (before) {
                fragment.appendChild(document.createTextNode(before));
            }
            fragment.appendChild(createMentionElement(match[0]));
            lastIndex = match.index + match[0].length;
            replaced = true;
        }

        if (!replaced) {
            return;
        }

        if (lastIndex < text.length) {
            fragment.appendChild(document.createTextNode(text.slice(lastIndex)));
        }

        textNode.parentNode?.replaceChild(fragment, textNode);
    }

    function createMentionElement(mentionText) {
        const span = document.createElement('span');
        span.className = 'rich-text-mention';
        span.setAttribute('contenteditable', 'false');
        span.dataset.mention = 'true';
        span.tabIndex = -1;
        span.textContent = mentionText;
        return span;
    }

    function syncToTextarea(context) {
        const { editor, textarea } = context;
        const shouldRestoreSelection = document.activeElement === editor;
        const selectionState = shouldRestoreSelection ? saveSelectionState(editor) : null;
        normalizeEditorDom(editor);
        ensureEditorHasContent(editor);
        if (shouldRestoreSelection) {
            restoreSelectionState(editor, selectionState);
        }
        const markup = htmlToMarkup(editor);
        textarea.value = markup;
        triggerInput(textarea);
    }

    function normalizeEditorDom(root) {
        const walker = document.createTreeWalker(root, NodeFilter.SHOW_ELEMENT, null);
        const nodes = [];
        while (walker.nextNode()) {
            nodes.push(walker.currentNode);
        }

        nodes.forEach(node => {
            const tag = node.tagName.toLowerCase();

            if (tag === 'b') {
                replaceTag(node, 'strong');
                return;
            }

            if (tag === 'i') {
                replaceTag(node, 'em');
                return;
            }

            if (tag === 'u') {
                replaceTag(node, 'span', 'rich-text-underline');
                return;
            }

            if (tag === 'font') {
                const color = node.getAttribute('color');
                if (color) {
                    convertColorElement(node, color);
                } else {
                    unwrap(node);
                }
                return;
            }

            if (tag === 'span') {
                handleSpanNormalization(node);
                return;
            }

            if (tag === 'mark') {
                node.removeAttribute('style');
            }
        });

        collapseEmptyNodes(root);
    }

    function handleSpanNormalization(node) {
        const style = node.getAttribute('style') || '';
        const textDecoration = extractStyleValue(style, 'text-decoration');
        const underline = textDecoration && textDecoration.toLowerCase().includes('underline');
        const background = extractStyleValue(style, 'background-color');
        const color = extractStyleValue(style, 'color');

        if (background && colorsEqual(normalizeColor(background), HIGHLIGHT_COLOR)) {
            const mark = replaceTag(node, 'mark');
            mark.removeAttribute('style');
            return;
        }

        if (underline) {
            const underlineSpan = replaceTag(node, 'span', 'rich-text-underline');
            underlineSpan.removeAttribute('style');
            return;
        }

        if (color) {
            convertColorElement(node, color);
            return;
        }

        if (!node.classList.length && !node.attributes.length) {
            unwrap(node);
        }
    }

    function convertColorElement(node, colorValue) {
        const normalized = normalizeColor(colorValue);
        if (!normalized) {
            unwrap(node);
            return;
        }

        if (colorsEqual(normalized, DEFAULT_TEXT_COLOR)) {
            unwrap(node);
            return;
        }

        const colorName = findColorNameByHex(normalized);
        if (!colorName) {
            unwrap(node);
            return;
        }

        const span = replaceTag(node, 'span');
        span.classList.add('rich-text-color', `rich-text-color-${colorName}`);
        span.removeAttribute('style');
    }

    function replaceTag(node, newTagName, className) {
        const replacement = document.createElement(newTagName);
        if (className) {
            replacement.className = className;
        }
        while (node.firstChild) {
            replacement.appendChild(node.firstChild);
        }
        node.replaceWith(replacement);
        return replacement;
    }

    function unwrap(node) {
        const parent = node.parentNode;
        if (!parent) {
            return;
        }
        while (node.firstChild) {
            parent.insertBefore(node.firstChild, node);
        }
        parent.removeChild(node);
    }

    function collapseEmptyNodes(root) {
        const walker = document.createTreeWalker(root, NodeFilter.SHOW_ELEMENT, null);
        const toRemove = [];
        while (walker.nextNode()) {
            const current = walker.currentNode;
            if (current === root) {
                continue;
            }
            if (!current.textContent && current.childNodes.length === 0) {
                toRemove.push(current);
            }
        }
        toRemove.forEach(node => node.remove());
    }

    function saveSelectionState(root) {
        const selection = window.getSelection();
        if (!selection || selection.rangeCount === 0) {
            return null;
        }
        const range = selection.getRangeAt(0);
        if (!root.contains(range.startContainer) || !root.contains(range.endContainer)) {
            return null;
        }
        return {
            start: describeSelectionPosition(root, range.startContainer, range.startOffset),
            end: describeSelectionPosition(root, range.endContainer, range.endOffset)
        };
    }

    function describeSelectionPosition(root, node, offset) {
        const path = [];
        let current = node;
        while (current && current !== root) {
            const parent = current.parentNode;
            if (!parent) {
                break;
            }
            const index = Array.prototype.indexOf.call(parent.childNodes, current);
            path.unshift(index);
            current = parent;
        }
        return { path, offset };
    }

    function restoreSelectionState(root, savedState) {
        if (!savedState) {
            return;
        }
        const selection = window.getSelection();
        if (!selection) {
            return;
        }
        const startNode = locateSelectionNode(root, savedState.start);
        const endNode = locateSelectionNode(root, savedState.end);
        if (!startNode || !endNode) {
            return;
        }
        const range = document.createRange();
        range.setStart(startNode, Math.min(savedState.start.offset, getNodeLength(startNode)));
        range.setEnd(endNode, Math.min(savedState.end.offset, getNodeLength(endNode)));
        selection.removeAllRanges();
        selection.addRange(range);
    }

    function locateSelectionNode(root, descriptor) {
        if (!descriptor) {
            return null;
        }
        let node = root;
        for (const index of descriptor.path) {
            if (!node || !node.childNodes || node.childNodes.length <= index) {
                return null;
            }
            node = node.childNodes[index];
        }
        return node;
    }

    function getNodeLength(node) {
        if (node.nodeType === Node.TEXT_NODE) {
            return node.textContent ? node.textContent.length : 0;
        }
        return node.childNodes.length;
    }

    function markupToHtml(markup) {
        if (!markup) {
            return '';
        }

        const stack = [];
        let html = '';
        let i = 0;

        while (i < markup.length) {
            if (markup.startsWith('{{color:', i)) {
                const end = markup.indexOf('}}', i);
                const color = markup.slice(i + 8, end).trim().toLowerCase();
                const colorName = color in colorHexMap ? color : null;
                if (colorName) {
                    html += `<span class="rich-text-color rich-text-color-${colorName}">`;
                    stack.push({
                        type: 'color',
                        open: `<span class="rich-text-color rich-text-color-${colorName}">`,
                        close: '</span>'
                    });
                    i = end + 2;
                    continue;
                }
            }

            if (markup.startsWith('{{/color}}', i)) {
                const { closed, matched } = closeStackUntilType(stack, 'color');
                closed.forEach(token => {
                    html += token.close;
                });
                html += matched ? matched.close : '</span>';
                for (let index = closed.length - 1; index >= 0; index -= 1) {
                    const reopen = closed[index];
                    html += reopen.open;
                    stack.push(reopen);
                }
                i += '{{/color}}'.length;
                continue;
            }

            const token = getInlineToken(markup, i);
            if (token) {
                const existing = findTokenIndex(stack, token.type);
                if (existing === -1) {
                    html += token.open;
                    stack.push(token);
                } else {
                    while (stack.length > existing) {
                        const toClose = stack.pop();
                        html += toClose.close;
                    }
                }
                i += token.marker.length;
                continue;
            }

            const char = markup[i];
            if (char === '\r') {
                i += 1;
                continue;
            }
            if (char === '\n') {
                html += '<br>';
                i += 1;
                continue;
            }

            html += escapeHtml(char);
            i += 1;
        }

        while (stack.length) {
            html += stack.pop().close;
        }

        return html;
    }

    function getInlineToken(text, index) {
        const tokens = [
            { marker: '**', type: 'bold', open: '<strong>', close: '</strong>' },
            { marker: '++', type: 'underline', open: '<span class="rich-text-underline">', close: '</span>' },
            { marker: '~~', type: 'strike', open: '<del>', close: '</del>' },
            { marker: '==', type: 'highlight', open: '<mark>', close: '</mark>' },
            { marker: '*', type: 'italic', open: '<em>', close: '</em>' }
        ];

        for (const token of tokens) {
            if (text.startsWith(token.marker, index)) {
                return token;
            }
        }

        return null;
    }

    function closeStackUntilType(stack, type) {
        const closed = [];
        while (stack.length) {
            const token = stack.pop();
            if (token.type === type) {
                return { closed, matched: token };
            }
            closed.push(token);
        }
        return { closed, matched: null };
    }

    function findTokenIndex(stack, type) {
        for (let i = stack.length - 1; i >= 0; i -= 1) {
            if (stack[i].type === type) {
                return i;
            }
        }
        return -1;
    }

    function htmlToMarkup(editor) {
        const clone = editor.cloneNode(true);
        normalizeEditorDom(clone);
        const markup = serializeChildren(clone);
        return cleanupMarkup(markup);
    }

    function serializeChildren(node) {
        let result = '';
        node.childNodes.forEach(child => {
            result += serializeNode(child);
        });
        return result;
    }

    function serializeNode(node) {
        if (node.nodeType === Node.TEXT_NODE) {
            return node.textContent || '';
        }

        if (node.nodeType !== Node.ELEMENT_NODE) {
            return '';
        }

        const element = node;
        const tag = element.tagName.toLowerCase();

        switch (tag) {
            case 'strong':
                return wrapMarkup('**', serializeChildren(element));
            case 'em':
                return wrapMarkup('*', serializeChildren(element));
            case 'span':
                if (element.classList.contains('rich-text-underline')) {
                    return wrapMarkup('++', serializeChildren(element));
                }
                if (element.classList.contains('rich-text-color')) {
                    const colorName = Array.from(element.classList)
                        .map(cls => cls.replace('rich-text-color-', ''))
                        .find(name => colorHexMap[name]);
                    if (colorName) {
                        return `{{color:${colorName}}}${serializeChildren(element)}{{/color}}`;
                    }
                }
                return serializeChildren(element);
            case 'del':
            case 's':
                return wrapMarkup('~~', serializeChildren(element));
            case 'mark':
                return wrapMarkup('==', serializeChildren(element));
            case 'u':
                return wrapMarkup('++', serializeChildren(element));
            case 'br':
                return '\n';
            case 'p':
            case 'div': {
                const content = serializeChildren(element);
                return content ? `${content}\n\n` : '\n\n';
            }
            case 'ul':
                return serializeList(element, '-');
            case 'ol':
                return serializeOrderedList(element);
            case 'li':
                return serializeChildren(element);
            case 'blockquote': {
                const lines = serializeChildren(element)
                    .split('\n')
                    .map(line => (line ? `> ${line}` : '>'))
                    .join('\n');
                return `${lines}\n`;
            }
            default:
                return serializeChildren(element);
        }
    }

    function serializeList(listElement, bullet) {
        const items = Array.from(listElement.children)
            .filter(child => child.tagName && child.tagName.toLowerCase() === 'li')
            .map(li => `${bullet} ${serializeChildren(li).trim()}`);
        if (!items.length) {
            return '';
        }
        return `\n${items.join('\n')}\n`;
    }

    function serializeOrderedList(listElement) {
        let index = 1;
        const items = Array.from(listElement.children)
            .filter(child => child.tagName && child.tagName.toLowerCase() === 'li')
            .map(li => `${index++}. ${serializeChildren(li).trim()}`);
        if (!items.length) {
            return '';
        }
        return `\n${items.join('\n')}\n`;
    }

    function wrapMarkup(marker, content) {
        const trimmed = content;
        if (!trimmed) {
            return '';
        }
        return `${marker}${trimmed}${marker}`;
    }

    function cleanupMarkup(markup) {
        return markup
            .replace(/\r\n/g, '\n')
            .replace(/\u00a0/g, ' ')
            .replace(/\n{3,}/g, '\n\n')
            .trim();
    }

    function escapeHtml(char) {
        if (char === '&') {
            return '&amp;';
        }
        if (char === '<') {
            return '&lt;';
        }
        if (char === '>') {
            return '&gt;';
        }
        if (char === '"') {
            return '&quot;';
        }
        if (char === "'") {
            return '&#39;';
        }
        return char;
    }

    function extractStyleValue(style, property) {
        const regex = new RegExp(`${property}\\s*:\\s*([^;]+)`, 'i');
        const match = regex.exec(style);
        return match ? match[1].trim() : null;
    }

    function normalizeColor(value) {
        if (!value) {
            return null;
        }

        const ctx = document.createElement('canvas').getContext('2d');
        if (!ctx) {
            return null;
        }

        ctx.fillStyle = value;
        const computed = ctx.fillStyle;
        ctx.fillStyle = computed;
        const rgb = ctx.fillStyle;

        if (rgb.startsWith('#')) {
            return rgb.toLowerCase();
        }

        const match = /rgba?\((\d+),\s*(\d+),\s*(\d+)/i.exec(rgb);
        if (!match) {
            return null;
        }

        const toHex = (component) => {
            const hex = parseInt(component, 10).toString(16);
            return hex.length === 1 ? `0${hex}` : hex;
        };

        return `#${toHex(match[1])}${toHex(match[2])}${toHex(match[3])}`;
    }

    function colorsEqual(a, b) {
        if (!a || !b) {
            return false;
        }
        return a.toLowerCase() === b.toLowerCase();
    }

    function findColorNameByHex(hex) {
        const lower = hex.toLowerCase();
        return Object.entries(colorHexMap).find(([, value]) => value === lower)?.[0] ?? null;
    }

    function triggerInput(element) {
        const event = new Event('input', { bubbles: true });
        element.dispatchEvent(event);
    }

    window.hOpsCreateMentionElement = createMentionElement;
})();
