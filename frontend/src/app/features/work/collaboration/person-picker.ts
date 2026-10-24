import {
  ChangeDetectionStrategy,
  Component,
  ElementRef,
  afterRenderEffect,
  computed,
  input,
  linkedSignal,
  output,
  signal,
  viewChild,
} from '@angular/core';
import type { DirectoryUserDto } from '@core/api/schema';
import { anchoredPopover } from '@shared/browser/anchored-popover';
import { nameInitial } from '@shared/browser/format';
import { positionPopover } from '@shared/browser/popover-position';
import { rovingIndex } from '@shared/browser/roving-index';
import { uniqueId } from '@shared/browser/unique-id';
import { Icon } from '@shared/ui/icon';
import { TextHighlight } from '@shared/ui/text-highlight';
import type { DirectorySearch } from './directory-search';

/**
 * 人員選擇器：搜尋欄＋浮在下方的結果（combobox），查詢狀態由呼叫端的 `directorySearch` 管理。
 * 焦點一直留在輸入框，方向鍵移動、Enter 選取、Esc 收起；選取後要不要清空由呼叫端決定
 * （連續加入多人的清單通常成功後才清）。
 * `nameHint` 有值時結果最後多一列「使用姓名」，給還沒有帳號的人（例如新進員工的卡片）。
 * `disabled` 只設 `readonly`＋`aria-disabled`，不設原生 disabled：送出中（`pendingId`）焦點仍在
 * 輸入框，清單才留得住那一列的轉圈。
 */
@Component({
  selector: 'nx-person-picker',
  imports: [Icon, TextHighlight],
  changeDetection: ChangeDetectionStrategy.OnPush,
  styleUrl: './person-picker.scss',
  host: { '[class.is-disabled]': 'disabled() && !pendingId()' },
  template: `<div #anchor class="person-picker-field">
      <nx-icon
        [name]="search().loading() ? 'loading' : 'search'"
        [class.is-busy]="search().loading()"
      /><input
        #field
        type="text"
        role="combobox"
        autocomplete="off"
        aria-autocomplete="list"
        [attr.aria-label]="label()"
        [attr.aria-expanded]="opened()"
        [attr.aria-controls]="id + '-list'"
        [attr.aria-activedescendant]="opened() && active() >= 0 ? id + '-' + active() : null"
        [attr.aria-busy]="search().loading() || null"
        [attr.aria-disabled]="disabled() || null"
        [placeholder]="placeholder()"
        [value]="search().text()"
        [readOnly]="disabled()"
        [maxLength]="120"
        (input)="type(field.value)"
        (focus)="focused.set(true)"
        (blur)="focused.set(false)"
        (keydown)="key($event)"
      />
      @if (search().text() && !disabled()) {
        <button
          type="button"
          class="icon-button"
          [attr.aria-label]="'清除' + label()"
          (click)="clear(field)"
        >
          <nx-icon name="close" />
        </button>
      }
    </div>
    <!-- 面板上按下不搶走輸入框的焦點；選項的點選由各列處理。 -->
    <div
      #panel
      popover="manual"
      class="popover-panel person-picker-panel"
      (mousedown)="$event.preventDefault()"
    >
      @if (!search().results().length) {
        <!-- 防抖等待與查詢途中還沒有答案，不先說「沒有符合」。 -->
        <p class="person-picker-empty">
          {{
            search().loading()
              ? '正在搜尋…'
              : search().error()
                ? '無法搜尋使用者，請稍後再試。'
                : emptyText()
          }}
        </p>
      }
      <div role="listbox" [id]="id + '-list'" [attr.aria-label]="label()">
        @for (user of search().results(); track user.id; let i = $index) {
          @let pending = pendingId() === user.id;
          <button
            type="button"
            role="option"
            tabindex="-1"
            class="person-picker-option"
            [id]="id + '-' + i"
            [class.is-active]="active() === i"
            [attr.aria-selected]="active() === i"
            [attr.aria-busy]="pending || null"
            (pointermove)="active.set(i)"
            (click)="choose(i)"
          >
            <span class="avatar" aria-hidden="true">{{ initial(user.displayName) }}</span>
            <span class="person-picker-text"
              ><strong><nx-text-highlight [text]="user.displayName" [query]="query()" /></strong
              ><small><nx-text-highlight [text]="user.account" [query]="query()" /></small
            ></span>
            @if (pending) {
              <nx-icon name="loading" class="person-picker-pending" />
            }
          </button>
        }
        @if (namable()) {
          @let i = search().results().length;
          <button
            type="button"
            role="option"
            tabindex="-1"
            class="person-picker-option is-name"
            [id]="id + '-' + i"
            [class.is-active]="active() === i"
            [attr.aria-selected]="active() === i"
            (pointermove)="active.set(i)"
            (click)="choose(i)"
          >
            <span class="avatar" aria-hidden="true"><nx-icon name="plus" /></span>
            <span class="person-picker-text"
              ><strong>使用姓名「{{ search().text().trim() }}」</strong
              ><small>{{ nameHint() }}</small></span
            >
          </button>
        }
      </div>
    </div>
    <p class="sr-only" role="status">{{ status() }}</p>`,
})
export class PersonPicker {
  readonly search = input.required<DirectorySearch>();
  readonly label = input.required<string>();
  readonly placeholder = input('輸入至少兩個字，搜尋姓名或帳號');
  readonly emptyText = input('沒有符合的帳號，或使用者尚未登入過平台。');
  /** 「使用姓名」那一列的說明；空字串表示不提供只填姓名。 */
  readonly nameHint = input('');
  readonly disabled = input(false);
  /** 正在加入的使用者：那一列顯示轉圈，其他選取先擋下。 */
  readonly pendingId = input<string | null>(null);
  readonly picked = output<DirectoryUserDto>();
  readonly named = output<string>();

