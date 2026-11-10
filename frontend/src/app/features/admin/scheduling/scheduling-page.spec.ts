import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { afterEach, beforeAll, describe, expect, it, vi } from 'vitest';
import type { ResidentServiceDto, ScheduledTaskBoardDto } from '@core/api/schema';
import { AuthService } from '@core/auth/auth-service';
import { WorkspaceSession } from '@core/auth/workspace-session';
import { ApiError } from '@core/errors/safe-errors';
import { SchedulingApi } from './scheduling-api';
import { SchedulingPage } from './scheduling-page';

const service = (key: string, patch: Partial<ResidentServiceDto> = {}): ResidentServiceDto => ({
  key,
  name: key,
  category: '背景作業',
  description: '',
  rhythm: '每 5 秒',
  state: 'running',
  reason: null,
  ...patch,
});

const BOARD: ScheduledTaskBoardDto = {
  serverTime: '2026-11-08T06:00:00Z',
  executesHere: true,
  timelineFrom: '2026-11-08T02:00:00Z',
  timelineTo: '2026-11-09T02:00:00Z',
  tasks: [],
};

function setup(services: () => Promise<{ host: string; services: ResidentServiceDto[] }>) {
  vi.stubGlobal('matchMedia', (query: string) => ({
    matches: false,
    media: query,
    addEventListener() {},
    removeEventListener() {},
  }));
  const generation = signal(1);
  const session = {
    auth: { generation },
    load: vi.fn(async () => true),
    me: signal({ id: 'u1' }),
    has: (feature: string) => feature === 'scheduling',
    featureName: () => '背景作業',
  };
  const api = {
    board: vi.fn(async () => BOARD),
    jobs: vi.fn(async () => ({ items: [], total: 0, offset: 0, complete: true })),
    services: vi.fn(services),
  };
  TestBed.configureTestingModule({
    providers: [
      provideRouter([]),
      { provide: WorkspaceSession, useValue: session },
      { provide: AuthService, useValue: { generation, guard: () => () => true } },
      { provide: SchedulingApi, useValue: api },
    ],
  });
  const fixture = TestBed.createComponent(SchedulingPage);
  return { fixture, root: fixture.nativeElement as HTMLElement, api };
}

describe('SchedulingPage resident services', () => {
  beforeAll(() => {
    Element.prototype.scrollTo ??= function () {};
  });
  afterEach(() => {
    TestBed.resetTestingModule();
    vi.unstubAllGlobals();
  });

  it('reads the services only when that view is opened and shows each one with its state', async () => {
    const { fixture, root, api } = setup(async () => ({
      host: 'NEXUS-WEB-01',
      services: [
        service('jobs.worker', {
          name: '佇列工作處理',
          description: '執行使用者送出的工作。',
          rhythm: '有工作就立即處理',
        }),
        service('scheduling.engine', {
          name: '排程引擎',
          state: 'inactive',
          reason: '這個站台不執行排程（Scheduling:Execute），由其他站台執行。',
        }),
        service('chat.generation', { name: '回答生成', category: '聊天', state: 'stopped' }),
        service('mail.dispatcher', { name: '郵件寄送', state: 'failed', reason: 'SqlException' }),
      ],
    }));
    await fixture.whenStable();
    expect(api.services).not.toHaveBeenCalled();

    fixture.componentInstance.view.set('services');
    await fixture.whenStable();

    expect(api.services).toHaveBeenCalledTimes(1);
    expect(root.querySelector('.scheduling-services .ui-table-toolbar')?.textContent).toContain(
      'NEXUS-WEB-01',
    );
    const rows = [...root.querySelectorAll('.scheduling-services tbody tr')].map(
      (row) => row.textContent ?? '',
    );
    expect(rows).toHaveLength(4);
    expect(rows[0]).toContain('佇列工作處理');
    expect(rows[0]).toContain('執行使用者送出的工作。');
    expect(rows[0]).toContain('有工作就立即處理');
    expect(rows[0]).toContain('執行中');
    expect(rows[1]).toContain('未啟用');
    expect(rows[1]).toContain('Scheduling:Execute');
    expect(rows[2]).toContain('聊天');
    expect(rows[2]).toContain('已停止');
    expect(rows[2]).toContain('系統日誌');
    expect(rows[3]).toContain('已中止');
    expect(rows[3]).toContain('SqlException');
    // 照常執行的那一列不必多一句說明。
    expect(
      root.querySelectorAll('.scheduling-services tbody tr')[0].querySelector('td small'),
    ).toBeNull();
  });

  it('offers a retry when the services cannot be read', async () => {
    let fail = true;
    const { fixture, root, api } = setup(async () => {
      if (fail) throw new ApiError(503, 'service_unavailable');
      return { host: 'NEXUS-WEB-01', services: [service('jobs.worker', { name: '佇列工作處理' })] };
    });
    fixture.componentInstance.view.set('services');
    await fixture.whenStable();
    expect(root.querySelector('.scheduling-services tbody tr')).toBeNull();
    expect(root.querySelector('.scheduling-services nx-table-state')?.textContent).toContain(
      '無法取得常駐服務',
    );
    const retry = [...root.querySelectorAll('button')].find(
      (x) => x.textContent?.trim() === '重試',
    );
    expect(retry).toBeDefined();

    fail = false;
    retry!.click();
    await fixture.whenStable();
    expect(api.services).toHaveBeenCalledTimes(2);
    expect(root.querySelector('.scheduling-services tbody tr')?.textContent).toContain(
      '佇列工作處理',
    );
  });
});
