import { TestBed } from '@angular/core/testing';
import { afterEach, describe, expect, it } from 'vitest';
import { JobProgress } from './job-progress';

describe('JobProgress', () => {
  afterEach(() => TestBed.resetTestingModule());
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
