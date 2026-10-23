import { Component, signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { afterEach, describe, expect, it } from 'vitest';
import { ChoiceGroups, type ChoiceGroup } from './choice-groups';

@Component({
  selector: 'nx-choice-groups-host',
  imports: [ChoiceGroups],
  template: `<nx-choice-groups
    label="授予功能"
    [groups]="groups"
    [selected]="selected()"
    [disabled]="disabled()"
    [pending]="pending()"
    (selectedChange)="selected.set($event)"
  />`,
})
class Host {
  readonly groups: ChoiceGroup[] = [
    {
      id: 'work',
      name: '工作',
      options: [
        { id: 'chat', label: '對話', description: 'chat' },
        { id: 'files', label: '檔案庫', description: 'files' },
        { id: 'projects', label: '專案', description: 'projects' },
      ],
    },
    { id: 'system', name: '系統', options: [{ id: 'admin', label: '存取管理' }] },
    { id: 'empty', name: '空分類', options: [] },
  ];
  // `retired` 在所有群組之外，任何變更都必須保留它。
  readonly selected = signal<string[]>(['retired', 'files']);
  readonly disabled = signal(false);
  readonly pending = signal(false);
}

function setup() {
  const fixture = TestBed.createComponent(Host);
  fixture.detectChanges();
  const element = fixture.nativeElement as HTMLElement;
  const cards = () => [...element.querySelectorAll<HTMLElement>('.choice-group')];
  return { fixture, host: fixture.componentInstance, element, cards };
}

describe('ChoiceGroups', () => {
  afterEach(() => TestBed.resetTestingModule());

  it('frames each category as a named group with its selected count', () => {
    const { element, cards } = setup();
    expect(element.querySelector('nx-choice-groups')!.getAttribute('aria-label')).toBe('授予功能');
    expect(cards()).toHaveLength(2);
    const [work] = cards();
    expect(work.getAttribute('role')).toBe('group');
    expect(document.getElementById(work.getAttribute('aria-labelledby')!)?.textContent).toBe(
      '工作',
    );
    expect(work.querySelector('.choice-group-count')!.textContent).toBe('1/3');
    expect(work.querySelector('.sr-only')!.textContent).toBe('已選 1 項，共 3 項');
  });

  it('toggles one option and keeps ids outside the groups', () => {
    const { fixture, host, cards } = setup();
    cards()[0].querySelector<HTMLInputElement>('input[aria-label="對話"]')!.click();
    fixture.detectChanges();
    expect(host.selected()).toEqual(['retired', 'files', 'chat']);
    cards()[0].querySelector<HTMLInputElement>('input[aria-label="檔案庫"]')!.click();
    fixture.detectChanges();
    expect(host.selected()).toEqual(['retired', 'chat']);
  });

  it('selects a whole category, then clears it, from one button', () => {
    const { fixture, host, cards } = setup();
    const batch = () => cards()[0].querySelector<HTMLButtonElement>('.choice-group-batch')!;
    expect(batch().getAttribute('aria-label')).toBe('全選：工作');
    batch().click();
    fixture.detectChanges();
    expect(host.selected()).toEqual(['retired', 'files', 'chat', 'projects']);
    expect(batch().textContent!.trim()).toBe('全不選');
    expect(cards()[0].querySelector('.choice-group-count')!.textContent).toBe('3/3');
    batch().click();
    fixture.detectChanges();
    expect(host.selected()).toEqual(['retired']);
    // 只有一個選項不需要批次按鈕。
    expect(cards()[1].querySelector('.choice-group-batch')).toBeNull();
  });

  it('spins beside the option just changed and ignores other changes while it saves', () => {
    const { fixture, host, element, cards } = setup();
    const box = (label: string) =>
      cards()[0].querySelector<HTMLInputElement>(`input[aria-label="${label}"]`)!;
    box('對話').click();
    host.pending.set(true);
    fixture.detectChanges();
    expect(element.querySelector('nx-choice-groups')!.getAttribute('aria-busy')).toBe('true');
    expect(element.querySelectorAll('.selection-pending')).toHaveLength(1);
    expect(box('對話').getAttribute('aria-busy')).toBe('true');
    expect(box('專案').disabled).toBe(false);
    box('專案').click();
    cards()[0].querySelector<HTMLButtonElement>('.choice-group-batch')!.click();
    fixture.detectChanges();
    expect(host.selected()).toEqual(['retired', 'files', 'chat']);

    host.pending.set(false);
    fixture.detectChanges();
    cards()[0].querySelector<HTMLButtonElement>('.choice-group-batch')!.click();
    host.pending.set(true);
    fixture.detectChanges();
    // 全選由卡片的按鈕轉圈，選項旁不轉。
    const batch = cards()[0].querySelector<HTMLButtonElement>('.choice-group-batch')!;
    expect(batch.getAttribute('aria-busy')).toBe('true');
    expect(element.querySelectorAll('.selection-pending')).toHaveLength(0);
  });

  it('disables the options and the batch button together', () => {
    const { fixture, host, cards } = setup();
    host.disabled.set(true);
    fixture.detectChanges();
    expect(cards()[0].querySelector<HTMLButtonElement>('.choice-group-batch')!.disabled).toBe(true);
    expect(
      [...cards()[0].querySelectorAll<HTMLInputElement>('input')].every((input) => input.disabled),
    ).toBe(true);
  });
});
