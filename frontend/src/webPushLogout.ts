import { deactivateCurrentWebPushSubscription } from './api';
import { getCurrentBrowserSubscription, supportsWebPush } from './webPush';

export async function deactivateCurrentWebPushForLogout(developmentUserKey?: string) {
  if (!supportsWebPush()) return;
  const subscription = await getCurrentBrowserSubscription();
  if (!subscription) return;
  try {
    await deactivateCurrentWebPushSubscription(developmentUserKey, subscription.endpoint, 'Logout');
  } finally {
    await subscription.unsubscribe();
  }
}
