import { Notice } from '@shared/ui/notice';
import {
  ChangeDetectionStrategy,
  Component,
  ElementRef,
  computed,
  inject,
  input,
  signal,
  viewChild,
} from '@angular/core';
import { RouterLink } from '@angular/router';
import { Icon } from '@shared/ui/icon';
import { anchoredPopover } from '@shared/browser/anchored-popover';
import { positionPopover } from '@shared/browser/popover-position';
import { KnowledgeSelection } from './knowledge-selection';
import { Checkbox } from '@shared/ui/checkbox';
import { SearchField } from '@shared/ui/search-field';

@Component({
  selector: 'nx-knowledge-picker',
  imports: [Notice, Icon, RouterLink, Checkbox, SearchField],
  changeDetection: ChangeDetectionStrategy.OnPush,
  styleUrl: './knowledge-picker.scss',
  template: `<button
      #trigger
      type="button"
      class="knowledge-trigger"
      [class.selected]="selection.ids().length"
      aria-label="選取對話知識來源"
      aria-haspopup="dialog"
      [attr.aria-expanded]="opened()"
      [disabled]="disabled() || selection.saving()"
      (click)="open()"
    >
      <nx-icon name="library" /><span>{{
        selection.ids().length ? '來源 ' + selection.ids().length : '來源'
      }}</span>
    </button>
    <div
      #panel
      class="popover-panel knowledge-picker-panel ui-density-compact"
      popover="auto"
      role="dialog"
      aria-label="對話知識來源"
      (toggle)="popover.sync($event)"
    >
      <div class="picker-heading"><strong>知識來源</strong><span>最多 3 個 · 選取即儲存</span></div>
      <nx-search-field
        label="搜尋可用知識庫"
        placeholder="尋找知識庫"
        [value]="query()"
        (valueChange)="query.set($event)"
      />
      @if (selection.error()) {
        <nx-notice tone="danger" [message]="selection.error()" />
      }
      @if (selection.loadFailed()) {
        <button
          type="button"
          class="secondary-button"
          [disabled]="selection.saving()"
          (click)="selection.reload()"
        >
          重新載入知識來源
        </button>
      }
      <div class="knowledge-choices">
        @for (collection of visible(); track collection.resource.id) {
          <nx-checkbox
            [label]="collection.resource.name"
            [description]="
              collection.readyDocuments +
              ' 份文件可查詢' +
              (collection.resource.isOwner ? '' : ' · 已共用')
            "
            [checked]="selection.ids().includes(collection.resource.id)"
            [disabled]="
              disabled() ||
              selection.saving() ||
              selection.loadFailed() ||
              (selection.ids().length >= 3 && !selection.ids().includes(collection.resource.id))
            "
            (checkedChange)="selection.toggle(collection.resource.id, $event)"
          />
        } @empty {
          <p class="form-note">目前沒有符合的知識庫。</p>
        }
      </div>
      @for (id of unavailable(); track id) {
        <button
          class="quiet-button error-note"
          [disabled]="selection.saving() || disabled()"
          (click)="selection.toggle(id, false)"
        >
          移除已失效的來源<nx-icon name="close" />
        </button>
      }
      <a routerLink="/knowledge" class="quiet-button" (click)="popover.hide()"
        >管理知識庫<nx-icon name="chevron"
      /></a>
    </div>`,
})
export class KnowledgePicker {
  readonly selection = inject(KnowledgeSelection);
  readonly disabled = input(false);
  readonly query = signal('');
  readonly visible = computed(() =>
    this.selection
      .collections()
      .filter((item) => item.resource.name.toLowerCase().includes(this.query().toLowerCase())),
  );
  readonly unavailable = computed(() =>
    this.selection
      .ids()
      .filter((item) => !this.selection.collections().some((card) => card.resource.id === item)),
  );
  readonly trigger = viewChild.required<ElementRef<HTMLButtonElement>>('trigger');
  readonly panel = viewChild.required<ElementRef<HTMLDivElement>>('panel');
  private from: HTMLElement | null = null;
  protected readonly popover = anchoredPopover({
    panel: () => this.panel().nativeElement,
    place: (panel) =>
      positionPopover(this.from ?? this.trigger().nativeElement, panel, 360, 'left'),
  });
  readonly opened = this.popover.opened;
  /** 當另一個控制項（窄版輸入框的工具選單）要求時，在 `from` 旁開啟。 */
  open(from?: HTMLElement) {
    if (this.opened()) {
      this.popover.hide();
      return;
    }
    this.from = from ?? null;
    this.popover.show();
    this.panel().nativeElement.querySelector<HTMLInputElement>('input[type=search]')?.focus();
    void this.selection.initialize();
  }
}
