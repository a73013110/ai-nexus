import {
  ChangeDetectionStrategy,
  Component,
  ElementRef,
  Injector,
  afterNextRender,
  computed,
  inject,
} from '@angular/core';
import type { DirectoryPersonDto } from '@core/api/schema';
import { Checkbox } from '@shared/ui/checkbox';
import { CopyValue } from '@shared/ui/copy-value';
import {
  DataTable,
  DataTableColumn,
  DataTableRow,
  TableState,
  type TableColumn,
} from '@shared/ui/data-table';
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
 * 點列（或在名稱上按 Enter）在右側檢視 AD 屬性，勾選框才是選取，兩件事不互相干擾；
 * Shift＋勾選把上一次勾的人到這一位之間設成同一狀態（篩選後的清單順序）。
 */
@Component({
  selector: 'nx-directory-people-table',
  imports: [
    Checkbox,
    CopyValue,
    DataTable,
    DataTableColumn,
    DataTableRow,
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
  private readonly host = inject<ElementRef<HTMLElement>>(ElementRef);
  private readonly injector = inject(Injector);
  readonly columns: TableColumn[] = [
    { id: 'select', label: '選取', hideable: false },
    { id: 'name', label: '人員', hideable: false },
    { id: 'department', label: '部門與職稱' },
    { id: 'email', label: '信箱' },
    { id: 'state', label: '平台狀態', hideable: false },
  ];
  /** 右側面板的 id（對話框裡的 aside），名稱按鈕以 aria-controls 指向它。 */
  readonly panelId = 'directory-side';
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
    // null 是人數太多或沒有逐一計算（同一層子節點太多），兩者都只能說「未計算」。
    const total = node.people === null ? '含子 OU 人數未計算' : `含子 OU 共 ${node.people} 人`;
    if (this.store.subtree() || !this.store.listed() || this.store.searchedServer()) return total;
    const direct = this.store.more() ? '1,000+' : String(this.store.all().length);
    return `直屬 ${direct} 人 · ${total}`;
  });
  /** 清單中可以勾選的人數；沒有時表頭全選停用，避免點了沒反應。 */
  readonly selectableCount = computed(() => this.store.visible().filter(selectable).length);
  readonly empty = computed(() => {
    if (!this.store.node())
      return { title: '無法讀取目錄', text: '請檢查左側的錯誤訊息，再按「重新讀取目錄」。' };
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
  /**
   * 檢視一個人。窄螢幕時右側面板排在表格下方，看不到就捲過去，否則點了像沒反應。
   */
  inspect(person: DirectoryPersonDto) {
    this.store.inspect(person);
    afterNextRender(
      () => {
        const side = this.host.nativeElement.ownerDocument.getElementById(this.panelId);
        const rect = side?.getBoundingClientRect();
        if (side && rect && (rect.top >= window.innerHeight || rect.bottom <= 0))
          side.scrollIntoView?.({ block: 'start' });
      },
      { injector: this.injector },
    );
  }
  /** 「只留需要處理的人」等動作會讓提示列消失，焦點移到表頭的全選框，不掉回頁面最前面。 */
  dropBanner(people: readonly DirectoryPersonDto[]) {
    this.store.remove(people);
    afterNextRender(
      () => this.host.nativeElement.querySelector<HTMLInputElement>('thead input')?.focus(),
      { injector: this.injector },
    );
  }
  /** 方向鍵在名稱間移動時，屬性面板已打開就跟著換人；停在匯入籃時不搶走面板。 */
  follow(person: DirectoryPersonDto) {
    if (this.store.panel() === 'attributes') this.store.inspect(person);
  }
  check(person: DirectoryPersonDto, checked: boolean) {
    this.store.toggle(person, checked, this.shift);
    this.shift = false;
  }
}
