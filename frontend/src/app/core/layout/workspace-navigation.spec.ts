import { Component, signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { Router, provideRouter } from '@angular/router';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { WorkspaceSession } from '../auth/workspace-session';
import { ThemeService } from '../preferences/theme-service';
import { WorkspaceNavigation } from './workspace-navigation';

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
      const active = fixture.nativeElement.querySelectorAll('a.current');
      expect(active.length).toBe(1);
      expect(active[0].getAttribute('href')).toBe(route);
      expect(active[0].getAttribute('aria-current')).toBe('page');
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
});
