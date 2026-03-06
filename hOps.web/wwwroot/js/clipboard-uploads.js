(function () {
    const SELECTOR = '[data-clipboard-upload-target]';
    const ACCEPTED_IMAGE_TYPES = [
        'image/png',
        'image/jpeg',
        'image/jpg',
        'image/webp',
        'image/gif',
        'image/bmp',
        'image/heic',
        'image/heif'
    ];
    const FALLBACK_EXTENSION = 'png';
    const FEEDBACK_CLASS = 'clipboard-upload-feedback';
    const HIGHLIGHT_CLASS = 'clipboard-upload-target--highlight';
    const FEEDBACK_DURATION_MS = 4000;

    function init() {
        document.addEventListener('paste', (event) => {
            tryHandlePaste(event);
        });
    }

    function tryHandlePaste(event, explicitTarget) {
        if (!event) {
            return false;
        }

        if (event.__clipboardUploadHandled) {
            return false;
        }

        const targetElement = explicitTarget || resolveTarget(event.target);
        if (!targetElement) {
            return false;
        }

        const clipboardData = event.clipboardData || window.clipboardData;
        if (!clipboardData) {
            return false;
        }

        const files = extractImageFiles(clipboardData);
        if (!files.length) {
            return false;
        }

        const input = resolveFileInput(targetElement);
        if (!input) {
            return false;
        }

        if (!appendFilesToInput(input, files)) {
            return false;
        }

        event.__clipboardUploadHandled = true;
        showFeedback(targetElement, input, files.length);
        return true;
    }

    function resolveTarget(node) {
        if (!(node instanceof Element)) {
            return null;
        }
        return node.closest(SELECTOR);
    }

    function resolveFileInput(element) {
        if (!(element instanceof Element)) {
            return null;
        }

        const selector = element.getAttribute('data-clipboard-upload-target');
        if (!selector) {
            return null;
        }

        try {
            const resolved = document.querySelector(selector);
            if (resolved && resolved.tagName === 'INPUT' && resolved.type === 'file') {
                return resolved;
            }
        } catch {
            return null;
        }

        return null;
    }

    function extractImageFiles(clipboardData) {
        const blobs = [];

        if (clipboardData.files && clipboardData.files.length) {
            for (const file of clipboardData.files) {
                if (isAcceptedImageType(file?.type)) {
                    blobs.push(file);
                }
            }
        } else if (clipboardData.items && clipboardData.items.length) {
            for (const item of clipboardData.items) {
                if (item.kind === 'file' && isAcceptedImageType(item.type)) {
                    const file = item.getAsFile();
                    if (file) {
                        blobs.push(file);
                    }
                }
            }
        }

        return blobs.map((blob, index) => convertBlobToFile(blob, index));
    }

    function isAcceptedImageType(type) {
        if (!type || typeof type !== 'string') {
            return false;
        }
        const normalized = type.toLowerCase();
        if (ACCEPTED_IMAGE_TYPES.includes(normalized)) {
            return true;
        }
        return normalized.startsWith('image/');
    }

    function convertBlobToFile(blob, index) {
        const extension = getExtensionForType(blob?.type) || FALLBACK_EXTENSION;
        const timestamp = new Date()
            .toISOString()
            .replace(/[:.]/g, '-');
        const fileName = `pasted-image-${timestamp}-${index + 1}.${extension}`;

        try {
            return new File([blob], fileName, { type: blob?.type || `image/${extension}` });
        } catch {
            blob.name = fileName;
            return blob;
        }
    }

    function getExtensionForType(type) {
        if (!type || typeof type !== 'string') {
            return null;
        }
        const normalized = type.toLowerCase();
        const known = {
            'image/png': 'png',
            'image/jpeg': 'jpg',
            'image/jpg': 'jpg',
            'image/gif': 'gif',
            'image/webp': 'webp',
            'image/bmp': 'bmp',
            'image/heic': 'heic',
            'image/heif': 'heif'
        };
        return known[normalized] || normalized.replace('image/', '');
    }

    function appendFilesToInput(input, files) {
        let dataTransfer;
        try {
            dataTransfer = new DataTransfer();
        } catch (error) {
            console.warn('Clipboard upload: unable to append files programmatically.', error);
            return false;
        }

        if (input.files && input.files.length) {
            Array.from(input.files).forEach(file => dataTransfer.items.add(file));
        }

        files.forEach(file => {
            if (file) {
                dataTransfer.items.add(file);
            }
        });

        input.files = dataTransfer.files;
        input.dispatchEvent(new Event('change', { bubbles: true }));
        return true;
    }

    function showFeedback(targetElement, input, count) {
        if (!(targetElement instanceof Element)) {
            return;
        }

        removeExistingFeedback(targetElement);

        const message = count === 1
            ? 'Added 1 image to attachments.'
            : `Added ${count} images to attachments.`;

        const feedback = document.createElement('div');
        feedback.className = `${FEEDBACK_CLASS} text-success small`;
        feedback.textContent = message;

        const parent = targetElement.parentElement || targetElement;
        parent.appendChild(feedback);

        if (input && input.classList) {
            input.classList.add(HIGHLIGHT_CLASS);
            window.setTimeout(() => input.classList.remove(HIGHLIGHT_CLASS), FEEDBACK_DURATION_MS);
        }

        window.setTimeout(() => {
            feedback.remove();
        }, FEEDBACK_DURATION_MS);
    }

    function removeExistingFeedback(element) {
        const parent = element.parentElement;
        if (!parent) {
            return;
        }
        parent
            .querySelectorAll(`.${FEEDBACK_CLASS}`)
            .forEach(node => node.remove());
    }

    init();

    window.ClipboardUploads = {
        tryHandlePaste: tryHandlePaste
    };
})();
