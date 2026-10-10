import { Injectable, computed, inject, signal } from '@angular/core';
import { ActivatedRoute } from '@angular/router';
import { formatDateTimeInput, parseDateTimeInput } from '../../../shared/browser/format';
import type { LogFilter } from './system-logs-api';

export type LogFields = Omit<LogFilter, 'from' | 'to' | 'take' | 'sortDirection'>;
export const emptyLogFields = (): LogFields => ({
  level: '',
  category: '',
  eventName: '',
  issueCode: '',
  traceId: '',
  jobId: '',
  runId: '',
  errorCode: '',
  instance: '',
  text: '',
});

/** What the log filter form holds before it is submitted; links can preset it. */
@Injectable()
export class LogFilterState {
  readonly range = signal('24');
  private readonly initialEnd = Date.now() + 60_000;
  readonly fromLocal = signal(formatDateTimeInput(new Date(this.initialEnd - 24 * 60 * 60_000)));
  readonly toLocal = signal(formatDateTimeInput(new Date(this.initialEnd)));
  readonly fields = signal<LogFields>(emptyLogFields());
  readonly advancedCount = computed(
    () =>
      Object.entries(this.fields()).filter(
        ([key, value]) => !['level', 'issueCode'].includes(key) && value.trim(),
      ).length,
  );
  constructor() {
    const params = inject(ActivatedRoute).snapshot.queryParamMap;
    const traceId = params.get('traceId'),
      issueCode = params.get('issueCode');
    if (traceId && /^[a-f\d]{32}$/i.test(traceId)) this.set('traceId', traceId.toLowerCase());
    if (issueCode && /^NX-[a-f\d]{32}$/i.test(issueCode)) this.set('issueCode', issueCode);
    const from = Date.parse(params.get('from') || ''),
      to = Date.parse(params.get('to') || '');
    if (
      Number.isFinite(from) &&
      Number.isFinite(to) &&
      from < to &&
      to - from <= 24 * 60 * 60_000
    ) {
      this.fromLocal.set(formatDateTimeInput(new Date(from)));
      this.toLocal.set(formatDateTimeInput(new Date(to)));
      this.range.set('custom');
    }
  }
  set(key: keyof LogFields, value: string) {
    this.fields.update((fields) => ({ ...fields, [key]: value }));
  }
  setDate(which: 'from' | 'to', value: string) {
    (which === 'from' ? this.fromLocal : this.toLocal).set(value);
    this.range.set('custom');
  }
  setRange(value: string) {
    this.range.set(value);
    if (value === 'custom') return;
    const now = Date.now();
    this.fromLocal.set(formatDateTimeInput(new Date(now + 60_000 - Number(value) * 60 * 60_000)));
    this.toLocal.set(formatDateTimeInput(new Date(now + 60_000)));
  }
  reset() {
    this.fields.set(emptyLogFields());
    this.setRange('24');
  }
  /** The filter to query, or the reason the form cannot be queried. */
  build(): LogFilter | string {
    const from = parseDateTimeInput(this.fromLocal()),
      to = parseDateTimeInput(this.toLocal());
    if (!Number.isFinite(from.getTime()) || !Number.isFinite(to.getTime()) || from >= to)
      return '請選擇有效的時間範圍，結束時間須晚於起始時間。';
    const fields = Object.fromEntries(
      Object.entries(this.fields()).map(([key, value]) => [key, value.trim()]),
    ) as LogFields;
    if (fields.text && to.getTime() - from.getTime() > 24 * 60 * 60_000)
      return '訊息模板文字查詢限一天，請縮小時間範圍。';
    return { ...fields, from: from.toISOString(), to: to.toISOString() };
  }
}
