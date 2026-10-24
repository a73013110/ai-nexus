import { ApplicationRef, signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { By } from '@angular/platform-browser';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import type { RosterExclusionDto } from '@core/api/schema';
import { AuthService } from '@core/auth/auth-service';
import { WorkspaceSession } from '@core/auth/workspace-session';
import { ConfirmDialog } from '@shared/ui/confirm-dialog';
import { CollaborationApi } from '@features/work/collaboration/collaboration-api';
import { AttendanceApi } from '../attendance-api';
import { ExclusionDialog } from './exclusion-dialog';
import { RosterExclusions } from './roster-exclusions';

const director: RosterExclusionDto = {
  userId: 'u9',
  displayName: '林董事',
  account: 'TEST\\lin',
  note: null,
  createdAt: '2026-10-01T02:00:00Z',
};

function setup(features: string[] = ['attendance.cards', 'attendance.cards.maintain']) {
  const generation = signal(1);
  const api = {
    exclusions: vi.fn(async (): Promise<RosterExclusionDto[]> => [director]),
    exclude: vi.fn(async () => undefined),
    restore: vi.fn(async () => undefined),
  };
  TestBed.configureTestingModule({
    providers: [
      {
        provide: WorkspaceSession,
        useValue: {
          auth: { generation },
          load: vi.fn(async () => true),
          has: (feature: string) => features.includes(feature),
          testing: signal(false),
          testingNote: signal(''),
        },
      },
      { provide: AuthService, useValue: { generation, guard: () => () => true } },
      { provide: AttendanceApi, useValue: api },
      { provide: CollaborationApi, useValue: { directory: vi.fn(async () => []) } },
    ],
  });
  const fixture = TestBed.createComponent(RosterExclusions);
  const host = fixture.nativeElement as HTMLElement;
  const settle = async () => {
    await TestBed.inject(ApplicationRef).whenStable();
    fixture.detectChanges();
  };
  // 編輯對話框裡也有一個確認框；名單自己的是最後一個。
  const confirm = () =>
    fixture.debugElement.queryAll(By.directive(ConfirmDialog)).at(-1)!
      .componentInstance as ConfirmDialog;
  const dialog = () =>
    fixture.debugElement.query(By.directive(ExclusionDialog)).componentInstance as ExclusionDialog;
  return { fixture, host, api, settle, confirm, dialog };
}

describe('RosterExclusions', () => {
  beforeEach(() => {
    HTMLDialogElement.prototype.showModal = vi.fn(function (this: HTMLDialogElement) {
      this.setAttribute('open', '');
    });
    HTMLDialogElement.prototype.close = vi.fn(function (this: HTMLDialogElement) {
      this.removeAttribute('open');
    });
  });
  afterEach(() => TestBed.resetTestingModule());

  it('lists who is left out with the reason, and hides changes from read-only viewers', async () => {
    const { host, settle } = setup(['attendance.cards']);
    await settle();
    const row = host.querySelector('tbody tr')!;
    expect(row.textContent).toContain('林董事');
    expect(row.textContent).toContain('TEST\\lin');
    expect(row.textContent).toContain('—');
    expect(host.querySelector('.ui-count-label')?.textContent).toContain('1 人');
    expect(host.querySelector('.exclusions-add')).toBeNull();
    expect(host.querySelector('nx-action-menu')).toBeNull();
  });

  it('restores a person only after confirming and reads the list again', async () => {
    const { fixture, api, settle, confirm } = setup();
    await settle();
    expect(fixture.nativeElement.querySelector('.exclusions-add')).not.toBeNull();

    fixture.componentInstance.act(director, 'restore');
    await settle();
    confirm().answer(false);
    await settle();
    expect(api.restore).not.toHaveBeenCalled();

    fixture.componentInstance.act(director, 'restore');
    await settle();
    confirm().answer(true);
    await vi.waitFor(() => expect(api.restore).toHaveBeenCalledWith('u9'));
    await vi.waitFor(() => expect(api.exclusions).toHaveBeenCalledTimes(2));
  });

  it('adds a person picked from the directory with a trimmed reason', async () => {
    const { fixture, api, settle, dialog } = setup();
    await settle();
    dialog().create();
    await settle();
    const event = new Event('submit', { cancelable: true });
    await dialog().save(event);
    expect(dialog().personError()).toBe('請選擇人員。');
    expect(api.exclude).not.toHaveBeenCalled();

    dialog().choose({ id: 'u7', displayName: '張外派', account: 'TEST\\chang' } as never);
    dialog().update({ note: '  外派新加坡 ' });
    await dialog().save(event);
    expect(api.exclude).toHaveBeenCalledWith('u7', { note: '外派新加坡' });
    await settle();
    expect(api.exclusions).toHaveBeenCalledTimes(2);
    expect(fixture.nativeElement.querySelector('dialog[open]')).toBeNull();
  });

  it('edits only the reason of someone already left out', async () => {
    const { fixture, api, settle, dialog } = setup();
    await settle();
    fixture.componentInstance.act({ ...director, note: '董事' }, 'edit');
    await settle();
    const form = fixture.nativeElement as HTMLElement;
    expect(form.querySelector('.form-choice')?.textContent).toContain('林董事');
    // 修改原因時人已固定，沒有「更換」。
    expect(form.querySelector('.form-choice button')).toBeNull();
    expect(dialog().dirty()).toBe(false);
    dialog().update({ note: '' });
    await dialog().save(new Event('submit', { cancelable: true }));
    expect(api.exclude).toHaveBeenCalledWith('u9', { note: null });
  });
});
