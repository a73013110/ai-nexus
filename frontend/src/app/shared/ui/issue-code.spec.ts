import { TestBed } from '@angular/core/testing';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { IssueCode } from './issue-code';
import { JobProgress } from './job-progress';

describe('issue controls', () => {
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
  it('does not display a persisted legacy job exception message', () => {
    const fixture = TestBed.createComponent(JobProgress);
    const code = 'NX-' + 'E'.repeat(32);
    fixture.componentRef.setInput('job', {
      status: 'failed',
      stage: '需要重試',
      completedUnits: 0,
      totalUnits: null,
      errorCode: 'ocr_failed',
      errorMessage: 'Password=secret https://internal.test/provider',
      issueCode: code,
    });
    fixture.detectChanges();
    const text = (fixture.nativeElement as HTMLElement).textContent;
    expect(text).toContain(code);
    expect(text).not.toContain('Password');
    expect(text).not.toContain('internal.test');
  });
});
