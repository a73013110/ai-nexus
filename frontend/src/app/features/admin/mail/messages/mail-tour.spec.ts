import { ApplicationRef, signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { afterEach, describe, expect, it, vi } from 'vitest';
import type { MailMessageDetailDto, MailMessageDto, MailStatusDto } from '@core/api/schema';
import { AuthService } from '@core/auth/auth-service';
import { WorkspaceSession } from '@core/auth/workspace-session';
import { ClientIssues } from '@core/errors/client-issues';
import { cursorPaginationStep } from '@shared/ui/data-workspace-tour';
import { ProductTour, tourSteps, type ProductTourDefinition } from '@shared/ui/product-tour';
import { MailApi } from '../mail-api';
import { MailMessagesPage } from './mail-messages-page';
import { mailDetailTour, mailMessagesTour } from './mail-tour';

const titles = (definition: ProductTourDefinition | null) =>
  definition ? tourSteps(definition).map((step) => step.title) : [];
const copy = (definition: ProductTourDefinition) =>
  tourSteps(definition)
    .map((step) => step.description)
    .join('\n');

describe('mailMessagesTour', () => {
  it('walks diagnosis, test, status, query, table and paging for readers', () => {
    const reader = mailMessagesTour({ resend: false, testing: false, logs: null });
    expect(titles(reader)).toEqual([
      '寄送紀錄回答什麼',
      '連線診斷',
      '寄信測試',
      '依狀態切換',
      '查詢條件',
      '寄送紀錄',
      '翻頁',
    ]);
    expect(copy(reader)).not.toContain('立即重試或重寄');
    expect(tourSteps(reader).at(-1)).toEqual(
      cursorPaginationStep('nx-mail-messages-page', { sortable: false }),
    );
  });

  it('adds the auto-retry switch and detail actions with the resend grant', () => {
    const operator = mailMessagesTour({ resend: true, testing: false, logs: null });
    expect(titles(operator)).toContain('自動重試');
    expect(copy(operator)).toContain('可立即重試或重寄');
  });

  it('names system logs only when granted and warns test identities', () => {
    expect(copy(mailMessagesTour({ resend: false, testing: false, logs: '系統日誌' }))).toContain(
      '到「系統日誌」查',
    );
    const denied = copy(mailMessagesTour({ resend: false, testing: true, logs: null }));
    expect(denied).not.toContain('日誌');
    expect(denied).toContain('測試身分，只能查看');
    expect(copy(mailMessagesTour({ resend: false, testing: false, logs: null }))).not.toContain(
      '測試身分',
    );
  });

  it('shows at most one detail action, chosen by the message state', () => {
    expect(titles(mailDetailTour({ action: null, attempts: false, logs: null }))).toEqual([
      '郵件摘要',
      '內文與保存期限',
      '關聯識別',
      '上一筆／下一筆',
    ]);
    expect(titles(mailDetailTour({ action: 'retry', attempts: true, logs: null }))).toEqual([
      '郵件摘要',
      '立即重試',
      '寄送嘗試',
      '內文與保存期限',
      '關聯識別',
      '上一筆／下一筆',
    ]);
    const resend = mailDetailTour({ action: 'resend', attempts: true, logs: '系統日誌' });
    expect(titles(resend)).toContain('重寄');
    expect(titles(resend)).not.toContain('立即重試');
    expect(copy(resend)).toContain('到「系統日誌」查');
    expect(copy(mailDetailTour({ action: 'resend', attempts: true, logs: null }))).not.toContain(
      '日誌',
    );
  });

  it('scopes page steps to the page and drawer steps to the drawer', () => {
    for (const step of tourSteps(mailMessagesTour({ resend: true, testing: true, logs: 'x' })))
      expect(step.target).toMatch(/^nx-mail-messages-page /);
    for (const action of ['retry', 'resend'] as const)
      for (const step of tourSteps(mailDetailTour({ action, attempts: true, logs: 'x' })))
        expect(step.target).toMatch(/^nx-mail-messages-page nx-detail-drawer /);
  });
});

const message = (patch: Partial<MailMessageDto>): MailMessageDto => ({
  id: '00000000-0000-0000-0000-0000000000a1',
  createdAt: '2026-10-21T04:00:00Z',
  status: 'sent',
  category: 'account.email-change-code',
  priority: 'normal',
  recipient: 'a***@company.com',
  subject: '設定測試信',
  attempts: 1,
  nextAttemptAt: '2026-10-21T04:00:00Z',
  sentAt: '2026-10-21T04:00:01Z',
  errorCode: null,
  sensitive: false,
  ...patch,
});
const sent = message({});
const waiting = message({
  id: '00000000-0000-0000-0000-0000000000a2',
  status: 'queued',
  sentAt: null,
  errorCode: 'mail_deferred',
  // 遠在未來的下次嘗試，背景更新不會在測試期間觸發。
  nextAttemptAt: '2099-01-01T00:00:00Z',
});
const detailOf = (row: MailMessageDto): MailMessageDetailDto => ({
  message: row,
  headerMessageId: '<a1@company.com>',
  fromAddress: 'noreply@company.com',
  batchId: null,
  resendOfId: null,
  requestedById: null,
  requestedBy: null,
  recipientUserId: null,
  transport: 'Smtp',
  providerResponse: null,
  issueCode: null,
  traceId: null,
  expiresAt: null,
  updatedAt: row.createdAt,
  contentPurgedAt: null,
  recipientAvailable: true,
  text: '內文',
  canResend: row.status === 'sent',
  resendBlockedBy: null,
  attempts: [
    {
      attempt: 1,
      transport: 'Smtp',
      startedAt: row.createdAt,
      finishedAt: row.createdAt,
      outcome: row.status === 'sent' ? 'sent' : 'transient',
      smtpStatus: row.status === 'sent' ? 250 : 451,
      enhancedStatus: null,
      response: null,
      errorCode: row.errorCode,
      issueCode: null,
      stage: null,
    },
  ],
});
const status: MailStatusDto = {
  transport: 'Smtp',
  enabled: true,
  fromAddress: 'noreply@company.com',
  server: 'smtp.company.com:587',
  pausedUntil: null,
  pauseReason: null,
  queued: 0,
  sending: 0,
  failedLastHour: 0,
  sentLastDay: 1,
  oldestQueuedAt: null,
  maxPerMinute: 0,
  maxPerDay: 0,
  giveUpAfterHours: 48,
  autoRetry: true,
};

describe('mail messages tour targets', () => {
  afterEach(() => {
    TestBed.resetTestingModule();
    vi.unstubAllGlobals();
  });

  it('finds every page and drawer target for an operator', async () => {
    vi.stubGlobal('matchMedia', () => ({
      matches: false,
      addEventListener: () => undefined,
      removeEventListener: () => undefined,
    }));
    // jsdom 沒有 dialog.close() 與 scrollTo()；每次套用查詢頁面都會關閉抽屜並重設表格捲動。
    HTMLDialogElement.prototype.close ??= function (this: HTMLDialogElement) {
      this.removeAttribute('open');
    };
    Element.prototype.scrollTo ??= () => undefined;
    const generation = signal(1);
    const granted = new Set(['mail.messages', 'mail.messages.resend', 'diagnostics.logs']);
    const rows = [waiting, sent];
    TestBed.configureTestingModule({
      providers: [
        provideRouter([]),
        {
          provide: MailApi,
          useValue: {
            list: vi.fn(async () => rows),
            status: vi.fn(async () => status),
            get: vi.fn(async (id: string) => detailOf(rows.find((row) => row.id === id)!)),
          },
        },
        {
          provide: WorkspaceSession,
          useValue: {
            auth: { generation },
            testing: signal(false),
            testingNote: signal(''),
            me: signal(null),
            load: vi.fn(async () => true),
            has: (feature: string) => granted.has(feature),
            featureName: (feature: string) => feature,
          },
        },
        { provide: AuthService, useValue: { generation, guard: AuthService.prototype.guard } },
        { provide: ProductTour, useValue: { loading: signal(false), active: signal(false) } },
        { provide: ClientIssues, useValue: {} },
      ],
    });
    const fixture = TestBed.createComponent(MailMessagesPage);
    const settle = async () => {
      await TestBed.inject(ApplicationRef).whenStable();
      fixture.detectChanges();
    };
    await settle();
    await settle();
    const component = fixture.componentInstance;
    const host = fixture.nativeElement as HTMLElement;
    expect(component.rows()).toHaveLength(2);
    // 元件宿主本身就是頁面標籤，查詢時去掉開頭的宿主選擇器。
    const expectTargets = (definition: ProductTourDefinition | null) => {
      expect(definition).not.toBeNull();
      for (const step of tourSteps(definition!))
        expect(
          host.querySelector(step.target.replace(/^nx-mail-messages-page /, '')),
          step.target,
        ).not.toBeNull();
    };
    expect(host.querySelector('nx-feature-page nx-product-tour-button')).not.toBeNull();
    expect(titles(component.tour())).toContain('自動重試');
    expect(copy(component.tour()!)).toContain('「diagnostics.logs」');
    expectTargets(component.tour());
    expect(component.drawerTour()).toBeNull();

    // 直接設定選取，不經由 jsdom 的 dialog.show()；抽屜內容不論開關都在 DOM。
    component.selectedId.set(waiting.id);
    await settle();
    await settle();
    expect(host.querySelector('nx-detail-drawer nx-product-tour-button')).not.toBeNull();
    expect(titles(component.drawerTour())).toContain('立即重試');
    expectTargets(component.drawerTour());

    component.selectedId.set(sent.id);
    await settle();
    await settle();
    expect(titles(component.drawerTour())).toContain('重寄');
    expectTargets(component.drawerTour());
  });
});
