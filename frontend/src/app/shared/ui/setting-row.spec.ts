import { Component, signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { afterEach, describe, expect, it } from 'vitest';
import { SettingRow } from './setting-row';

@Component({
  selector: 'nx-setting-row-host',
  imports: [SettingRow],
  template: `<nx-setting-row
    label="寄送摘要"
    [checked]="on()"
    [pending]="pending()"
    (checkedChange)="on.set($event)"
  />`,
})
class Host {
  readonly on = signal(false);
  readonly pending = signal(false);
}

describe('SettingRow', () => {
  afterEach(() => TestBed.resetTestingModule());

  it('spins beside a switch that is saving and ignores presses but keeps the focus', () => {
    const fixture = TestBed.createComponent(Host);
    fixture.detectChanges();
    const host = fixture.componentInstance;
    const element = fixture.nativeElement as HTMLElement;
    const toggle = element.querySelector<HTMLButtonElement>('[role="switch"]')!;
    host.pending.set(true);
    fixture.detectChanges();
    expect(toggle.getAttribute('aria-busy')).toBe('true');
    expect(toggle.getAttribute('aria-disabled')).toBe('true');
    expect(toggle.disabled).toBe(false);
    expect(element.querySelector('.setting-pending')?.classList).toContain('is-spinning');
    toggle.click();
    expect(host.on()).toBe(false);

    host.pending.set(false);
    fixture.detectChanges();
    expect(element.querySelector('.setting-pending')).toBeNull();
    toggle.click();
    expect(host.on()).toBe(true);
  });

  it('hands the option loading to its select so the row keeps its place', () => {
    const fixture = TestBed.createComponent(SettingRow);
    fixture.componentRef.setInput('label', '預設模型');
    fixture.componentRef.setInput('options', []);
    fixture.componentRef.setInput('value', 'model-a');
    fixture.componentRef.setInput('loading', true);
    fixture.detectChanges();
    const trigger = (fixture.nativeElement as HTMLElement).querySelector('.select-trigger')!;
    expect(trigger.getAttribute('aria-busy')).toBe('true');
    expect(trigger.textContent).toContain('正在載入…');
  });
});
