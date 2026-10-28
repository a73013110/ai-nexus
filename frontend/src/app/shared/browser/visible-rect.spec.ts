import { afterEach, describe, expect, it, vi } from 'vitest';
import { clipBounds, visibleRect } from './visible-rect';

function box(element: HTMLElement, left: number, top: number, width: number, height: number) {
  vi.spyOn(element, 'getBoundingClientRect').mockReturnValue(new DOMRect(left, top, width, height));
  Object.defineProperty(element, 'clientWidth', { value: width, configurable: true });
  Object.defineProperty(element, 'clientHeight', { value: height, configurable: true });
}

describe('visibleRect', () => {
  afterEach(() => {
    document.body.replaceChildren();
    vi.restoreAllMocks();
  });

  function scrolled() {
    const body = document.createElement('div'),
      section = document.createElement('section');
    body.style.overflowY = 'auto';
    body.append(section);
    document.body.append(body);
    // 對話框內容 100–500，段落比它高、上緣已捲出 40px。
    box(body, 20, 100, 600, 400);
    box(section, 40, 60, 560, 900);
    return { body, section };
  }

  it('keeps only the part of a target its scrolling container shows', () => {
    const { section } = scrolled();
    expect(clipBounds(section)).toEqual({ left: 0, top: 100, right: innerWidth, bottom: 500 });
    expect(visibleRect(section)).toEqual({ left: 40, top: 100, right: 600, bottom: 500 });
  });

  it('pads outward only where the container leaves room', () => {
    const { section } = scrolled();
    // 只裁切縱軸：左右可外擴，上下停在容器邊緣。
    expect(visibleRect(section, 6)).toEqual({ left: 34, top: 100, right: 606, bottom: 500 });
  });

  it('reports nothing for a target scrolled out of view', () => {
    const { section } = scrolled();
    box(section, 40, 520, 560, 100);
    expect(visibleRect(section, 6)).toBeNull();
  });
});
