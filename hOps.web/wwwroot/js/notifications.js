(function () {
    const menu = document.getElementById('notificationsMenu');
    const badge = document.getElementById('notificationBadge');
    const list = document.getElementById('notificationList');
    if (!menu || !badge || !list) {
        return;
    }

    const escapeHtml = (value) => {
        const element = document.createElement('textarea');
        element.textContent = value ?? '';
        return element.innerHTML;
    };

    const renderNotifications = (data) => {
        const items = Array.isArray(data.items) ? data.items : [];
        if (!items.length) {
            list.innerHTML = '<span class="dropdown-item-text text-muted">No new notifications.</span>';
        } else {
            list.innerHTML = items.map(item => {
                const title = item.title ?? 'Notification';
                const content = item.content ? <div class="small text-muted"></div> : '';
                const time = item.createdAt ? <div class="small text-muted"></div> : '';
                const linkStart = item.linkUrl ? <a class="dropdown-item" href=""> : '<div class="dropdown-item">';
                const linkEnd = item.linkUrl ? '</a>' : '</div>';
                const unreadClass = item.isRead ? '' : 'fw-semibold';
                return ${linkStart}<div class=""></div>;
            }).join('');
        }

        if (typeof data.unreadCount === 'number' && data.unreadCount > 0) {
            badge.textContent = data.unreadCount > 9 ? '9+' : data.unreadCount;
            badge.classList.remove('d-none');
        } else {
            badge.classList.add('d-none');
        }
    };

    const fetchSummary = async () => {
        try {
            const response = await fetch('/Notifications/Summary', { headers: { 'Accept': 'application/json' } });
            if (!response.ok) {
                throw new Error('Network response was not ok');
            }
            const data = await response.json();
            renderNotifications(data);
        } catch (error) {
            list.innerHTML = '<span class="dropdown-item-text text-danger">Unable to load notifications.</span>';
            badge.classList.add('d-none');
        }
    };

    fetchSummary();
    const interval = setInterval(fetchSummary, 60000);

    document.addEventListener('visibilitychange', () => {
        if (document.visibilityState === 'visible') {
            fetchSummary();
        }
    });

    menu.addEventListener('shown.bs.dropdown', () => {
        fetchSummary();
    });

    window.addEventListener('unload', () => clearInterval(interval));
})();
