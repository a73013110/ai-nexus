import { Component, signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { Router, provideRouter } from '@angular/router';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
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

const narrowScreen = (matches: boolean) =>
  vi.stubGlobal('matchMedia', (query: string) => ({
    matches,
    media: query,
    addEventListener() {},
    removeEventListener() {},
  }));

describe('workspace navigation ownership', () => {
  beforeEach(() => {
    vi.stubGlobal(
      'ResizeObserver',
      class {
        observe() {}
        disconnect() {}
      },
    );
    narrowScreen(false);
  });
  afterEach(() => {
    TestBed.resetTestingModule();
    vi.unstubAllGlobals();
    localStorage.clear();
  });
  it('selects one most-specific feature while preserving child routes and ignoring query/fragment', async () => {
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
  it('labels headings by level, hints the current page when collapsed and toggles every group', async () => {
    narrowScreen(true);
    TestBed.configureTestingModule({
      providers: [
        { provide: ThemeService, useValue: { reducedMotion: () => true } },
        provideRouter([{ path: '**', component: Destination }]),
        {
          provide: WorkspaceSession,
          useValue: {
            me: signal({
              id: 'levels',
              access: {
                navigation: [
                  group('work', 'AI 工作台', [page('chat', '對話', '/work/chat')]),
                  group('system', '系統管理', [
                    group('identity', '身分與權限', [
                      page('identity.users', '使用者', '/admin/identity/users'),
                    ]),
                    group('ops', '監控與紀錄', [
                      page('diagnostics.logs', '系統日誌', '/admin/diagnostics/logs'),
                      group('mail', '郵件', [
                        page('mail.outbox', '寄送佇列', '/admin/mail/outbox'),
                        page('mail.deliveries', '寄送紀錄', '/admin/mail/deliveries'),
                      ]),
                    ]),
                  ]),
                ],
              },
            }),
          },
        },
      ],
    });
    const fixture = TestBed.createComponent(WorkspaceNavigation);
    await TestBed.inject(Router).navigateByUrl('/admin/mail/outbox');
    fixture.detectChanges();
    const element: HTMLElement = fixture.nativeElement;
    const toggle = (name: string) =>
      [...element.querySelectorAll<HTMLButtonElement>('.workspace-group-toggle')].find(
        (button) => button.querySelector('.group-name')!.textContent!.trim() === name,
      )!;
    // 第三層以上帶上層路徑（不含第一層）；第一層一律顯示數量，子分群收合時才顯示。
    expect(toggle('郵件').querySelector('.group-path')!.textContent!.trim()).toBe('監控與紀錄 ›');
    expect(toggle('監控與紀錄').querySelector('.group-path')).toBeNull();
    expect(toggle('系統管理').querySelector('.group-count')!.textContent!.trim()).toBe('4');
    // 窄螢幕又沒有儲存的偏好：只展開目前頁面所在的那一支。
    expect(toggle('身分與權限').getAttribute('aria-expanded')).toBe('false');
    expect(toggle('身分與權限').querySelector('.group-count')!.textContent!.trim()).toBe('1');
    expect(toggle('監控與紀錄').getAttribute('aria-expanded')).toBe('true');
    expect(toggle('監控與紀錄').classList).toContain('is-trail');
    expect(toggle('監控與紀錄').querySelector('.group-here')).toBeNull();

    const all = () =>
      element.querySelector<HTMLButtonElement>('.glyph-toggle[aria-label^="全部"]')!;
    expect(all().getAttribute('aria-label')).toBe('全部展開');
    all().click();
    fixture.detectChanges();
    expect(toggle('身分與權限').getAttribute('aria-expanded')).toBe('true');
    expect(all().getAttribute('aria-label')).toBe('全部收合');
    expect(all().title).toBe('全部收合');
    all().click();
    fixture.detectChanges();
    expect(toggle('AI 工作台').getAttribute('aria-expanded')).toBe('false');
    // 收合的分群含目前頁面時，標題標示圓點並以描述報讀。
    const system = toggle('系統管理');
    expect(system.querySelector('.group-here')).not.toBeNull();
    expect(element.querySelector(`#${system.getAttribute('aria-describedby')}`)!.textContent).toBe(
      '目前頁面在此',
    );
    expect(toggle('AI 工作台').querySelector('.group-here')).toBeNull();
    expect(all().getAttribute('aria-label')).toBe('全部展開');
  });
});
