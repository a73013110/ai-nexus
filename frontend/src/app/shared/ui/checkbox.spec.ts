import { Component, signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { afterEach, describe, expect, it } from 'vitest';
import { Checkbox } from './checkbox';

@Component({
  selector: 'nx-checkbox-host',
  imports: [Checkbox],
  template: `<nx-checkbox
    label="對話"
    description="chat"
    [checked]="checked()"
    [disabled]="disabled()"
    (checkedChange)="checked.set($event)"
  />`,
})
class Host {
  readonly checked = signal(false);
  readonly disabled = signal(false);
}

function setup() {
  const fixture = TestBed.createComponent(Host);
  fixture.detectChanges();
  const element = fixture.nativeElement as HTMLElement;
  return {
    fixture,
    host: fixture.componentInstance,
    row: element.querySelector<HTMLLabelElement>('.selection-control')!,
    input: element.querySelector<HTMLInputElement>('input[type="checkbox"]')!,
  };
}

describe('Checkbox', () => {
  afterEach(() => TestBed.resetTestingModule());

  it('toggles from anywhere on the row and names the native input', () => {
    const { fixture, host, row, input } = setup();
    expect(input.getAttribute('aria-label')).toBe('對話');
    expect(document.getElementById(input.getAttribute('aria-describedby')!)?.textContent).toBe(
      'chat',
    );
    row.querySelector('strong')!.click();
    fixture.detectChanges();
    expect(host.checked()).toBe(true);
    expect(input.checked).toBe(true);
  });

  it('shows the checked state only on the box, so a list keeps one row background', () => {
    const { fixture, host, row } = setup();
    const before = row.className;
    host.checked.set(true);
    fixture.detectChanges();
    expect(row.className).toBe(before);
  });

  it('marks a disabled row and ignores clicks', () => {
    const { fixture, host, row, input } = setup();
    host.disabled.set(true);
    fixture.detectChanges();
    expect(row.classList).toContain('is-disabled');
    expect(input.disabled).toBe(true);
    row.click();
    fixture.detectChanges();
    expect(host.checked()).toBe(false);
  });
});
