import { Component } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { beforeAll, describe, expect, it } from 'vitest';
import { DialogHeader } from './dialog-header';

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
    expect([...bar.children].map((x) => x.className)).toEqual([
      'dialog-title',
      'tool',
      'icon-button',
    ]);
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
});
