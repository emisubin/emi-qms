export interface ReadyHealth {
  name: 'ready';
  status: string;
  database: {
    isReady: boolean;
    reason: string;
  };
  checkedAtUtc: string;
}
