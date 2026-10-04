import { describe, expect, it } from 'vitest';
import { toCsv } from './csv';
describe('CSV export', () => {
  it('preserves Unicode, commas, quotes and line breaks', () => {
    expect(toCsv([['王小明', 'a,b', 'a"b', 'a\nb']])).toBe('\ufeff"王小明","a,b","a""b","a\nb"');
  });
  it('neutralizes formula prefixes including leading whitespace', () => {
    const csv = toCsv([['=HYPERLINK("https://example.com")', '\t+1', '@SUM(1)', '-1', '一般文字']]);
    expect(csv).toContain('"\'=HYPERLINK');
    expect(csv).toContain('"\'\t+1"');
    expect(csv).toContain('"\'@SUM(1)"');
    expect(csv).toContain('"\'-1"');
    expect(csv).toContain('"一般文字"');
  });
});
