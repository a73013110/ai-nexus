import { Component, ElementRef, signal, viewChild } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { ThemeService } from '../../core/preferences/theme-service';
import { followScroll } from './follow-scroll';

@Component({
  selector: 'nx-follow-scroll-host',
  template: '<div #viewport></div>',
})
class Host {
  readonly start = signal(true);
  readonly viewport = viewChild.required<ElementRef<HTMLElement>>('viewport');
  readonly scroll = followScroll({
    viewport: () => this.viewport().nativeElement,
    initial: () => this.start(),
  });
}

describe('followScroll', () => {
  let frames: FrameRequestCallback[];
  const behavior = signal<ScrollBehavior>('smooth');
  const flush = () => frames.splice(0).forEach((frame) => frame(0));
  beforeEach(() => {
    frames = [];
    behavior.set('smooth');
    TestBed.configureTestingModule({
      providers: [{ provide: ThemeService, useValue: { scrollBehavior: behavior } }],
    });
  });
  afterEach(() => {
    vi.restoreAllMocks();
    TestBed.resetTestingModule();
  });
  /** A viewport 100px tall over 1000px of content, starting at the bottom. */
  const setup = () => {
    const fixture = TestBed.createComponent(Host);
    fixture.detectChanges();
    const host = fixture.componentInstance;
    const view = host.viewport().nativeElement;
    Object.defineProperty(view, 'scrollHeight', { configurable: true, value: 1000 });
    Object.defineProperty(view, 'clientHeight', { configurable: true, value: 100 });
    Object.defineProperty(view, 'scrollTop', { configurable: true, writable: true, value: 900 });
    view.scrollTo = vi.fn();
    // Mocked after the first render, so only this helper's frames are queued.
    vi.spyOn(window, 'requestAnimationFrame').mockImplementation((frame) => frames.push(frame));
    vi.spyOn(window, 'cancelAnimationFrame').mockImplementation((id) => {
      if (id) frames.splice(id - 1, 1, () => undefined);
    });
    return { fixture, host, view };
  };
  const scrollTo = (host: Host, view: HTMLElement, top: number) => {
    view.scrollTop = top;
    host.scroll.scrolled();
  };

  it('scrolls to the bottom once per frame while following', () => {
    const { host, view } = setup();
    host.scroll.schedule();
    host.scroll.schedule();
    expect(frames).toHaveLength(1);
    flush();
    expect(view.scrollTo).toHaveBeenCalledExactlyOnceWith({ top: 1000, behavior: 'instant' });
  });

  it('pauses when the reader scrolls up and resumes at the bottom', () => {
    const { host, view } = setup();
    scrollTo(host, view, 500);
    expect(host.scroll.following()).toBe(false);
    host.scroll.schedule();
    expect(frames).toHaveLength(0);
    scrollTo(host, view, 850);
    expect(host.scroll.following()).toBe(true);
  });

  it('does not resume on the first event of a jump upwards that starts near the bottom', () => {
    const { host, view } = setup();
    host.scroll.pause();
    scrollTo(host, view, 880);
    expect(host.scroll.following()).toBe(false);
  });

  it('cancels a pending frame when following stops', () => {
    const { host, view } = setup();
    host.scroll.schedule();
    host.scroll.pause();
    flush();
    expect(view.scrollTo).not.toHaveBeenCalled();
  });

  it('jumps to the latest with the theme scroll behaviour', () => {
    const { host, view } = setup();
    host.scroll.pause();
    host.scroll.toLatest();
    expect(host.scroll.following()).toBe(true);
    expect(view.scrollTo).toHaveBeenLastCalledWith({ top: 1000, behavior: 'smooth' });
    behavior.set('instant');
    host.scroll.toLatest();
    expect(view.scrollTo).toHaveBeenLastCalledWith({ top: 1000, behavior: 'instant' });
  });

  it('starts over from `initial` when its inputs change', () => {
    const { host, view } = setup();
    scrollTo(host, view, 0);
    expect(host.scroll.following()).toBe(false);
    host.start.set(false);
    host.start.set(true);
    expect(host.scroll.following()).toBe(true);
  });
});
