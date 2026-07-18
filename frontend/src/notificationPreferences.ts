export type NotificationPreferenceItem = {
  deliveryType: string;
  channel: string;
  eventLabel: string;
  channelLabel: string;
  description: string;
  enabled: boolean;
  canChange: boolean;
  isOverridden: boolean;
  lockReason: string | null;
};

export type NotificationPreferenceResponse = {
  userId: string;
  userDisplayName: string;
  taxonomyVersion: string;
  version: number;
  isDefault: boolean;
  changed: boolean;
  items: NotificationPreferenceItem[];
};

export type UpdateNotificationPreferencesRequest = {
  expectedVersion: number;
  items: Array<{
    deliveryType: string;
    channel: string;
    enabled: boolean;
  }>;
};
