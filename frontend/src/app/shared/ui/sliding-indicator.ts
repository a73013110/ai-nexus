import { afterNextRender, DestroyRef, Directive, ElementRef, inject, input } from '@angular/core';

/**
 * Measures the selected item of a group and publishes its box as `--indicator-x/-y/-w/-h` on the
 * host, so one chip or underline (the host's `::before`) slides between items of any width. It
 * watches the selection attribute and sizes itself, so it never runs on unrelated renders.
 * `data-indicator` marks that something is selected; `data-indicator-ready` turns the slide on
 * after the first placement, so the chip does not fly in from the corner.
 */
@Directive({ selector: '[nxSlidingIndicator]' })
export class SlidingIndicator {
  /** Selector for the selected item; the default covers toggle groups, tabs and nav links. */
  readonly nxSlidingIndicator = input(
    '[aria-pressed="true"], [aria-selected="true"], [aria-current="page"]',
  );

  constructor() {
    const host = inject<ElementRef<HTMLElement>>(ElementRef).nativeElement;
    if (typeof ResizeObserver === 'undefined') return;
    let frame = 0;
    const place = () => {
      frame = 0;
      const active = host.querySelector<HTMLElement>(this.nxSlidingIndicator());
      host.toggleAttribute('data-indicator', !!active?.offsetWidth);
      if (!active?.offsetWidth) return;
      host.style.setProperty('--indicator-x', `${active.offsetLeft}px`);
      host.style.setProperty('--indicator-y', `${active.offsetTop}px`);
      host.style.setProperty('--indicator-w', `${active.offsetWidth}px`);
      host.style.setProperty('--indicator-h', `${active.offsetHeight}px`);
    };
    const schedule = () => (frame ||= requestAnimationFrame(place));
    const resize = new ResizeObserver(schedule);
    const mutation = new MutationObserver((records) => {
      for (const record of records)
        if (record.type === 'childList')
          record.addedNodes.forEach((node) => node instanceof Element && resize.observe(node));
      schedule();
    });
    afterNextRender(() => {
      resize.observe(host);
      for (const child of host.children) resize.observe(child);
      mutation.observe(host, {
        subtree: true,
        childList: true,
        attributes: true,
        attributeFilter: ['aria-pressed', 'aria-selected', 'aria-current', 'class'],
      });
      place();
      requestAnimationFrame(() => host.setAttribute('data-indicator-ready', ''));
    });
    inject(DestroyRef).onDestroy(() => {
      cancelAnimationFrame(frame);
      resize.disconnect();
      mutation.disconnect();
    });
  }
}
