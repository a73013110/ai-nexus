import { Component, signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { afterEach, describe, expect, it } from 'vitest';
import type { JobDto } from '@core/api/schema';
import { JobProgress } from './job-progress';

@Component({
  imports: [JobProgress],
  template: `<nx-job-progress [job]="job()">
    @if (canControl()) {
      @if (job().status === 'running') {
        <button type="button">停止</button>
      } @else {
        <button type="button">重試</button>
      }
    }
  </nx-job-progress>`,
})
class Host {
  readonly canControl = signal(true);
  readonly job = signal({
    id: 'j1',
    status: 'failed',
    stage: '需要重試',
    completedUnits: 0,
    totalUnits: null,
    errorCode: null,
    errorMessage: null,
    issueCode: null,
  } as unknown as JobDto);
}

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

  it('projects actions chosen by nested control flow', () => {
    const fixture = TestBed.createComponent(Host);
    fixture.detectChanges();
    const root = fixture.nativeElement as HTMLElement;
    expect(root.querySelector('.job-stage-meta button')?.textContent).toBe('重試');
  });
});
