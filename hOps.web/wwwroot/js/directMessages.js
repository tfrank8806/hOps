(function () {
    function autoScrollThread() {
        const container = document.getElementById('directMessageScroll');
        if (container) {
            container.scrollTop = container.scrollHeight;
        }
    }

    function bindNewConversationState() {
        const collapses = document.querySelectorAll('[data-dm-new-conversation="true"]');
        if (collapses.length === 0) {
            return;
        }

        const CLASS_HIDDEN = 'dm-hidden-by-new-conversation';
        let openCount = 0;

        const updateBodyClass = () => {
            document.body.classList.toggle('dm-new-conversation-open', openCount > 0);
        };

        collapses.forEach(collapse => {
            const widgetRoot = collapse.closest('.direct-messages-widget');
            const pageRoot = collapse.closest('.direct-messages-page');

            const toggleTargets = (isOpen) => {
                const targets = [
                    widgetRoot?.querySelector('.direct-messages-widget__thread'),
                    widgetRoot?.querySelector('.direct-message-composer'),
                    pageRoot?.querySelector('.direct-message-thread'),
                    pageRoot?.querySelector('.direct-message-composer')
                ];

                targets.forEach(element => {
                    if (element) {
                        element.classList.toggle(CLASS_HIDDEN, isOpen);
                    }
                });
            };

            if (collapse.classList.contains('show')) {
                openCount += 1;
                toggleTargets(true);
            }

            collapse.addEventListener('shown.bs.collapse', () => {
                openCount += 1;
                toggleTargets(true);
                updateBodyClass();
            });

            collapse.addEventListener('hidden.bs.collapse', () => {
                openCount = Math.max(0, openCount - 1);
                toggleTargets(false);
                updateBodyClass();
            });
        });

        updateBodyClass();
    }

    function bindExistingConversationRedirect() {
        const forms = document.querySelectorAll('form[data-dm-existing-url]');
        forms.forEach(form => {
            const select = form.querySelector('select[name="RecipientUserId"]');
            if (!select) {
                return;
            }

            const baseUrl = form.dataset.dmExistingUrl;
            if (!baseUrl) {
                return;
            }

            select.addEventListener('change', () => {
                const selected = select.selectedOptions[0];
                const conversationId = selected?.dataset?.conversationId;
                if (!conversationId) {
                    return;
                }

                try {
                    const url = new URL(baseUrl, window.location.origin);
                    url.searchParams.set('conversationId', conversationId);
                    window.location.href = url.toString();
                } catch (err) {
                    console.error('Failed to navigate to conversation', err);
                }
            });
        });
    }

    function init() {
        autoScrollThread();
        bindNewConversationState();
        bindExistingConversationRedirect();
    }

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', init);
    } else {
        init();
    }
})();
