import { Component, computed, signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { afterEach, describe, expect, it } from 'vitest';
import { ThemeService } from '@core/preferences/theme-service';
import { SortableRows, SortHandle, SortRank, type RowMove } from './sortable-rows';

@Component({
  imports: [SortableRows, SortHandle, SortRank],
  template: `<table>
    <tbody
      [nxSortableRows]="ids()"
      [sortDisabled]="locked()"
      [sortName]="name"
      (sortMove)="moved($event)"
    >
      @for (person of people(); track person.id; let index = $index) {
        <tr [attr.data-sort-id]="person.id">
          <td>
            <nx-sort-handle
              [id]="person.id"
              [name]="person.name"
              [position]="index + 1"
              [count]="people().length"
              [locked]="locked()"
            /><nx-sort-rank
              [name]="person.name"
              [position]="index + 1"
              [count]="people().length"
              [unsorted]="index > 1"
              (place)="moved({ id: person.id, index: $event - 1 })"
            />
          </td>
          <td class="person">{{ person.name }}</td>
        </tr>
      }
    </tbody>
  </table>`,
})
class Host {
  readonly people = signal([
    { id: 'a', name: '周雅婷' },
    { id: 'b', name: '吳志明' },
    { id: 'c', name: '李佳蓉' },
    { id: 'd', name: '黃建宏' },
  ]);
  readonly locked = signal(false);
  readonly ids = computed(() => this.people().map((person) => person.id));
  readonly moves: RowMove[] = [];
  readonly name = (id: string) => this.people().find((person) => person.id === id)?.name ?? '';
  moved(move: RowMove) {
    this.moves.push(move);
    const list = [...this.people()];
    const from = list.findIndex((person) => person.id === move.id);
    list.splice(move.index, 0, ...list.splice(from, 1));
    this.people.set(list);
  }
}

describe('SortableRows', () => {
  afterEach(() => TestBed.resetTestingModule());

  function setup() {
    // jsdom 沒有捲動；方向鍵移動後會把列捲進畫面。
    Element.prototype.scrollIntoView ??= function () {};
    TestBed.configureTestingModule({
      providers: [{ provide: ThemeService, useValue: { reducedMotion: () => true } }],
    });
    const fixture = TestBed.createComponent(Host);
    fixture.detectChanges();
    const element = fixture.nativeElement as HTMLElement;
    const names = () => [...element.querySelectorAll('.person')].map((node) => node.textContent);
    const handle = (name: string) =>
      [...element.querySelectorAll<HTMLButtonElement>('[data-sort-handle]')].find((button) =>
        button.getAttribute('aria-label')?.startsWith(`移動「${name}」`),
      )!;
    const press = (name: string, key: string) =>
      handle(name).dispatchEvent(new KeyboardEvent('keydown', { key, bubbles: true }));
    const live = () => document.body.querySelector('p[aria-live]')?.textContent;
    return { fixture, host: fixture.componentInstance, element, names, handle, press, live };
  }

  it('names each handle with its position and the keys it accepts', () => {
    const { handle } = setup();
    expect(handle('李佳蓉').getAttribute('aria-label')).toBe(
      '移動「李佳蓉」，目前第 3 位，共 4 位',
    );
    expect(handle('李佳蓉').getAttribute('aria-description')).toContain('Home／End');
  });

  it('moves with arrow, page and home or end keys and announces the new row', () => {
    const { fixture, host, names, press, live } = setup();
    press('黃建宏', 'Home');
    fixture.detectChanges();
    expect(host.moves).toEqual([{ id: 'd', index: 0 }]);
    expect(names()).toEqual(['黃建宏', '周雅婷', '吳志明', '李佳蓉']);
    expect(live()).toBe('「黃建宏」移到第 1 列，共 4 列');
    press('周雅婷', 'PageDown');
    expect(host.moves.at(-1)).toEqual({ id: 'a', index: 3 });
    fixture.detectChanges();
    // 已在最上方再按 ↑ 不送出。
    press('黃建宏', 'ArrowUp');
    expect(host.moves).toHaveLength(2);
  });

  it('ignores keys while locked but still moves by a typed position', () => {
    const { fixture, host, element, handle, press } = setup();
    host.locked.set(true);
    fixture.detectChanges();
    expect(handle('李佳蓉').getAttribute('aria-disabled')).toBe('true');
    press('李佳蓉', 'Home');
    expect(host.moves).toEqual([]);
    const rank = element.querySelectorAll<HTMLButtonElement>('button.sort-rank')[2];
    expect(rank.classList).toContain('is-unsorted');
    rank.click();
    fixture.detectChanges();
    const input = element.querySelector<HTMLInputElement>('.sort-rank-input')!;
    expect(input.getAttribute('aria-label')).toBe('把「李佳蓉」移到第幾位（1 至 4）');
    input.value = '1';
    input.dispatchEvent(new KeyboardEvent('keydown', { key: 'Enter', bubbles: true }));
    expect(host.moves).toEqual([{ id: 'c', index: 0 }]);
  });

  it('drops a typed position beyond the end onto the last place and skips the same place', () => {
    const { fixture, host, element } = setup();
    const type = (value: string) => {
      element.querySelector<HTMLButtonElement>('button.sort-rank')!.click();
      fixture.detectChanges();
      const input = element.querySelector<HTMLInputElement>('.sort-rank-input')!;
      input.value = value;
      input.dispatchEvent(new KeyboardEvent('keydown', { key: 'Enter', bubbles: true }));
      fixture.detectChanges();
    };
    type('1');
    expect(host.moves).toEqual([]);
    type('99');
    expect(host.moves).toEqual([{ id: 'a', index: 3 }]);
  });
});
