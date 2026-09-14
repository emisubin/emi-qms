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

// Serialize clicks so an in-flight launch is reused by the next notification.
let notificationNavigation = Promise.resolve(null);
self.addEventListener('notificationclick', (event) => {
  event.notification.close();
  let target = new URL('/notifications', self.location.origin);
  try {
    const requested = new URL(event.notification.data?.url || '/notifications', self.location.origin);
    if (requested.origin === self.location.origin) target = requested;
  } catch { /* Invalid notification links fall back to the notification list. */ }
  const targetUrl = target.href;
  notificationNavigation = notificationNavigation.catch(() => null).then(async (previousClient) => {
    const windows = await self.clients.matchAll({ type: 'window', includeUncontrolled: true });
    // A newly opened window may not yet appear in matchAll.
    if (previousClient && !windows.some(client => client.id === previousClient.id)) windows.unshift(previousClient);
    for (const client of windows) {
      if (new URL(client.url).origin !== self.location.origin || !('focus' in client)) continue;
      try {
        const navigated = await client.navigate(targetUrl);
        if (navigated) return await navigated.focus();
      } catch { /* A closed window must not prevent the remaining candidates from opening. */ }
    }
    return self.clients.openWindow(targetUrl);
  });
  event.waitUntil(notificationNavigation);
});
