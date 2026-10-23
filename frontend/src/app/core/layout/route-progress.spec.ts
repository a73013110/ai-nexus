import { Component } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { Router, provideRouter } from '@angular/router';
import { afterEach, describe, expect, it } from 'vitest';
import { ROUTE_PROGRESS_DELAY, RouteProgress } from './route-progress';

@Component({ selector: 'nx-route-progress-page', template: '' })
class Page {}

const wait = (ms: number) => new Promise((done) => setTimeout(done, ms));

function setup() {
  let release: (allow: boolean) => void = () => undefined;
  let failure: (error: Error) => void = () => undefined;
  TestBed.configureTestingModule({
    providers: [
      provideRouter([
        { path: 'quick', component: Page },
        {
          // 模擬延遲載入的 chunk 或 guard 等 `/me`：由測試決定何時放行、拒絕或失敗。
          path: 'slow',
          component: Page,
          canActivate: [
            () =>
              new Promise<boolean>((resolve, reject) => {
                release = resolve;
                failure = reject;
              }),
          ],
        },
      ]),
    ],
  });
  const fixture = TestBed.createComponent(RouteProgress);
  fixture.detectChanges();
  const router = TestBed.inject(Router);
  const bar = () => {
    fixture.detectChanges();
    return (fixture.nativeElement as HTMLElement).querySelector('[role="progressbar"]');
  };
  return {
    router,
    bar,
    release: (allow: boolean) => release(allow),
    fail: (e: Error) => failure(e),
  };
}

describe('RouteProgress', () => {
  afterEach(() => TestBed.resetTestingModule());

  it('stays hidden for a navigation that finishes quickly', async () => {
    const { router, bar } = setup();
    await router.navigateByUrl('/quick');
    await wait(ROUTE_PROGRESS_DELAY + 50);
    expect(bar()).toBeNull();
  });

  it('shows a top line after the delay while a guard waits, until the navigation ends', async () => {
    const { router, bar, release } = setup();
    const done = router.navigateByUrl('/slow');
    await wait(ROUTE_PROGRESS_DELAY / 2);
    expect(bar()).toBeNull();
    await wait(ROUTE_PROGRESS_DELAY);
    expect(bar()?.getAttribute('aria-label')).toBe('正在載入頁面…');
    release(true);
    await done;
    expect(bar()).toBeNull();
  });

  it('goes away when the navigation is refused or fails', async () => {
    const { router, bar, release, fail } = setup();
    let done = router.navigateByUrl('/slow');
    await wait(ROUTE_PROGRESS_DELAY + 50);
    expect(bar()).not.toBeNull();
    release(false);
    expect(await done).toBe(false);
    expect(bar()).toBeNull();

    done = router.navigateByUrl('/slow');
    await wait(ROUTE_PROGRESS_DELAY + 50);
    expect(bar()).not.toBeNull();
    fail(new Error('chunk 載入失敗'));
    await expect(done).rejects.toThrow('chunk 載入失敗');
    expect(bar()).toBeNull();
  });
});
