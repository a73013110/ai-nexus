import { Component, signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { afterEach, describe, expect, it } from 'vitest';
import { CompactDialog } from './compact-dialog';

@Component({
  selector: 'nx-compact-dialog-host',
  imports: [CompactDialog],
  template: `<dialog nxCompactDialog aria-label="範例" [busy]="busy()"></dialog>`,
})
class Host {
  readonly busy = signal(false);
}

describe('CompactDialog', () => {
  afterEach(() => TestBed.resetTestingModule());

  it('holds Esc and the cancel request only while busy', () => {
    const fixture = TestBed.createComponent(Host);
    fixture.detectChanges();
    const dialog = (fixture.nativeElement as HTMLElement).querySelector('dialog')!;
    const send = () => {
      const escape = new KeyboardEvent('keydown', { key: 'Escape', cancelable: true });
      const cancel = new Event('cancel', { cancelable: true });
      dialog.dispatchEvent(escape);
      dialog.dispatchEvent(cancel);
      return [escape.defaultPrevented, cancel.defaultPrevented];
    };
    expect(dialog.getAttribute('aria-busy')).toBeNull();
    expect(send()).toEqual([false, false]);

    fixture.componentInstance.busy.set(true);
    fixture.detectChanges();
    expect(dialog.getAttribute('aria-busy')).toBe('true');
    expect(send()).toEqual([true, true]);
  });
});
