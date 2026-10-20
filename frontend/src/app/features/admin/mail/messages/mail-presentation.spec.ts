import {
  MAIL_ACTIVE_POLL,
  MAIL_IDLE_POLL,
  mailError,
  mailOutcome,
  mailPollDelay,
  mailPriority,
  mailStage,
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

  it('polls quickly only while mail is in flight or about to be retried', () => {
    const at = (offset: number) => new Date(now + offset).toISOString();
    expect(mailPollDelay([{ status: 'sending', nextAttemptAt: at(0) }], null, now)).toBe(
      MAIL_ACTIVE_POLL,
    );
    // 已到期的等待信：下一輪認領就會寄，馬上再看。
    expect(mailPollDelay([{ status: 'queued', nextAttemptAt: at(-60_000) }], null, now)).toBe(
      MAIL_ACTIVE_POLL,
    );
    // 10 秒後重試：時間到再看，不每 3 秒問一次。
    expect(mailPollDelay([{ status: 'queued', nextAttemptAt: at(10_000) }], null, now)).toBe(
      11_000,
    );
    // 一小時後重試：最多等閒置間隔。
    expect(mailPollDelay([{ status: 'queued', nextAttemptAt: at(3_600_000) }], null, now)).toBe(
      MAIL_IDLE_POLL,
    );
    expect(mailPollDelay([{ status: 'sent', nextAttemptAt: at(0) }], MAIL_IDLE_POLL, now)).toBe(
      MAIL_IDLE_POLL,
    );
    expect(mailPollDelay([{ status: 'failed', nextAttemptAt: at(0) }], null, now)).toBeNull();
  });

  it('labels delivery codes and keeps unknown ones readable', () => {
    expect(mailError('mail_rejected')).toBe('收件端拒收');
    expect(mailError('mail_new_code')).toBe('mail_new_code');
    expect(mailError(null)).toBe('');
    expect(mailOutcome('transport').tone).toBe('danger');
    expect(mailPriority('security')).toBe('安全');
    expect(resendBlocked('mail_content_unavailable')).toContain('不能重寄');
    expect(resendBlocked(null)).toBe('');
    expect(mailStage('tls')).toBe('TLS／憑證');
    expect(mailStage(null)).toBe('');
  });
});
