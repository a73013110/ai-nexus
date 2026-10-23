import {
  ChangeDetectionStrategy,
  Component,
  ElementRef,
  EnvironmentInjector,
  afterNextRender,
  computed,
  inject,
} from '@angular/core';
import type { DirectoryPersonDto } from '@core/api/schema';
import { EmptyState } from '@shared/ui/empty-state';
import { Icon } from '@shared/ui/icon';
import { DirectoryImportStore, parentDn } from './directory-import-store';
import { changedFields } from './directory-outcomes';

const groups = [
  {
    key: 'new',
    title: '新增',
    description: '建立平台帳號（或綁定預建帳號）：一般使用者角色、只開 AD 登入。',
  },
  { key: 'changed', title: '更新', description: '以 AD 的值覆寫有差異的欄位。' },
  { key: 'synced', title: '略過', description: '與 AD 一致，不會寫入。' },
] as const;

/**
 * 匯入籃：跨節點累積的已選人員，依預計結果分成新增、更新、略過三組，可整組清除或逐一移除。
 * 分組依清單當下的狀態，實際結果以預覽時伺服器重新比對為準。
 */
@Component({
  selector: 'nx-directory-basket',
  imports: [EmptyState, Icon],
  changeDetection: ChangeDetectionStrategy.OnPush,
  styleUrl: './directory-basket.scss',
  template: `@if (store.selection().size) {
      <p class="directory-basket-total">
        已選 <strong>{{ store.selection().size }}</strong> / {{ store.limit }} 人
        <button type="button" class="quiet-button" (click)="clearAll()">全部清除</button>
      </p>
      @for (group of sections(); track group.key) {
        <section class="directory-basket-group" [attr.aria-labelledby]="'basket-' + group.key">
          <header>
            <span class="directory-mark" [attr.data-group]="group.key" aria-hidden="true"></span>
            <h4 [id]="'basket-' + group.key">{{ group.title }}</h4>
            <span class="directory-basket-count">{{ group.people.length }} 人</span>
            <button
              type="button"
              class="quiet-button"
              [attr.aria-label]="'清除' + group.title + '的 ' + group.people.length + ' 人'"
              (click)="clearGroup(group.people)"
            >
              清除
            </button>
          </header>
          <p>{{ group.description }}</p>
          <ul>
            @for (person of group.people; track person.sid) {
              <li>
                <button
                  type="button"
                  class="directory-basket-person"
                  [title]="person.dn"
                  (click)="show(person)"
                >
                  <span>{{ person.displayName }}</span>
                  <small [class.is-warning]="person.disabled">{{ note(person) }}</small>
                </button>
                <button
                  type="button"
                  class="icon-button"
                  [attr.aria-label]="'從匯入籃移除 ' + person.displayName"
                  [attr.data-remove]="person.sid"
                  (click)="drop(person)"
                >
                  <nx-icon name="close" />
                </button>
              </li>
            }
          </ul>
        </section>
      }
    } @else {
      <nx-empty-state>
        <nx-icon name="user" />
        <strong>還沒有選人</strong>
        <span>在中間的清單勾選人員；切換節點後，已選的人會留在這裡。</span>
      </nx-empty-state>
    }`,
})
export class DirectoryBasket {
  readonly store = inject(DirectoryImportStore);
  private readonly host = inject<ElementRef<HTMLElement>>(ElementRef);
  // 檢視會把面板換成屬性卡、匯入籃元件隨即銷毀；用環境注入器，換頁後的焦點移動才不會被一起取消。
  private readonly injector = inject(EnvironmentInjector);
  readonly sections = computed(() => {
    const basket = this.store.basket();
    return groups
      .map((group) => ({ ...group, people: basket[group.key] }))
      .filter((group) => group.people.length);
  });

  // 下列動作都會讓按下的按鈕消失；焦點移到合理的下一站，不掉回對話框最前面。

  /** 檢視：面板換成屬性卡，焦點移到卡片標題，報讀器接著讀這個人。 */
  show(person: DirectoryPersonDto) {
    this.store.inspect(person);
    this.focusLater(() => this.document().querySelector('#directory-side h4[tabindex]'));
  }
  /** 移除一人：焦點移到下一位（沒有就上一位）的移除鈕，可以連續移除。 */
  drop(person: DirectoryPersonDto) {
    const buttons = [...this.host.nativeElement.querySelectorAll<HTMLElement>('[data-remove]')];
    const index = buttons.findIndex((button) => button.dataset['remove'] === person.sid);
    const next = (buttons[index + 1] ?? buttons[index - 1])?.dataset['remove'];
    this.store.remove([person]);
    this.focusLater(() =>
      next
        ? this.host.nativeElement.querySelector(`[data-remove="${CSS.escape(next)}"]`)
        : this.headerBox(),
    );
  }
  clearGroup(people: readonly DirectoryPersonDto[]) {
    this.store.remove(people);
    this.focusLater(() => this.headerBox());
  }
  clearAll() {
    this.store.clear();
    this.focusLater(() => this.headerBox());
  }

  /** 清單表頭的全選框：清掉一批人之後最可能接著做的是重新勾選。 */
  private headerBox() {
    return this.document().querySelector('nx-directory-people-table thead input');
  }
  private document() {
    return this.host.nativeElement.ownerDocument;
  }
  private focusLater(target: () => Element | null) {
    afterNextRender(() => (target() as HTMLElement | null)?.focus(), {
      injector: this.injector,
    });
  }

  /** 更新的人寫會改哪些欄位，其餘寫所在節點；AD 停用另外標出（預設不匯入）。 */
  note(person: DirectoryPersonDto) {
    const where = person.changes.length
      ? `${changedFields(person.changes)} 將更新`
      : person.path || parentDn(person.dn);
    return person.disabled ? `${where} · AD 停用` : where;
  }
}
