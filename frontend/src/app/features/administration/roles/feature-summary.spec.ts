import { TestBed } from '@angular/core/testing';
import { describe, expect, it } from 'vitest';
import type { NavigationGroupDto } from '@core/api/schema';
import { FeatureSummary } from './feature-summary';

const features = [
  { id: 'audit', name: '活動稽核', groupId: 'system', sortOrder: 97 },
  { id: 'logs.export', name: '日誌匯出', groupId: 'reports', sortOrder: 96 },
  { id: 'chat', name: '對話', groupId: 'work', sortOrder: 10 },
  { id: 'retired', name: 'retired', groupId: null, sortOrder: 0 },
];
const groups: NavigationGroupDto[] = [
  { id: 'work', parentId: null, name: 'AI 工作台', sortOrder: 10 },
  { id: 'system', parentId: null, name: '系統管理', sortOrder: 30 },
  { id: 'reports', parentId: 'system', name: '報表', sortOrder: 40 },
];

function render(withGroups: boolean) {
  const fixture = TestBed.createComponent(FeatureSummary);
  fixture.componentRef.setInput('features', features);
  if (withGroups) fixture.componentRef.setInput('groups', groups);
  fixture.detectChanges();
  return [...fixture.nativeElement.querySelectorAll('.feature-summary-group')].map(
    (section: HTMLElement) => ({
      label: section.querySelector('.feature-category-label')?.textContent ?? null,
      names: [...section.querySelectorAll('.resource-badges span')].map((x) => x.textContent),
    }),
  );
}

describe('FeatureSummary', () => {
  it('lists features under the path of their menu group, in menu order', () => {
    expect(render(true)).toEqual([
      { label: null, names: ['retired'] },
      { label: 'AI 工作台', names: ['對話'] },
      { label: '系統管理', names: ['活動稽核'] },
      { label: '系統管理 › 報表', names: ['日誌匯出'] },
    ]);
  });

  it('keeps every feature in one unlabelled list without groups', () => {
    expect(render(false)).toEqual([
      { label: null, names: ['retired', '對話', '日誌匯出', '活動稽核'] },
    ]);
  });
});
