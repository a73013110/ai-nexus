import { RefreshButton } from '@shared/ui/refresh-button';
import {
  ChangeDetectionStrategy,
  Component,
  ElementRef,
  computed,
  inject,
  signal,
  viewChild,
} from '@angular/core';
import type { DirectoryOutcomeDto } from '@core/api/schema';
import { safeMessage } from '@core/errors/safe-errors';
import { Checkbox } from '@shared/ui/checkbox';
import { CompactDialog } from '@shared/ui/compact-dialog';
import { DialogHeader } from '@shared/ui/dialog-header';
import { Notice } from '@shared/ui/notice';
import { PendingButton } from '@shared/ui/pending-button';
import { StatGrid, StatTile } from '@shared/ui/stat-tile';
import { TreeView } from '@shared/ui/tree-view';
import { ViewSwitch, type ViewOption } from '@shared/ui/view-switch';
import { AdminUsersStore } from '../admin-users-store';
import { IdentityApi } from '../identity-api';
import { directoryImportTour } from '../identity-tour';
import { DirectoryBasket } from './directory-basket';
import { DirectoryImportStore } from './directory-import-store';
import { DirectoryOutcomeTable } from './directory-outcome-table';
import { fieldLabel, summarize, tally, writesUser } from './directory-outcomes';
import { DirectoryPeopleTable } from './directory-people-table';
import { DirectoryPersonCard } from './directory-person-card';

type Step = 'select' | 'preview' | 'result';

/**
 * 從 AD 匯入使用者：依 OU 樹瀏覽節點 → 勾選人員（跨節點累積，最多 100 人）→ 伺服器預覽 → 確認匯入 → 結果。
 * 已在平台的人也可勾選，匯入時以 AD 資料更新（與登入同一套規則）。結果步驟可繼續匯入別的節點；
 * 選取狀態放在 {@link DirectoryImportStore}，三個窗格共用。
 */
@Component({
  selector: 'nx-directory-import-dialog',
  imports: [
    RefreshButton,
    Checkbox,
    CompactDialog,
    DialogHeader,
    DirectoryBasket,
    DirectoryOutcomeTable,
    DirectoryPeopleTable,
    DirectoryPersonCard,
    Notice,
    PendingButton,
    StatGrid,
    StatTile,
    TreeView,
    ViewSwitch,
  ],
  providers: [DirectoryImportStore],
  changeDetection: ChangeDetectionStrategy.OnPush,
  styleUrl: './directory-import-dialog.scss',
  templateUrl: './directory-import-dialog.html',
})
export class DirectoryImportDialog {
  private readonly api = inject(IdentityApi);
  private readonly users = inject(AdminUsersStore);
  readonly store = inject(DirectoryImportStore);
  private readonly dialog = viewChild.required<ElementRef<HTMLDialogElement>>('dialog');
  readonly step = signal<Step>('select');
  readonly includeDisabled = signal(false);
  readonly outcomes = signal<DirectoryOutcomeDto[]>([]);
  /** 進行中的寫入：重新預覽與匯入各自的按鈕轉圈，兩者都擋下關閉與其他操作。 */
  readonly task = signal<'preview' | 'import' | null>(null);
  readonly busy = computed(() => this.task() !== null);
  readonly error = signal('');
  /** 預覽的那一批人；確認時只匯入這一批，預覽中途改了勾選也不會匯入沒看過的人。 */
  private previewed: string[] = [];

  readonly tally = computed(() => tally(this.outcomes()));
  readonly disabledCount = computed(
    () =>
      this.outcomes().filter(
        (item) => item.outcome === 'disabled' || (item.directoryDisabled && writesUser(item)),
      ).length,
  );
  readonly pending = computed(() => this.outcomes().filter(writesUser).length);
  readonly failed = computed(() => this.outcomes().some((item) => item.outcome === 'failed'));
  readonly summary = computed(() => summarize(this.outcomes()));
  readonly skippedNote = computed(
    () => summarize(this.outcomes().filter((item) => !writesUser(item))) || '沒有略過的人',
  );
  /** 「更新」會改到哪些欄位（去重），寫在統計卡的說明。 */
  readonly updatedFields = computed(() => {
    const labels = new Set<string>();
    for (const item of this.outcomes())
      if (item.outcome === 'update')
        for (const change of item.changes) labels.add(fieldLabel(change.field));
    return [...labels].join('、');
  });
  readonly tour = computed(() => directoryImportTour({ step: this.step() }));
  readonly heading = computed(() =>
    this.step() === 'select' ? '選取人員' : this.step() === 'preview' ? '預覽變更' : '完成',
  );
  readonly panels = computed<ViewOption[]>(() => [
    { value: 'basket', label: `匯入籃 ${this.store.selection().size}` },
    { value: 'attributes', label: 'AD 屬性' },
  ]);
  readonly footNote = computed(() => {
    const size = this.store.selection().size;
    return size
      ? `已選 ${size} 人，來自 ${this.store.sources()} 個節點；可切換節點繼續勾選。`
      : `一次最多 ${this.store.limit} 人；切換節點時已選的人會保留。`;
  });

  open() {
    this.previewed = [];
    this.step.set('select');
    this.includeDisabled.set(false);
    this.outcomes.set([]);
    this.error.set('');
    this.store.open();
    this.dialog().nativeElement.showModal();
  }
  closed() {
    this.store.close();
  }
  async preview(includeDisabled = this.includeDisabled()) {
    if (!this.store.selection().size || this.busy()) return;
    const sids =
      includeDisabled === this.includeDisabled() || !this.previewed.length
        ? [...this.store.selection().keys()]
        : this.previewed;
    await this.run('preview', async () => {
      const result = await this.api.previewDirectoryImport({ sids, includeDisabled });
      this.previewed = sids;
      this.includeDisabled.set(includeDisabled);
      this.outcomes.set(result.items);
      this.step.set('preview');
    });
  }
  back() {
    // 部分失敗後返回：成功的人狀態已變，重讀清單，匯入籃的分組才會跟著更新。
    if (this.step() === 'result') this.store.people.reload();
    this.error.set('');
    this.previewed = [];
    this.step.set('select');
  }
  async confirm() {
    if (!this.pending() || this.busy()) return;
    await this.run('import', async () => {
      const result = await this.api.importDirectoryUsers({
        sids: this.previewed,
        includeDisabled: this.includeDisabled(),
      });
      this.users.changed();
      this.outcomes.set(result.items);
      this.step.set('result');
    });
  }
  /** 結果步驟的「繼續匯入」：清空匯入籃、重讀目前節點（狀態已變），回到選取。 */
  again() {
    this.store.clear();
    this.outcomes.set([]);
    this.includeDisabled.set(false);
    this.back();
  }
  private async run(task: 'preview' | 'import', work: () => Promise<void>) {
    this.task.set(task);
    this.error.set('');
    try {
      await work();
    } catch (error) {
      this.error.set(safeMessage(error));
    } finally {
      this.task.set(null);
    }
  }
}
