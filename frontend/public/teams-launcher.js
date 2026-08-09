(() => {
  'use strict';

  const notificationPattern = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/iu;
  const params = new URLSearchParams(window.location.search);

  function normalizeNotificationId(value) {
    if (typeof value !== 'string') return null;
    const trimmed = value.trim().replace(/^notification:/iu, '');
    return notificationPattern.test(trimmed) ? trimmed.toLowerCase() : null;
  }

  function readContextNotificationId() {
    const direct = normalizeNotificationId(
      params.get('notificationId')
      || params.get('subEntityId')
      || params.get('subPageId')
    );
    if (direct) return direct;

    const rawContext = params.get('context');
    if (!rawContext) return null;
    try {
      const context = JSON.parse(rawContext);
      return normalizeNotificationId(
        context?.page?.subPageId
        || context?.page?.subEntityId
        || context?.subPageId
        || context?.subEntityId
      );
    } catch {
      return null;
    }
  }

  const notificationId = readContextNotificationId();
  const destinationPath = notificationId
    ? `/teams/activity/notifications/${notificationId}`
    : '/';
  const destination = new URL(destinationPath, window.location.origin);
  const link = document.getElementById('open-emi-pms');
  const destinationText = document.getElementById('launch-destination');

  if (link instanceof HTMLAnchorElement) {
    link.href = destination.href;
  }
  if (destinationText && notificationId) {
    destinationText.textContent = 'Microsoft 365 인증 후 선택한 알림의 상세 화면으로 이동합니다.';
  }
})();
