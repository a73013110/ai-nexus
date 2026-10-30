import { afterEach, describe, expect, it } from 'vitest';
import { isAbove, ownsEscape } from './top-layer';

/** jsdom 沒有 popover 與 dialog 的方法：自己發 beforetoggle、以 open 屬性代表開著。 */
function open(layer: HTMLElement) {
  layer.setAttribute('open', '');
  document.body.append(layer);
  layer.dispatchEvent(Object.assign(new Event('beforetoggle'), { newState: 'open' }));
  return layer;
}

function escape(target: Element) {
  const event = new KeyboardEvent('keydown', { key: 'Escape', bubbles: true, cancelable: true });
  target.dispatchEvent(event);
  return event;
}

describe('top layer', () => {
  afterEach(() => document.body.replaceChildren());

  it('orders layers by when they last entered, so a re-shown layer goes back on top', () => {
    const drawer = open(document.createElement('dialog'));
    const table = open(document.createElement('div'));
    expect(isAbove(table, drawer)).toBe(true);
    open(drawer);
    expect(isAbove(drawer, table)).toBe(true);
    // 從沒進過 top layer 的頁面元素在所有圖層下面。
    expect(isAbove(document.body, drawer)).toBe(false);
  });

  it('gives Escape to the layer holding focus, and to a non-modal layer from the page', () => {
    const drawer = open(document.createElement('dialog'));
    const inside = drawer.appendChild(document.createElement('button'));
    const page = document.body.appendChild(document.createElement('button'));
    const other = open(document.createElement('dialog'));
    const elsewhere = other.appendChild(document.createElement('button'));
    let event = escape(inside);
    expect(ownsEscape(drawer, event)).toBe(true);
    expect(ownsEscape(drawer, escape(page))).toBe(true);
    expect(ownsEscape(drawer, escape(elsewhere))).toBe(false);
    event = escape(inside);
    event.preventDefault();
    expect(ownsEscape(drawer, event)).toBe(false);
  });
});
