(function () {
    const conversationListEl = document.querySelector('[data-dm-conversation-list]');
    const conversationListUrl = conversationListEl?.dataset?.dmConversationListUrl ?? '';
    const threadEl = document.querySelector('[data-dm-thread]');
    const threadUrl = threadEl?.dataset?.dmThreadUrl ?? '';
    const threadScrollEl = document.querySelector('[data-dm-thread-scroll]') || document.getElementById('directMessageScroll');
    const POLL_INTERVAL_MS = 45000;

    function autoScrollThread(force = false) {
        if (!threadScrollEl) {
            return;
        }

        const delta = threadScrollEl.scrollHeight - threadScrollEl.clientHeight - threadScrollEl.scrollTop;
        const isNearBottom = delta <= 40;
        if (force || isNearBottom) {
            threadScrollEl.scrollTop = threadScrollEl.scrollHeight;
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

    function getActiveConversationId() {
        const fromThread = threadEl?.dataset?.dmActiveConversationId;
        const fromList = conversationListEl?.dataset?.dmActiveConversationId;
        const raw = fromThread || fromList || '';
        const parsed = parseInt(raw, 10);
        return Number.isFinite(parsed) && parsed > 0 ? parsed : 0;
    }

    function setActiveConversationId(conversationId) {
        const value = Number.isFinite(conversationId) && conversationId > 0 ? conversationId.toString() : '0';
        if (conversationListEl) {
            conversationListEl.dataset.dmActiveConversationId = value;
        }
        if (threadEl) {
            threadEl.dataset.dmActiveConversationId = value;
        }
    }

    function buildUrl(baseUrl, params = {}) {
        if (!baseUrl) {
            return null;
        }

        try {
            const url = new URL(baseUrl, window.location.origin);
            Object.entries(params).forEach(([key, value]) => {
                if (value !== undefined && value !== null && value !== '') {
                    url.searchParams.set(key, value);
                }
            });
            return url;
        } catch (error) {
            console.error('Failed to build URL for direct messages', error);
            return null;
        }
    }

    async function refreshConversationList(options = {}) {
        if (!conversationListEl || !conversationListUrl) {
            return false;
        }

        const targetUrl = buildUrl(conversationListUrl, { conversationId: getActiveConversationId() || undefined });
        if (!targetUrl) {
            return false;
        }

        try {
            const response = await fetch(targetUrl.toString(), {
                headers: {
                    'X-Requested-With': 'XMLHttpRequest'
                },
                credentials: 'same-origin'
            });

            if (!response.ok) {
                throw new Error(`Conversation list refresh failed with status ${response.status}`);
            }

            const html = await response.text();
            conversationListEl.innerHTML = html;
            return true;
        } catch (error) {
            if (!options.silent) {
                console.error('Unable to refresh conversation list', error);
            }
            return false;
        }
    }

    function dispatchMessageSyncEvent(action = 'sync') {
        try {
            const detail = {
                type: 'message',
                action,
                source: 'direct-messages'
            };
            window.dispatchEvent(new CustomEvent('realtime:notification', { detail }));
        } catch (error) {
            console.error('Unable to dispatch message sync event', error);
        }
    }

    async function refreshMessageThread(conversationId, options = {}) {
        const {
            silent = false,
            forceScroll = false,
            notifyMessageSync = false,
            refreshListAfter = false
        } = options;

        if (!threadEl || !threadUrl || !conversationId) {
            return false;
        }

        const targetUrl = buildUrl(threadUrl, { conversationId });
        if (!targetUrl) {
            return false;
        }

        const wasNearBottom = threadScrollEl
            ? (threadScrollEl.scrollHeight - threadScrollEl.clientHeight - threadScrollEl.scrollTop) <= 40
            : false;

        try {
            const response = await fetch(targetUrl.toString(), {
                headers: {
                    'X-Requested-With': 'XMLHttpRequest'
                },
                credentials: 'same-origin'
            });

            if (!response.ok) {
                throw new Error(`Conversation refresh failed with status ${response.status}`);
            }

            const html = await response.text();
            if (threadScrollEl) {
                threadScrollEl.innerHTML = html;
            }
            setActiveConversationId(conversationId);

            if (refreshListAfter) {
                await refreshConversationList({ silent: true });
            }

            if (notifyMessageSync) {
                dispatchMessageSyncEvent('read');
            }

            if (forceScroll || wasNearBottom) {
                autoScrollThread(true);
            }
            return true;
        } catch (error) {
            if (!silent) {
                console.error('Unable to refresh conversation thread', error);
            }
            return false;
        }
    }

    function parseConversationIdFromPayload(payload) {
        if (!payload) {
            return 0;
        }

        const type = (payload.type ?? payload.Type ?? '').toString().toLowerCase();
        if (type !== 'message') {
            return 0;
        }

        const rawUrl = payload.url ?? payload.Url ?? '';
        if (!rawUrl) {
            return 0;
        }

        try {
            const url = new URL(rawUrl, window.location.origin);
            const idParam = url.searchParams.get('conversationId');
            if (!idParam) {
                return 0;
            }

            const parsed = parseInt(idParam, 10);
            return Number.isFinite(parsed) && parsed > 0 ? parsed : 0;
        } catch (error) {
            console.error('Failed to parse conversation id from notification', error);
            return 0;
        }
    }

    function handleRealtimeNotification(event) {
        const conversationId = parseConversationIdFromPayload(event?.detail);
        if (conversationId === 0) {
            return;
        }

        refreshConversationList({ silent: true });

        if (conversationId === getActiveConversationId()) {
            refreshMessageThread(conversationId, {
                silent: true,
                refreshListAfter: true,
                notifyMessageSync: true
            });
        }
    }

    function schedulePolling() {
        if (!conversationListEl) {
            return;
        }

        const execute = () => {
            refreshConversationList({ silent: true });
            const activeId = getActiveConversationId();
            if (activeId) {
                refreshMessageThread(activeId, { silent: true });
            }
        };

        const intervalId = window.setInterval(() => {
            if (document.visibilityState !== 'visible') {
                return;
            }
            execute();
        }, POLL_INTERVAL_MS);

        document.addEventListener('visibilitychange', () => {
            if (document.visibilityState === 'visible') {
                execute();
            }
        });

        window.addEventListener('unload', () => {
            window.clearInterval(intervalId);
        });
    }

    function initRealtime() {
        window.addEventListener('realtime:notification', handleRealtimeNotification);
    }

    function init() {
        autoScrollThread(true);
        bindNewConversationState();
        bindExistingConversationRedirect();
        initRealtime();
        schedulePolling();

        if (getActiveConversationId()) {
            dispatchMessageSyncEvent('initial');
        }
    }

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', init);
    } else {
        init();
    }
})();
