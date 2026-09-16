import { fetchJson } from './api';

export type OsanNotificationPreferenceItem = {
  kind: string;
  label: string;
  mailEnabled: boolean;
  pushEnabled: boolean;
};

export type OsanStepCompletedPreferenceItem = {
  sequence: number;
  label: string;
  mailEnabled: boolean;
  pushEnabled: boolean;
};

export type OsanNotificationPreferenceResponse = {
  version: number;
  items: OsanNotificationPreferenceItem[];
  stepCompletedStages: OsanStepCompletedPreferenceItem[];
};

export type UpdateOsanNotificationPreferencesRequest = OsanNotificationPreferenceResponse & {
  expectedVersion: number;
};

export function getOsanNotificationPreferences(
  developmentUserKey: string | undefined,
  signal?: AbortSignal
) {
  return fetchJson<OsanNotificationPreferenceResponse>(
    '/api/osan/my/notification-preferences',
    developmentUserKey,
    { signal }
  );
}

export function saveOsanNotificationPreferences(
  developmentUserKey: string | undefined,
  request: UpdateOsanNotificationPreferencesRequest,
  signal?: AbortSignal
) {
  return fetchJson<OsanNotificationPreferenceResponse>(
    '/api/osan/my/notification-preferences',
    developmentUserKey,
    { method: 'PUT', body: JSON.stringify(request), signal }
  );
}
