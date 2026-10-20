import { Component, signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { Router, provideRouter } from '@angular/router';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { WorkspaceSession } from '../auth/workspace-session';
import { ThemeService } from '../preferences/theme-service';
import { WorkspaceNavigation } from './workspace-navigation';
import { provideNavigationBadges, type NavigationBadge } from './navigation-badges';

@Component({ template: '' })
class Destination {}

const node = {
  icon: null,
  featureId: null,
  route: null,
  url: null,
  openInNewTab: false,
  children: [],
};
const page = (id: string, name: string, route: string) => ({
  ...node,
  id,
  kind: 'page',
  name,
  featureId: id,
  route,
});
const group = (id: string, name: string, children: unknown[]) => ({
  ...node,
  id,
  kind: 'group',
  name,
  children,
});
const link = (url: string, name: string) => ({
  ...node,
  id: 'runbook',
  kind: 'link',
  name,
  url,
  openInNewTab: true,
});

describe('workspace navigation ownership', () => {
  afterEach(() => {
    TestBed.resetTestingModule();
    vi.unstubAllGlobals();
  });
  it('selects one most-specific feature while preserving child routes and ignoring query/fragment', async () => {
    vi.stubGlobal(
      'ResizeObserver',
      class {
        observe() {}
        disconnect() {}
      },
    );
    TestBed.configureTestingModule({
      providers: [
        { provide: ThemeService, useValue: { reducedMotion: () => true } },
        provideRouter([{ path: '**', component: Destination }]),
        {
          provide: WorkspaceSession,
          useValue: {
            me: signal({
              access: {
                // 伺服器已修剪的選單樹；外部連結沒有本站路由，不能成為目前頁面。
                navigation: [
                  link('https://wiki.example.com/admin', '維運手冊'),
                  ...[
                    ['identity.users', '使用者', '/admin/identity/users'],
                    ['operations.usage', '平台用量', '/admin/operations/usage'],
                    ['audit', '活動稽核', '/admin/audit'],
                    ['diagnostics.logs', '系統日誌', '/admin/diagnostics/logs'],
                    ['knowledge', '知識庫', '/work/knowledge'],
                    ['knowledge.retrieval', '檢索管理', '/work/knowledge/retrieval'],
                    ['projects', '專案', '/work/projects'],
                    ['chat', '對話', '/work/chat'],
                  ].map(([id, name, route]) => page(id, name, route)),
                ],
              },
            }),
          },
        },
      ],
    });
    const fixture = TestBed.createComponent(WorkspaceNavigation);
    const router = TestBed.inject(Router);
    for (const [url, route] of [
      ['/admin/diagnostics/logs?level=Error#events', '/admin/diagnostics/logs'],
      ['/admin/audit?traceId=abc&category=authentication', '/admin/audit'],
      ['/admin/identity/users', '/admin/identity/users'],
      ['/admin/operations/usage', '/admin/operations/usage'],
      ['/work/knowledge/retrieval', '/work/knowledge/retrieval'],
      ['/work/knowledge/collection-1', '/work/knowledge'],
      ['/work/projects/project-123?view=members', '/work/projects'],
      ['/work/chat/conversation-123', '/work/chat'],
    ]) {
      await router.navigateByUrl(url);
      fixture.detectChanges();
      const active: HTMLElement[] = [...fixture.nativeElement.querySelectorAll('a.current')];
      // 目前所在以功能為單位：預設釘選的對話與知識庫在釘選列與全部功能各標一次。
      expect(active.length).toBe(['/work/chat', '/work/knowledge'].includes(route) ? 2 : 1);
      for (const link of active) {
        expect(link.getAttribute('href')).toBe(route);
        expect(link.getAttribute('aria-current')).toBe('page');
      }
    }
    await router.navigateByUrl('/admin/identity');
    fixture.detectChanges();
    expect(fixture.nativeElement.querySelectorAll('a.current').length).toBe(0);
    const external = fixture.nativeElement.querySelector(
      'a[href="https://wiki.example.com/admin"]',
    );
    expect(external.getAttribute('rel')).toBe('noopener noreferrer');
    expect(external.getAttribute('target')).toBe('_blank');
    expect(external.getAttribute('aria-label')).toBe('維運手冊（外部連結，開新分頁）');
  });
  it('shows badges on every entry, hides zero, caps at 99+ and hints at badges it hides', async () => {
    vi.stubGlobal(
      'ResizeObserver',
      class {
        observe() {}
        disconnect() {}
      },
    );
    const badges = signal<ReadonlyMap<string, NavigationBadge>>(
      new Map<string, NavigationBadge>([
        ['chat', 3],
        ['projects', 120],
        ['knowledge', 0],
        ['audit', 'dot'],
      ]),
    );
    TestBed.configureTestingModule({
      providers: [
        { provide: ThemeService, useValue: { reducedMotion: () => true } },
        provideRouter([{ path: '**', component: Destination }]),
        provideNavigationBadges(() => badges),
        // 第二個來源與第一個相加；只有圓點的不會蓋掉數字。
        provideNavigationBadges(() => signal(new Map<string, NavigationBadge>([['chat', 'dot']]))),
        {
          provide: WorkspaceSession,
          useValue: {
            me: signal({
              id: 'badges',
              access: {
                navigation: [
                  group('work', 'AI 工作台', [
                    page('chat', '對話', '/work/chat'),
                    page('projects', '專案', '/work/projects'),
                    page('knowledge', '知識庫', '/work/knowledge'),
                  ]),
                  group('system', '系統管理', [
                    page('audit', '活動稽核', '/admin/audit'),
                    page('identity.users', '使用者', '/admin/identity/users'),
                  ]),
                ],
              },
            }),
          },
        },
      ],
    });
    const fixture = TestBed.createComponent(WorkspaceNavigation);
    fixture.detectChanges();
    const element: HTMLElement = fixture.nativeElement;
    const badgesOf = (name: string) =>
      [...element.querySelectorAll(`a[aria-label="${name}"] nx-count-badge`)].map((badge) =>
        badge.classList.contains('is-dot') ? 'dot' : badge.textContent!.trim(),
      );
    // 對話已釘選：釘選列與全部功能都有同一個徽章；全部功能的那一格另有圖釘。
    expect(badgesOf('對話')).toEqual(['3', '3']);
    expect(element.querySelectorAll('a[aria-label="對話"] .entry-pin').length).toBe(1);
    expect(badgesOf('專案')).toEqual(['99+']);
    expect(badgesOf('知識庫')).toEqual([]);
    expect(badgesOf('活動稽核')).toEqual(['dot']);
    expect(badgesOf('使用者')).toEqual([]);
    // 名稱不隨數量變動，數量放在描述。
    const chat = element.querySelector<HTMLElement>('a[aria-label="對話"]')!;
    const description = element.querySelector(`#${chat.getAttribute('aria-describedby')}`)!;
    expect(description.textContent!.trim()).toBe('3 則新動態');

    // 收合分群時，標題代為提示裡面有徽章。
    const toggle = () =>
      [...element.querySelectorAll<HTMLButtonElement>('.workspace-group-toggle')].find((button) =>
        button.textContent!.includes('系統管理'),
      )!;
    expect(toggle().querySelector('nx-count-badge')).toBeNull();
    toggle().click();
    fixture.detectChanges();
    expect(toggle().querySelector('nx-count-badge.is-dot')).not.toBeNull();

    badges.set(new Map());
    fixture.detectChanges();
    expect(badgesOf('對話')).toEqual(['dot', 'dot']);
    expect(badgesOf('專案')).toEqual([]);
    expect(toggle().querySelector('nx-count-badge')).toBeNull();
  });
});
