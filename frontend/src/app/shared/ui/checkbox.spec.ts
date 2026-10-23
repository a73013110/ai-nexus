import { Component, signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { By } from '@angular/platform-browser';
import { afterEach, describe, expect, it } from 'vitest';
import { Checkbox } from './checkbox';
import { Icon } from './icon';

@Component({
  selector: 'nx-checkbox-host',
  imports: [Checkbox],
  template: `<nx-checkbox
    label="對話"
    description="chat"
    [checked]="checked()"
    [disabled]="disabled()"
    [pending]="pending()"
    (checkedChange)="checked.set($event)"
  />`,
})
class Host {
  readonly checked = signal(false);
  readonly disabled = signal(false);
  readonly pending = signal(false);
}

@Component({
  selector: 'nx-checkbox-rejecting',
  imports: [Checkbox],
  template: `<nx-checkbox label="對話" [checked]="false" (checkedChange)="asked = asked + 1" />`,
})
class Rejecting {
  asked = 0;
}

@Component({
  selector: 'nx-checkbox-saving',
  imports: [Checkbox],
  template: `<nx-checkbox
    label="對話"
    [checked]="checked()"
    [pending]="pending()"
    (checkedChange)="pending.set(true)"
  />`,
})
class Saving {
  readonly checked = signal(false);
  readonly pending = signal(false);
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

  it('shows a partly picked header as mixed until it is checked', () => {
    const fixture = TestBed.createComponent(Checkbox);
    fixture.componentRef.setInput('label', '選取清單中的全部人員');
    fixture.componentRef.setInput('mixed', true);
    fixture.detectChanges();
    const element = fixture.nativeElement as HTMLElement;
    const input = element.querySelector<HTMLInputElement>('input')!;
    expect(input.indeterminate).toBe(true);
    const icon = () => fixture.debugElement.query(By.directive(Icon)).componentInstance as Icon;
    expect(icon().name()).toBe('minus');

    // 勾選的優先：全部選了就不再是 mixed，即使使用端還沒把 mixed 關掉。
    fixture.componentRef.setInput('checked', true);
    fixture.detectChanges();
    expect(input.indeterminate).toBe(false);
    expect(icon().name()).toBe('check');
  });

  it('hides the visible label but keeps the name for assistive technology', () => {
    @Component({
      imports: [Checkbox],
      template: `<nx-checkbox hideLabel label="選取 林可蘿" description="研發處" />`,
    })
    class Hidden {}
    const fixture = TestBed.createComponent(Hidden);
    fixture.detectChanges();
    const element = fixture.nativeElement as HTMLElement;
    expect(element.querySelector('.selection-control')?.classList).toContain('is-label-hidden');
    expect(element.querySelector('.selection-copy')).toBeNull();
    expect(element.querySelector('input')?.getAttribute('aria-label')).toBe('選取 林可蘿');
  });

  it('spins beside a pending box and ignores toggles without disabling it', () => {
    const { fixture, host, row, input } = setup();
    host.pending.set(true);
    fixture.detectChanges();
    expect(input.disabled).toBe(false);
    expect(input.getAttribute('aria-busy')).toBe('true');
    expect(row.querySelector('.selection-pending')?.classList).toContain('is-spinning');
    input.click();
    fixture.detectChanges();
    expect(host.checked()).toBe(false);
    expect(input.checked).toBe(false);

    host.pending.set(false);
    fixture.detectChanges();
    expect(row.querySelector('.selection-pending')).toBeNull();
    input.click();
    fixture.detectChanges();
    expect(host.checked()).toBe(true);
  });

  it('shows the click at once and snaps back after the next render when the owner rejects it', () => {
    const fixture = TestBed.createComponent(Rejecting);
    fixture.detectChanges();
    const input = (fixture.nativeElement as HTMLElement).querySelector<HTMLInputElement>(
      'input[type="checkbox"]',
    )!;
    input.click();
    // 畫面更新前就讀得到使用者點出來的狀態（自動化測試與報讀器都在這時讀）。
    expect(input.checked).toBe(true);
    expect(fixture.componentInstance.asked).toBe(1);
    fixture.detectChanges();
    expect(input.checked).toBe(false);
  });

  it('keeps the click while the owner saves and settles on the saved value afterwards', () => {
    const fixture = TestBed.createComponent(Saving);
    fixture.detectChanges();
    const host = fixture.componentInstance;
    const input = (fixture.nativeElement as HTMLElement).querySelector<HTMLInputElement>(
      'input[type="checkbox"]',
    )!;
    input.click();
    fixture.detectChanges();
    // 儲存中：仍顯示使用者勾的狀態，不先跳回舊值。
    expect(host.pending()).toBe(true);
    expect(input.checked).toBe(true);

    // 儲存失敗：輸入沒變，結束送出後才拉回。
    host.pending.set(false);
    fixture.detectChanges();
    expect(input.checked).toBe(false);

    input.click();
    fixture.detectChanges();
    host.checked.set(true);
    host.pending.set(false);
    fixture.detectChanges();
    expect(input.checked).toBe(true);
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
