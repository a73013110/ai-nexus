import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { afterEach, describe, expect, it, vi } from 'vitest';
import type { AccountReviewsDto } from '@core/api/schema';
import { AdminUsersStore } from '../admin-users-store';
import { AccountReviews } from './account-reviews';

const reviews: AccountReviewsDto = {
  users: [
    {
      id: 'u1',
      account: 'CORP\\wang',
      displayName: '王小明',
      enabled: false,
      deleted: false,
      items: [
        {
          code: 'attendance.card_held',
          title: '仍持有卡號',
          detail: '卡號 A001',
          route: '/attendance/cards?search=A001',
        },
        {
          code: 'access-control.role_orphaned',
          title: '唯一的角色持有人',
          detail: null,
          route: null,
        },
      ],
    },
    {
      id: 'u2',
      account: 'CORP\\lin',
      displayName: '林小華',
      enabled: false,
      deleted: true,
      items: [{ code: 'attendance.card_held', title: '仍持有卡號', detail: null, route: null }],
    },
  ],
  notices: ['AD 目錄暫時無法讀取，這次沒有檢查 AD 已停用的帳號。'],
};

function setup(value: AccountReviewsDto | null = reviews) {
  // 面板依視口決定是否模態。
  vi.stubGlobal('matchMedia', () => ({
    matches: false,
    addEventListener: () => undefined,
    removeEventListener: () => undefined,
  }));
  const store = {
    reviews: signal(value),
    reviewsRead: { refreshing: signal(false), error: signal(''), reload: vi.fn() },
    find: vi.fn(),
  };
  TestBed.configureTestingModule({
    providers: [provideRouter([]), { provide: AdminUsersStore, useValue: store }],
  });
  const fixture = TestBed.createComponent(AccountReviews);
  fixture.detectChanges();
  return { host: fixture.nativeElement as HTMLElement, store, fixture };
}

describe('account reviews', () => {
  afterEach(() => {
    TestBed.resetTestingModule();
    vi.unstubAllGlobals();
  });

  it('counts users with reviews and groups their items with links to where they are handled', () => {
    const { host } = setup();
    const button = host.querySelector<HTMLButtonElement>('.account-reviews-button')!;
    expect(button.getAttribute('aria-label')).toBe('帳號待辦：2 位使用者');
    expect(host.querySelector('nx-count-badge')?.textContent?.trim()).toBe('2');
    expect(host.querySelector('nx-notice')?.textContent).toContain('AD 目錄暫時無法讀取');
    const [first, second] = Array.from(host.querySelectorAll('section'));
    expect(first.getAttribute('aria-label')).toBe('王小明');
    const links = first.querySelectorAll('a');
    expect(links.length).toBe(1);
    expect(links[0].getAttribute('href')).toBe('/attendance/cards?search=A001');
    expect(first.textContent).toContain('唯一的角色持有人');
    // 已刪除的人不在列表裡，所以不提供搜尋。
    expect(second.querySelector('button')).toBeNull();
    expect(second.textContent).toContain('已刪除');
  });

  it('searches the list for a user by account', () => {
    const { host, store } = setup();
    host.querySelector<HTMLButtonElement>('section button')!.click();
    expect(store.find).toHaveBeenCalledWith('CORP\\wang');
  });

  it('says there is nothing to review only after reading', () => {
    const empty = setup({ users: [], notices: [] });
    expect(empty.host.querySelector('nx-empty-state')?.textContent).toContain('目前沒有帳號待辦');
    expect(empty.host.querySelector('nx-count-badge')?.hasAttribute('hidden')).toBe(true);
    TestBed.resetTestingModule();
    const pending = setup(null);
    expect(pending.host.querySelector('nx-empty-state')).toBeNull();
    expect(pending.host.querySelector('.skeleton')).not.toBeNull();
  });
});
