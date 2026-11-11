import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { recentActivation } from './activation';

describe('recentActivation', () => {
  beforeEach(() => vi.useFakeTimers());
  afterEach(() => {
    vi.useRealTimers();
    document.body.replaceChildren();
  });

  function button() {
    const element = document.createElement('button');
    element.innerHTML = '<span>開啟</span>';
    document.body.append(element);
    return element;
  }

  it('remembers the clicked control, not the icon or text inside it', () => {
    const control = button();
    control.querySelector('span')!.click();
    expect(recentActivation()).toBe(control);
  });

  it('forgets after the window, on other input, or when the control is gone', () => {
    const control = button();
    control.click();
    vi.advanceTimersByTime(2001);
    expect(recentActivation()).toBeNull();

    control.click();
    document.body.dispatchEvent(new KeyboardEvent('keydown', { key: 'k', bubbles: true }));
    expect(recentActivation()).toBeNull();

    control.click();
    document.body.dispatchEvent(new Event('pointerdown', { bubbles: true }));
    expect(recentActivation()).toBeNull();

    control.click();
    control.remove();
    expect(recentActivation()).toBeNull();
  });
});
