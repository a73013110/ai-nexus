import { describe, expect, it } from 'vitest';
import {
  changeText,
  eventState,
  failureText,
  runDuration,
  runItemOutcome,
  runItemReasons,
  runStatus,
} from './calendar-sync-presentation';

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

  it('labels each run item the same way as the run totals', () => {
    expect(runItemOutcome('deleted').label).toBe('刪除');
    expect(runItemOutcome('failed').tone).toBe('danger');
    expect(runItemOutcome('later')).toEqual({ label: 'later', tone: 'neutral' });
  });

  it('writes a field change in the words people see in the document system', () => {
    expect(changeText({ field: 'flowStatus', from: 'Send', to: 'Decide' })).toBe(
      '簽核：簽核中 → 已核准',
    );
    expect(changeText({ field: 'flowStatus', from: 'Decide', to: 'Cancel' })).toBe(
      '簽核：已核准 → 銷單',
    );
    expect(changeText({ field: 'agentName', from: null, to: '林小芳' })).toBe(
      '代理人：（無） → 林小芳',
    );
    expect(changeText({ field: 'flowStatus', from: null, to: 'Finish' })).toBe('簽核：退回結案');
  });

  it('lists field changes as the reason and explains why an event went away', () => {
    const change = { field: 'flowStatus', from: 'Send', to: 'Decide' };
    expect(runItemReasons({ cause: 'content', changes: [change] })).toEqual([
      '簽核：簽核中 → 已核准',
    ]);
    expect(runItemReasons({ cause: 'content', changes: [] })).toEqual(['事件格式更新']);
    expect(runItemReasons({ cause: 'form_missing', changes: [] })).toEqual([
      '公文系統已找不到這張單',
    ]);
    expect(runItemReasons({ cause: null, changes: [] })).toEqual([]);
    expect(runItemReasons({ cause: 'new_cause', changes: [] })).toEqual(['new_cause']);
  });

  it('measures how long a run took', () => {
    expect(
      runDuration({ startedAt: '2026-11-08T12:47:29Z', finishedAt: '2026-11-08T12:47:41Z' }),
    ).toBe(12000);
  });
});
