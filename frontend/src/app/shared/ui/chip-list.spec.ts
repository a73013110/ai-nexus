import { ChangeDetectionStrategy, Component, signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { ChipList, type Chip } from './chip-list';

@Component({
  imports: [ChipList],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `<nx-chip-list
    [chips]="chips()"
    [busy]="busy()"
    [hidden]="!chips().length"
    (chipRemove)="drop($event)"
    (allRemoved)="emptied()"
  />`,
})
class Host {
  readonly chips = signal<Chip[]>([
    { id: 'a', label: '部門：資訊室' },
    { id: 'b', label: '狀態：進行中' },
  ]);
  readonly busy = signal(false);
  readonly emptied = vi.fn();
  drop(chip: Chip) {
    this.chips.update((chips) => chips.filter((item) => item.id !== chip.id));
  }
}

const frame = () => new Promise((resolve) => requestAnimationFrame(resolve));

describe('ChipList', () => {
  afterEach(() => TestBed.resetTestingModule());

  function setup() {
    const fixture = TestBed.createComponent(Host);
    fixture.detectChanges();
    const root = fixture.nativeElement as HTMLElement;
    document.body.append(root);
    const buttons = () => [...root.querySelectorAll<HTMLButtonElement>('[data-chip-remove]')];
    return { fixture, root, buttons };
  }

  it('names each remove button after its chip and moves focus to the chip that takes its place', async () => {
    const { fixture, buttons } = setup();
    expect(buttons().map((button) => button.getAttribute('aria-label'))).toEqual([
      '移除條件：部門：資訊室',
      '移除條件：狀態：進行中',
    ]);
    buttons()[0].click();
    fixture.detectChanges();
    await frame();
    expect(document.activeElement).toBe(buttons()[0]);
    expect(buttons()[0].getAttribute('aria-label')).toBe('移除條件：狀態：進行中');
  });

  it('reports when the last chip is gone so the caller can place focus', async () => {
    const { fixture, buttons } = setup();
    fixture.componentInstance.chips.set([{ id: 'a', label: '部門：資訊室' }]);
    fixture.detectChanges();
    buttons()[0].click();
    fixture.detectChanges();
    await frame();
    expect(fixture.componentInstance.emptied).toHaveBeenCalledTimes(1);
  });

  it('keeps the buttons focusable but ignores removal while busy', () => {
    const { fixture, buttons } = setup();
    fixture.componentInstance.busy.set(true);
    fixture.detectChanges();
    buttons()[0].click();
    fixture.detectChanges();
    expect(buttons()).toHaveLength(2);
    expect(buttons()[0].disabled).toBe(false);
    expect(buttons()[0].getAttribute('aria-disabled')).toBe('true');
  });
});
