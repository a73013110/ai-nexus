import { Component, signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { afterEach, describe, expect, it } from 'vitest';
import { Icon } from './icon';
import { PendingButton } from './pending-button';

@Component({
  selector: 'nx-pending-host',
  imports: [PendingButton, Icon],
  template: `<form (submit)="$event.preventDefault(); submits = submits + 1">
      <button nxPending type="submit" class="primary-button" [pending]="saving()">
        <nx-icon name="check" />{{ saving() ? '儲存中…' : '儲存' }}
      </button>
    </form>
    <button
      nxPending
      type="button"
      class="secondary-button"
      [pending]="saving()"
      (click)="clicks = clicks + 1"
    >
      匯出
    </button>`,
})
class Host {
  readonly saving = signal(false);
  submits = 0;
  clicks = 0;
}

function setup() {
  const fixture = TestBed.createComponent(Host);
  fixture.detectChanges();
  const [submit, plain] = Array.from(
    (fixture.nativeElement as HTMLElement).querySelectorAll('button'),
  );
  return { fixture, host: fixture.componentInstance, submit, plain };
}

describe('PendingButton', () => {
  afterEach(() => TestBed.resetTestingModule());

  it('works as a plain button while idle', () => {
    const { host, submit, plain } = setup();
    expect(submit.getAttribute('aria-busy')).toBeNull();
    expect(submit.getAttribute('aria-disabled')).toBeNull();
    expect(submit.querySelector('.pending-icon')).toBeNull();
    submit.click();
    plain.click();
    expect(host.submits).toBe(1);
    expect(host.clicks).toBe(1);
  });

  it('puts a spinner before the leading icon or text and stays focusable while pending', () => {
    const { fixture, host, submit, plain } = setup();
    host.saving.set(true);
    fixture.detectChanges();
    expect(submit.getAttribute('aria-busy')).toBe('true');
    expect(submit.getAttribute('aria-disabled')).toBe('true');
    expect(submit.disabled).toBe(false);
    expect(submit.classList).toContain('is-pending');
    const icons = submit.querySelectorAll('nx-icon');
    // 轉圈排在投影的前導圖示前，controls.scss 以 `.pending-icon + nx-icon` 藏起後者。
    expect(icons[0].classList).toContain('pending-icon');
    expect(icons[0].classList).toContain('is-spinning');
    expect(icons[1].previousElementSibling).toBe(icons[0]);
    expect(submit.textContent).toContain('儲存中…');
    expect(plain.firstElementChild?.classList).toContain('pending-icon');
  });

  it('swallows clicks and the form submit they would start while pending', () => {
    const { fixture, host, submit, plain } = setup();
    host.saving.set(true);
    fixture.detectChanges();
    submit.click();
    plain.click();
    expect(host.submits).toBe(0);
    expect(host.clicks).toBe(0);

    host.saving.set(false);
    fixture.detectChanges();
    expect(submit.querySelector('.pending-icon')).toBeNull();
    submit.click();
    plain.click();
    expect(host.submits).toBe(1);
    expect(host.clicks).toBe(1);
  });
});
