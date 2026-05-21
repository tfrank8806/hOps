(() => {
    const container = document.getElementById('realtimeNotificationContainer');
    if (!container || typeof signalR === 'undefined') {
        return;
    }

    const translate = typeof window.hopsTranslate === 'function'
        ? window.hopsTranslate
        : (key, fallback) => (typeof fallback === 'string' && fallback.length ? fallback : key);

    function createBubble(data) {
        const payload = {
            title: data?.title ?? data?.Title ?? translate('Shared.Notifications.DefaultTitle', 'Notification'),
            message: data?.message ?? data?.Message ?? '',
            url: data?.url ?? data?.Url ?? '',
            type: (data?.type ?? data?.Type ?? 'info').toLowerCase()
        };

        const bubble = document.createElement('button');
        bubble.type = 'button';
        bubble.className = `realtime-notification realtime-notification-${payload.type}`;
        bubble.innerHTML = `
            <div class="realtime-notification-title">${payload.title}</div>
            <div class="realtime-notification-body">${payload.message}</div>
        `;

        const removeBubble = () => {
            bubble.classList.add('realtime-notification-hide');
            setTimeout(() => bubble.remove(), 200);
        };

        bubble.addEventListener('click', () => {
            if (payload.url) {
                window.location.href = payload.url;
            }
            removeBubble();
        });

        container.appendChild(bubble);
        requestAnimationFrame(() => bubble.classList.add('show'));
        setTimeout(removeBubble, 10000);
    }

    function dispatchRealtimeEvent(payload) {
        try {
            const detail = payload ?? {};
            window.dispatchEvent(new CustomEvent('realtime:notification', { detail }));
            document.dispatchEvent(new CustomEvent('realtime:notification', { detail }));
        } catch (error) {
            console.error('Realtime notification dispatch failure', error);
        }
    }

    const connection = new signalR.HubConnectionBuilder()
        .withUrl('/hubs/notifications')
        .withAutomaticReconnect()
        .build();

    connection.on('ReceiveNotification', payload => {
        createBubble(payload);
        dispatchRealtimeEvent(payload);
    });

    connection.start().catch(err => console.error('Notification hub error', err));
})();
