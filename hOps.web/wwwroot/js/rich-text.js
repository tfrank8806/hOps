(function () {
    const selector = '[data-rich-text="true"]';
    const editors = Array.from(document.querySelectorAll(selector));
    if (!editors.length) {
        return;
    }

    editors.forEach(initializeEditor);

    function initializeEditor(textarea) {
        if (textarea.dataset.richTextInitialized === 'true') {
            return;
        }

        textarea.dataset.richTextInitialized = 'true';
        textarea.classList.add('rich-text-input');

        const toolbar = buildToolbar(textarea);
        const parent = textarea.parentNode;
        if (parent) {
            parent.insertBefore(toolbar, textarea);
        }

        textarea.addEventListener('keydown', (event) => {
            if (!(event.ctrlKey || event.metaKey)) {
                return;
            }

            const key = event.key.toLowerCase();
            if (key === 'b') {
                event.preventDefault();
                toggleWrap(textarea, '**', '**', 'bold text');
            } else if (key === 'i') {
                event.preventDefault();
                toggleWrap(textarea, '*', '*', 'italic text');
            } else if (key === 'u') {
                event.preventDefault();
                toggleWrap(textarea, '++', '++', 'underlined text');
            }
        });
    }

    function buildToolbar(textarea) {
        const toolbar = document.createElement('div');
        toolbar.className = 'rich-text-toolbar btn-toolbar mb-2';

        const emphasisGroup = createGroup(toolbar);
        addButton(emphasisGroup, {
            label: 'B',
            title: 'Bold (Ctrl+B)',
            action: () => toggleWrap(textarea, '**', '**', 'bold text')
        });
        addButton(emphasisGroup, {
            label: 'I',
            title: 'Italic (Ctrl+I)',
            action: () => toggleWrap(textarea, '*', '*', 'italic text')
        });
        addButton(emphasisGroup, {
            label: 'U',
            title: 'Underline (Ctrl+U)',
            action: () => toggleWrap(textarea, '++', '++', 'underlined text')
        });
        addButton(emphasisGroup, {
            label: 'S',
            title: 'Strikethrough',
            action: () => toggleWrap(textarea, '~~', '~~', 'strikethrough')
        });

        const listGroup = createGroup(toolbar);
        addButton(listGroup, {
            label: '•',
            title: 'Bulleted list',
            action: () => toggleUnorderedList(textarea)
        });
        addButton(listGroup, {
            label: '1.',
            title: 'Numbered list',
            action: () => toggleOrderedList(textarea)
        });
        addButton(listGroup, {
            label: '❝',
            title: 'Quote',
            action: () => toggleQuote(textarea)
        });

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

    function toggleWrap(textarea, before, after, placeholder) {
        const selection = getSelection(textarea);
        const selectedText = selection.text;

        if (selectedText.startsWith(before) && selectedText.endsWith(after)) {
            const unwrapped = selectedText.slice(before.length, selectedText.length - after.length);
            replaceRange(textarea, selection.start, selection.end, unwrapped, true);
            return;
        }

        const content = selectedText || placeholder;
        const wrapped = `${before}${content}${after}`;
        const range = replaceRange(textarea, selection.start, selection.end, wrapped, true);
        textarea.setSelectionRange(range.start + before.length, range.end - after.length);
    }

    function toggleUnorderedList(textarea) {
        toggleLineFormatting(
            textarea,
            (line, _index, removeExisting) => {
                if (!line.trim()) {
                    return line;
                }
                if (removeExisting) {
                    return line.replace(/^(\s*)[-*]\s+/, '$1');
                }
                if (/^\s*[-*]\s+/.test(line)) {
                    return line;
                }
                return line.replace(/^(\s*)/, '$1- ');
            },
            (line) => /^\s*[-*]\s+/.test(line) || !line.trim()
        );
    }

    function toggleOrderedList(textarea) {
        toggleLineFormatting(
            textarea,
            (line, index, removeExisting) => {
                if (!line.trim()) {
                    return line;
                }
                if (removeExisting) {
                    return line.replace(/^(\s*)\d+\.\s+/, '$1');
                }
                if (/^\s*\d+\.\s+/.test(line)) {
                    return line;
                }
                return line.replace(/^(\s*)/, (_, spaces) => `${spaces}${index + 1}. `);
            },
            (line) => /^\s*\d+\.\s+/.test(line) || !line.trim()
        );
    }

    function toggleQuote(textarea) {
        toggleLineFormatting(
            textarea,
            (line, _index, removeExisting) => {
                if (!line.trim()) {
                    return line;
                }
                if (removeExisting) {
                    return line.replace(/^\s*>\s?/, '');
                }
                if (/^\s*>\s?/.test(line)) {
                    return line;
                }
                return line.replace(/^(\s*)/, '$1> ');
            },
            (line) => /^\s*>/.test(line) || !line.trim()
        );
    }

    function toggleLineFormatting(textarea, formatter, predicate) {
        const selection = getExpandedSelection(textarea);
        const lines = selection.text.split(/\r?\n/);
        const removeExisting = lines.every(predicate);
        const updated = lines.map((line, index) => formatter(line, index, removeExisting));
        const newContent = updated.join('\n');
        replaceRange(textarea, selection.start, selection.end, newContent, false);
        textarea.setSelectionRange(selection.start, selection.start + newContent.length);
        textarea.focus();
    }

    function getSelection(textarea) {
        const start = textarea.selectionStart ?? 0;
        const end = textarea.selectionEnd ?? start;
        return {
            start,
            end,
            text: textarea.value.slice(start, end)
        };
    }

    function getExpandedSelection(textarea) {
        const value = textarea.value;
        let start = textarea.selectionStart ?? 0;
        let end = textarea.selectionEnd ?? start;

        let lineStart = value.lastIndexOf('\n', start - 1);
        if (lineStart === -1) {
            lineStart = 0;
        } else {
            lineStart += 1;
        }

        let lineEnd = value.indexOf('\n', end);
        if (lineEnd === -1) {
            lineEnd = value.length;
        }

        return {
            start: lineStart,
            end: lineEnd,
            text: value.slice(lineStart, lineEnd)
        };
    }

    function replaceRange(textarea, start, end, replacement, focus) {
        const value = textarea.value;
        textarea.value = value.slice(0, start) + replacement + value.slice(end);
        if (focus) {
            textarea.focus();
        }
        triggerInput(textarea);
        return {
            start,
            end: start + replacement.length
        };
    }

    function triggerInput(element) {
        const event = new Event('input', { bubbles: true });
        element.dispatchEvent(event);
    }
})();
