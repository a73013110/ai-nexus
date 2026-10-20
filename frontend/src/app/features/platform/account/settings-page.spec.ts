import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { AuthService } from '@core/auth/auth-service';
import { WorkspaceSession } from '@core/auth/workspace-session';
import { WorkspaceApi } from '@core/api/workspace-api';
import { ThemeService } from '@core/preferences/theme-service';
import { DraftRepository } from '@core/preferences/draft-repository';
import { UserSettingsService } from '@core/preferences/user-settings';
import { SettingsPage } from './settings-page';

function setup() {
  const session = {
    auth: { generation: signal(1), session: signal(null), logout: vi.fn() },
    load: vi.fn(async () => true),
    me: signal({ id: 'u1', displayName: '王小明', account: 'wang', access: { roles: [] } }),
    has: (feature: string) => feature === 'chat',
  };
  const service = {
    load: vi.fn(async () => {
      throw new Error('settings unavailable');
    }),
    usage: vi.fn(async () => null),
    policy: vi.fn(async () => null),
    preview: vi.fn(),
    restore: vi.fn(),
    save: vi.fn(),
  };
  const api = {
    models: vi.fn(async () => null),
    webSearchStatus: vi
      .fn()
      .mockRejectedValueOnce(new Error('web search unavailable'))
      .mockResolvedValue({ available: true, notice: '' }),
  };
  TestBed.configureTestingModule({
    providers: [
      provideRouter([]),
      { provide: WorkspaceSession, useValue: session },
      { provide: AuthService, useValue: { guard: () => () => true } },
      { provide: UserSettingsService, useValue: service },
      { provide: WorkspaceApi, useValue: api },
      { provide: DraftRepository, useValue: { clearAccount: vi.fn() } },
      { provide: ThemeService, useValue: { reducedMotion: signal(true) } },
    ],
  });
  const fixture = TestBed.createComponent(SettingsPage);
  const root = fixture.nativeElement as HTMLElement;
  return { fixture, root, api };
}

describe('SettingsPage', () => {
  afterEach(() => TestBed.resetTestingModule());

  it('reads the web search status again when the reload button is pressed', async () => {
    const { fixture, root, api } = setup();
    await fixture.whenStable();
    expect(api.webSearchStatus).toHaveBeenCalledOnce();
    const reload = [...root.querySelectorAll('button')].find(
      (button) => button.textContent?.trim() === '重新載入',
    );
    reload!.click();
    await fixture.whenStable();
    expect(api.webSearchStatus).toHaveBeenCalledTimes(2);
    fixture.componentInstance.section.set('conversation');
    await fixture.whenStable();
    expect(root.textContent).toContain('網路搜尋 · 已啟用');
  });
});
