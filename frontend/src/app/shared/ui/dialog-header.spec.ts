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
