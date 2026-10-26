import { Component, signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { beforeAll, describe, expect, it } from 'vitest';
import { ClientIssues } from '@core/errors/client-issues';
import { CompactDialog } from './compact-dialog';
import { DialogHeader } from './dialog-header';
import { IdleChrome } from './idle-chrome';
import { ProductTour } from './product-tour';

@Component({
  selector: 'nx-dialog-header-host',
  imports: [DialogHeader],
  template: `<dialog><nx-dialog-header heading="標題" detail="3 份" /></dialog>`,
})
class Host {}

@Component({
  selector: 'nx-dialog-header-projection-host',
  imports: [DialogHeader],
  template: `<dialog>
    <nx-dialog-header heading="王小明">
      <span dialog-title class="badge">啟用</span>
      <a class="tool" href="/audit">稽核</a>
    </nx-dialog-header>
  </dialog>`,
})
class ProjectionHost {}

@Component({
  selector: 'nx-dialog-header-record-host',
  imports: [DialogHeader],
  template: `<dialog>
    <nx-dialog-header heading="病假" icon="calendar">
      <p dialog-subtitle class="dialog-subtitle"><span>請假單</span><span>L115102201</span></p>
    </nx-dialog-header>
  </dialog>`,
})
class RecordHost {}

@Component({
  selector: 'nx-dialog-header-maximize-host',
  imports: [DialogHeader, CompactDialog, IdleChrome],
  template: `<dialog nxCompactDialog nxIdleChrome size="full">
      <nx-dialog-header heading="圖表" maximizable />
    </dialog>
    <dialog nxCompactDialog size="full"><nx-dialog-header heading="表單" maximizable /></dialog>`,
})
class MaximizeHost {}

@Component({
  selector: 'nx-dialog-header-tour-host',
  imports: [DialogHeader],
  template: `<dialog>
    <nx-dialog-header heading="設定" [tour]="tour()" />
  </dialog>`,
})
class TourHost {
  readonly tour = signal<{ steps: [] } | null>({ steps: [] });
}

function setup() {
  const fixture = TestBed.createComponent(Host);
  fixture.detectChanges();
  const root = fixture.nativeElement as HTMLElement;
  const dialog = root.querySelector('dialog')!;
  dialog.setAttribute('open', '');
  const button = root.querySelector<HTMLButtonElement>('button')!;
  return { dialog, button, root };
}

describe('DialogHeader', () => {
  beforeAll(() => {
    HTMLDialogElement.prototype.close ??= function (this: HTMLDialogElement) {
      this.removeAttribute('open');
    };
  });

  it('shows the heading and detail', () => {
    const { root } = setup();
    expect(root.querySelector('h2')?.textContent).toBe('標題');
    expect(root.querySelector('small')?.textContent).toBe('3 份');
  });

  it('places title content beside the heading and tools before the close button', () => {
    const fixture = TestBed.createComponent(ProjectionHost);
    fixture.detectChanges();
    const root = fixture.nativeElement as HTMLElement;
    const title = root.querySelector('.dialog-title')!;
    expect(title.querySelector('h2 + .badge')?.textContent).toBe('啟用');
    expect(root.querySelector('h2')?.textContent).toBe('王小明');
    const bar = root.querySelector('nx-dialog-header')!;
    expect([...bar.children].map((item) => item.className)).toEqual([
      'dialog-heading',
      'tool',
      'icon-button',
    ]);
    expect(bar.classList).not.toContain('is-record');
  });

  it('shows a record icon and a subtitle line under the heading', () => {
    const fixture = TestBed.createComponent(RecordHost);
    fixture.detectChanges();
    const bar = (fixture.nativeElement as HTMLElement).querySelector('nx-dialog-header')!;
    expect(bar.classList).toContain('is-record');
    expect(bar.firstElementChild?.className).toBe('dialog-icon');
    expect(bar.querySelector('.dialog-icon')?.getAttribute('aria-hidden')).toBe('true');
    const heading = bar.querySelector('.dialog-heading')!;
    expect([...heading.children].map((item) => item.className)).toEqual([
      'dialog-title',
      'dialog-subtitle',
    ]);
    expect(bar.querySelector('h2')?.textContent).toBe('病假');
  });

  it('shows the compact tour trigger before the close button only when a tour is given', () => {
    TestBed.configureTestingModule({
      providers: [
        { provide: ProductTour, useValue: { loading: signal(false), active: signal(false) } },
        { provide: ClientIssues, useValue: {} },
      ],
    });
    const fixture = TestBed.createComponent(TourHost);
    fixture.detectChanges();
    const bar = (fixture.nativeElement as HTMLElement).querySelector('nx-dialog-header')!;
    expect([...bar.children].map((item) => item.localName)).toEqual([
      'div',
      'nx-product-tour-button',
      'button',
    ]);
    expect(bar.querySelector('nx-product-tour-button button')?.className).toContain('icon-button');
    fixture.componentInstance.tour.set(null);
    fixture.detectChanges();
    expect(bar.querySelector('nx-product-tour-button')).toBeNull();
  });

  it('closes the dialog through a cancelable cancel event', () => {
    const { dialog, button } = setup();
    let cancelled = 0;
    dialog.addEventListener('cancel', () => cancelled++);
    button.click();
    expect(cancelled).toBe(1);
    expect(dialog.open).toBe(false);
  });

  it('keeps the dialog open when cancel is prevented', () => {
    const { dialog, button } = setup();
    dialog.addEventListener('cancel', (event) => event.preventDefault());
    button.click();
    expect(dialog.open).toBe(true);
  });

  it('toggles the dialog to maximized; only inside a viewer scope does the bar become idle chrome', () => {
    const fixture = TestBed.createComponent(MaximizeHost);
    fixture.detectChanges();
    const [viewer, form] = [...(fixture.nativeElement as HTMLElement).querySelectorAll('dialog')];
    const toggle = (dialog: HTMLDialogElement) => {
      dialog.querySelector<HTMLButtonElement>('button[aria-label="放大視窗"]')!.click();
      fixture.detectChanges();
    };
    expect(viewer.hasAttribute('data-maximized')).toBe(false);
    expect(viewer.querySelector('nx-dialog-header')!.hasAttribute('data-chrome')).toBe(false);
    toggle(viewer);
    expect(viewer.hasAttribute('data-maximized')).toBe(true);
    expect(viewer.querySelector('nx-dialog-header')!.getAttribute('data-chrome')).toBe('shown');
    expect(viewer.querySelector('button[aria-label="還原視窗"]')).not.toBeNull();
    toggle(form);
    expect(form.hasAttribute('data-maximized')).toBe(true);
    expect(form.querySelector('nx-dialog-header')!.hasAttribute('data-chrome')).toBe(false);
  });
});
