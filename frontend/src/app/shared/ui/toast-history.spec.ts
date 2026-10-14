import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { AuthService } from '@core/auth/auth-service';
import { ToastHistory } from './toast-history';
import { ToastService } from './toast-service';

describe('ToastHistory', () => {
  afterEach(() => {
    vi.useRealTimers();
    TestBed.resetTestingModule();
  });
  const setup = () => {
    TestBed.configureTestingModule({
      providers: [{ provide: AuthService, useValue: { generation: signal(0) } }],
    });
    const fixture = TestBed.createComponent(ToastHistory);
    fixture.detectChanges();
    return { fixture, element: fixture.nativeElement as HTMLElement };
  };

  it('lists settled toasts newest first with their time in the workspace zone', () => {
    vi.useFakeTimers({ toFake: ['Date'] });
    vi.setSystemTime(new Date('2026-10-11T06:05:00Z'));
    const { fixture, element } = setup();
    expect(element.textContent).toContain('這個分頁還沒有操作通知');
    const toasts = TestBed.inject(ToastService);
    toasts.success('變更已儲存', '會套用於其他裝置。');
    toasts.show({ tone: 'warning', title: '部分檔案未加入' });
    fixture.detectChanges();
    const rows = [...element.querySelectorAll('li')];
    expect(rows.map((row) => row.querySelector('strong')!.textContent)).toEqual([
      '部分檔案未加入',
      '變更已儲存',
    ]);
    expect(rows[1].textContent).toContain('會套用於其他裝置。');
    const time = rows[0].querySelector('time')!;
    expect(time.textContent!.trim()).toBe('14:05');
    expect(time.getAttribute('datetime')).toBe('2026-10-11T06:05:00.000Z');
  });

  it('runs a recorded action once and tells the host first', () => {
    const { fixture, element } = setup();
    const run = vi.fn();
    const activated = vi.fn();
    fixture.componentInstance.activated.subscribe(activated);
    TestBed.inject(ToastService).show({ title: '已封存', action: { label: '復原', run } });
    fixture.detectChanges();
    const button = element.querySelector<HTMLButtonElement>('li button')!;
    expect(button.getAttribute('aria-label')).toBe('復原：已封存');
    button.click();
    fixture.detectChanges();
    expect(activated).toHaveBeenCalledTimes(1);
    expect(run).toHaveBeenCalledTimes(1);
    expect(element.querySelector('li button')).toBeNull();
  });
});
