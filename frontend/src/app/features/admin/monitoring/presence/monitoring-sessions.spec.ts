import { TestBed } from '@angular/core/testing';
import { afterEach, describe, expect, it } from 'vitest';
import type { OnlineSession } from '@core/api/schema';
import { WorkspaceSession } from '@core/auth/workspace-session';
import { MonitoringSessions } from './monitoring-sessions';

const now = '2026-11-02T08:00:00Z';

const sessions = (count: number): OnlineSession[] =>
  Array.from({ length: count }, (_, i) => ({
    id: `session-${i}`,
    userId: `00000000-0000-0000-0000-${String(i).padStart(12, '0')}`,
    account: `CORP\\user${i}`,
    displayName: `使用者 ${String(i).padStart(2, '0')}`,
    authentication: 'ad',
    address: `10.20.3.${i}`,
    browser: 'Edge',
    device: 'Windows',
    feature: i % 2 ? 'chat' : 'knowledge',
    state: 'active',
    connectedAt: now,
    lastSeenAt: now,
    lastAction: null,
    lastActionAt: null,
    requests: i,
    errors: 0,
    receivedBytes: 0,
    sentBytes: 0,
    testingIdentity: false,
  }));

function render() {
  TestBed.configureTestingModule({
    providers: [{ provide: WorkspaceSession, useValue: { featureName: (id: string) => id } }],
  });
  const fixture = TestBed.createComponent(MonitoringSessions);
  fixture.componentRef.setInput('sessions', sessions(25));
  fixture.detectChanges();
  fixture.componentInstance.table.goTo(1);
  fixture.detectChanges();
  return fixture;
}

describe('MonitoringSessions', () => {
  afterEach(() => TestBed.resetTestingModule());

  it('stays on the current page when a live snapshot arrives', () => {
    const fixture = render();
    fixture.componentRef.setInput('sessions', sessions(25));
    fixture.detectChanges();
    expect(fixture.componentInstance.table.pageIndex()).toBe(1);
  });

  it('stops at the last page when a snapshot has fewer sessions', () => {
    const fixture = render();
    fixture.componentRef.setInput('sessions', sessions(5));
    fixture.detectChanges();
    expect(fixture.componentInstance.table.pageIndex()).toBe(0);
    expect(fixture.componentInstance.table.rows()).toHaveLength(5);
  });

  it('returns to the first page when the search or feature changes', () => {
    const fixture = render();
    fixture.componentInstance.query.set('使用者');
    fixture.detectChanges();
    expect(fixture.componentInstance.table.pageIndex()).toBe(0);

    fixture.componentInstance.table.goTo(1);
    fixture.detectChanges();
    fixture.componentInstance.feature.set('chat');
    fixture.detectChanges();
    expect(fixture.componentInstance.table.pageIndex()).toBe(0);
  });
});
