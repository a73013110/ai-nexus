import { ApplicationRef, signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { afterEach, describe, expect, it, vi } from 'vitest';
import type { DiagnosticHealthDto } from '../../../core/api/schema';
import { WorkspaceSession } from '../../../core/auth/workspace-session';
import { LogHealth } from './log-health';
import { SystemLogsApi } from './system-logs-api';

const health = (queueDepth: number, status = 'healthy') =>
  ({
    status,
    queueDepth,
    writeFailures: 0,
    pendingBytes: 0,
    estimatedSqlRows: 0,
    lost: 0,
    corrupt: 0,
    replayed: 0,
    diskBytes: 0,
    sampled: 0,
    lastSqlWrite: null,
    lastFileWrite: null,
    exportFailures: 0,
    failure: null,
  }) as unknown as DiagnosticHealthDto;

function setup(fresh = health(9, 'degraded')) {
  const read = vi.fn(async () => fresh);
  TestBed.configureTestingModule({
    providers: [
      { provide: SystemLogsApi, useValue: { health: read } },
      {
        provide: WorkspaceSession,
        useValue: { auth: { generation: signal(1) }, load: async () => true, has: () => true },
      },
    ],
  });
  const fixture = TestBed.createComponent(LogHealth);
  const settle = () => TestBed.inject(ApplicationRef).whenStable();
  return { fixture, health: fixture.componentInstance, read, settle };
}

describe('LogHealth', () => {
  afterEach(() => TestBed.resetTestingModule());

  it('shows the reported health and reads nothing until asked', async () => {
    const { fixture, health: view, read, settle } = setup();
    fixture.componentRef.setInput('reported', health(1));
    await settle();
    expect(read).not.toHaveBeenCalled();
    expect(view.health()?.queueDepth).toBe(1);
    expect((fixture.nativeElement as HTMLElement).textContent).toContain('日誌系統正常');
  });

  it('shows whichever health arrived last', async () => {
    const { fixture, health: view, read, settle } = setup();
    fixture.componentRef.setInput('reported', health(1));
    await settle();
    view.refresh();
    await settle();
    expect(read).toHaveBeenCalledTimes(1);
    expect(view.health()?.queueDepth).toBe(9);
    expect((fixture.nativeElement as HTMLElement).textContent).toContain('日誌系統降級');
    fixture.componentRef.setInput('reported', health(2));
    await settle();
    expect(view.health()?.queueDepth).toBe(2);
  });
});
