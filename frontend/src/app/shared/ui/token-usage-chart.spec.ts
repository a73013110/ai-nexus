import { TestBed } from '@angular/core/testing';
import { describe, expect, it } from 'vitest';
import { TokenUsageChart } from './token-usage-chart';

describe('token usage reporting', () => {
  it('aggregates models, fills dates in the report timezone and preserves coverage', () => {
    const fixture = TestBed.createComponent(TokenUsageChart);
    fixture.componentRef.setInput('usage', {
      from: '2026-10-01T16:00:00Z',
      until: '2026-10-04T16:00:00Z',
      timezoneOffsetMinutes: 480,
      daily: [
        {
          date: '2026-10-02',
          modelId: 'a',
          requests: 2,
          requestsWithUsage: 1,
          inputTokens: 100,
          outputTokens: 20,
        },
        {
          date: '2026-10-02',
          modelId: 'b',
          requests: 1,
          requestsWithUsage: 1,
          inputTokens: 200,
          outputTokens: 40,
        },
        {
          date: '2026-10-04',
          modelId: 'a',
          requests: 1,
          requestsWithUsage: 1,
          inputTokens: 50,
          outputTokens: 10,
        },
      ],
    });
    const chart = fixture.componentInstance;
    expect(chart.points()).toEqual([
      { label: '2026-10-02', value: 360 },
      { label: '2026-10-03', value: 0 },
      { label: '2026-10-04', value: 60 },
    ]);
    expect(chart.totals()).toEqual({ input: 350, output: 70, requests: 4, known: 3 });
    chart.model.set('a');
    chart.metric.set('output');
    expect(chart.points().map((x) => x.value)).toEqual([20, 0, 10]);
    expect(chart.totals().known).toBe(2);
    chart.model.set('removed-model');
    expect(chart.selectedModel()).toBe('all');
    fixture.destroy();
  });
});
