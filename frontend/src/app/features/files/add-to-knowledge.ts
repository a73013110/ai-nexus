import { apiResource } from '../../core/api/api-resource';
import { Notice } from '../../shared/ui/notice';
import { CompactDialog } from '../../shared/ui/compact-dialog';
import {
  ChangeDetectionStrategy,
  Component,
  ElementRef,
  computed,
  inject,
  output,
  signal,
  viewChild,
  linkedSignal,
} from '@angular/core';
import { RouterLink } from '@angular/router';
import type { CollectionDto, LibraryFileDto } from '../../core/api/schema';
import { KnowledgeApi } from '../knowledge/knowledge-api';
import { ViewScope } from '../../shared/browser/view-scope';
import { Icon } from '../../shared/ui/icon';
import { Select } from '../../shared/ui/select';

@Component({
  selector: 'nx-add-to-knowledge',
  imports: [Notice, CompactDialog, Icon, Select, RouterLink],
  providers: [ViewScope],
  changeDetection: ChangeDetectionStrategy.OnPush,
  styleUrl: './add-to-knowledge.scss',
  template: `<dialog
    nxCompactDialog
    #dialog
    class="platform-dialog"
    aria-label="加入知識庫"
    (cancel)="saving() && $event.preventDefault()"
  >
    <div class="dialog-scroll">
      <div class="dialog-heading">
        <h2>加入知識庫</h2>
        <button
          class="icon-button"
          aria-label="關閉加入知識庫"
          [disabled]="saving()"
          (click)="dialog.close()"
        >
          <nx-icon name="close" />
        </button>
      </div>
      <p class="knowledge-file-label">
        <nx-icon [name]="file()?.file?.isImage ? 'image' : 'document'" /><strong>{{
          file()?.file?.fileName
        }}</strong>
      </p>
      <p class="form-note">
        共用原檔並建立檢索索引。加入後，該知識庫的成員可以閱讀原檔與查詢內容。
      </p>
      @if (error()) {
        <nx-notice tone="danger" [message]="error()" />
      }
      @if (loading()) {
        <p role="status">正在載入可編輯的知識庫…</p>
      } @else if (!choices().length) {
        <p>目前沒有可加入的知識庫。</p>
        <a class="secondary-button" routerLink="/knowledge" (click)="dialog.close()"
          >前往建立知識庫</a
        >
      } @else {
        <nx-select
          label="目標知識庫"
          [options]="choices()"
          [value]="selected()"
          (valueChange)="selected.set($event)"
        />
        <div class="dialog-actions">
          <button class="secondary-button" [disabled]="saving()" (click)="dialog.close()">
            取消</button
          ><button class="primary-button" [disabled]="saving() || !selected()" (click)="save()">
            {{ saving() ? '正在加入…' : '加入並建立索引' }}
          </button>
        </div>
      }
    </div>
  </dialog>`,
})
export class AddToKnowledge {
  private readonly api = inject(KnowledgeApi);
  private readonly scope = inject(ViewScope);
  private readonly dialog = viewChild.required<ElementRef<HTMLDialogElement>>('dialog');
  readonly file = signal<LibraryFileDto | null>(null);
  /** Each opening reads the collections again; nothing is read before the first one. */
  private readonly opened = signal(0);
  private readonly collectionsRead = apiResource({
    params: () => this.opened() || undefined,
    loader: () => this.api.collections(),
  });
  readonly collections = computed<CollectionDto[]>(() => this.collectionsRead.value() ?? []);
  readonly selected = linkedSignal(
    () => this.collections().find((value) => value.resource.canEdit)?.resource.id || '',
  );
  readonly loading = this.collectionsRead.refreshing;
  readonly saving = signal(false);
  readonly saveError = signal('');
  readonly error = computed(() => this.saveError() || this.collectionsRead.error());
  readonly added = output<void>();
  readonly choices = computed(() =>
    this.collections()
      .filter((value) => value.resource.canEdit)
      .map((value) => ({
        value: value.resource.id,
        label: value.resource.name,
        description: value.resource.isOwner ? '私人或由你共用的知識庫' : '共用知識庫 · 成員可閱讀',
      })),
  );
  open(file: LibraryFileDto) {
    this.file.set(file);
    this.saveError.set('');
    this.opened.update((value) => value + 1);
    this.dialog().nativeElement.showModal();
  }
  async save() {
    const file = this.file();
    if (!file || this.saving() || !this.selected()) return;
    const valid = this.scope.guard();
    this.saving.set(true);
    this.saveError.set('');
    try {
      await this.api.add(this.selected(), file.file.id);
      if (valid()) {
        this.dialog().nativeElement.close();
        this.added.emit();
      }
    } catch (error) {
      if (valid()) this.saveError.set(this.scope.message(error));
    } finally {
      if (valid()) this.saving.set(false);
    }
  }
}
