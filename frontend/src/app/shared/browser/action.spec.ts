import { describe, expect, it } from 'vitest';
import { ApiError, STALE_WRITE_MESSAGE } from '@core/errors/safe-errors';
import { Action } from './action';
import type { ViewScope } from './view-scope';

/** 以旗標代替帳號與頁面：`alive` 改成 false 等於已離開頁面。 */
function setup(message?: (error: unknown) => string) {
  const view = { alive: true };
  const scope = { guard: () => () => view.alive } as unknown as ViewScope;
  return { view, action: new Action(scope, message) };
}

describe('Action', () => {
  it('marks the run as pending and returns the value', async () => {
    const { action } = setup();
    let finish!: (value: number) => void;
    const running = action.run(() => new Promise<number>((resolve) => (finish = resolve)), 'row:1');
    expect(action.pending()).toBe(true);
    expect(action.key()).toBe('row:1');
    finish(7);
    expect(await running).toEqual({ ok: true, value: 7 });
    expect(action.pending()).toBe(false);
    expect(action.key()).toBe('');
  });

  it('skips a second run while one is pending', async () => {
    const { action } = setup();
    let finish!: () => void;
    const first = action.run(() => new Promise<void>((resolve) => (finish = resolve)));
    let called = false;
    expect(await action.run(async () => (called = true))).toEqual({
      ok: false,
      error: '',
      stale: false,
    });
    expect(called).toBe(false);
    finish();
    await first;
  });

  it('keeps a reviewed message and flags stale writes', async () => {
    const { action } = setup();
    const conflict = new ApiError(409, 'admin_write_conflict');
    const outcome = await action.run(() => Promise.reject(conflict));
    expect(outcome).toEqual({
      ok: false,
      error: STALE_WRITE_MESSAGE,
      stale: true,
      cause: conflict,
    });
    expect(action.error()).toBe(STALE_WRITE_MESSAGE);
    // 下一次開始時清掉上一次的失敗。
    await action.run(async () => undefined);
    expect(action.error()).toBe('');
  });

  it('uses the given message for failures', async () => {
    const { action } = setup(() => '公文系統暫時無法使用。');
    await action.run(() => Promise.reject(new Error('down')));
    expect(action.error()).toBe('公文系統暫時無法使用。');
  });

  it('drops results that come back after leaving the page', async () => {
    const { view, action } = setup();
    let fail!: (error: unknown) => void;
    const running = action.run(() => new Promise((_, reject) => (fail = reject)));
    view.alive = false;
    fail(new Error('late'));
    expect(await running).toEqual({ ok: false, error: '', stale: false });
    expect(action.error()).toBe('');
    // 換帳號時頁面可能還在，按鈕不能一直轉。
    expect(action.pending()).toBe(false);
  });
});
