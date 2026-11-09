import { ChangeDetectionStrategy, Component, signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { describe, expect, it } from 'vitest';
import { PasswordField, PasswordInput } from './password-field';

@Component({
  imports: [PasswordField, PasswordInput],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `<nx-password-field [disabled]="busy()"
    ><input nxPasswordInput aria-label="密碼"
  /></nx-password-field>`,
})
class Host {
  readonly busy = signal(false);
}

async function render() {
  const fixture = TestBed.createComponent(Host);
  await fixture.whenStable();
  const root = fixture.nativeElement as HTMLElement;
  return { fixture, input: root.querySelector('input')!, toggle: root.querySelector('button')! };
}

describe('PasswordField', () => {
  it('toggles between hidden and visible text', async () => {
    const { fixture, input, toggle } = await render();
    expect(input.type).toBe('password');
    expect(toggle.getAttribute('aria-label')).toBe('顯示密碼');
    toggle.click();
    await fixture.whenStable();
    expect(input.type).toBe('text');
    expect(toggle.getAttribute('aria-pressed')).toBe('true');
    expect(toggle.getAttribute('aria-label')).toBe('隱藏密碼');
  });

  it('hides the password while the form is submitting', async () => {
    const { fixture, input, toggle } = await render();
    toggle.click();
    await fixture.whenStable();
    expect(input.type).toBe('text');
    fixture.componentInstance.busy.set(true);
    await fixture.whenStable();
    expect(input.type).toBe('password');
  });

  it('warns about CapsLock while the field has focus', async () => {
    const { fixture, input } = await render();
    const key = new KeyboardEvent('keydown', { modifierCapsLock: true });
    input.dispatchEvent(key);
    await fixture.whenStable();
    const hint = (fixture.nativeElement as HTMLElement).querySelector('[role="status"]')!;
    expect(hint.textContent).toContain('大寫鎖定');
    expect(input.getAttribute('aria-describedby')).toBe(hint.id);
    input.dispatchEvent(new FocusEvent('blur'));
    await fixture.whenStable();
    expect((fixture.nativeElement as HTMLElement).querySelector('[role="status"]')).toBeNull();
    expect(input.hasAttribute('aria-describedby')).toBe(false);
  });
});
