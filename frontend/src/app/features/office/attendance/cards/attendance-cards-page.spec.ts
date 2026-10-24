import { ApplicationRef, signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { afterEach, describe, expect, it, vi } from 'vitest';
import type { CardAssignmentDto, CardDto } from '@core/api/schema';
import { AuthService } from '@core/auth/auth-service';
import { WorkspaceSession } from '@core/auth/workspace-session';
import { tourSteps, type ProductTourDefinition } from '@shared/ui/product-tour';
import { ToastService } from '@shared/ui/toast-service';
import { CollaborationApi } from '@features/work/collaboration/collaboration-api';
import { AttendanceApi } from '../attendance-api';
import { AttendanceCardsPage } from './attendance-cards-page';

const PAGE = 'nx-attendance-cards-page';
const held: CardAssignmentDto = {
  id: 'a1',
  cardId: 'c1',
  cardNumber: '0012345',
  userId: 'u1',
  userDisplayName: '王小明',
  holderName: null,
  startsAt: '2026-01-01T00:00:00Z',
  endsAt: null,
  note: null,
  createdAt: '2026-01-01T00:00:00Z',
};
const card: CardDto = {
  id: 'c1',
  cardNumber: '0012345',
  kind: 'employee',
  note: null,
  createdAt: '2026-01-01T00:00:00Z',
  current: held,
};

describe('AttendanceCardsPage tour targets', () => {
  afterEach(() => {
    TestBed.resetTestingModule();
    vi.unstubAllGlobals();
  });

  it('finds every page step on both tabs and every drawer step once a card is selected', async () => {
    vi.stubGlobal('matchMedia', (query: string) => ({
      matches: false,
      media: query,
      addEventListener() {},
      removeEventListener() {},
    }));
    // jsdom 沒有捲動與 dialog.close()；換分頁時頁面會關閉抽屜並重設表格捲動。
    Element.prototype.scrollTo ??= function () {};
    HTMLDialogElement.prototype.close ??= function (this: HTMLDialogElement) {
      this.removeAttribute('open');
    };
    const generation = signal(1);
    TestBed.configureTestingModule({
      providers: [
        provideRouter([]),
        {
          provide: WorkspaceSession,
          useValue: {
            auth: { generation },
            load: vi.fn(async () => true),
            me: signal({ id: 'u1' }),
            has: (feature: string) => feature.startsWith('attendance.cards'),
            featureName: () => '卡號對應',
            testing: signal(false),
            testingNote: signal(''),
          },
        },
        { provide: AuthService, useValue: { generation, guard: () => () => true } },
        {
          provide: AttendanceApi,
          useValue: {
            cards: vi.fn(async () => [card]),
            unattributed: vi.fn(async () => [
              {
                cardNumber: '0077777',
                cardId: null,
                punchCount: 3,
                firstPunchedAt: '2026-10-01T00:55:00Z',
                lastPunchedAt: '2026-10-02T10:01:00Z',
              },
            ]),
            assignments: vi.fn(async () => [held]),
            exclusions: vi.fn(async () => [
              {
                userId: 'u9',
                displayName: '林董事',
                account: 'TEST\\lin',
                note: null,
                createdAt: '2026-10-01T02:00:00Z',
              },
            ]),
          },
        },
        { provide: CollaborationApi, useValue: { directory: vi.fn(async () => []) } },
        { provide: ToastService, useValue: { success: vi.fn() } },
      ],
    });
    const fixture = TestBed.createComponent(AttendanceCardsPage);
    const page = fixture.componentInstance;
    const host = fixture.nativeElement as HTMLElement;
    const settle = async () => {
      await TestBed.inject(ApplicationRef).whenStable();
      fixture.detectChanges();
    };
    // 元件宿主本身就是頁面標籤，查詢時去掉開頭的宿主選擇器。
    const expectTargets = (definition: ProductTourDefinition | null) => {
      expect(definition).not.toBeNull();
      for (const step of tourSteps(definition!))
        expect(host.querySelector(step.target.slice(PAGE.length + 1)), step.target).not.toBeNull();
    };
    await settle();
    expect(host.querySelector('nx-feature-page nx-product-tour-button')).not.toBeNull();
    expectTargets(page.tour());
    expect(page.drawerTour()).toBeNull();

    page.store.selectedId.set(card.id);
    await settle();
    expect(host.querySelector('nx-detail-drawer nx-product-tour-button')).not.toBeNull();
    expectTargets(page.drawerTour());

    page.query.commit({ view: 'unattributed' });
    await settle();
    await settle();
    expect(tourSteps(page.tour()).map((step) => step.title)).toContain('指派');
    expectTargets(page.tour());

    page.query.commit({ view: 'exclusions' });
    await settle();
    await settle();
    expect(tourSteps(page.tour()).map((step) => step.title)).toContain('加入');
    expectTargets(page.tour());
  });
});
