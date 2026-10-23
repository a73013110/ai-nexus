import { ChangeDetectionStrategy, Component, computed, inject } from '@angular/core';
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
        <button type="button" class="quiet-button" (click)="store.clear()">全部清除</button>
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
              (click)="store.remove(group.people)"
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
                  (click)="store.inspect(person)"
                >
                  <span>{{ person.displayName }}</span>
                  <small [class.is-warning]="person.disabled">{{ note(person) }}</small>
                </button>
                <button
                  type="button"
                  class="icon-button"
                  [attr.aria-label]="'從匯入籃移除 ' + person.displayName"
                  (click)="store.remove([person])"
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
  readonly sections = computed(() => {
    const basket = this.store.basket();
    return groups
      .map((group) => ({ ...group, people: basket[group.key] }))
      .filter((group) => group.people.length);
  });

  /** 更新的人寫會改哪些欄位，其餘寫所在節點；AD 停用另外標出（預設不匯入）。 */
  note(person: DirectoryPersonDto) {
    const where = person.changes.length
      ? `${changedFields(person.changes)} 將更新`
      : person.path || parentDn(person.dn);
    return person.disabled ? `${where} · AD 停用` : where;
  }
}
