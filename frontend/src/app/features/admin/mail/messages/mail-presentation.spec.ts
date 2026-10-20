import {
  mailError,
  mailOutcome,
  mailPriority,
  mailStatus,
  resendBlocked,
} from './mail-presentation';

describe('mail presentation', () => {
  const now = Date.parse('2026-10-20T08:00:00Z');

  it('shows queued mail that already failed once as waiting to retry', () => {
    expect(
      mailStatus({ status: 'queued', attempts: 0, nextAttemptAt: '2026-10-20T07:59:00Z' }, now)
        .label,
    ).toBe('等待寄送');
    expect(
      mailStatus({ status: 'queued', attempts: 1, nextAttemptAt: '2026-10-20T08:05:00Z' }, now),
    ).toEqual({
      label: '等待重試',
      tone: 'warning',
    });
    expect(
      mailStatus({ status: 'failed', attempts: 1, nextAttemptAt: '2026-10-20T07:59:00Z' }, now)
        .tone,
    ).toBe('danger');
    expect(mailStatus({ status: 'unknown', attempts: 0, nextAttemptAt: '' }, now).label).toBe(
      'unknown',
    );
  });

  it('labels delivery codes and keeps unknown ones readable', () => {
    expect(mailError('mail_rejected')).toBe('收件端拒收');
    expect(mailError('mail_new_code')).toBe('mail_new_code');
    expect(mailError(null)).toBe('');
    expect(mailOutcome('transport').tone).toBe('danger');
    expect(mailPriority('security')).toBe('安全');
    expect(resendBlocked('mail_content_unavailable')).toContain('不能重寄');
    expect(resendBlocked(null)).toBe('');
  });
});
