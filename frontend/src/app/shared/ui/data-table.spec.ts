import { Component, signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { afterEach, describe, expect, it } from 'vitest';
import { DataTable, DataTableColumn, DataTableRow } from './data-table';

@Component({
  selector: 'nx-data-table-host',
  imports: [DataTable, DataTableColumn, DataTableRow],
  template: `<nx-data-table label="範例" [stacked]="stacked()" [columns]="columns">
    <table>
      <tbody>
        <tr nxTableRow (rowActivate)="opened.set(opened() + 1)">
          <td nxTableColumn="name">名稱</td>
          <td nxTableColumn="note" data-label="備註">說明</td>
          <td data-stack="end"><button type="button" data-row-action>開啟</button></td>
        </tr>
      </tbody>
      <tbody>
        <tr nxTableRow>
          <td><button type="button" data-row-action>第二組</button></td>
        </tr>
      </tbody>
    </table>
  </nx-data-table>`,
})
class Host {
  readonly stacked = signal(false);
  readonly opened = signal(0);
  readonly columns = [
    { id: 'name', label: '名稱', hideable: false },
    { id: 'note', label: '備註' },
  ];
}

describe('DataTable', () => {
  afterEach(() => TestBed.resetTestingModule());

  it('stacks rows into cards only when a table opts in', () => {
    const fixture = TestBed.createComponent(Host);
    fixture.detectChanges();
    const table = (fixture.nativeElement as HTMLElement).querySelector('nx-data-table')!;
    expect(table.classList).toContain('ui-data-table');
    expect(table.classList).not.toContain('ui-table-stack');
    fixture.componentInstance.stacked.set(true);
    fixture.detectChanges();
    expect(table.classList).toContain('ui-table-stack');
  });

  it('keeps row activation and column visibility in the stacked mode', () => {
    const fixture = TestBed.createComponent(Host);
    fixture.componentInstance.stacked.set(true);
    fixture.detectChanges();
    const element = fixture.nativeElement as HTMLElement;
    element.querySelector<HTMLElement>('td')!.click();
    expect(fixture.componentInstance.opened()).toBe(1);
    expect(document.activeElement?.textContent).toBe('開啟');
    element.querySelector<HTMLButtonElement>('button[data-row-action]')!.click();
    expect(fixture.componentInstance.opened()).toBe(1);
    const table = fixture.debugElement.children[0].injector.get(DataTable);
    table.toggleColumn('note');
    fixture.detectChanges();
    expect(element.querySelector<HTMLElement>('[data-label]')!.hidden).toBe(true);
  });

  it('moves between row actions across row groups with the arrow keys', () => {
    const fixture = TestBed.createComponent(Host);
    fixture.detectChanges();
    const [first, second] = (fixture.nativeElement as HTMLElement).querySelectorAll<HTMLElement>(
      'button[data-row-action]',
    );
    first.focus();
    first.dispatchEvent(new KeyboardEvent('keydown', { key: 'ArrowDown', bubbles: true }));
    expect(document.activeElement).toBe(second);
    second.dispatchEvent(new KeyboardEvent('keydown', { key: 'Home', bubbles: true }));
    expect(document.activeElement).toBe(first);
  });

  it('returns focus from a dialog without the ring when the row was clicked open', () => {
    const fixture = TestBed.createComponent(Host);
    fixture.detectChanges();
    const row = (fixture.nativeElement as HTMLElement).querySelector('tr')!;
    const action = row.querySelector<HTMLElement>('[data-row-action]')!;
    // The browser's focus-visible decision, which a dialog closed with Escape sets to true.
    let visible = false;
    Object.defineProperty(action, 'matches', {
      value: (selector: string) =>
        selector === ':focus-visible' ? visible : Element.prototype.matches.call(action, selector),
    });
    const dialog = document.createElement('dialog');
    const inside = dialog.appendChild(document.createElement('button'));
    document.body.append(dialog);
    const focus = (from: Element | null) =>
      action.dispatchEvent(new FocusEvent('focusin', { bubbles: true, relatedTarget: from }));
    try {
      focus(null);
      visible = true;
      focus(inside);
      expect(row.classList).toContain('is-focus-returned');
      action.dispatchEvent(new KeyboardEvent('keydown', { key: 'Shift', bubbles: true }));
      expect(row.classList).toContain('is-focus-returned');
      action.dispatchEvent(new KeyboardEvent('keydown', { key: 'Enter', bubbles: true }));
      expect(row.classList).not.toContain('is-focus-returned');
      // Opened from the keyboard, the returning focus keeps its ring.
      focus(inside);
      expect(row.classList).not.toContain('is-focus-returned');
      // A focus move from a live control is the keyboard's, never a return.
      visible = false;
      focus(row.ownerDocument.body);
      visible = true;
      focus(row.ownerDocument.body);
      expect(row.classList).not.toContain('is-focus-returned');
    } finally {
      dialog.remove();
    }
  });
});
