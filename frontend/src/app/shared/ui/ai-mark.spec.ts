import { Component, signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { afterEach, describe, expect, it } from 'vitest';
import { AiMark, type AiMarkState } from './ai-mark';

@Component({
  selector: 'nx-ai-mark-host',
  imports: [AiMark],
  template: `<nx-ai-mark [state]="state()" />`,
})
class Host {
  readonly state = signal<AiMarkState>('rest');
}

describe('AiMark', () => {
  afterEach(() => TestBed.resetTestingModule());

  it('is decorative and exposes its state for styling', () => {
    const fixture = TestBed.createComponent(Host);
    fixture.detectChanges();
    const mark = (fixture.nativeElement as HTMLElement).querySelector('nx-ai-mark')!;
    expect(mark.getAttribute('aria-hidden')).toBe('true');
    expect(mark.getAttribute('data-state')).toBe('rest');

    fixture.componentInstance.state.set('working');
    fixture.detectChanges();
    expect(mark.getAttribute('data-state')).toBe('working');
  });

  it('sends each node to the next vertex and to its own slot in the row', () => {
    const fixture = TestBed.createComponent(Host);
    fixture.detectChanges();
    const nodes = [
      ...(fixture.nativeElement as HTMLElement).querySelectorAll<SVGGElement>('.ai-mark-node'),
    ];
    expect(nodes).toHaveLength(3);
    const hops = nodes.map((node) => node.style.getPropertyValue('--hop'));
    // 三段位移加起來繞三角一圈，回到原點。
    const sum = hops
      .map((hop) => hop.split(' ').map(parseFloat))
      .reduce(([x, y], [dx, dy]) => [x + dx, y + dy], [0, 0]);
    expect(sum).toEqual([0, 0]);
    // 收成一排時三點各佔一格，跳起的先後不重複。
    const order = nodes.map((node) => node.style.getPropertyValue('--order').trim());
    expect([...order].sort()).toEqual(['0', '1', '2']);
  });
});
