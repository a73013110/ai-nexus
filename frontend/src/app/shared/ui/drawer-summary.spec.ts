import { Component, signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { describe, expect, it } from 'vitest';
import { DrawerSummary } from './drawer-summary';

@Component({
  selector: 'nx-drawer-summary-host',
  imports: [DrawerSummary],
  template: `<nx-drawer-summary heading="20261001.txt" [subtitle]="subtitle()" [mono]="mono()">
    @if (status()) {
      <time>09:30</time>
    }
  </nx-drawer-summary>`,
})
class Host {
  readonly subtitle = signal<string | null>(null);
  readonly mono = signal(false);
  readonly status = signal(false);
}

describe('DrawerSummary', () => {
  function render() {
    const fixture = TestBed.createComponent(Host);
    fixture.detectChanges();
    return { fixture, root: fixture.nativeElement as HTMLElement };
  }

  it('renders the heading and leaves the subtitle out until there is one', () => {
    const { fixture, root } = render();
    expect(root.querySelector('.ui-drawer-summary h3')?.textContent).toBe('20261001.txt');
    expect(root.querySelector('.ui-drawer-summary p')).toBeNull();
    fixture.componentInstance.subtitle.set('手動匯入');
    fixture.detectChanges();
    expect(root.querySelector('.ui-drawer-summary p')?.textContent).toBe('手動匯入');
  });

  it('uses the code face only for identifiers', () => {
    const { fixture, root } = render();
    expect(root.querySelector('h3')?.classList.contains('is-mono')).toBe(false);
    fixture.componentInstance.mono.set(true);
    fixture.detectChanges();
    expect(root.querySelector('h3')?.classList.contains('is-mono')).toBe(true);
  });

  it('projects the status row, which stays empty without status content', () => {
    const { fixture, root } = render();
    const status = root.querySelector('.ui-drawer-status')!;
    expect(status.matches(':empty')).toBe(true);
    fixture.componentInstance.status.set(true);
    fixture.detectChanges();
    expect(status.querySelector('time')?.textContent).toBe('09:30');
  });
});
