import { describe, expect, it } from 'vitest';
import { notificationUrl } from './notification-target';

describe('typed notification destinations', () => {
  const id = '01234567-89ab-4cde-8000-0123456789ab';
  it('maps supported targets to their exact internal resource', () => {
    expect(notificationUrl({ kind: 'conversation', id })).toBe(`/chat/${id}`);
    expect(notificationUrl({ kind: 'share', id })).toBe(`/shared/${id}`);
    expect(notificationUrl({ kind: 'task', id })).toBe(`/tasks?job=${id}`);
    expect(notificationUrl({ kind: 'repository-review', id })).toBe(`/repositories?review=${id}`);
  });
  it('keeps unknown versions, arbitrary links and malformed identifiers inert', () => {
    expect(notificationUrl({ kind: 'share', id }, 2)).toBeNull();
    expect(notificationUrl({ kind: 'https://external.invalid', id })).toBeNull();
    for (const invalid of ['', '../settings', 'https://external.invalid', id + '?redirect=1'])
      expect(notificationUrl({ kind: 'conversation', id: invalid })).toBeNull();
  });
});
