import { TestBed } from '@angular/core/testing';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { IssueCode } from './issue-code';

describe('IssueCode', () => {
  afterEach(() => TestBed.resetTestingModule());
  it('copies only the validated code', async () => {
    const writeText = vi.fn().mockResolvedValue(undefined);
    Object.defineProperty(navigator, 'clipboard', { value: { writeText }, configurable: true });
    const fixture = TestBed.createComponent(IssueCode);
    const code = 'NX-' + 'D'.repeat(32);
    fixture.componentRef.setInput('message', '操作未完成。查證代碼：' + code);
    fixture.detectChanges();
    await fixture.componentInstance.copy();
    expect(writeText).toHaveBeenCalledWith(code);
    expect(fixture.componentInstance.copied()).toBe(true);
  });
});
