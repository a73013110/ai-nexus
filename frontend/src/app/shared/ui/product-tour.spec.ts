import { TestBed } from '@angular/core/testing';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { ThemeService } from '@core/preferences/theme-service';
import { ProductTour, tourSteps, type ProductTourDefinition } from './product-tour';

const step = (target: string, title: string) => ({ target, title, description: title + '。' });

describe('ProductTour', () => {
  let tour: ProductTour;
  beforeEach(() => {
    // jsdom 沒有版面與 top layer：以 hidden 屬性代表不可見，popover 只記錄狀態。
    HTMLElement.prototype.checkVisibility = function (this: HTMLElement) {
      return !this.closest('[hidden]');
    };
    HTMLElement.prototype.showPopover = vi.fn();
    HTMLElement.prototype.scrollIntoView = vi.fn();
    TestBed.configureTestingModule({
      // 減少動態效果時 driver.js 同步渲染說明框，不必等影格。
      providers: [{ provide: ThemeService, useValue: { reducedMotion: () => true } }],
    });
    tour = TestBed.inject(ProductTour);
    vi.spyOn(
      tour as unknown as { loadStyles: () => Promise<void> },
      'loadStyles',
    ).mockResolvedValue();
  });
  afterEach(() => {
    tour.stop();
    document.body.replaceChildren();
    vi.restoreAllMocks();
    delete (HTMLElement.prototype as Partial<HTMLElement>).checkVisibility;
    delete (HTMLElement.prototype as Partial<HTMLElement>).showPopover;
    delete (HTMLElement.prototype as Partial<HTMLElement>).scrollIntoView;
  });
  const popover = () => document.querySelector<HTMLElement>('.driver-popover')!;
  const next = () =>
    popover().querySelector<HTMLButtonElement>('.driver-popover-next-btn')!.click();

  it('drops falsy steps from a definition', () => {
    const edit = false;
    const definition: ProductTourDefinition = {
      steps: [step('.a', '甲'), edit && step('.b', '乙'), null, undefined, step('.c', '丙')],
    };
    expect(tourSteps(definition).map((item) => item.title)).toEqual(['甲', '丙']);
  });

  it('skips hidden steps and adds the replay hint to the last shown one', async () => {
    document.body.innerHTML = `<div class="a"></div><div class="b" hidden></div><div class="c"></div>`;
    await tour.start({
      steps: [step('.a', '甲'), false, step('.b', '乙'), step('.c', '丙'), step('.missing', '丁')],
    });
    expect(tour.active()).toBe(true);
    expect(popover().querySelector('.driver-popover-progress-text')?.textContent).toBe('1 / 2');
    expect(popover().querySelector('.driver-popover-description')?.textContent).toBe('甲。');
    next();
    expect(popover().querySelector('.driver-popover-title')?.textContent).toBe('丙');
    expect(popover().querySelector('.driver-popover-description')?.textContent).toMatch(
      /^丙。.*重播導覽。$/,
    );
  });

  it('runs inside an open dialog: Escape closes only the tour and focus returns to the trigger', async () => {
    document.body.innerHTML = `<dialog open><button class="trigger">導覽</button><div class="target"></div></dialog>`;
    const dialog = document.querySelector('dialog')!;
    const trigger = document.querySelector<HTMLButtonElement>('.trigger')!;
    const dialogKeys = vi.fn();
    dialog.addEventListener('keydown', dialogKeys);
    trigger.focus();
    await tour.start({ steps: [step('.target', '目標')] });
    // 說明框放進 dialog 內的導覽層，模態時才不會被 inert。
    expect(dialog.querySelector('.nx-tour-layer .driver-popover')).not.toBeNull();

    const keydown = new KeyboardEvent('keydown', {
      key: 'Escape',
      bubbles: true,
      cancelable: true,
    });
    document.querySelector('.target')!.dispatchEvent(keydown);
    expect(keydown.defaultPrevented).toBe(true);
    expect(dialogKeys).not.toHaveBeenCalled();
    const cancel = new Event('cancel', { cancelable: true });
    dialog.dispatchEvent(cancel);
    expect(cancel.defaultPrevented).toBe(true);

    document.dispatchEvent(new KeyboardEvent('keyup', { key: 'Escape', bubbles: true }));
    expect(tour.active()).toBe(false);
    expect(dialog.open).toBe(true);
    expect(document.querySelector('.nx-tour-layer, .driver-popover')).toBeNull();
    await new Promise(requestAnimationFrame);
    expect(document.activeElement).toBe(trigger);

    // 導覽結束後，Escape 與 cancel 照常交給 dialog。
    const after = new Event('cancel', { cancelable: true });
    dialog.dispatchEvent(after);
    expect(after.defaultPrevented).toBe(false);
  });

  it('stops when its dialog closes', async () => {
    document.body.innerHTML = `<dialog open><div class="target"></div></dialog>`;
    const dialog = document.querySelector('dialog')!;
    await tour.start({ steps: [step('.target', '目標')] });
    expect(tour.active()).toBe(true);
    dialog.dispatchEvent(new Event('close'));
    expect(tour.active()).toBe(false);
    expect(document.querySelector('.nx-tour-layer, .driver-overlay')).toBeNull();
  });

  it('stops only the tour it was asked to stop', async () => {
    document.body.innerHTML = `<div class="target"></div>`;
    const definition = { steps: [step('.target', '目標')] };
    await tour.start(definition);
    tour.stop({ steps: [] });
    expect(tour.active()).toBe(true);
    tour.stop(definition);
    expect(tour.active()).toBe(false);
  });
});
