import { ChangeDetectionStrategy, Component, signal } from '@angular/core';
import { TestBed, type ComponentFixture } from '@angular/core/testing';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { WorkspaceSession } from '@core/auth/workspace-session';
import { DataTable } from './data-table';
import { TableHeader } from './table-header';
import { localTable } from './table-model';

interface Person {
  id: number;
  name: string;
  requests: number;
  state: string;
}

const people: Person[] = [
  { id: 1, name: '王小明', requests: 3, state: 'active' },
  { id: 2, name: '林小華', requests: 12, state: 'idle' },
  { id: 3, name: '陳大文', requests: 7, state: 'active' },
];

@Component({
  imports: [DataTable, TableHeader],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `<nx-data-table label="使用者" [table]="table">
    <table>
      <thead>
        <tr>
          <th nxHeader="name"></th>
          <th nxHeader="requests"></th>
          <th nxHeader="state"></th>
        </tr>
      </thead>
      <tbody>
        @for (row of table.rows(); track row.id) {
          <tr>
            <td>{{ row.name }}</td>
            <td>{{ row.requests }}</td>
            <td>{{ row.state }}</td>
          </tr>
        }
      </tbody>
    </table>
  </nx-data-table>`,
})
class Host {
  readonly rows = signal(people);
  readonly table = localTable({
    rows: this.rows,
    columns: [
      { id: 'name', label: '使用者' },
      { id: 'requests', label: '請求數', sort: 'desc', numeric: true },
      {
        id: 'state',
        label: '狀態',
        filter: {
          options: [
            { value: 'active', label: '活動中' },
            { value: 'idle', label: '閒置' },
          ],
        },
      },
    ],
  });
}

describe('TableHeader', () => {
  let fixture: ComponentFixture<Host>;
  let root: HTMLElement;
  const header = (index: number) => root.querySelectorAll<HTMLElement>('th')[index];
  const names = () =>
    [...root.querySelectorAll('tbody tr')].map((row) => row.firstElementChild?.textContent);

  beforeEach(() => {
    HTMLElement.prototype.showPopover = vi.fn();
    HTMLElement.prototype.hidePopover = vi.fn();
    TestBed.configureTestingModule({
      providers: [{ provide: WorkspaceSession, useValue: { has: () => true } }],
    });
    fixture = TestBed.createComponent(Host);
    fixture.detectChanges();
    root = fixture.nativeElement as HTMLElement;
  });
  afterEach(() => TestBed.resetTestingModule());

  it('shows the label alone for a column without sort or filter', () => {
    expect(header(0).textContent?.trim()).toBe('使用者');
    expect(header(0).querySelector('button')).toBeNull();
    expect(header(0).getAttribute('scope')).toBe('col');
  });

  it('sorts from the column definition, starting high to low for numeric columns', () => {
    const sort = header(1).querySelector<HTMLButtonElement>('.ui-table-sort')!;
    expect(header(1).classList).toContain('ui-table-numeric');
    expect(sort.getAttribute('aria-label')).toBe('依請求數排序');
    sort.click();
    fixture.detectChanges();
    expect(header(1).getAttribute('aria-sort')).toBe('descending');
    expect(names()).toEqual(['林小華', '陳大文', '王小明']);
    expect(sort.getAttribute('aria-label')).toBe('請求數：由大到小，切換為由小到大');
    sort.click();
    fixture.detectChanges();
    expect(header(1).getAttribute('aria-sort')).toBe('ascending');
  });

  it('filters from the popover and lists the filter as a removable chip', () => {
    const trigger = header(2).querySelector<HTMLButtonElement>('.ui-table-filter-trigger')!;
    expect(trigger.getAttribute('aria-label')).toBe('篩選狀態');
    trigger.click();
    fixture.detectChanges();
    root
      .querySelectorAll<HTMLInputElement>('.ui-table-filter-panel input[type="checkbox"]')[1]
      .click();
    fixture.detectChanges();
    expect(names()).toEqual(['林小華']);
    expect(trigger.getAttribute('aria-label')).toBe('篩選狀態（已套用）');
    const chip = root.querySelector<HTMLButtonElement>('.ui-table-chips [data-chip-remove]')!;
    expect(chip.getAttribute('aria-label')).toBe('移除條件：狀態：閒置');
    chip.click();
    fixture.detectChanges();
    expect(names()).toHaveLength(3);
  });
});
