import { describe, expect, it } from 'vitest';
import { notificationFeature, notificationUrl, showsTarget } from './notification-target';

describe('typed notification destinations', () => {
  const id = '01234567-89ab-4cde-8000-0123456789ab';
  it('maps supported targets to their exact internal resource', () => {
    expect(notificationUrl({ kind: 'conversation', id })).toBe(`/work/chat/${id}`);
    expect(notificationUrl({ kind: 'share', id })).toBe(`/work/sharing/${id}`);
    expect(notificationUrl({ kind: 'task', id })).toBe(`/work/jobs?job=${id}`);
    expect(notificationUrl({ kind: 'repository-review', id })).toBe(
      `/work/repositories?review=${id}`,
    );
    expect(notificationUrl({ kind: 'flow', id })).toBe(`/office/flow/approvals?instance=${id}`);
    expect(notificationUrl({ kind: 'repository-access', id })).toBe(
      `/work/repositories/access?repository=${id}`,
    );
  });
  it('names the feature a target belongs to for the sidebar badges', () => {
    expect(notificationFeature({ kind: 'conversation', id })).toBe('chat');
    expect(notificationFeature({ kind: 'repository-review', id })).toBe('repositories');
    expect(notificationFeature({ kind: 'constructor', id })).toBeNull();
  });
  it('keeps unknown versions, arbitrary links and malformed identifiers inert', () => {
    expect(notificationUrl({ kind: 'share', id }, 2)).toBeNull();
    expect(notificationUrl({ kind: 'https://external.invalid', id })).toBeNull();
    for (const invalid of ['', '../settings', 'https://external.invalid', id + '?redirect=1'])
      expect(notificationUrl({ kind: 'conversation', id: invalid })).toBeNull();
  });
  it('recognises the destination on screen even when the page adds its own state', () => {
    expect(showsTarget(`/work/chat/${id}`, { kind: 'conversation', id })).toBe(true);
    expect(showsTarget(`/work/jobs?tab=failed&job=${id}#log`, { kind: 'task', id })).toBe(true);
    expect(showsTarget('/work/jobs', { kind: 'task', id })).toBe(false);
    expect(showsTarget(`/work/chat/${id}/x`, { kind: 'conversation', id })).toBe(false);
    expect(showsTarget(`/work/sharing/${id}`, { kind: 'share', id }, 2)).toBe(false);
  });
});
