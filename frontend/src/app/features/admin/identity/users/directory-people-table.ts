import { ChangeDetectionStrategy, Component, computed, inject } from '@angular/core';
import type { DirectoryPersonDto } from '@core/api/schema';
import { Checkbox } from '@shared/ui/checkbox';
import { CopyValue } from '@shared/ui/copy-value';
import { DataTable, DataTableColumn, TableState, type TableColumn } from '@shared/ui/data-table';
import { Icon } from '@shared/ui/icon';
import { Notice } from '@shared/ui/notice';
import { SearchField } from '@shared/ui/search-field';
import { StatusBadge } from '@shared/ui/status-badge';
import { ViewSwitch, type ViewOption } from '@shared/ui/view-switch';
import { DirectoryImportStore, type PeopleFilter } from './directory-import-store';
import { changedFields, selectable, stateLabel, stateTone } from './directory-outcomes';

const filterLabels: [PeopleFilter, string][] = [
  ['all', '全部'],
  ['new', '未匯入'],
  ['changed', '有異動'],
  ['synced', '已同步'],
  ['blocked', '無法匯入'],
];

/**
 * 匯入對話框中間的窗格：目前節點的路徑與 DN、狀態篩選與節點內搜尋、人員表。
 * 整列可點選勾選；Shift＋點選把上一次點的人到這一位之間設成同一狀態（篩選後的清單順序）。
 */
@Component({
  selector: 'nx-directory-people-table',
  imports: [
    Checkbox,
    CopyValue,
    DataTable,
    DataTableColumn,
    Icon,
    Notice,
    SearchField,
    StatusBadge,
    TableState,
    ViewSwitch,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: { '(pointerdown)': 'track($event)', '(keydown)': 'track($event)' },
  styleUrl: './directory-people-table.scss',
  templateUrl: './directory-people-table.html',
})
export class DirectoryPeopleTable {
  readonly store = inject(DirectoryImportStore);
  readonly columns: TableColumn[] = [
    { id: 'select', label: '選取', hideable: false },
    { id: 'name', label: '人員', hideable: false },
    { id: 'department', label: '部門與職稱' },
    { id: 'email', label: '信箱' },
    { id: 'state', label: '平台狀態', hideable: false },
    { id: 'inspect', label: 'AD 屬性', hideable: false },
  ];
  readonly selectable = selectable;
  readonly stateLabel = stateLabel;
  readonly stateTone = stateTone;
  readonly changedFields = changedFields;
  /** 最近一次指標或按鍵是否按著 Shift；核取方塊的 change 事件本身不帶修飾鍵。 */
  private shift = false;

  /** 「無法匯入」沒有人時不顯示，平常只有四個篩選。 */
  readonly filters = computed<ViewOption[]>(() => {
    const counts = this.store.counts();
    return filterLabels
      .filter(([value]) => value !== 'blocked' || counts.blocked || this.store.filter() === value)
      .map(([value, label]) => ({ value, label: `${label} ${counts[value]}` }));
  });
  readonly subtitle = computed(() => {
    const node = this.store.current();
    if (!node) return '';
    const total = node.people === null ? '超過 20,000' : String(node.people);
    if (this.store.subtree()) return `含子 OU 共 ${total} 人`;
    return this.store.people.value()
      ? `直屬 ${this.store.all().length} 人 · 含子 OU 共 ${total} 人`
      : `含子 OU 共 ${total} 人`;
  });
  readonly empty = computed(() => {
    const node = this.store.current();
    if (node?.people === 0)
      return { title: '這裡沒有使用者', text: '電腦、群組與內建帳號不會列出。' };
    if (!this.store.all().length && !this.store.subtree())
      return {
        title: '這一層沒有直屬人員',
        text: '人員都在下層 OU；打開左下的「包含子 OU」一起列出。',
      };
    return { title: '沒有符合的人員', text: '換個關鍵字，或清除篩選與「隱藏 AD 停用」。' };
  });

  track(event: MouseEvent | KeyboardEvent) {
    this.shift = event.shiftKey;
  }
  /** Shift＋點選時瀏覽器會延伸文字選取，先擋掉；點擊本身照常觸發。 */
  hold(event: MouseEvent) {
    if (event.shiftKey) event.preventDefault();
  }
  /** 點在列的空白處也能勾選；核取方塊、按鈕與連結自己處理。 */
  rowClick(event: MouseEvent, person: DirectoryPersonDto) {
    this.shift = event.shiftKey;
    if (
      !selectable(person) ||
      (event.target instanceof Element && event.target.closest('button,a,input,label')) ||
      window.getSelection()?.toString()
    )
      return;
    this.store.toggle(person, !this.store.selection().has(person.sid), event.shiftKey);
  }
  check(person: DirectoryPersonDto, checked: boolean) {
    this.store.toggle(person, checked, this.shift);
    this.shift = false;
  }
}
