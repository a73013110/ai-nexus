import { Component, signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { Router, provideRouter } from '@angular/router';
import { afterEach, describe, expect, it } from 'vitest';
import { WorkspaceSession } from '../../core/auth/workspace-session';
import { WorkspaceNavigation } from './workspace-navigation';

@Component({ template: '' })
class Destination {}

describe('workspace navigation ownership', () => {
  afterEach(() => TestBed.resetTestingModule());
  it('selects one most-specific feature while preserving child routes and ignoring query/fragment', async () => {
    TestBed.configureTestingModule({
      providers: [
        provideRouter([{ path: '**', component: Destination }]),
        {
          provide: WorkspaceSession,
          useValue: {
            me: signal({
              access: {
                features: [
                  { id: 'admin', name: '平台管理', route: '/admin' },
                  { id: 'logs.query', name: '系統日誌', route: '/admin/logs' },
                  { id: 'projects', name: '專案', route: '/projects' },
                  { id: 'chat', name: '對話', route: '/chat' },
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
      ['/admin/logs?level=Error#events', '/admin/logs'],
      ['/admin/logs/details/123', '/admin/logs'],
      ['/admin?tab=users', '/admin'],
      ['/projects/project-123?view=members', '/projects'],
      ['/chat/conversation-123', '/chat'],
    ]) {
      await router.navigateByUrl(url);
      fixture.detectChanges();
      const active = fixture.nativeElement.querySelectorAll('a.current');
      expect(active.length).toBe(1);
      expect(active[0].getAttribute('href')).toBe(route);
      expect(active[0].getAttribute('aria-current')).toBe('page');
    }
    await router.navigateByUrl('/administration');
    fixture.detectChanges();
    expect(fixture.nativeElement.querySelectorAll('a.current').length).toBe(0);
  });
});
