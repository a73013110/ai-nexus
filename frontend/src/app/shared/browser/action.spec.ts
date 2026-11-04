import { describe, expect, it } from 'vitest';
import { ApiError, STALE_WRITE_MESSAGE } from '@core/errors/safe-errors';
import { Action, type ActionOptions } from './action';

/** 以旗標代替帳號與頁面：`alive` 改成 false 等於已離開頁面。 */
function setup(options: ActionOptions = {}) {
  const view = { alive: true };
  const scope = { guard: () => () => view.alive };
  return { view, action: new Action(scope, options) };
}

const deferred = <T>() => {
  let resolve!: (value: T) => void;
  let reject!: (error: unknown) => void;
  const promise = new Promise<T>((ok, fail) => ((resolve = ok), (reject = fail)));
  return { promise, resolve, reject };
};

describe('Action', () => {
  it('marks the run as pending and returns the value', async () => {
    const { action } = setup();
    const work = deferred<number>();
    const running = action.run(() => work.promise, { key: 'row:1' });
    expect(action.pending()).toBe(true);
    expect(action.pendingFor('row:1')).toBe(true);
    expect(action.pendingFor('row:2')).toBe(false);
    work.resolve(7);
    expect(await running).toEqual({ ok: true, value: 7 });
    expect(action.pending()).toBe(false);
    expect(action.pendingFor('row:1')).toBe(false);
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
    const { action } = setup({ message: () => '公文系統暫時無法使用。' });
    await action.run(() => Promise.reject(new Error('down')));
    expect(action.error()).toBe('公文系統暫時無法使用。');
  });

  it('does not start after leaving the page', async () => {
    const { view, action } = setup();
    view.alive = false;
    let called = false;
    expect(await action.run(async () => (called = true))).toEqual({
      ok: false,
      error: '',
      stale: false,
    });
    expect(called).toBe(false);
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

  it('runs different rows at once when concurrent, but never the same row twice', async () => {
    const { action } = setup({ concurrent: true });
    const first = deferred<void>();
    const running = action.run(() => first.promise, { key: 'job:1' });
    expect(await action.run(async () => 2, { key: 'job:2' })).toEqual({ ok: true, value: 2 });
    expect(await action.run(async () => 3, { key: 'job:1' })).toEqual({
      ok: false,
      error: '',
      stale: false,
    });
    expect(action.pendingFor('job:1')).toBe(true);
    first.resolve();
    await running;
    expect(action.pending()).toBe(false);
  });

  it('drops the result when the target changed meanwhile', async () => {
    const { action } = setup();
    const target = { id: 'a' };
    const work = deferred<number>();
    const running = action.run(() => work.promise, { current: () => target.id === 'a' });
    target.id = 'b';
    work.reject(new Error('late'));
    expect(await running).toEqual({ ok: false, error: '', stale: false });
    expect(action.error()).toBe('');
  });

  it('passes the guard to the work and ignores cancellations', async () => {
    const { view, action } = setup();
    let seen: boolean[] = [];
    await action.run(async ({ alive }) => {
      seen.push(alive());
      view.alive = false;
      seen.push(alive());
    });
    expect(seen).toEqual([true, false]);
    view.alive = true;
    seen = [];
    const outcome = await action.run(() => Promise.reject(new DOMException('stop', 'AbortError')));
    expect(outcome).toEqual({ ok: false, error: '', stale: false });
    expect(action.error()).toBe('');
  });

  it('resets every pending run and the last failure', async () => {
    const { action } = setup({ concurrent: true });
    action.fail('上一次的失敗');
    const first = action.run(() => new Promise<number>(() => {}), { key: 'a' });
    void action.run(() => new Promise<number>(() => {}), { key: 'b' });
    action.reset();
    expect(action.pending()).toBe(false);
    expect(action.error()).toBe('');
    expect(await action.run(async () => 1, { key: 'a' })).toEqual({ ok: true, value: 1 });
    void first;
  });

  it('cancels a run so the same work can start again at once', async () => {
    const { action } = setup();
    let aborted = false;
    const first = action.run(
      ({ signal }) =>
        new Promise((_, reject) =>
          signal.addEventListener('abort', () => {
            aborted = true;
            reject(new DOMException('stop', 'AbortError'));
          }),
        ),
    );
    action.cancel();
    expect(aborted).toBe(true);
    expect(action.pending()).toBe(false);
    const second = deferred<string>();
    const again = action.run(() => second.promise);
    expect(await first).toEqual({ ok: false, error: '', stale: false });
    // 舊的那次結束時不會把新的那次標成已完成。
    expect(action.pending()).toBe(true);
    second.resolve('新結果');
    expect(await again).toEqual({ ok: true, value: '新結果' });
  });
});
