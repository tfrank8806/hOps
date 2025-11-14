(function () {
    const triggerBadge = document.getElementById('messagesMenuTriggerBadge');
    const conversationBadge = document.getElementById('messagesMenuConversationsBadge');
    const alertsBadge = document.getElementById('messagesMenuAlertsBadge');
    const menu = document.getElementById('messagesMenu');

    if (!menu || !triggerBadge || !conversationBadge || !alertsBadge) {
        return;
    }

    const applyCountToBadge = (badge, count) => {
        if (!badge) {
            return;
        }

        if (count > 0) {
            const displayValue = count > 99 ? '99+' : count.toString();
            badge.textContent = displayValue;
            badge.classList.remove('d-none');
        } else {
            badge.classList.add('d-none');
        }
    };

    const refreshUnreadCount = async () => {
        try {
            const response = await fetch('/Notifications/Summary', {
                headers: {
                    'Accept': 'application/json'
                },
                credentials: 'same-origin'
            });

            if (!response.ok) {
                throw new Error('Failed to load message summary.');
            }

            const data = await response.json();
            const totalUnread = typeof data.totalUnread === 'number' ? data.totalUnread : 0;
            const unreadConversations = typeof data.unreadConversations === 'number' ? data.unreadConversations : 0;
            const unreadAlerts = typeof data.unreadAlerts === 'number' ? data.unreadAlerts : 0;
            applyCountToBadge(triggerBadge, totalUnread);
            applyCountToBadge(conversationBadge, unreadConversations);
            applyCountToBadge(alertsBadge, unreadAlerts);
        } catch (error) {
            console.error('Unable to refresh message counts:', error);
        }
    };

    refreshUnreadCount();

    const intervalId = window.setInterval(refreshUnreadCount, 60000);

    const handleRealtimeNotification = (event) => {
        const type = (event?.detail?.type ?? event?.detail?.Type ?? '').toString().toLowerCase();
        if (type) {
            refreshUnreadCount();
        }
    };

    window.addEventListener('realtime:notification', handleRealtimeNotification);

    document.addEventListener('visibilitychange', () => {
        if (document.visibilityState === 'visible') {
            refreshUnreadCount();
        }
    });

    window.addEventListener('unload', () => {
        window.clearInterval(intervalId);
        window.removeEventListener('realtime:notification', handleRealtimeNotification);
    });
})();
