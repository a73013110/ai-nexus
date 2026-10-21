import { ApplicationRef } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { ThemeService } from '@core/preferences/theme-service';
import { ProductTour, tourSteps, type ProductTourDefinition } from './product-tour';
// 先靜態載入導覽層：它經 Icon 帶進 @lucide/angular，測試環境載入要近 10 秒，滿載時會超過 hook 的逾時。
// 模組載入不計逾時；正式程式的 import() 照樣執行，只是取到已載入的模組。
import './product-tour-overlay';

const step = (target: string, title: string) => ({ target, title, description: title + '。' });

describe('ProductTour', () => {
  let tour: ProductTour;
  // 導覽層在渲染後才進入 top layer 並定位。
  const rendered = () => TestBed.inject(ApplicationRef).whenStable();
  const start = async (definition: ProductTourDefinition) => {
    await tour.start(definition);
    await rendered();
  };
  const card = () => document.querySelector<HTMLElement>('.product-tour')!;
  const text = (selector: string) => card().querySelector(selector)?.textContent?.trim();
  const button = (name: string) =>
    [...card().querySelectorAll('button')].find(
      (item) => item.textContent?.trim() === name || item.getAttribute('aria-label') === name,
    )!;
  const press = (key: string, target: EventTarget = document.body, shiftKey = false) => {
    const event = new KeyboardEvent('keydown', { key, shiftKey, bubbles: true, cancelable: true });
    target.dispatchEvent(event);
    return event;
  };
  beforeEach(() => {
    // jsdom 沒有版面與 top layer：以 hidden 屬性代表不可見，popover 只記錄狀態；
    // popover 內的元素在 jsdom 無法取得焦點，說明框內的焦點由 e2e 驗證。
    HTMLElement.prototype.checkVisibility = function (this: HTMLElement) {
      return !this.closest('[hidden]');
    };
    HTMLElement.prototype.showPopover = vi.fn();
    HTMLElement.prototype.hidePopover = vi.fn();
    HTMLElement.prototype.scrollIntoView = vi.fn();
    TestBed.configureTestingModule({
      // 減少動態效果時退場不等動畫，結束即移除導覽層。
      providers: [{ provide: ThemeService, useValue: { reducedMotion: () => true } }],
    });
    tour = TestBed.inject(ProductTour);
  });
  afterEach(() => {
    tour.stop();
    document.body.replaceChildren();
    vi.restoreAllMocks();
    delete (HTMLElement.prototype as Partial<HTMLElement>).checkVisibility;
    delete (HTMLElement.prototype as Partial<HTMLElement>).showPopover;
    delete (HTMLElement.prototype as Partial<HTMLElement>).hidePopover;
    delete (HTMLElement.prototype as Partial<HTMLElement>).scrollIntoView;
  });

  it('drops falsy steps from a definition', () => {
    const edit = false;
    const definition: ProductTourDefinition = {
      steps: [step('.a', '甲'), edit && step('.b', '乙'), null, undefined, step('.c', '丙')],
    };
    expect(tourSteps(definition).map((item) => item.title)).toEqual(['甲', '丙']);
  });

  it('skips hidden steps and adds the replay hint to the last shown one', async () => {
    document.body.innerHTML = `<div class="a"></div><div class="b" hidden></div><div class="c"></div>`;
    await start({
      steps: [step('.a', '甲'), false, step('.b', '乙'), step('.c', '丙'), step('.missing', '丁')],
    });
    expect(tour.active()).toBe(true);
    expect(card().getAttribute('role')).toBe('dialog');
    expect(text('.tour-progress')).toBe('1 / 2');
    expect(text('.tour-description')).toBe('甲。');
    expect(button('上一步').disabled).toBe(true);
    // 目標每步都捲到可見處。
    expect(document.querySelector('.a')!.scrollIntoView).toHaveBeenCalled();
    button('下一步').click();
    await rendered();
    expect(text('.tour-title')).toBe('丙');
    expect(text('.tour-description')).toMatch(/^丙。.*重播導覽。$/);
    expect(button('上一步').disabled).toBe(false);
    button('開始使用').click();
    expect(tour.active()).toBe(false);
    expect(document.querySelector('nx-product-tour')).toBeNull();
  });

  it('walks with the arrow keys and does not finish on the last arrow', async () => {
    document.body.innerHTML = `<div class="a"></div><div class="b"></div>`;
    await start({ steps: [step('.a', '甲'), step('.b', '乙')] });
    expect(press('ArrowRight').defaultPrevented).toBe(true);
    await rendered();
    expect(text('.tour-title')).toBe('乙');
    press('ArrowRight');
    await rendered();
    expect(tour.active()).toBe(true);
    press('ArrowLeft');
    await rendered();
    expect(text('.tour-title')).toBe('甲');
    // Tab 由導覽接手，不會跑到遮罩後面的頁面。
    expect(press('Tab').defaultPrevented).toBe(true);
  });

  it('closes when the scrim is clicked but not when the card is', async () => {
    document.body.innerHTML = `<div class="a"></div>`;
    await start({ steps: [step('.a', '甲')] });
    card().querySelector('p')!.click();
    expect(tour.active()).toBe(true);
    document.querySelector<HTMLElement>('.tour-spot')!.click();
    expect(tour.active()).toBe(false);
  });

  it('runs inside an open dialog: Escape closes only the tour and focus returns to the trigger', async () => {
    document.body.innerHTML = `<dialog open><button class="trigger">導覽</button><div class="target"></div></dialog>`;
    const dialog = document.querySelector('dialog')!;
    const trigger = document.querySelector<HTMLButtonElement>('.trigger')!;
    const dialogKeys = vi.fn();
    dialog.addEventListener('keydown', dialogKeys);
    trigger.focus();
    await start({ steps: [step('.target', '目標')] });
    // 導覽層放在 dialog 內，模態時才不會被 inert。
    expect(dialog.querySelector('nx-product-tour .product-tour')).not.toBeNull();

    const escape = press('Escape', document.querySelector('.target')!);
    expect(escape.defaultPrevented).toBe(true);
    expect(dialogKeys).not.toHaveBeenCalled();
    expect(tour.active()).toBe(false);
    expect(dialog.open).toBe(true);
    expect(document.querySelector('nx-product-tour')).toBeNull();
    await new Promise(requestAnimationFrame);
    expect(document.activeElement).toBe(trigger);

    // 導覽結束後，cancel 照常交給 dialog。
    const after = new Event('cancel', { cancelable: true });
    dialog.dispatchEvent(after);
    expect(after.defaultPrevented).toBe(false);
  });

  it('turns a close request on its dialog into ending the tour', async () => {
    document.body.innerHTML = `<dialog open><div class="target"></div></dialog>`;
    const dialog = document.querySelector('dialog')!;
    await start({ steps: [step('.target', '目標')] });
    const cancel = new Event('cancel', { cancelable: true });
    dialog.dispatchEvent(cancel);
    expect(cancel.defaultPrevented).toBe(true);
    expect(tour.active()).toBe(false);
  });

  it('stops when its dialog closes', async () => {
    document.body.innerHTML = `<dialog open><div class="target"></div></dialog>`;
    const dialog = document.querySelector('dialog')!;
    await start({ steps: [step('.target', '目標')] });
    expect(tour.active()).toBe(true);
    dialog.dispatchEvent(new Event('close'));
    expect(tour.active()).toBe(false);
    expect(document.querySelector('nx-product-tour')).toBeNull();
  });

  it('stops only the tour it was asked to stop', async () => {
    document.body.innerHTML = `<div class="target"></div>`;
    const definition = { steps: [step('.target', '目標')] };
    await start(definition);
    tour.stop({ steps: [] });
    expect(tour.active()).toBe(true);
    tour.stop(definition);
    expect(tour.active()).toBe(false);
  });
});
