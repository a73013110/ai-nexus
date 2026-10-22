import {
  ChangeDetectionStrategy,
  Component,
  ElementRef,
  computed,
  inject,
  signal,
  viewChild,
} from '@angular/core';
import { apiResource } from '@core/api/api-resource';
import type { DirectoryOutcomeDto, DirectoryPersonDto } from '@core/api/schema';
import { safeMessage } from '@core/errors/safe-errors';
import { ViewScope } from '@shared/browser/view-scope';
import { Checkbox } from '@shared/ui/checkbox';
import { CompactDialog } from '@shared/ui/compact-dialog';
import { DataTable, DataTableColumn, TableState, type TableColumn } from '@shared/ui/data-table';
import { DialogHeader } from '@shared/ui/dialog-header';
import { Icon } from '@shared/ui/icon';
import { Notice } from '@shared/ui/notice';
import { SearchField } from '@shared/ui/search-field';
import { StatusBadge } from '@shared/ui/status-badge';
import { AdminUsersStore } from '../admin-users-store';
import { IdentityApi } from '../identity-api';
import { DirectoryOutcomeTable } from './directory-outcome-table';
import {
  DIRECTORY_BATCH_LIMIT,
  type DirectorySelection,
  selectable,
  selectPeople,
  selectsAll,
  stateLabel,
  stateTone,
  summarize,
  writesUser,
} from './directory-outcomes';

type Step = 'select' | 'preview' | 'result';

/**
 * 從 AD 匯入使用者：搜尋（防抖）→ 勾選（跨搜尋累積，最多 100 人）→ 伺服器預覽 → 確認匯入。
 * 已在平台的人也可勾選，匯入時以 AD 資料更新（與登入同一套規則）。全部寫入成功就關閉並以 toast 摘要；
 * 有人未完成時停在結果步驟，讓管理員看到是誰、為什麼。
 */
@Component({
  selector: 'nx-directory-import-dialog',
  imports: [
    Checkbox,
    CompactDialog,
    DataTable,
    DataTableColumn,
    DialogHeader,
    DirectoryOutcomeTable,
    Icon,
    Notice,
    SearchField,
    StatusBadge,
    TableState,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  styleUrl: './directory-import-dialog.scss',
  templateUrl: './directory-import-dialog.html',
})
export class DirectoryImportDialog {
  private readonly api = inject(IdentityApi);
  private readonly store = inject(AdminUsersStore);
  private readonly scope = inject(ViewScope);
  private readonly dialog = viewChild.required<ElementRef<HTMLDialogElement>>('dialog');
  readonly limit = DIRECTORY_BATCH_LIMIT;
  readonly opened = signal(false);
  readonly step = signal<Step>('select');
  readonly search = signal('');
  private readonly query = signal('');
  private readonly typing = signal(false);
  readonly selection = signal<DirectorySelection>(new Map());
  readonly includeDisabled = signal(false);
  readonly outcomes = signal<DirectoryOutcomeDto[]>([]);
  readonly busy = signal(false);
  readonly error = signal('');
  readonly limitNote = signal('');
  readonly columns: TableColumn[] = [
    { id: 'name', label: '人員', hideable: false },
    { id: 'email', label: '信箱' },
    { id: 'path', label: '部門與 OU' },
    { id: 'state', label: '狀態', hideable: false },
  ];
  readonly stateLabel = stateLabel;
  readonly stateTone = stateTone;
  readonly selectable = selectable;
  readonly results = apiResource({
    feature: 'identity.users.import',
    params: () => (this.opened() && this.query() ? this.query() : undefined),
    loader: (search, signal) => this.api.searchDirectory(search, signal),
  });
  readonly people = computed(() => (this.query() ? (this.results.value()?.people ?? []) : []));
  readonly searching = computed(() => this.typing() || this.results.loading());
  readonly selected = computed(() => [...this.selection().values()]);
  readonly allChecked = computed(() => selectsAll(this.selection(), this.people()));
  readonly anySelectable = computed(() => this.people().some(selectable));
  readonly disabledCount = computed(
    () => this.outcomes().filter((item) => item.outcome === 'disabled').length,
  );
  readonly pending = computed(() => this.outcomes().filter(writesUser).length);
  readonly summary = computed(() => summarize(this.outcomes()));
  readonly heading = computed(() =>
    this.step() === 'select'
      ? '選取人員'
      : this.step() === 'preview'
        ? '確認匯入內容'
        : '部分人員未完成',
  );

  open() {
    this.step.set('select');
    this.search.set('');
    this.query.set('');
    this.selection.set(new Map());
    this.includeDisabled.set(false);
    this.outcomes.set([]);
    this.error.set('');
    this.limitNote.set('');
    this.opened.set(true);
    this.dialog().nativeElement.showModal();
  }
  closed() {
    this.opened.set(false);
    this.scope.cancel('directory-search');
  }
  find(value: string) {
    this.search.set(value);
    this.typing.set(true);
    this.scope.later(
      () => {
        this.typing.set(false);
        this.query.set(value.trim());
      },
      300,
      'directory-search',
    );
  }
  toggle(person: DirectoryPersonDto, checked: boolean) {
    this.apply([person], checked);
  }
  toggleAll(checked: boolean) {
    this.apply(this.people(), checked);
  }
  clear() {
    this.selection.set(new Map());
    this.limitNote.set('');
  }
  async preview(includeDisabled = this.includeDisabled()) {
    if (!this.selection().size || this.busy()) return;
    await this.run(async () => {
      const result = await this.api.previewDirectoryImport({
        sids: [...this.selection().keys()],
        includeDisabled,
      });
      this.includeDisabled.set(includeDisabled);
      this.outcomes.set(result.items);
      this.step.set('preview');
    });
  }
  back() {
    this.error.set('');
    this.step.set('select');
  }
  async confirm() {
    if (!this.pending() || this.busy()) return;
    await this.run(async () => {
      const result = await this.api.importDirectoryUsers({
        sids: [...this.selection().keys()],
        includeDisabled: this.includeDisabled(),
      });
      const saved = result.items.filter(writesUser).length;
      this.store.readUsers();
      if (result.items.some((item) => item.outcome === 'failed')) {
        this.outcomes.set(result.items);
        this.step.set('result');
        return;
      }
      this.dialog().nativeElement.close();
      this.store.toast.success(`已從 AD 匯入 ${saved} 位使用者`, summarize(result.items));
    });
  }
  private apply(people: readonly DirectoryPersonDto[], checked: boolean) {
    const { selection, skipped } = selectPeople(this.selection(), people, checked);
    this.selection.set(selection);
    this.limitNote.set(
      skipped ? `一次最多匯入 ${this.limit} 人，另有 ${skipped} 人沒有加入。` : '',
    );
  }
  private async run(work: () => Promise<void>) {
    this.busy.set(true);
    this.error.set('');
    try {
      await work();
    } catch (error) {
      this.error.set(safeMessage(error));
    } finally {
      this.busy.set(false);
    }
  }
}