  readonly id = uniqueId('nx-person-picker');
  protected readonly initial = nameInitial;
  readonly focused = signal(false);
  /** Esc 收起後，直到再次輸入前不自動打開。 */
  private readonly dismissed = signal(false);
  readonly query = computed(() => this.search().text().trim());
  readonly namable = computed(
    () => !!this.nameHint() && this.search().searchable() && !this.search().loading(),
  );
  private readonly count = computed(
    () => this.search().results().length + (this.namable() ? 1 : 0),
  );
  /** 結果換了就回到第一列。 */
  readonly active = linkedSignal<readonly unknown[], number>({
    source: () => [this.search().results(), this.namable()] as const,
    computation: () => (this.count() ? 0 : -1),
  });
  private readonly show = computed(
    () =>
      this.focused() &&
      !this.dismissed() &&
      this.search().searchable() &&
      (!this.disabled() || !!this.pendingId()),
  );
  readonly status = computed(() =>
    !this.show() || this.search().loading()
      ? ''
      : this.search().results().length
        ? `${this.search().results().length} 位符合的使用者`
        : this.emptyText(),
  );
  private readonly field = viewChild.required<ElementRef<HTMLInputElement>>('field');
  private readonly anchor = viewChild.required<ElementRef<HTMLElement>>('anchor');
  private readonly panel = viewChild.required<ElementRef<HTMLElement>>('panel');
  private readonly popover = anchoredPopover({
    panel: () => this.panel().nativeElement,
    place: (panel) => positionPopover(this.anchor().nativeElement, panel, 280, 'left', 320),
  });
  readonly opened = this.popover.opened;

  constructor() {
    afterRenderEffect(() => {
      const show = this.show();
      this.search().results();
      if (show && !this.opened()) this.popover.show();
      else if (!show && this.opened()) this.popover.hide();
      else this.popover.reposition();
    });
  }

  focus() {
    this.field().nativeElement.focus();
  }
  type(value: string) {
    this.dismissed.set(false);
    this.search().find(value);
  }
  clear(field: HTMLInputElement) {
    this.search().clear();
    field.focus();
  }
  choose(index: number) {
    if (this.disabled() || this.pendingId()) return;
    const user = this.search().results()[index];
    if (user) this.picked.emit(user);
    else if (index === this.search().results().length && this.namable())
      this.named.emit(this.query());
  }
  key(event: KeyboardEvent) {
    if (event.isComposing) return;
    if (!this.opened()) {
      if (event.key === 'ArrowDown' && this.dismissed()) {
        event.preventDefault();
        this.dismissed.set(false);
      }
      return;
    }
    if (event.key === 'Escape') {
      // 只收起清單；不讓對話框把 Esc 當成關閉。
      event.preventDefault();
      event.stopPropagation();
      this.dismissed.set(true);
      return;
    }
    if (event.key === 'Enter') {
      event.preventDefault();
      if (this.active() >= 0) this.choose(this.active());
      return;
    }
    if (!['ArrowDown', 'ArrowUp'].includes(event.key) || !this.count()) return;
    event.preventDefault();
    this.active.set(rovingIndex(event.key, this.active(), this.count())!);
    requestAnimationFrame(() =>
      document.getElementById(`${this.id}-${this.active()}`)?.scrollIntoView({ block: 'nearest' }),
    );
  }
}
