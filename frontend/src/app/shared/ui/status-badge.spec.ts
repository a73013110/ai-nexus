import { Component, signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { afterEach, describe, expect, it } from 'vitest';
import { StatusBadge } from './status-badge';

@Component({
  selector: 'nx-status-badge-host',
  imports: [StatusBadge],
  template: `<nx-status-badge [tone]="tone()" [state]="state()">{{ state() }}</nx-status-badge>`,
})
class Host {
  readonly tone = signal<'info' | 'success'>('info');
  readonly state = signal('寄送中');
}

const frame = () => new Promise((resolve) => requestAnimationFrame(resolve));

describe('StatusBadge', () => {
  afterEach(() => TestBed.resetTestingModule());

  it('plays the settle motion only when the state changes after the first render', async () => {
    const fixture = TestBed.createComponent(Host);
    fixture.detectChanges();
    await frame();
    fixture.detectChanges();
    const badge = (fixture.nativeElement as HTMLElement).querySelector('nx-status-badge')!;
    expect(badge.hasAttribute('data-changed')).toBe(false);

    fixture.componentInstance.state.set('已寄出');
    fixture.componentInstance.tone.set('success');
    fixture.detectChanges();
    await frame();
    fixture.detectChanges();
    expect(badge.getAttribute('data-tone')).toBe('success');
    expect(badge.hasAttribute('data-changed')).toBe(true);
  });
});
