import { TestBed } from '@angular/core/testing';
import { ActivatedRoute, convertToParamMap } from '@angular/router';
import { afterEach, describe, expect, it } from 'vitest';
import { LogFilterState } from './log-filter-state';

function setup(query: Record<string, string> = {}) {
  TestBed.configureTestingModule({
    providers: [
      LogFilterState,
      {
        provide: ActivatedRoute,
        useValue: { snapshot: { queryParamMap: convertToParamMap(query) } },
      },
    ],
  });
  return TestBed.inject(LogFilterState);
}

describe('log filter state', () => {
  afterEach(() => TestBed.resetTestingModule());

  it('presets a valid trace, issue code and range from a link', () => {
    const state = setup({
      traceId: 'ABCDEF0123456789ABCDEF0123456789',
      issueCode: 'NX-0123456789abcdef0123456789abcdef',
      from: '2026-10-01T00:00:00Z',
      to: '2026-10-01T06:00:00Z',
    });
    expect(state.fields().traceId).toBe('abcdef0123456789abcdef0123456789');
    expect(state.fields().issueCode).toBe('NX-0123456789abcdef0123456789abcdef');
    expect(state.range()).toBe('custom');
    const filter = state.build();
    expect(typeof filter).toBe('object');
    expect(filter).toMatchObject({
      from: '2026-10-01T00:00:00.000Z',
      to: '2026-10-01T06:00:00.000Z',
    });
  });

  it('ignores malformed links and ranges longer than a day', () => {
    const state = setup({
      traceId: 'not-a-trace',
      issueCode: 'NX-short',
      from: '2026-10-01T00:00:00Z',
      to: '2026-10-03T00:00:00Z',
    });
    expect(state.fields().traceId).toBe('');
    expect(state.fields().issueCode).toBe('');
    expect(state.range()).toBe('24');
  });

  it('rejects an inverted range and template text over more than a day', () => {
    const state = setup();
    state.setDate('from', '2026-10-02T00:00:00');
    state.setDate('to', '2026-10-01T00:00:00');
    expect(state.build()).toContain('結束時間須晚於起始時間');
    state.setDate('to', '2026-10-04T00:00:00');
    state.set('text', 'timeout');
    expect(state.build()).toContain('限一天');
    state.set('text', '');
    expect(typeof state.build()).toBe('object');
  });

  it('trims fields, counts advanced filters and resets to the last day', () => {
    const state = setup();
    state.set('category', '  Inference ');
    state.set('level', 'Error');
    expect(state.advancedCount()).toBe(1);
    expect(state.build()).toMatchObject({ category: 'Inference', level: 'Error' });
    state.setRange('custom');
    state.reset();
    expect(state.range()).toBe('24');
    expect(state.advancedCount()).toBe(0);
  });
});
