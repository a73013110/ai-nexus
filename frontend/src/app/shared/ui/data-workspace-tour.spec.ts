import { describe, expect, it } from 'vitest';
import { cursorPaginationStep, queryBarStep } from './data-workspace-tour';

describe('data workspace tour steps', () => {
  it('mentions the extra conditions only when the page has them', () => {
    expect(queryBarStep('nx-x', { fields: '日期' }).description).not.toContain('＋條件');
    expect(queryBarStep('nx-x', { fields: '日期', more: '模組' }).description).toContain(
      '模組收在「＋條件」',
    );
  });

  it('talks about sorting only for sortable tables', () => {
    const sortable = cursorPaginationStep('nx-x');
    expect(sortable.target).toBe('nx-x nx-table-pagination');
    expect(sortable.description).toContain('排序');
    expect(cursorPaginationStep('nx-x', { sortable: true }).description).toBe(sortable.description);
    const fixed = cursorPaginationStep('nx-x', { sortable: false }).description;
    expect(fixed).toContain('由新到舊');
    expect(fixed).not.toContain('排序');
  });
});
