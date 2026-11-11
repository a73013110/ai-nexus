import { Component } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { ThemeService } from '@core/preferences/theme-service';
import { OriginMotion, originKeyframe } from './origin-motion';

@Component({
  selector: 'nx-origin-motion-host',
  imports: [OriginMotion],
  template: `<button type="button" class="trigger">開啟</button>
    <dialog nxOriginMotion></dialog>`,
})
class Host {}

const box = (left: number, top: number, width: number, height: number) =>
  new DOMRect(left, top, width, height);

function setup(reducedMotion = false) {
  TestBed.configureTestingModule({
    providers: [{ provide: ThemeService, useValue: { reducedMotion: () => reducedMotion } }],
  });
  const fixture = TestBed.createComponent(Host);
  document.body.append(fixture.nativeElement);
  fixture.detectChanges();
  const root = fixture.nativeElement as HTMLElement;
  const trigger = root.querySelector<HTMLButtonElement>('.trigger')!;
  const dialog = root.querySelector('dialog')!;
  trigger.getBoundingClientRect = () => box(100, 500, 80, 40);
  dialog.getBoundingClientRect = () => box(200, 100, 800, 600);
  const animate = vi.fn((frames: Keyframe[], timing: KeyframeAnimationOptions) => ({
    frames,
    timing,
    cancel: vi.fn(),
  }));
  dialog.animate = animate as unknown as HTMLDialogElement['animate'];
  // 測試環境的 dialog 不送 beforetoggle：照瀏覽器的順序自己送，open 屬性在事件之後才改。
  const toggle = (newState: 'open' | 'closed') => {
    dialog.dispatchEvent(Object.assign(new Event('beforetoggle'), { newState }));
    dialog.toggleAttribute('open', newState === 'open');
    fixture.detectChanges();
  };
  return { trigger, dialog, animate, toggle };
}

describe('originKeyframe', () => {
  it('scales the frame uniformly to cover the origin, centred on it and clipped to its shape', () => {
    const frame = originKeyframe(box(200, 100, 800, 600), box(100, 500, 80, 40), 20);
    // 寬 80/800、高 40/600：取較大的 0.1 才蓋得住；縮小後 80×60，中心對到來源中心 (140, 520)。
    expect(frame['transform']).toBe('translate(-100px, 390px) scale(0.1)');
    // 高度多出的 20px（縮放後）在對話框座標是上下各 100px；圓角同樣除回比例。
    expect(frame['clipPath']).toBe('inset(100px 0px round 200px)');
  });

  it('never enlarges the frame for an origin larger than it', () => {
    const frame = originKeyframe(box(200, 100, 400, 200), box(0, 0, 1000, 48), 0);
    // 中心從 (400, 200) 移到來源中心 (500, 24)，高度裁到來源的 48px。
    expect(frame['transform']).toBe('translate(100px, -176px) scale(1)');
    expect(frame['clipPath']).toBe('inset(76px 0px round 0px)');
  });
});

describe('OriginMotion', () => {
  afterEach(() => {
    TestBed.resetTestingModule();
    document.body.replaceChildren();
  });

  it('grows from the control that was just activated and returns to it on close', () => {
    const { trigger, dialog, animate, toggle } = setup();
    trigger.click();
    toggle('open');
    expect(dialog.hasAttribute('data-origin')).toBe(true);
    const [opening] = animate.mock.calls[0];
    expect(opening[0]['transform']).toBe('translate(-100px, 390px) scale(0.1)');
    expect(opening[1]['transform']).toBe('none');

    toggle('closed');
    const [closing, timing] = animate.mock.calls[1];
    expect(closing[1]['transform']).toBe('translate(-100px, 390px) scale(0.1)');
    expect(timing.fill).toBe('forwards');
  });

  it('leaves the default entrance alone without an origin or once the origin is gone', () => {
    const { trigger, dialog, animate, toggle } = setup();
    toggle('open');
    expect(dialog.hasAttribute('data-origin')).toBe(false);
    expect(animate).not.toHaveBeenCalled();

    toggle('closed');
    trigger.click();
    toggle('open');
    expect(animate).toHaveBeenCalledTimes(1);
    trigger.getBoundingClientRect = () => box(0, 0, 0, 0);
    toggle('closed');
    expect(dialog.hasAttribute('data-origin')).toBe(false);
    expect(animate).toHaveBeenCalledTimes(1);
  });

  it('stays out of the way with reduced motion', () => {
    const { trigger, dialog, animate, toggle } = setup(true);
    trigger.click();
    toggle('open');
    expect(dialog.hasAttribute('data-origin')).toBe(false);
    expect(animate).not.toHaveBeenCalled();
  });
});
