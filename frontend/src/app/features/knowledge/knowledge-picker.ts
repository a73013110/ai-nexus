import { IssueCode } from '../../shared/ui/issue-code';
import {
  ChangeDetectionStrategy,
  Component,
  DestroyRef,
  ElementRef,
  computed,
  inject,
  input,
  signal,
  viewChild,
} from '@angular/core';
import { RouterLink } from '@angular/router';
import { Icon } from '../../shared/ui/icon';
import { positionPopover } from '../../shared/browser/popover-position';
import { KnowledgeSelection } from './knowledge-selection';
import { Checkbox } from '../../shared/ui/checkbox';
import { SearchField } from '../../shared/ui/search-field';

@Component({
  selector: 'nx-knowledge-picker',
  imports: [IssueCode,Icon, RouterLink, Checkbox, SearchField],
  changeDetection: ChangeDetectionStrategy.OnPush,
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
      class="knowledge-picker-panel"
      popover="auto"
      role="dialog"
      aria-label="對話知識來源"
      (toggle)="opened.set($any($event).newState === 'open')"
    >
      <div class="picker-heading"><strong>知識來源</strong><span>最多 3 個 · 選取即儲存</span></div>
      <nx-search-field
        label="搜尋可用知識庫"
        placeholder="尋找知識庫"
        [value]="query()"
        (valueChange)="query.set($event)"
      />
      @if (selection.error()) {
        <p class="error-note" role="alert">{{ selection.error() }}<nx-issue-code [message]="selection.error()" /></p>
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
      <a routerLink="/knowledge" class="quiet-button" (click)="panel.hidePopover()"
        >管理知識庫<nx-icon name="chevron"
      /></a>
    </div>`,
})
export class KnowledgePicker {
  readonly selection = inject(KnowledgeSelection);
  readonly disabled = input(false);
  readonly query = signal('');
  readonly opened = signal(false);
  readonly visible = computed(() =>
    this.selection
      .collections()
      .filter((x) => x.resource.name.toLowerCase().includes(this.query().toLowerCase())),
  );
  readonly unavailable = computed(() =>
    this.selection
      .ids()
      .filter((x) => !this.selection.collections().some((c) => c.resource.id === x)),
  );
  readonly trigger = viewChild.required<ElementRef<HTMLButtonElement>>('trigger');
  readonly panel = viewChild.required<ElementRef<HTMLDivElement>>('panel');
  constructor() {
    const update = () => {
      if (this.opened())
        positionPopover(this.trigger().nativeElement, this.panel().nativeElement, 360, 'left');
    };
    window.addEventListener('resize', update);
    window.addEventListener('scroll', update, true);
    inject(DestroyRef).onDestroy(() => {
      window.removeEventListener('resize', update);
      window.removeEventListener('scroll', update, true);
    });
  }
  open() {
    const panel = this.panel().nativeElement;
    if (this.opened()) {
      panel.hidePopover();
      return;
    }
    panel.showPopover();
    this.opened.set(true);
    positionPopover(this.trigger().nativeElement, panel, 360, 'left');
    panel.querySelector<HTMLInputElement>('input[type=search]')?.focus();
    void this.selection.initialize();
  }
}
