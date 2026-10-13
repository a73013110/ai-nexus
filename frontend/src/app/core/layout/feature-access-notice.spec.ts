import { TestBed } from '@angular/core/testing';
import { afterEach, describe, expect, it } from 'vitest';
import { WorkspaceSession } from '../auth/workspace-session';
import { FeatureAccessNotice } from './feature-access-notice';

describe('feature access notice', () => {
  afterEach(() => TestBed.resetTestingModule());

  it('names the missing feature as navigation names it', () => {
    const names: Record<string, string> = { operations: '平台營運' };
    TestBed.configureTestingModule({
      providers: [
        { provide: WorkspaceSession, useValue: { featureName: (id: string) => names[id] } },
      ],
    });
    const fixture = TestBed.createComponent(FeatureAccessNotice);
    fixture.componentRef.setInput('feature', 'operations');
    fixture.detectChanges();
    const notice = fixture.nativeElement.querySelector('nx-notice');
    expect(notice.getAttribute('role')).toBe('alert');
    expect(notice.textContent).toContain('需要「平台營運」權限，請聯絡管理員。');
  });
});
