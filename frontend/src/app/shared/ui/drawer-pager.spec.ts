import { TestBed } from '@angular/core/testing';
import { afterEach, describe, expect, it } from 'vitest';
import { DrawerPager } from './drawer-pager';

describe('DrawerPager', () => {
  afterEach(() => TestBed.resetTestingModule());

  function setup(index: number, count: number) {
    const fixture = TestBed.createComponent(DrawerPager);
    const moves: number[] = [];
    fixture.componentInstance.move.subscribe((direction) => moves.push(direction));
    fixture.componentRef.setInput('noun', '日誌');
    fixture.componentRef.setInput('index', index);
    fixture.componentRef.setInput('count', count);
    fixture.detectChanges();
    const host = fixture.nativeElement as HTMLElement;
    const button = (name: string) =>
      host.querySelector<HTMLButtonElement>(`[aria-label="${name}"]`)!;
    return { fixture, moves, host, previous: button('上一筆日誌'), next: button('下一筆日誌') };
  }

  it('keeps focus on 下一筆 when it reaches the last row', () => {
    const { fixture, moves, next } = setup(1, 3);
    next.focus();
    next.click();
    // 頁面換到最後一筆。
    fixture.componentRef.setInput('index', 2);
    fixture.detectChanges();
    expect(moves).toEqual([1]);
    expect(next.disabled).toBe(false);
    expect(next.getAttribute('aria-disabled')).toBe('true');
    expect(document.activeElement).toBe(next);
    next.click();
    expect(moves).toEqual([1]);
  });

  it('moves back from the last row and blocks both ends off the page', () => {
    const { fixture, moves, host, previous, next } = setup(2, 3);
    expect(host.textContent).toContain('3 / 3');
    previous.click();
    expect(moves).toEqual([-1]);
    fixture.componentRef.setInput('index', -1);
    fixture.detectChanges();
    expect(previous.getAttribute('aria-disabled')).toBe('true');
    expect(next.getAttribute('aria-disabled')).toBe('true');
    previous.click();
    next.click();
    expect(moves).toEqual([-1]);
  });
});
