self.addEventListener('push', (event) => {
  let payload = {};
  try {
    payload = event.data ? event.data.json() : {};
  } catch {
    payload = {};
  }

  const title = typeof payload.title === 'string' && payload.title.trim()
    ? payload.title
    : 'EMI PMS 알림';
  const options = {
    body: typeof payload.body === 'string' ? payload.body : 'EMI PMS에서 알림 내용을 확인해 주세요.',
    icon: typeof payload.icon === 'string' ? payload.icon : '/icons/emi-qms-192.png',
    badge: typeof payload.badge === 'string' ? payload.badge : '/icons/favicon-32.png',
    tag: typeof payload.tag === 'string' ? payload.tag : undefined,
    data: { url: typeof payload.url === 'string' ? payload.url : '/notifications' }
  };
  event.waitUntil(self.registration.showNotification(title, options));
});

self.addEventListener('notificationclick', (event) => {
  event.notification.close();
  const targetUrl = new URL(event.notification.data?.url || '/notifications', self.location.origin).href;
  event.waitUntil((async () => {
    const windows = await self.clients.matchAll({ type: 'window', includeUncontrolled: true });
    for (const client of windows) {
      if ('focus' in client) {
        await client.navigate(targetUrl);
        return client.focus();
      }
    }
    return self.clients.openWindow(targetUrl);
  })());
});
