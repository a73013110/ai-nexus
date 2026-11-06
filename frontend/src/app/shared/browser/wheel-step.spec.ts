import { Component } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { describe, expect, it } from 'vitest';
import { WheelStep } from './wheel-step';

@Component({
  imports: [WheelStep],
  template: `<div (nxWheelStep)="steps.push($event)"></div>`,
})
class Host {
  readonly steps: number[] = [];
}

function setup() {
  const fixture = TestBed.createComponent(Host);
  fixture.detectChanges();
  const target = (fixture.nativeElement as HTMLElement).querySelector('div')!;
  const wheel = (timeStamp: number, init: WheelEventInit) => {
    const event = new WheelEvent('wheel', { cancelable: true, bubbles: true, ...init });
    Object.defineProperty(event, 'timeStamp', { value: timeStamp });
    target.dispatchEvent(event);
    return event;
  };
  return { steps: fixture.componentInstance.steps, wheel };
}

describe('WheelStep', () => {
  it('steps once per mouse notch without scrolling the page', () => {
    const { steps, wheel } = setup();
    expect(wheel(1000, { deltaY: 100 }).defaultPrevented).toBe(true);
    expect(steps).toEqual([1]);
    // 走一步後 150ms 內的捲動（觸控板的慣性）不再算。
    wheel(1050, { deltaY: 100 });
    expect(steps).toEqual([1]);
    wheel(1300, { deltaY: -100 });
    expect(steps).toEqual([1, -1]);
    // 以行為單位的滾輪（Firefox）一行也算得到。
    wheel(1600, { deltaY: 4, deltaMode: WheelEvent.DOM_DELTA_LINE });
    expect(steps).toEqual([1, -1, 1]);
  });

  it('adds up small touchpad scrolls before stepping', () => {
    const { steps, wheel } = setup();
    wheel(1000, { deltaY: -30 });
    expect(steps).toEqual([]);
    wheel(1020, { deltaY: -40 });
    expect(steps).toEqual([-1]);
  });

  it('leaves zoom and horizontal scrolling alone', () => {
    const { steps, wheel } = setup();
    expect(wheel(1000, { deltaY: 100, ctrlKey: true }).defaultPrevented).toBe(false);
    expect(wheel(1200, { deltaY: 20, deltaX: 100 }).defaultPrevented).toBe(false);
    expect(steps).toEqual([]);
  });
});
