import { TestBed } from '@angular/core/testing';
import { describe, expect, it } from 'vitest';
import { FeatureSummary } from './feature-summary';

function render(features: { id: string; name: string }[]) {
  const fixture = TestBed.createComponent(FeatureSummary);
  fixture.componentRef.setInput('features', features);
  fixture.detectChanges();
  return [...fixture.nativeElement.querySelectorAll('.feature-summary-group')].map(
    (section: HTMLElement) => ({
      label: section.querySelector('.feature-category-label')?.textContent || null,
      names: [...section.querySelectorAll('.resource-badges span')].map((item) => item.textContent),
    }),
  );
}

describe('FeatureSummary', () => {
  it('lists features under their catalog area in catalog order, unknown ids last', () => {
    expect(
      render([
        { id: 'chat', name: '對話' },
        { id: 'retired', name: 'retired' },
        { id: 'diagnostics.logs.export', name: '日誌匯出' },
        { id: 'audit', name: '活動稽核' },
        { id: 'diagnostics.logs', name: '系統日誌' },
      ]),
    ).toEqual([
      { label: 'AI 工作台', names: ['對話'] },
      { label: '系統管理', names: ['活動稽核', '系統日誌', '日誌匯出'] },
      { label: null, names: ['retired'] },
    ]);
  });

  it('says so when nothing is granted', () => {
    const fixture = TestBed.createComponent(FeatureSummary);
    fixture.componentRef.setInput('features', []);
    fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('尚無功能授權');
  });
});
