document.addEventListener('DOMContentLoaded', () => {
    const launcher = document.getElementById('messagesLauncher');
    const panel = document.getElementById('messagesFloatingPanel');
    const closeButton = document.getElementById('messagesFloatingClose');
    const backdrop = document.getElementById('messagesFloatingBackdrop');

    if (!launcher || !panel || !closeButton || !backdrop) {
        return;
    }

    let lastFocusedElement = null;

    function openPanel() {
        lastFocusedElement = document.activeElement instanceof HTMLElement ? document.activeElement : null;
        panel.classList.add('messages-floating-panel--open');
        panel.setAttribute('aria-hidden', 'false');
        launcher.setAttribute('aria-expanded', 'true');
        backdrop.classList.add('messages-floating-backdrop--visible');
        closeButton.focus();
    }

    function closePanel() {
        panel.classList.remove('messages-floating-panel--open');
        panel.setAttribute('aria-hidden', 'true');
        launcher.setAttribute('aria-expanded', 'false');
        backdrop.classList.remove('messages-floating-backdrop--visible');
        if (lastFocusedElement && document.body.contains(lastFocusedElement)) {
            lastFocusedElement.focus();
        } else {
            launcher.focus();
        }
    }

    function togglePanel() {
        const isOpen = panel.classList.contains('messages-floating-panel--open');
        if (isOpen) {
            closePanel();
        } else {
            openPanel();
        }
    }

    launcher.addEventListener('click', togglePanel);
    closeButton.addEventListener('click', closePanel);
    backdrop.addEventListener('click', closePanel);

    document.addEventListener('keydown', event => {
        if (event.key === 'Escape' && panel.classList.contains('messages-floating-panel--open')) {
            closePanel();
        }
    });
});
