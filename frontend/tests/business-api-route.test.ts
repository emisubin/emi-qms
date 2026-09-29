import { describe, expect, it } from 'vitest';
import { businessApiRoute } from '../src/businessApiRoute';

describe('fixed business API routes', () => {
  it('uses distinct URLs for the same shared notice operation', () => {
    expect(businessApiRoute('/api/notices?limit=10', 'CHEONGJU')).toBe('/cheongju/api/notices?limit=10');
    expect(businessApiRoute('/api/notices?limit=10', 'OSAN')).toBe('/osan/api/notices?limit=10');
  });
  it('keeps login and integrated user management on the common entry point', () => {
    expect(businessApiRoute('/api/me', null)).toBe('/access/api/me');
    expect(businessApiRoute('/api/me', 'OSAN')).toBe('/osan/api/me');
    expect(businessApiRoute('/api/me', 'CHEONGJU')).toBe('/cheongju/api/me');
    expect(businessApiRoute('/api/runtime-mode', null)).toBe('/access/api/runtime-mode');
    expect(businessApiRoute('/api/runtime-mode', 'CHEONGJU')).toBe('/cheongju/api/runtime-mode');
    expect(businessApiRoute('/api/runtime-mode', 'OSAN')).toBe('/osan/api/runtime-mode');
    expect(businessApiRoute('/api/admin/user-access/users', 'OSAN')).toBe('/access/api/admin/user-access/users');
    expect(businessApiRoute('/api/me/profile-photo', 'OSAN')).toBe('/osan/api/me/profile-photo');
  });
  it('does not mistake similarly named routes or health checks for common administration', () => {
    expect(businessApiRoute('/api/admin/user-access-evil', 'OSAN')).toBe('/osan/api/admin/user-access-evil');
    expect(businessApiRoute('/health/ready', null)).toBe('/health/ready');
  });
});
