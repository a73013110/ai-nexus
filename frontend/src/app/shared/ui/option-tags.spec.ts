import { TestBed } from '@angular/core/testing';
import { afterEach, describe, expect, it } from 'vitest';
import { OptionTags, optionTags } from './option-tags';

describe('OptionTags', () => {
  afterEach(() => TestBed.resetTestingModule());

  it('builds neutral tags from the conditions that hold', () => {
    const user = { enabled: false, hidden: false };
    expect(optionTags(!user.enabled && '停用', user.hidden && '已隱藏', null, '')).toEqual([
      { label: '停用' },
    ]);
  });

  it('renders each tag with its tone and a separator only screen readers hear', () => {
    const fixture = TestBed.createComponent(OptionTags);
    fixture.componentRef.setInput('tags', [{ label: '停用' }, { label: '過期', tone: 'warning' }]);
    fixture.detectChanges();
    const host = fixture.nativeElement as HTMLElement;
    const tags = [...host.querySelectorAll('.option-tag')];
    expect(tags.map((tag) => [tag.textContent, tag.getAttribute('data-tone')])).toEqual([
      ['停用', 'neutral'],
      ['過期', 'warning'],
    ]);
    expect(host.querySelectorAll('.sr-only')).toHaveLength(2);
  });
});
