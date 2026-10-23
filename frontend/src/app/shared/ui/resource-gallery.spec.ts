import { Component, signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { afterEach, describe, expect, it } from 'vitest';
import { ResourceGallery } from './resource-gallery';

@Component({
  selector: 'nx-gallery-host',
  imports: [ResourceGallery],
  template: `<nx-resource-gallery
    label="專案"
    [loading]="loading()"
    [busy]="busy()"
    [empty]="!cards().length"
  >
    @for (card of cards(); track card) {
      <article>{{ card }}</article>
    }
    <p gallery-empty>沒有符合的專案。</p>
  </nx-resource-gallery>`,
})
class Host {
  readonly loading = signal(true);
  readonly busy = signal(true);
  readonly cards = signal<string[]>([]);
}

describe('ResourceGallery', () => {
  afterEach(() => TestBed.resetTestingModule());

  function setup() {
    const fixture = TestBed.createComponent(Host);
    fixture.detectChanges();
    const element = fixture.nativeElement as HTMLElement;
    return {
      fixture,
      host: fixture.componentInstance,
      element,
      grid: () => element.querySelector<HTMLElement>('.gallery-grid')!,
      line: () => element.querySelector('.refresh-line')!,
      search: () => element.querySelector('nx-search-field input')!,
    };
  }

  it('draws skeleton cards on the first load without dimming them', () => {
    const { grid, line } = setup();
    expect(grid().getAttribute('role')).toBe('status');
    expect(grid().getAttribute('aria-label')).toBe('正在載入專案');
    expect(grid().querySelectorAll('.skeleton').length).toBeGreaterThan(0);
    expect(grid().classList).not.toContain('is-refreshing');
    expect(line().classList).not.toContain('is-active');
  });

  it('keeps the cards, dims them and spins the search while refreshing', () => {
    const { fixture, host, grid, line, search } = setup();
    host.loading.set(false);
    host.cards.set(['星河計畫', '晨光專案']);
    fixture.detectChanges();
    expect(grid().textContent).toContain('星河計畫');
    expect(grid().classList).toContain('is-refreshing');
    expect(grid().getAttribute('aria-busy')).toBe('true');
    expect(line().classList).toContain('is-active');
    expect(search().getAttribute('aria-busy')).toBe('true');

    host.busy.set(false);
    fixture.detectChanges();
    expect(grid().classList).not.toContain('is-refreshing');
    expect(grid().hasAttribute('aria-busy')).toBe(false);
    expect(line().classList).not.toContain('is-active');
    expect(search().hasAttribute('aria-busy')).toBe(false);
  });

  it('holds the skeleton instead of the empty state until the search answers', () => {
    const { fixture, host, element, grid, line } = setup();
    host.loading.set(false);
    fixture.detectChanges();
    expect(grid().getAttribute('role')).toBe('status');
    expect(element.textContent).not.toContain('沒有符合的專案。');
    expect(line().classList).not.toContain('is-active');

    host.busy.set(false);
    fixture.detectChanges();
    expect(element.textContent).toContain('沒有符合的專案。');
  });
});
