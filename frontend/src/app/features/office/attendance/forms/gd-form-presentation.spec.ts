import { describe, expect, it } from 'vitest';
import type { GdFlowStep, GdFormDetailDto } from '@core/api/schema';
import {
  approvalSteps,
  attachmentItems,
  dateStamp,
  focusSegment,
  formHeading,
  hoursSummary,
  periodDays,
  periodMonths,
  periodRules,
  progressText,
} from './gd-form-presentation';

const at = (day: string, time: string) => `2026-${day}T${time}:00+08:00`;
const segment = (day: string, from: string, to: string) => ({
  startsAt: at(day, from),
  endsAt: at(day, to),
});

describe('gd form presentation', () => {
  it('groups cross-period segments into weekly rules, Monday first', () => {
    const segments = [
      segment('10-21', '13:00', '17:00'),
      segment('10-19', '09:00', '12:00'),
      segment('10-26', '09:00', '12:00'),
      segment('10-25', '09:00', '12:00'),
    ];
    expect(
      periodRules(segments).map((rule) => [rule.label, rule.count, rule.morning, rule.afternoon]),
    ).toEqual([
      ['每週一 09:00–12:00', 2, true, false],
      ['每週三 13:00–17:00', 1, false, true],
      ['每週日 09:00–12:00', 1, true, false],
    ]);
  });

  it('finds which segment the opened day is', () => {
    const segments = [segment('10-19', '09:00', '12:00'), segment('10-22', '08:00', '12:00')];
    expect(focusSegment(segments, '2026-10-22')).toEqual({
      label: '2026/10/22（四）08:00–12:00',
      position: '第 2／2 段',
    });
    expect(focusSegment(segments, '2026-10-20')).toBeNull();
    expect(focusSegment(segments, null)).toBeNull();
    expect(dateStamp(at('09-01', '08:00'))).toBe('2026/09/01（二）');
  });

  it('marks mornings and afternoons per day and spans every month between', () => {
    const segments = [
      segment('10-19', '08:00', '12:00'),
      segment('10-19', '13:00', '17:00'),
      segment('12-02', '13:00', '17:00'),
    ];
    expect(periodDays(segments).get('2026-10-19')).toEqual({
      morning: true,
      afternoon: true,
      times: ['08:00–12:00', '13:00–17:00'],
    });
    expect(periodDays(segments).get('2026-12-02')).toMatchObject({
      morning: false,
      afternoon: true,
    });
    expect(periodMonths(segments)).toEqual(['2026-10', '2026-11', '2026-12']);
    expect(
      periodMonths([
        segment('12-30', '09:00', '12:00'),
        { startsAt: '2027-01-04T09:00:00+08:00', endsAt: '2027-01-04T12:00:00+08:00' },
      ]),
    ).toEqual(['2026-12', '2027-01']);
  });

  it('sums hours as days at eight hours a day', () => {
    expect([4, 8, 20].map((hours) => hoursSummary(hours))).toEqual([
      { value: '4 小時', detail: '' },
      { value: '1 天', detail: '共 8 小時' },
      { value: '2.5 天', detail: '共 20 小時' },
    ]);
    expect(hoursSummary(144, 36)).toEqual({ value: '18 天', detail: '共 36 段・144 小時' });
    expect(hoursSummary(2.5, 5)).toEqual({ value: '2.5 小時', detail: '共 5 段' });
    expect([hoursSummary(0), hoursSummary(null)]).toEqual([null, null]);
  });

  it('heads a leave with its kind and a trip with its subject', () => {
    const form = (kind: 'Leave' | 'Travel', title: string) => ({ kind, title }) as GdFormDetailDto;
    expect(formHeading(form('Leave', '病假'))).toBe('病假');
    expect(formHeading(form('Travel', '參加研討會'))).toBe('參加研討會');
    expect(formHeading(form('Travel', ''))).toBe('出差單');
  });

  it('turns flow steps into timeline steps with waiting days and agents', () => {
    const steps: GdFlowStep[] = [
      {
        state: 'Done',
        action: '送出',
        person: { name: '林予安', unit: '資訊組', title: '專員' },
        agent: null,
        at: at('10-20', '09:30'),
        memo: null,
      },
      {
        state: 'Current',
        action: '會簽',
        person: { name: '張文彥', unit: null, title: null },
        agent: '吳思妤',
        at: at('10-20', '09:31'),
        memo: '請補件',
      },
      { state: 'Pending', action: '決行', person: null, agent: null, at: null, memo: null },
    ];
    const items = approvalSteps(steps, Date.parse(at('10-22', '10:00')));
    expect(
      items.map((item) => [
        item.state,
        item.title,
        item.person,
        item.detail,
        item.note,
        item.waiting,
        item.time,
      ]),
    ).toEqual([
      ['done', '送出', '林予安', '資訊組 · 專員', null, null, '2026/10/20（二）09:30'],
      ['current', '會簽', '張文彥', null, '代理：吳思妤', '已等候 2 天', '2026/10/20（二）09:31'],
      ['pending', '決行', null, null, null, null, null],
    ]);
    expect(approvalSteps([steps[1]], Date.parse(at('10-20', '18:00')))[0].waiting).toBe('今天送達');
    expect(progressText(steps)).toBe('第 2 關，共 3 關');
    expect(progressText([steps[0]])).toBe('共 1 關，已完成');
    expect(progressText([])).toBe('');
  });

  it('builds file items that only carry the ticket', () => {
    const [item] = attachmentItems([
      {
        file: { ticket: 'a+b', fileName: '照片.jpg', contentType: 'image/jpeg', isImage: true },
        memo: '醫師證明',
        uploader: '林予安',
      },
    ]);
    expect(item.detail).toBe('醫師證明 · 林予安 上傳');
    expect(item.thumbnail).toBe('/api/v1/remote-files/content?ticket=a%2Bb');
    expect(item.downloadUrl).toBe('/api/v1/remote-files/content?ticket=a%2Bb&download=true');
    expect(item.reader.remote).toEqual({
      ticket: 'a+b',
      fileName: '照片.jpg',
      contentType: 'image/jpeg',
    });
  });
});
