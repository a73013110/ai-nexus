import { describe, expect, it } from 'vitest';
import type { HealthSummary } from '@core/api/schema';
import {
  factText,
  factTone,
  healthAnnouncement,
  healthHeadline,
  stopView,
  subsystemStatus,
  toneOf,
  uptimeText,
  type HealthView,
} from './monitoring-format';

const issue = (severity: string, title: string) => ({
  code: severity === 'critical' ? 'dependency_failing' : 'latency_high',
  severity,
  subject: 'gitea',
  title,
  detail: '',
});
const view = (health: HealthSummary, overrides: Partial<HealthView> = {}): HealthView => ({
  enabled: true,
  health,
  connection: 'live',
  stale: false,
  at: '2026-10-13T04:02:31Z',
  minutes: 5,
  ...overrides,
});

describe('monitoring format', () => {
  it('writes every fact unit, and an unknown value as a dash rather than zero', () => {
    const fact = (value: number | null, unit: string, text: string | null = null) =>
      factText({ value, unit, text });
    expect(fact(1234, 'count')).toBe('1,234');
    expect(fact(0, 'count')).toBe('0');
    expect(fact(null, 'count')).toBe('—');
    expect(fact(52_000_000_000, 'bytes')).toBe('52.00 GB');
    expect(fact(64.2, 'percent')).toBe('64%');
    expect(fact(820, 'ms')).toBe('820 ms');
    expect(fact(42, 'seconds')).toBe('42.0 秒');
    expect(fact(90_000, 'seconds')).toBe('1 天 1 小時');
    expect(fact(1_234_567, 'tokens')).toBe('123.5萬');
    expect(fact(98_765, 'tokens')).toBe('98,765');
    expect(fact(null, 'text', ' D: ')).toBe('D:');
    expect(fact(null, 'text')).toBe('—');
    expect(fact(Number.NaN, 'bytes')).toBe('—');
    expect(fact(3, 'unknown-unit')).toBe('3');
  });

  it('says how long the process has run at a readable precision', () => {
    expect(uptimeText(null)).toBe('—');
    expect(uptimeText(-5)).toBe('—');
    expect(uptimeText(30)).toBe('不到 1 分鐘');
    expect(uptimeText(125)).toBe('2 分鐘');
    expect(uptimeText(3 * 3600 + 7 * 60)).toBe('3 小時 7 分');
    expect(uptimeText(3 * 86400 + 4 * 3600 + 59)).toBe('3 天 4 小時');
  });

  it('answers with one headline per health state', () => {
    expect(healthHeadline(view({ status: 'healthy', issues: [] }))).toMatchObject({
      key: 'healthy',
      title: '系統運作正常',
      detail: '最近 5 分鐘沒有需要處理的問題',
      tone: 'success',
    });
    expect(
      healthHeadline(view({ status: 'attention', issues: [issue('warning', 'API 回應偏慢')] })),
    ).toMatchObject({ title: '1 項需要注意', detail: 'API 回應偏慢', tone: 'warning' });
    expect(
      healthHeadline(
        view({
          status: 'critical',
          issues: [issue('critical', 'Gitea 呼叫全部失敗'), issue('warning', 'API 回應偏慢')],
        }),
      ),
    ).toMatchObject({
      title: '1 項嚴重問題',
      detail: '請優先處理：Gitea 呼叫全部失敗。另有 1 項需要注意',
      tone: 'danger',
    });
    expect(healthHeadline(view({ status: 'unobserved', issues: [] }))).toMatchObject({
      title: '尚無足夠觀測資料',
      tone: 'neutral',
    });
  });

  it('lets collection, pause and staleness override the health words, in that order', () => {
    const critical: HealthSummary = { status: 'critical', issues: [issue('critical', 'x')] };
    expect(
      healthHeadline(view(critical, { enabled: false, connection: 'paused', stale: true })).title,
    ).toBe('監控採集已停用');
    expect(healthHeadline(view(critical, { connection: 'paused', stale: true })).title).toBe(
      '已暫停更新',
    );
    const stale = healthHeadline(view(critical, { connection: 'reconnecting', stale: true }));
    expect(stale.title).toBe('連線中斷，顯示 12:02:31 的資料');
    expect(stale.tone).toBe('warning');
  });

  it('maps statuses to badge tones and words without colouring healthy as the accent', () => {
    expect(toneOf('healthy')).toBe('success');
    expect(toneOf('degraded')).toBe('warning');
    expect(toneOf('failing')).toBe('danger');
    expect(toneOf('idle')).toBe('neutral');
    expect(toneOf('something-new')).toBe('neutral');
    expect(factTone('positive')).toBe('success');
    expect(factTone('danger')).toBe('danger');
    expect(factTone('neutral')).toBe('neutral');
    expect(subsystemStatus('degraded')).toBe('效能下降');
    expect(subsystemStatus('later')).toBe('later');
    expect(healthAnnouncement({ status: 'healthy', issues: [] })).toBe('系統恢復正常');
  });

  it('answers whether stopping now interrupts work, and never says clear while blind', () => {
    const subsystem = (
      id: string,
      stop: { interrupts: number; resumes: number } | null,
      status = 'healthy',
    ) => ({
      id,
      name: id === 'generation' ? '生成佇列' : '背景工作',
      status,
      summary: null,
      facts: [],
      observedAt: '2026-10-13T04:02:31Z' as string | null,
      stop,
    });
    const busy = stopView({ status: 'interrupts', interrupts: 2, resumes: 1 }, [
      subsystem('generation', { interrupts: 2, resumes: 0 }),
      subsystem('jobs', { interrupts: 0, resumes: 1 }),
      subsystem('storage', null),
    ]);
    expect(busy.text).toBe('會中斷 2 項，1 項重啟後接續');
    expect(busy.alert).toBe(true);
    expect(busy.dots.map((dot) => [dot.tone, dot.title])).toEqual([
      ['warning', '生成佇列：2 項會中斷'],
      ['success', '背景工作：1 項重啟後接續'],
    ]);
    const quiet = [subsystem('jobs', { interrupts: 0, resumes: 0 })];
    expect(stopView({ status: 'clear', interrupts: 0, resumes: 0 }, quiet)).toMatchObject({
      text: '可以停站',
      alert: false,
      dots: [{ tone: 'success', title: '背景工作：沒有進行中的工作' }],
    });
    const unknown = { status: 'unknown', interrupts: 0, resumes: 0 };
    expect(stopView(unknown, [subsystem('jobs', null, 'unobserved')])).toMatchObject({
      text: '無法確認',
      dots: [{ tone: 'neutral', title: '背景工作：無法讀取' }],
    });
    const unread = { ...subsystem('jobs', null, 'unobserved'), observedAt: null };
    expect(stopView(unknown, [unread]).text).toBe('確認中');
  });
});
