import { Component, signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { Router, provideRouter } from '@angular/router';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { WorkspaceSession } from '../auth/workspace-session';
import { ThemeService } from '../preferences/theme-service';
import { WorkspaceNavigation } from './workspace-navigation';

@Component({ template: '' })
class Destination {}

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
                features: [
                  { id: 'identity.users', name: '使用者', route: '/admin/identity/users' },
                  { id: 'operations.usage', name: '平台用量', route: '/admin/operations/usage' },
                  { id: 'audit', name: '活動稽核', route: '/admin/audit' },
                  { id: 'diagnostics.logs', name: '系統日誌', route: '/admin/diagnostics/logs' },
                  { id: 'knowledge', name: '知識庫', route: '/work/knowledge' },
                  {
                    id: 'knowledge.retrieval',
                    name: '檢索管理',
                    route: '/work/knowledge/retrieval',
                  },
                  { id: 'projects', name: '專案', route: '/work/projects' },
                  { id: 'chat', name: '對話', route: '/work/chat' },
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
  });
});
