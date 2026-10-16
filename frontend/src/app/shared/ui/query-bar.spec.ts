import { ChangeDetectionStrategy, Component, signal } from '@angular/core';
import { TestBed, type ComponentFixture } from '@angular/core/testing';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { QueryBar } from './query-bar';
import { QueryState } from './query-state';

interface Sample {
  text: string;
  tag: string;
}

@Component({
  imports: [QueryBar],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `<nx-query-bar [state]="state" [busy]="busy()" [more]="true">
      <input aria-label="文字" />
      <input query-more aria-label="標籤" />
    </nx-query-bar>
    <button type="button" id="outside">外面</button>`,
})
class Host {
  readonly busy = signal(false);
  readonly state = new QueryState<Sample>({
    defaults: () => ({ text: '', tag: '' }),
    chips: (value) => [
      ...(value.text ? [{ id: 'text', label: '文字：' + value.text, clear: { text: '' } }] : []),
      ...(value.tag ? [{ id: 'tag', label: '標籤：' + value.tag, clear: { tag: '' } }] : []),
    ],
  });
}

const frame = () => new Promise((resolve) => requestAnimationFrame(resolve));

describe('QueryBar', () => {
  let fixture: ComponentFixture<Host>;
  let host: HTMLElement;
  const button = (name: string) =>
    [...host.querySelectorAll<HTMLButtonElement>('button')].find(
      (item) => item.getAttribute('aria-label') === name || item.textContent?.trim() === name,
    )!;

  beforeEach(() => {
    HTMLElement.prototype.showPopover = vi.fn();
    HTMLElement.prototype.hidePopover = vi.fn();
    fixture = TestBed.createComponent(Host);
    fixture.detectChanges();
    host = fixture.nativeElement as HTMLElement;
  });
  afterEach(() => {
    TestBed.resetTestingModule();
    delete (HTMLElement.prototype as Partial<HTMLElement>).showPopover;
    delete (HTMLElement.prototype as Partial<HTMLElement>).hidePopover;
  });

  it('keeps focus on the chips when removing one starts a read', async () => {
    const state = fixture.componentInstance.state;
    state.set('text', 'a');
    state.set('tag', 'b');
    state.apply();
    fixture.detectChanges();
    const first = button('移除條件：文字：a');
    first.focus();
    first.click();
    // 頁面套用條件後立刻開始讀取。
    fixture.componentInstance.busy.set(true);
    fixture.detectChanges();
    await frame();
    const next = button('移除條件：標籤：b');
    expect(document.activeElement).toBe(next);
    expect(next.disabled).toBe(false);
    expect(next.getAttribute('aria-disabled')).toBe('true');
    // 讀取中不能再移除。
    next.click();
    expect(state.applied()).toEqual({ text: '', tag: 'b' });

    fixture.componentInstance.busy.set(false);
    fixture.detectChanges();
    next.click();
    fixture.detectChanges();
    await frame();
    expect(state.chips()).toEqual([]);
    expect(document.activeElement).toBe(button('查詢'));
  });

  it('ignores 查詢 and 重設 while busy but keeps them focusable', () => {
    const state = fixture.componentInstance.state;
    fixture.componentInstance.busy.set(true);
    fixture.detectChanges();
    state.set('text', 'x');
    const submit = button('查詢');
    submit.focus();
    submit.click();
    button('重設').click();
    expect(state.applied().text).toBe('');
    expect(state.draft().text).toBe('x');
    expect(submit.disabled).toBe(false);
    expect(submit.getAttribute('aria-disabled')).toBe('true');
    expect(document.activeElement).toBe(submit);

    fixture.componentInstance.busy.set(false);
    fixture.detectChanges();
    expect(submit.hasAttribute('aria-disabled')).toBe(false);
    submit.click();
    expect(state.applied().text).toBe('x');
  });

  it('closes the more panel when Tab leaves it, but not when focus returns to its trigger', () => {
    const trigger = host.querySelector<HTMLButtonElement>('.ui-query-more')!;
    trigger.click();
    fixture.detectChanges();
    expect(trigger.getAttribute('aria-expanded')).toBe('true');
    const panel = host.querySelector<HTMLElement>('[role="dialog"]')!;
    const input = panel.querySelector('input')!;
    input.dispatchEvent(new FocusEvent('focusout', { bubbles: true, relatedTarget: trigger }));
    fixture.detectChanges();
    expect(trigger.getAttribute('aria-expanded')).toBe('true');
    input.dispatchEvent(
      new FocusEvent('focusout', { bubbles: true, relatedTarget: host.querySelector('#outside') }),
    );
    fixture.detectChanges();
    expect(trigger.getAttribute('aria-expanded')).toBe('false');
    expect(HTMLElement.prototype.hidePopover).toHaveBeenCalled();
  });

  it('closes the more panel with Escape and returns focus to its trigger', () => {
    const trigger = host.querySelector<HTMLButtonElement>('.ui-query-more')!;
    trigger.click();
    fixture.detectChanges();
    const panel = host.querySelector<HTMLElement>('[role="dialog"]')!;
    panel.dispatchEvent(new KeyboardEvent('keydown', { key: 'Escape', bubbles: true }));
    fixture.detectChanges();
    expect(trigger.getAttribute('aria-expanded')).toBe('false');
    expect(document.activeElement).toBe(trigger);
  });
});
