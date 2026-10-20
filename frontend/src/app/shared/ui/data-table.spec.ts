import { Component, signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { afterEach, describe, expect, it } from 'vitest';
import {
  CursorTrail,
  DataTable,
  DataTableColumn,
  DataTableRow,
  TablePagination,
} from './data-table';

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
    // 瀏覽器的 focus-visible 判斷；以 Escape 關閉的對話框會把它設為 true。
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
      // 以鍵盤開啟時，返回的焦點會保留外框。
      focus(inside);
      expect(row.classList).not.toContain('is-focus-returned');
      // 來自有作用的控制項的焦點移動屬於鍵盤，絕不算是返回。
      visible = false;
      focus(row.ownerDocument.body);
      visible = true;
      focus(row.ownerDocument.body);
      expect(row.classList).not.toContain('is-focus-returned');
    } finally {
      dialog.remove();
    }
  });

  it('goes back to the first page in cursor mode and jumps to a typed page in offset mode', () => {
    const fixture = TestBed.createComponent(TablePagination);
    const selected: number[] = [];
    fixture.componentInstance.pageSelect.subscribe((index) => selected.push(index));
    fixture.componentRef.setInput('pageIndex', 2);
    fixture.componentRef.setInput('hasNext', true);
    fixture.detectChanges();
    const host = fixture.nativeElement as HTMLElement;
    host.querySelector<HTMLButtonElement>('[aria-label="回到第一頁"]')!.click();
    expect(selected).toEqual([0]);

    fixture.componentRef.setInput('mode', 'offset');
    fixture.componentRef.setInput('pageIndex', 0);
    fixture.componentRef.setInput('pageSize', 100);
    fixture.componentRef.setInput('count', 100);
    fixture.componentRef.setInput('total', 523);
    fixture.detectChanges();
    expect(host.querySelector('[role="status"]')!.textContent!.trim()).toBe('1–100 / 523 筆');
    expect(host.textContent).toContain('/ 6');
    const jump = host.querySelector<HTMLInputElement>('[aria-label="跳到頁碼"]')!;
    jump.value = '4';
    jump.dispatchEvent(new Event('change'));
    jump.value = '9';
    jump.dispatchEvent(new Event('change'));
    expect(selected).toEqual([0, 3]);
    expect(jump.value).toBe('1');
  });

  it('reads a typed page once although Enter and the later change both fire', () => {
    const fixture = TestBed.createComponent(TablePagination);
    const selected: number[] = [];
    fixture.componentInstance.pageSelect.subscribe((index) => selected.push(index));
    fixture.componentRef.setInput('mode', 'offset');
    fixture.componentRef.setInput('pageSize', 100);
    fixture.componentRef.setInput('count', 100);
    fixture.componentRef.setInput('total', 523);
    fixture.detectChanges();
    const host = fixture.nativeElement as HTMLElement;
    const jump = host.querySelector<HTMLInputElement>('[aria-label="跳到頁碼"]')!;
    jump.value = '3';
    jump.dispatchEvent(new KeyboardEvent('keydown', { key: 'Enter', bubbles: true }));
    fixture.componentRef.setInput('busy', true);
    fixture.detectChanges();
    // 讀取中頁碼欄只是唯讀，焦點不會被拿走；失焦時的 change 不再送一次。
    expect(jump.disabled).toBe(false);
    expect(jump.readOnly).toBe(true);
    jump.dispatchEvent(new Event('change'));
    expect(selected).toEqual([2]);

    fixture.componentRef.setInput('pageIndex', 2);
    fixture.componentRef.setInput('busy', false);
    fixture.detectChanges();
    const next = host.querySelector<HTMLButtonElement>('[aria-label="下一頁"]')!;
    next.focus();
    fixture.componentRef.setInput('busy', true);
    fixture.detectChanges();
    expect(next.disabled).toBe(false);
    expect(document.activeElement).toBe(next);
    jump.value = '1';
    jump.dispatchEvent(new Event('change'));
    expect(selected).toEqual([2]);
    fixture.componentRef.setInput('busy', false);
    fixture.detectChanges();
    jump.dispatchEvent(new Event('change'));
    expect(selected).toEqual([2, 0]);
  });
});

describe('CursorTrail', () => {
  it('remembers the cursor of each visited page until the first page is read again', () => {
    const trail = new CursorTrail<string | null>(null);
    trail.visit(1, 'a');
    trail.visit(2, 'b');
    expect(trail.at(1)).toBe('a');
    expect(trail.at(2)).toBe('b');
    trail.visit(0, null);
    expect(trail.at(0)).toBeNull();
    expect(trail.at(1)).toBeUndefined();
  });
});
