import { ChangeDetectionStrategy, Component, computed, input, signal } from '@angular/core';
import { Checkbox } from './checkbox';
import { CopyValue } from './copy-value';
import type { BadgeTone } from './count-badge';
import { SearchField } from './search-field';
import { StatusBadge } from './status-badge';
import { TextHighlight } from './text-highlight';

/**
 * 一個屬性。`key` 是來源系統的名稱（例：LDAP 的 `pwdLastSet`），與 `label` 一起顯示、一起被搜尋；
 * `value` 為 null 或空字串是「沒有值」，預設收起。`badges`（旗標）與 `list`（多值）可與 `value` 並存。
 */
export interface PropertyItem {
  key: string;
  label: string;
  value?: string | null;
  hint?: string;
  badges?: readonly { label: string; tone: BadgeTone }[];
  list?: readonly string[];
  /** 值是識別碼，附複製鈕並以等寬字顯示。 */
  copy?: boolean;
}

export interface PropertyGroup {
  id: string;
  label: string;
  /** 整組共用的說明（例如資料來源的限制），寫一次，不在每個屬性重複。 */
  note?: string;
  items: readonly PropertyItem[];
}

/**
 * 分組的屬性清單（詳情面板、原始屬性檢視）：屬性很多時以分組與篩選取代捲動找欄位。
 * 篩選比對名稱、來源名稱與值並標出相符處；沒有值的屬性預設收起，數量寫在開關上，需要時再展開。
 * 重讀時保留舊屬性，由呼叫端在本元件加 `.is-refreshing` 與 `aria-busy`；首次載入由呼叫端畫骨架。
 */
@Component({
  selector: 'nx-property-list',
  imports: [Checkbox, CopyValue, SearchField, StatusBadge, TextHighlight],
  changeDetection: ChangeDetectionStrategy.OnPush,
  styleUrl: './property-list.scss',
  template: `<div class="ui-props-tools">
      <nx-search-field
        [label]="'篩選' + label()"
        placeholder="屬性名稱或值"
        [maxLength]="64"
        [value]="query()"
        (valueChange)="query.set($event)"
      />
      @if (blankCount()) {
        <nx-checkbox
          class="ui-props-blank-toggle"
          [label]="'顯示沒有值的屬性（' + blankCount() + '）'"
          [checked]="showBlank()"
          (checkedChange)="showBlank.set($event)"
        />
      }
    </div>
    @for (group of visible(); track group.id) {
      <section class="ui-props-group" [attr.aria-labelledby]="id + group.id">
        <h5 [id]="id + group.id">{{ group.label }}</h5>
        @if (group.note) {
          <p class="ui-props-note">{{ group.note }}</p>
        }
        <dl>
          @for (item of group.items; track item.key) {
            <div class="ui-props-item">
              <dt>
                <span><nx-text-highlight [text]="item.label" [query]="query()" /></span>
                <code><nx-text-highlight [text]="item.key" [query]="query()" /></code>
              </dt>
              <dd>
                @if (item.badges?.length) {
                  <span class="ui-props-badges">
                    @for (badge of item.badges; track badge.label) {
                      <nx-status-badge [tone]="badge.tone">{{ badge.label }}</nx-status-badge>
                    }
                  </span>
                }
                @if (item.value && item.copy) {
                  <nx-copy-value [label]="item.label" [value]="item.value" />
                } @else if (item.value) {
                  <span class="ui-props-value"
                    ><nx-text-highlight [text]="item.value" [query]="query()"
                  /></span>
                } @else if (!item.badges?.length && !item.list?.length) {
                  <span class="ui-props-none">—</span>
                }
                @if (item.list?.length) {
                  <ul class="ui-props-list">
                    @for (entry of item.list; track $index) {
                      <li><nx-text-highlight [text]="entry" [query]="query()" /></li>
                    }
                  </ul>
                }
                @if (item.hint) {
                  <small>{{ item.hint }}</small>
                }
              </dd>
            </div>
          }
        </dl>
      </section>
    } @empty {
      <p class="ui-props-empty" role="status">
        {{ query() ? '沒有符合「' + query().trim() + '」的屬性。' : '沒有可顯示的屬性。' }}
      </p>
    }`,
})
export class PropertyList {
  /** 清單的名稱，用於篩選欄與報讀，例如「AD 屬性」。 */
  readonly label = input.required<string>();
  readonly groups = input.required<readonly PropertyGroup[]>();
  readonly query = signal('');
  readonly showBlank = signal(false);
  protected readonly id = 'nx-props-' + ++sequence + '-';

  readonly blankCount = computed(
    () => this.groups().flatMap((group) => group.items.filter(blank)).length,
  );
  readonly visible = computed(() => {
    const words = this.query().trim().toLowerCase();
    const showBlank = this.showBlank() || words.length > 0;
    return this.groups()
      .map((group) => ({
        ...group,
        items: group.items.filter(
          (item) => (showBlank || !blank(item)) && (!words || matches(item, words)),
        ),
      }))
      .filter((group) => group.items.length > 0);
  });
}

let sequence = 0;

function blank(item: PropertyItem) {
  return !item.value && !item.badges?.length && !item.list?.length;
}

function matches(item: PropertyItem, words: string) {
  return [item.label, item.key, item.value, ...(item.badges ?? []).map((badge) => badge.label)]
    .concat(item.list ?? [])
    .some((text) => text?.toLowerCase().includes(words));
}
