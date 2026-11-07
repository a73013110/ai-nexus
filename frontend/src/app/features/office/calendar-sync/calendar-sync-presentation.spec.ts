import { describe, expect, it } from 'vitest';
import { eventState, failureText, runStatus } from './calendar-sync-presentation';

describe('calendar sync presentation', () => {
  it('explains every failure code the backend records', () => {
    for (const code of [
      'google_not_configured',
      'google_credentials_rejected',
      'google_forbidden',
      'google_calendar_not_found',
      'google_rate_limited',
      'google_unavailable',
      'google_rejected',
      'gd_unavailable',
      'removals_held',
      'unexpected',
    ]) {
      expect(failureText(code)).not.toContain(code);
    }
  });

  it('keeps unknown codes visible instead of hiding them', () => {
    expect(failureText('new_code')).toBe('同步失敗（new_code）。');
    expect(failureText(null)).toBe('');
    expect(eventState('later')).toEqual({ label: 'later', tone: 'neutral' });
  });

  it('marks failures as danger and held runs as warning', () => {
    expect(eventState('failed').tone).toBe('danger');
    expect(runStatus('held').tone).toBe('warning');
    expect(runStatus('succeeded').label).toBe('完成');
  });
});
