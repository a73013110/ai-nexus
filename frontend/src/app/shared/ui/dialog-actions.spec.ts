import { Component, signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { afterEach, describe, expect, it } from 'vitest';
import { DialogActions } from './dialog-actions';

@Component({
  selector: 'nx-dialog-actions-host',
  imports: [DialogActions],
  template: `<form (submit)="$event.preventDefault(); submits = submits + 1">
    <nx-dialog-actions
      label="建立單位"
      pendingLabel="建立中…"
      [pending]="saving()"
      [disabled]="incomplete()"
      [submit]="submit()"
      (confirm)="confirms = confirms + 1"
      (dismiss)="dismissals = dismissals + 1"
    />
  </form>`,
})
class Host {
  readonly saving = signal(false);
  readonly incomplete = signal(false);
  readonly submit = signal(true);
  submits = 0;
  confirms = 0;
  dismissals = 0;
}

function setup() {
  const fixture = TestBed.createComponent(Host);
  fixture.detectChanges();
  const [cancel, primary] = Array.from(
    (fixture.nativeElement as HTMLElement).querySelectorAll('button'),
  );
  return { fixture, host: fixture.componentInstance, cancel, primary };
}

describe('DialogActions', () => {
  afterEach(() => TestBed.resetTestingModule());

  it('submits the surrounding form and dismisses on cancel', () => {
    const { host, cancel, primary } = setup();
    expect(primary.textContent?.trim()).toBe('建立單位');
    primary.click();
    cancel.click();
    expect(host).toMatchObject({ submits: 1, confirms: 0, dismissals: 1 });
  });

  it('blocks both buttons while pending without disabling them', () => {
    const { fixture, host, cancel, primary } = setup();
    host.saving.set(true);
    host.incomplete.set(true);
    fixture.detectChanges();
    expect(primary.textContent?.trim()).toBe('建立中…');
    expect(primary.disabled).toBe(false);
    expect(cancel.getAttribute('aria-disabled')).toBe('true');
    primary.click();
    cancel.click();
    expect(host).toMatchObject({ submits: 0, dismissals: 0 });
  });

  it('disables the primary action until the form can be sent', () => {
    const { fixture, host, cancel, primary } = setup();
    host.incomplete.set(true);
    fixture.detectChanges();
    expect(primary.disabled).toBe(true);
    cancel.click();
    expect(host.dismissals).toBe(1);
  });

  it('emits confirm instead of submitting when submit is off', () => {
    const { fixture, host, primary } = setup();
    host.submit.set(false);
    fixture.detectChanges();
    expect(primary.getAttribute('type')).toBe('button');
    primary.click();
    expect(host).toMatchObject({ submits: 0, confirms: 1 });
  });
});
