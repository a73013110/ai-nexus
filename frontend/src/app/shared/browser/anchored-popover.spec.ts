import { Component, ElementRef, viewChild } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { anchoredPopover } from './anchored-popover';

@Component({
  selector: 'nx-anchored-popover-host',
  template: '<button type="button">外部</button><div #panel popover="manual"><input /></div>',
})
class Host {
  readonly panel = viewChild.required<ElementRef<HTMLElement>>('panel');
  readonly place = vi.fn();
  readonly dismiss = vi.fn();
  readonly popover = anchoredPopover({
    panel: () => this.panel().nativeElement,
    place: this.place,
    dismiss: this.dismiss,
  });
}

const toggle = (newState: 'open' | 'closed') => Object.assign(new Event('toggle'), { newState });

describe('anchoredPopover', () => {
  let frames: FrameRequestCallback[];
  const flush = () => frames.splice(0).forEach((frame) => frame(0));
  beforeEach(() => {
    frames = [];
    vi.spyOn(window, 'requestAnimationFrame').mockImplementation((frame) => frames.push(frame));
  });
  afterEach(() => {
    vi.restoreAllMocks();
    TestBed.resetTestingModule();
  });
  const setup = () => {
    const fixture = TestBed.createComponent(Host);
    fixture.detectChanges();
    document.body.append(fixture.nativeElement);
    const host = fixture.componentInstance;
    const panel = host.panel().nativeElement;
    panel.showPopover = vi.fn();
    panel.hidePopover = vi.fn();
    return { fixture, host, panel };
  };

  it('shows, places and hides the panel, and follows a native close', () => {
    const { host, panel } = setup();
    host.popover.show();
    expect(panel.showPopover).toHaveBeenCalled();
    expect(host.place).toHaveBeenCalledWith(panel);
    expect(host.popover.opened()).toBe(true);
    host.popover.sync(toggle('closed'));
    expect(host.popover.opened()).toBe(false);
    host.popover.sync(toggle('open'));
    expect(host.popover.opened()).toBe(true);
    expect(host.place).toHaveBeenCalledTimes(2);
    host.popover.hide();
    expect(panel.hidePopover).toHaveBeenCalled();
    expect(host.popover.opened()).toBe(false);
  });

  it('places again once per frame on scroll anywhere and resize, only while open', () => {
    const { fixture, host } = setup();
    host.popover.show();
    host.place.mockClear();
    fixture.nativeElement.querySelector('input').dispatchEvent(new Event('scroll'));
    window.dispatchEvent(new Event('resize'));
    flush();
    expect(host.place).toHaveBeenCalledTimes(1);
    host.popover.hide();
    window.dispatchEvent(new Event('scroll'));
    flush();
    expect(host.place).toHaveBeenCalledTimes(1);
  });

  it('stops listening when its view is destroyed', () => {
    const { fixture, host } = setup();
    host.popover.show();
    host.place.mockClear();
    fixture.destroy();
    window.dispatchEvent(new Event('resize'));
    document.body.dispatchEvent(new Event('pointerdown', { bubbles: true }));
    flush();
    expect(host.place).not.toHaveBeenCalled();
    expect(host.dismiss).not.toHaveBeenCalled();
  });

  it('light-dismisses on a pointerdown outside or Escape', () => {
    const { fixture, host, panel } = setup();
    document.body.dispatchEvent(new Event('pointerdown', { bubbles: true }));
    expect(host.dismiss).not.toHaveBeenCalled();
    host.popover.show();
    panel.querySelector('input')!.dispatchEvent(new Event('pointerdown', { bubbles: true }));
    expect(host.dismiss).not.toHaveBeenCalled();
    fixture.nativeElement
      .querySelector('button')
      .dispatchEvent(new Event('pointerdown', { bubbles: true }));
    expect(host.dismiss).toHaveBeenCalledTimes(1);
    const escape = new KeyboardEvent('keydown', { key: 'Escape', bubbles: true, cancelable: true });
    document.dispatchEvent(escape);
    expect(host.dismiss).toHaveBeenCalledTimes(2);
    expect(escape.defaultPrevented).toBe(true);
  });
});

@Component({
  selector: 'nx-anchored-popover-label-host',
  template: `<label
      >欄位<button #trigger type="button">觸發</button>
      <div #panel popover="auto">
        <p>說明文字</p>
        <label><input type="checkbox" />內層選項</label>
      </div></label
    ><button id="outside" type="button">外部</button>`,
})
class LabelHost {
  readonly trigger = viewChild.required<ElementRef<HTMLButtonElement>>('trigger');
  readonly panel = viewChild.required<ElementRef<HTMLElement>>('panel');
  readonly popover = anchoredPopover({
    panel: () => this.panel().nativeElement,
    trigger: () => this.trigger().nativeElement,
    closeOnFocusOut: true,
    place: () => undefined,
  });
}

describe('anchoredPopover inside a label', () => {
  afterEach(() => TestBed.resetTestingModule());
  const setup = () => {
    const fixture = TestBed.createComponent(LabelHost);
    fixture.detectChanges();
    const host = fixture.componentInstance;
    const panel = host.panel().nativeElement;
    panel.showPopover = vi.fn();
    panel.hidePopover = vi.fn();
    host.popover.show();
    return { fixture, host, panel };
  };
  const click = (target: Element) => {
    const event = new MouseEvent('click', { bubbles: true, cancelable: true });
    target.dispatchEvent(event);
    return event;
  };

  it('uses the trigger as the source and keeps clicks on the panel focus inside it', () => {
    const { host, panel } = setup();
    expect(panel.showPopover).toHaveBeenCalledWith({ source: host.trigger().nativeElement });
    expect(panel.tabIndex).toBe(-1);
  });

  it('stops the wrapping label from clicking the trigger, but not an inner label', () => {
    const { panel } = setup();
    expect(click(panel.querySelector('p')!).defaultPrevented).toBe(true);
    expect(click(panel.querySelector('label')!).defaultPrevented).toBe(false);
    expect(click(panel.querySelector('input')!).defaultPrevented).toBe(false);
  });

  it('closes when focus leaves the panel and its trigger', () => {
    const { fixture, host, panel } = setup();
    const input = panel.querySelector('input')!;
    const leave = (relatedTarget: EventTarget | null) =>
      input.dispatchEvent(new FocusEvent('focusout', { bubbles: true, relatedTarget }));
    leave(panel);
    leave(host.trigger().nativeElement);
    leave(null);
    expect(host.popover.opened()).toBe(true);
    leave(fixture.nativeElement.querySelector('#outside'));
    expect(host.popover.opened()).toBe(false);
    expect(panel.hidePopover).toHaveBeenCalled();
  });
});
