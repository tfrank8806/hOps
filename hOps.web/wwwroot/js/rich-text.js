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

        const EMOJI_LIBRARY_URL = '/js/emoji-data.json';
    const RECENT_EMOJI_STORAGE_KEY = 'hops.richText.recentEmojis';
    const MAX_RECENT_EMOJIS = 18;
    const MAX_SEARCH_RESULTS = 80;

    const emojiLibraryState = {
        library: null,
        promise: null
    };

    const fallbackEmojiData = [
        {
            label: 'Smileys & Emotion',
            emojis: [
                { char: '😀', shortcode: ':grinning_face:' },
                { char: '😃', shortcode: ':grinning_face_with_big_eyes:' },
                { char: '😂', shortcode: ':joy:' },
                { char: '😊', shortcode: ':smiling_face_with_smiling_eyes:' },
                { char: '😍', shortcode: ':heart_eyes:' },
                { char: '😎', shortcode: ':sunglasses:' }
            ]
        },
        {
            label: 'Gestures & People',
            emojis: [
                { char: '👍', shortcode: ':thumbs_up:' },
                { char: '👏', shortcode: ':clap:' },
                { char: '🙏', shortcode: ':folded_hands:' },
                { char: '💪', shortcode: ':flexed_bicep:' },
                { char: '🤝', shortcode: ':handshake:' },
                { char: '👋', shortcode: ':waving_hand:' }
            ]
        },
        {
            label: 'Symbols & Celebration',
            emojis: [
                { char: '🎉', shortcode: ':party_popper:' },
                { char: '🎊', shortcode: ':confetti_ball:' },
                { char: '🔥', shortcode: ':fire:' },
                { char: '✨', shortcode: ':sparkles:' },
                { char: '✅', shortcode: ':check_mark_button:' },
                { char: '💡', shortcode: ':light_bulb:' }
            ]
        }
    ];

    const recentEmojiCache = loadRecentEmojiList();

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
    let selectionTrackingInitialized = false;
    let activeSelectionContext = null;

    initializeSelectionTracking();
    elements.forEach(initializeEditor);

    function initializeSelectionTracking() {
        if (selectionTrackingInitialized || typeof document === 'undefined') {
            return;
        }

        selectionTrackingInitialized = true;
        document.addEventListener('selectionchange', handleDocumentSelectionChange);
    }

    function handleDocumentSelectionChange() {
        if (!activeSelectionContext) {
            return;
        }

        const selection = window.getSelection();
        if (!selection || selection.rangeCount === 0) {
            return;
        }

        const { editor } = activeSelectionContext;
        if (!editor || !editor.contains(selection.anchorNode) || !editor.contains(selection.focusNode)) {
            return;
        }

        captureEditorSelection(activeSelectionContext);
    }

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

        const clipboardTargetSelector = textarea.dataset.clipboardUploadTarget;
        if (clipboardTargetSelector) {
            editor.dataset.clipboardUploadTarget = clipboardTargetSelector;
        }

        if (textarea.placeholder) {
            editor.dataset.placeholder = textarea.placeholder;
        }

        const context = {
            textarea,
            editor,
            wrapper,
            selectionState: null
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

        const recordSelection = () => captureEditorSelection(context);
        editor.addEventListener('input', () => {
            captureEditorSelection(context);
            syncToTextarea(context);
        });
        editor.addEventListener('blur', () => {
            if (activeSelectionContext === context) {
                activeSelectionContext = null;
            }
            captureEditorSelection(context);
            syncToTextarea(context);
        });
        editor.addEventListener('keyup', recordSelection);
        editor.addEventListener('mouseup', recordSelection);
        editor.addEventListener('touchend', recordSelection);
        editor.addEventListener('focus', () => {
            activeSelectionContext = context;
            recordSelection();
        });
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
        createEmojiPicker(toolbar, context);

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


    function createEmojiPicker(toolbar, context) {
        const emojiGroup = createGroup(toolbar);
        emojiGroup.classList.add('rich-text-toolbar__emoji-group');

        const toggle = document.createElement('button');
        toggle.type = 'button';
        toggle.className = 'btn btn-outline-secondary rich-text-emoji-toggle';
        toggle.textContent = '😊';
        toggle.title = 'Insert emoji';
        toggle.setAttribute('aria-haspopup', 'true');
        toggle.setAttribute('aria-expanded', 'false');
        emojiGroup.appendChild(toggle);

        const menu = document.createElement('div');
        menu.className = 'rich-text-emoji-menu';
        emojiGroup.appendChild(menu);

        let library = null;
        let sectionsContainer = null;
        let searchInput = null;
        let currentQuery = '';

        function showEmojiStatus(message) {
            menu.innerHTML = '';
            const status = document.createElement('div');
            status.className = 'rich-text-emoji-menu__status';
            status.textContent = message;
            menu.appendChild(status);
        }

        function buildMenuStructure() {
            menu.innerHTML = '';

            const searchWrapper = document.createElement('div');
            searchWrapper.className = 'rich-text-emoji-menu__search';

            searchInput = document.createElement('input');
            searchInput.type = 'search';
            searchInput.className = 'rich-text-emoji-menu__search-input';
            searchInput.placeholder = 'Search emoji';
            searchInput.setAttribute('aria-label', 'Search emoji');
            searchInput.addEventListener('input', () => {
                currentQuery = searchInput.value || '';
                renderEmojiSections();
            });

            searchWrapper.appendChild(searchInput);
            menu.appendChild(searchWrapper);

            sectionsContainer = document.createElement('div');
            sectionsContainer.className = 'rich-text-emoji-menu__sections';
            menu.appendChild(sectionsContainer);

            renderEmojiSections();
        }

        function renderEmojiSections() {
            if (!sectionsContainer || !library) {
                return;
            }

            sectionsContainer.innerHTML = '';
            const query = (currentQuery || '').trim().toLowerCase();

            if (query) {
                const matches = searchEmojiLibrary(library, query);
                if (!matches.length) {
                    sectionsContainer.appendChild(createEmojiEmptyState('No emojis match your search.'));
                    return;
                }

                const section = createEmojiSection(`Search results (${matches.length})`, matches);
                if (section) {
                    sectionsContainer.appendChild(section);
                }
                return;
            }

            const sections = [];
            const recentEntries = getRecentEmojiEntries(library);
            if (recentEntries.length) {
                const recentSection = createEmojiSection('Recently used', recentEntries);
                if (recentSection) {
                    sections.push(recentSection);
                }
            }

            library.categories.forEach(category => {
                const section = createEmojiSection(category.label, category.emojis);
                if (section) {
                    sections.push(section);
                }
            });

            if (!sections.length) {
                sectionsContainer.appendChild(createEmojiEmptyState('No emojis available.'));
                return;
            }

            sections.forEach(section => sectionsContainer.appendChild(section));
        }

        function createEmojiSection(label, emojis) {
            if (!emojis || !emojis.length) {
                return null;
            }

            const section = document.createElement('div');
            section.className = 'rich-text-emoji-menu__section';

            const heading = document.createElement('div');
            heading.className = 'rich-text-emoji-menu__label';
            heading.textContent = label;
            section.appendChild(heading);

            const grid = document.createElement('div');
            grid.className = 'rich-text-emoji-menu__grid';
            section.appendChild(grid);

            emojis.forEach(entry => {
                const item = document.createElement('button');
                item.type = 'button';
                item.className = 'rich-text-emoji-menu__emoji';
                item.textContent = entry.char;
                const ariaLabel = entry.displayLabel || entry.shortcode || entry.char;
                item.setAttribute('aria-label', `Insert ${ariaLabel}`);
                item.addEventListener('click', (event) => {
                    event.preventDefault();
                    insertEmoji(context, entry.char);
                    rememberRecentEmoji(entry.char);
                    if (searchInput) {
                        searchInput.value = '';
                        currentQuery = '';
                    }
                    renderEmojiSections();
                    hideMenu();
                });
                grid.appendChild(item);
            });

            return section;
        }

        function createEmojiEmptyState(message) {
            const empty = document.createElement('div');
            empty.className = 'rich-text-emoji-menu__empty';
            empty.textContent = message;
            return empty;
        }

        function ensureLibraryLoaded() {
            if (library && sectionsContainer) {
                return Promise.resolve();
            }

            showEmojiStatus('Loading emojis…');
            return getEmojiLibrary().then(result => {
                library = result;
                buildMenuStructure();
            });
        }

        function hideMenu() {
            menu.classList.remove('show');
            toggle.setAttribute('aria-expanded', 'false');
        }

        toggle.addEventListener('click', (event) => {
            event.preventDefault();
            captureEditorSelection(context);
            const willOpen = !menu.classList.contains('show');
            if (!willOpen) {
                hideMenu();
                return;
            }

            closeOtherEmojiMenus(menu);
            ensureLibraryLoaded().then(() => {
                menu.classList.add('show');
                toggle.setAttribute('aria-expanded', 'true');
                renderEmojiSections();
                if (searchInput) {
                    try {
                        searchInput.focus({ preventScroll: true });
                    } catch {
                        searchInput.focus();
                    }
                }
            });
        });

        document.addEventListener('click', (event) => {
            if (!menu.classList.contains('show')) {
                return;
            }
            if (emojiGroup.contains(event.target)) {
                return;
            }
            hideMenu();
        });
    }

    function closeOtherEmojiMenus(currentMenu) {
        document
            .querySelectorAll('.rich-text-emoji-menu.show')
            .forEach(menu => {
                if (menu === currentMenu) {
                    return;
                }
                menu.classList.remove('show');
                const toggle = menu.previousElementSibling;
                if (toggle && toggle.classList.contains('rich-text-emoji-toggle')) {
                    toggle.setAttribute('aria-expanded', 'false');
                }
            });
    }

    function getEmojiLibrary() {
        if (emojiLibraryState.library) {
            return Promise.resolve(emojiLibraryState.library);
        }

        if (emojiLibraryState.promise) {
            return emojiLibraryState.promise;
        }

        emojiLibraryState.promise = fetch(EMOJI_LIBRARY_URL, { cache: 'force-cache' })
            .then(response => {
                if (!response.ok) {
                    throw new Error('Failed to load emoji dataset.');
                }
                return response.json();
            })
            .then(payload => {
                const parsed = parseEmojiPayload(payload);
                let library = createEmojiLibrary(parsed);
                if (!library.flat.length) {
                    library = createEmojiLibrary(fallbackEmojiData);
                }
                return library;
            })
            .catch(error => {
                console.warn('Rich text editor: loading fallback emoji set.', error);
                return createEmojiLibrary(fallbackEmojiData);
            })
            .then(library => {
                emojiLibraryState.library = library;
                return library;
            })
            .finally(() => {
                emojiLibraryState.promise = null;
            });

        return emojiLibraryState.promise;
    }

    function parseEmojiPayload(payload) {
        if (!payload) {
            return [];
        }

        if (Array.isArray(payload)) {
            return payload;
        }

        if (Array.isArray(payload.categories)) {
            return payload.categories;
        }

        return [];
    }

    function createEmojiLibrary(rawCategories) {
        const categories = [];
        const flat = [];
        const map = new Map();

        (rawCategories || []).forEach(rawCategory => {
            if (!rawCategory || !Array.isArray(rawCategory.emojis)) {
                return;
            }

            const label = rawCategory.label || 'Emoji';
            const normalizedEmojis = [];

            rawCategory.emojis.forEach(rawEmoji => {
                const entry = normalizeEmojiEntry(rawEmoji, label);
                if (!entry) {
                    return;
                }

                normalizedEmojis.push(entry);
                if (!map.has(entry.char)) {
                    map.set(entry.char, entry);
                }
                flat.push(entry);
            });

            if (normalizedEmojis.length) {
                categories.push({
                    label,
                    emojis: normalizedEmojis
                });
            }
        });

        return {
            categories,
            flat,
            map
        };
    }

    function normalizeEmojiEntry(rawEmoji, categoryLabel) {
        if (!rawEmoji && rawEmoji !== 0) {
            return null;
        }

        let char = '';
        let shortcode = '';
        let description = '';
        let keywords = [];

        if (typeof rawEmoji === 'string') {
            char = rawEmoji;
        } else if (typeof rawEmoji === 'object') {
            char = rawEmoji.char || rawEmoji.character || rawEmoji.emoji || '';
            shortcode = rawEmoji.shortcode || rawEmoji.short_name || rawEmoji.shortName || rawEmoji.name || '';
            description = rawEmoji.name || rawEmoji.description || '';
            if (Array.isArray(rawEmoji.keywords)) {
                keywords = rawEmoji.keywords
                    .map(value => (value ? value.toString() : ''))
                    .filter(Boolean);
            } else if (typeof rawEmoji.keywords === 'string') {
                keywords = rawEmoji.keywords
                    .split(/[,\s]+/)
                    .map(value => value.trim())
                    .filter(Boolean);
            }
        }

        if (!char) {
            return null;
        }

        const searchParts = [char, shortcode, description, categoryLabel]
            .concat(keywords)
            .filter(Boolean)
            .map(value => value.toString().toLowerCase());

        return {
            char,
            shortcode,
            keywords,
            description,
            category: categoryLabel,
            searchText: searchParts.join(' '),
            displayLabel: description || shortcode || ''
        };
    }

    function searchEmojiLibrary(library, query) {
        if (!library || !library.flat) {
            return [];
        }

        const normalized = (query || '').trim().toLowerCase();
        if (!normalized) {
            return library.flat.slice(0, MAX_SEARCH_RESULTS);
        }

        const results = [];
        for (let index = 0; index < library.flat.length; index += 1) {
            const entry = library.flat[index];
            if (entry.searchText.includes(normalized)) {
                results.push(entry);
            }
            if (results.length >= MAX_SEARCH_RESULTS) {
                break;
            }
        }

        return results;
    }

    function getRecentEmojiEntries(library) {
        if (!library || !library.map) {
            return [];
        }

        return recentEmojiCache
            .map(char => library.map.get(char))
            .filter(Boolean);
    }

    function rememberRecentEmoji(char) {
        if (!char) {
            return;
        }

        const existingIndex = recentEmojiCache.indexOf(char);
        if (existingIndex >= 0) {
            recentEmojiCache.splice(existingIndex, 1);
        }

        recentEmojiCache.unshift(char);
        if (recentEmojiCache.length > MAX_RECENT_EMOJIS) {
            recentEmojiCache.length = MAX_RECENT_EMOJIS;
        }

        persistRecentEmojiList();
    }

    function persistRecentEmojiList() {
        if (typeof window === 'undefined' || !window.localStorage) {
            return;
        }

        try {
            window.localStorage.setItem(RECENT_EMOJI_STORAGE_KEY, JSON.stringify(recentEmojiCache));
        } catch (error) {
            console.debug('Rich text editor: unable to save recent emojis.', error);
        }
    }

    function loadRecentEmojiList() {
        if (typeof window === 'undefined' || !window.localStorage) {
            return [];
        }

        try {
            const raw = window.localStorage.getItem(RECENT_EMOJI_STORAGE_KEY);
            if (!raw) {
                return [];
            }

            const parsed = JSON.parse(raw);
            if (!Array.isArray(parsed)) {
                return [];
            }

            return parsed
                .map(entry => (entry && entry.toString ? entry.toString() : ''))
                .filter(Boolean)
                .slice(0, MAX_RECENT_EMOJIS);
        } catch {
            return [];
        }
    }

    function insertEmoji(context, emoji) {
        if (!context || !context.editor || !emoji) {
            return;
        }

        focusEditor(context);

        let inserted = replaceSelectionWithText(context.editor, emoji);
        if (!inserted) {
            if (placeCaretAtEnd(context.editor)) {
                inserted = replaceSelectionWithText(context.editor, emoji);
            }
        }

        if (!inserted) {
            context.editor.appendChild(document.createTextNode(emoji));
            placeCaretAtEnd(context.editor);
        }

        captureEditorSelection(context);
        syncToTextarea(context);
    }

    function replaceSelectionWithText(editor, text) {
        if (!editor || !text) {
            return false;
        }

        const selection = window.getSelection();
        if (!selection || selection.rangeCount === 0) {
            return false;
        }

        const range = selection.getRangeAt(0);
        if (!editor.contains(range.startContainer) || !editor.contains(range.endContainer)) {
            return false;
        }

        range.deleteContents();
        const textNode = document.createTextNode(text);
        range.insertNode(textNode);
        range.setStartAfter(textNode);
        range.collapse(true);

        selection.removeAllRanges();
        selection.addRange(range);
        return true;
    }

    function placeCaretAtEnd(editor) {
        if (!editor) {
            return false;
        }

        const selection = window.getSelection();
        if (!selection) {
            return false;
        }

        const range = document.createRange();
        range.selectNodeContents(editor);
        range.collapse(false);
        selection.removeAllRanges();
        selection.addRange(range);
        return true;
    }

    function handlePaste(event, context) {
        if (window.ClipboardUploads && typeof window.ClipboardUploads.tryHandlePaste === 'function') {
            try {
                window.ClipboardUploads.tryHandlePaste(event, context.editor);
            } catch (error) {
                console.warn('Rich text editor: clipboard upload handler failed.', error);
            }
        }

        event.preventDefault();
        const clipboardData = event.clipboardData || window.clipboardData;
        const text = clipboardData ? clipboardData.getData('text/plain') : '';
        focusEditor(context);
        if (text) {
            document.execCommand('insertText', false, text);
        }
        syncToTextarea(context);
    }

    function handleKeydown(event, context) {
        if (!context || !context.editor) {
            return;
        }

        if (event.key === 'ArrowUp' || event.key === 'ArrowDown') {
            if (context.editor.dataset.mentionsActive !== 'true') {
                if (event.key === 'ArrowUp' && isCaretAtBoundary(context.editor, 'start')) {
                    event.preventDefault();
                    return;
                }
                if (event.key === 'ArrowDown' && isCaretAtBoundary(context.editor, 'end')) {
                    event.preventDefault();
                    return;
                }
            }
        }

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

    function isCaretAtBoundary(editor, boundary) {
        const selection = window.getSelection();
        if (!selection || selection.rangeCount === 0 || !selection.isCollapsed) {
            return false;
        }
        const range = selection.getRangeAt(0);
        if (!editor.contains(range.startContainer)) {
            return false;
        }
        const probe = document.createRange();
        probe.selectNodeContents(editor);
        if (boundary === 'start') {
            probe.setEnd(range.startContainer, range.startOffset);
            return probe.collapsed;
        }
        probe.setStart(range.endContainer, range.endOffset);
        return probe.collapsed;
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
        restoreEditorSelection(context);
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
            if (selectionState) {
                const refreshedState = saveSelectionState(editor);
                context.selectionState = refreshedState || selectionState;
            }
        }
        const markup = htmlToMarkup(editor);
        textarea.value = markup;
        triggerInput(textarea);
    }

    function captureEditorSelection(context) {
        if (!context || !context.editor) {
            return;
        }
        const state = saveSelectionState(context.editor);
        if (state) {
            context.selectionState = state;
        }
    }

    function restoreEditorSelection(context) {
        if (!context || !context.editor || !context.selectionState) {
            return;
        }
        restoreSelectionState(context.editor, context.selectionState);
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
                const tagName = current.tagName ? current.tagName.toLowerCase() : '';
                if (tagName === 'br') {
                    continue;
                }
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
            const text = node.textContent || '';
            if (!text.trim()) {
                return '';
            }
            return text;
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
        if (!content) {
            return '';
        }

        const leadingMatch = content.match(/^\s+/);
        const trailingMatch = content.match(/\s+$/);
        const leading = leadingMatch ? leadingMatch[0] : '';
        const trailing = trailingMatch ? trailingMatch[0] : '';
        const core = content.trim();

        if (!core) {
            return content;
        }

        return `${leading}${marker}${core}${marker}${trailing}`;
    }

    function cleanupMarkup(markup) {
        if (!markup) {
            return '';
        }

        return markup.replace(/\r\n/g, '\n');
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
