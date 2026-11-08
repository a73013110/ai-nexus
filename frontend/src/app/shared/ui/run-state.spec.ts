import { Component, signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { afterEach, describe, expect, it } from 'vitest';
import { RunState, type RunStateKind, runStateTone } from './run-state';

@Component({
  selector: 'nx-run-state-host',
  imports: [RunState],
  template: `<nx-run-state [state]="state()" label="執行狀態" />`,
})
class Host {
  readonly state = signal<RunStateKind>('running');
}

const frame = () => new Promise((resolve) => requestAnimationFrame(resolve));

describe('RunState', () => {
  afterEach(() => TestBed.resetTestingModule());

  it('draws the finished glyph only when the state changes after the first render', async () => {
    const fixture = TestBed.createComponent(Host);
    fixture.detectChanges();
    await frame();
    fixture.detectChanges();
    const icon = (fixture.nativeElement as HTMLElement).querySelector('nx-run-state')!;
    expect(icon.querySelector('.orbit')).not.toBeNull();
    expect(icon.hasAttribute('data-changed')).toBe(false);
    expect(icon.getAttribute('role')).toBe('img');
    expect(icon.getAttribute('aria-label')).toBe('執行狀態');

    fixture.componentInstance.state.set('succeeded');
    fixture.detectChanges();
    await frame();
    fixture.detectChanges();
    expect(icon.querySelector('.orbit')).toBeNull();
    expect(icon.querySelectorAll('.glyph')).toHaveLength(1);
    expect(icon.getAttribute('data-tone')).toBe('success');
    expect(icon.hasAttribute('data-changed')).toBe(true);
  });

  it('maps unknown states to a neutral tone', () => {
    expect(runStateTone('failed')).toBe('danger');
    expect(runStateTone('whatever')).toBe('neutral');
  });
});
