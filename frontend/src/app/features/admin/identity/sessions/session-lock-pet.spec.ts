import { Component, signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { ThemeService } from '@core/preferences/theme-service';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { SessionLockPet } from './session-lock-pet';

@Component({
  imports: [SessionLockPet],
  template: `<div #card></div>
    <nx-session-lock-pet
      [card]="card"
      [origin]="origin()"
      [shy]="shy()"
      [revealed]="revealed()"
      [capsLock]="capsLock()"
      [unlocked]="unlocked()"
      [rejections]="rejections()"
      owner="00000000-0000-0000-0000-000000000001"
    />`,
})
class Host {
  readonly origin = signal<{ x: number; y: number } | null>(null);
  readonly shy = signal(false);
  readonly revealed = signal(false);
  readonly capsLock = signal(false);
  readonly unlocked = signal(false);
  readonly rejections = signal(0);
}

const RECORD_KEY = 'ai-nexus.lock-pet.00000000-0000-0000-0000-000000000001';
/** 平日下午：沒有時段動作、也沒有節日打扮。 */
const PLAIN_AFTERNOON = new Date(2026, 10, 11, 15, 0);

/** 帶座標與時間的指標事件（jsdom 的 PointerEvent 不帶座標，`timeStamp` 也改不了）。 */
function pointer(type: string, x: number, y: number, time: number) {
  const event = Object.assign(new Event(type, { bubbles: true, cancelable: true }), {
    button: 0,
    pointerId: 1,
    pointerType: 'mouse',
    clientX: x,
    clientY: y,
  });
  Object.defineProperty(event, 'timeStamp', { value: time });
  return event;
}

describe('SessionLockPet', () => {
  let frames: FrameRequestCallback[];
  let clock: number;
  /** 以 60fps 推進 `seconds` 秒。 */
  const play = (seconds: number) => {
    for (let frame = 0; frame < seconds * 60; frame++) {
      clock += 1000 / 60;
      frames.splice(0).forEach((callback) => callback(clock));
    }
  };

  beforeEach(() => {
    frames = [];
    clock = performance.now();
    // 只換掉日期：打扮與時段看本地時間，逐幀與計時器照舊。
    vi.useFakeTimers({ toFake: ['Date'] });
    vi.setSystemTime(PLAIN_AFTERNOON);
    vi.spyOn(window, 'requestAnimationFrame').mockImplementation((frame) => frames.push(frame));
    vi.spyOn(window, 'cancelAnimationFrame').mockImplementation(() => {});
    vi.stubGlobal(
      'ResizeObserver',
      class {
        observe() {}
        disconnect() {}
      },
    );
  });
  afterEach(() => {
    vi.useRealTimers();
    vi.restoreAllMocks();
    vi.unstubAllGlobals();
    localStorage.clear();
    TestBed.resetTestingModule();
  });

  const setup = async (options: {
    reduced: boolean;
    cardTop?: number;
    origin?: { x: number; y: number };
  }) => {
    TestBed.configureTestingModule({
      providers: [{ provide: ThemeService, useValue: { reducedMotion: signal(options.reduced) } }],
    });
    const fixture = TestBed.createComponent(Host);
    const host = fixture.componentInstance;
    if (options.origin) host.origin.set(options.origin);
    const root = fixture.nativeElement as HTMLElement;
    document.body.append(root);
    const card = root.querySelector('div')!;
    const layer = root.querySelector<HTMLElement>('nx-session-lock-pet')!;
    // 1280×768 的視窗，24rem 的鎖定卡置中。
    card.getBoundingClientRect = () => new DOMRect(448, options.cardTop ?? 200, 384, 400);
    layer.getBoundingClientRect = () => new DOMRect(0, 0, 1280, 768);
    await fixture.whenStable();
    const figure = root.querySelector<HTMLElement>('.pet')!;
    const art = root.querySelector<HTMLElement>('nx-lock-pet-art')!;
    const bubble = root.querySelector<HTMLElement>('.pet-say')!;
    figure.setPointerCapture = vi.fn();
    const render = () => {
      fixture.detectChanges();
      return { mood: figure.dataset['mood'], saying: bubble.textContent!.trim() };
    };
    return { fixture, host, layer, figure, art, bubble, render };
  };

  it('is decoration only: hidden from assistive technology and never focusable', async () => {
    const { layer } = await setup({ reduced: true });
    expect(layer.getAttribute('aria-hidden')).toBe('true');
    expect(layer.querySelector('[tabindex], button, a, input')).toBeNull();
  });

  it('covers its eyes once the password field has text, and promises not to peek', async () => {
    const { host, render } = await setup({ reduced: true });
    play(0.1);
    expect(render().mood).toBe('idle');
    host.shy.set(true);
    TestBed.tick();
    play(0.1);
    expect(render()).toEqual({ mood: 'shy', saying: '我不會偷看' });
  });

  it('turns its back while the password is shown', async () => {
    const { host, art, render } = await setup({ reduced: true });
    play(0.1);
    host.shy.set(true);
    host.revealed.set(true);
    TestBed.tick();
    play(0.1);
    expect(render()).toEqual({ mood: 'turned', saying: '我轉過去了' });
    expect(art.dataset['mood']).toBe('turned');
    // 欄位清空就沒有東西可看，轉回來。
    host.shy.set(false);
    TestBed.tick();
    play(0.1);
    expect(render().mood).toBe('idle');
  });

  it('holds up a sign while Caps Lock is on', async () => {
    const { host, bubble, render } = await setup({ reduced: true });
    play(0.1);
    host.capsLock.set(true);
    TestBed.tick();
    play(0.1);
    expect(render()).toEqual({ mood: 'sign', saying: '大寫鎖定開著' });
    expect(bubble.classList).toContain('is-sign');
    host.capsLock.set(false);
    TestBed.tick();
    play(0.1);
    expect(render()).toEqual({ mood: 'idle', saying: '' });
    expect(bubble.classList).not.toContain('is-sign');
  });

  it('gets dizzy when a wrong password shakes the card', async () => {
    const { host, render } = await setup({ reduced: true });
    play(0.1);
    host.rejections.set(1);
    TestBed.tick();
    play(0.1);
    expect(render().mood).toBe('dizzy');
  });

  it('pops its own shackle open when the workspace unlocks', async () => {
    const { host, art, render } = await setup({ reduced: true });
    play(0.1);
    render();
    expect(art.hasAttribute('data-open')).toBe(false);
    host.unlocked.set(true);
    TestBed.tick();
    play(0.1);
    expect(render().mood).toBe('happy');
    expect(art.hasAttribute('data-open')).toBe(true);
  });

  it('hides when there is no room above the card', async () => {
    const { layer, fixture } = await setup({ reduced: true, cardTop: 20 });
    play(0.1);
    fixture.detectChanges();
    expect(layer.classList).toContain('is-away');
  });

  it('falls from the countdown island at half size and grows onto the card', async () => {
    const { figure, render } = await setup({ reduced: false, origin: { x: 760, y: 90 } });
    play(0.05);
    expect(figure.style.transform).toContain('translate(732px');
    expect(figure.style.transform).toMatch(/scale\(0\.[5-7]\d*, 0\.[5-7]\d*\)/);
    play(1.5);
    expect(render().mood).toBe('idle');
    expect(figure.style.transform).toMatch(/^translate\(732px, 140px\) rotate\(0deg\)/);
  });

  it('dresses for the time of day and the holidays', async () => {
    vi.setSystemTime(new Date(2026, 11, 25, 12, 15));
    const { art, render } = await setup({ reduced: true });
    play(0.1);
    render();
    expect(art.dataset).toMatchObject({ moment: 'lunch', hat: 'santa', held: 'bento' });
  });

  it('stretches after landing in the morning', async () => {
    vi.setSystemTime(new Date(2026, 10, 12, 8, 30));
    const { render } = await setup({ reduced: false });
    play(0.8);
    expect(render().mood).toBe('stretch');
    play(2);
    expect(render().mood).toBe('idle');
  });

  it('keeps the password field focused when picked up', async () => {
    const { figure } = await setup({ reduced: false });
    play(1);
    const press = pointer('pointerdown', 732, 170, 0);
    figure.dispatchEvent(press);
    expect(press.defaultPrevented).toBe(true);
  });

  it('flies when thrown, announces the distance and keeps the record on this browser', async () => {
    const { figure, render } = await setup({ reduced: false });
    // 從卡片上方落下、站穩。
    play(1);
    expect(render().mood).toBe('idle');
    const start = clock;
    figure.dispatchEvent(pointer('pointerdown', 732, 170, start));
    figure.dispatchEvent(pointer('pointermove', 800, 150, start + 10));
    figure.dispatchEvent(pointer('pointermove', 870, 130, start + 20));
    figure.dispatchEvent(pointer('pointerup', 870, 130, start + 25));
    play(0.1);
    expect(render().mood).toBe('fly');
    play(3);
    const { saying } = render();
    expect(saying).toMatch(/^新紀錄！飛了 \d+\.\d 公尺$/);
    expect(saying).toContain(Number(localStorage.getItem(RECORD_KEY)).toFixed(1));
  });
});
