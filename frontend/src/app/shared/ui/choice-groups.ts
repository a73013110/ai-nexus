import { ChangeDetectionStrategy, Component, computed, input, output } from '@angular/core';
import { Checkbox } from './checkbox';

export interface ChoiceOption {
  id: string;
  label: string;
  /** 標籤下方的第二行，例如穩定的 id。 */
  description?: string;
}
export interface ChoiceGroup {
  id: string;
  name: string;
  options: readonly ChoiceOption[];
}
let sequence = 0;

/**
 * 依分類拆開的多選列表：每個分類是一張有框的卡片，附上名稱、已選數量，以及一個「全選／全不選」按鈕。
 * 小卡片共用一列（能放下時為兩欄，窄螢幕為一欄）；選項多的卡片獨佔一列，讓選項排成多欄而不是拉成長條。
 * 沒有外層 fieldset：卡片本身就是區段。
 * `selected` 可能含有這些群組以外的 id；變更時會保留它們，並送出完整的選取內容。
 */
@Component({
  selector: 'nx-choice-groups',
  imports: [Checkbox],
  changeDetection: ChangeDetectionStrategy.OnPush,
  styleUrl: './choice-groups.scss',
  host: { role: 'group', '[attr.aria-label]': 'label()' },
  template: `@for (group of summary(); track group.id) {
    <section
      class="choice-group"
      role="group"
      [class.is-wide]="group.options.length > wideAfter"
      [attr.aria-labelledby]="id + '-' + group.id"
    >
      <header class="choice-group-head">
        <h3 [id]="id + '-' + group.id">{{ group.name }}</h3>
        <span class="choice-group-count" aria-hidden="true"
          >{{ group.checked }}/{{ group.options.length }}</span
        >
        <span class="sr-only">已選 {{ group.checked }} 項，共 {{ group.options.length }} 項</span>
        @if (batch() && group.options.length > 1) {
          <button
            type="button"
            class="quiet-button choice-group-batch"
            [attr.aria-label]="(group.all ? '全不選：' : '全選：') + group.name"
            [disabled]="disabled()"
            (click)="setAll(group.options, !group.all)"
          >
            {{ group.all ? '全不選' : '全選' }}
          </button>
        }
      </header>
      <div class="choice-group-options">
        @for (option of group.options; track option.id) {
          <nx-checkbox
            [label]="option.label"
            [description]="option.description ?? ''"
            [checked]="chosen().has(option.id)"
            [disabled]="disabled()"
            (checkedChange)="toggle(option.id, $event)"
          />
        }
      </div>
    </section>
  }`,
})
export class ChoiceGroups {
  readonly id = `nx-choice-groups-${++sequence}`;
  /** Accessible name of the whole list, e.g. 「授予功能」. */
  readonly label = input.required<string>();
  readonly groups = input.required<readonly ChoiceGroup[]>();
  readonly selected = input.required<readonly string[]>();
  readonly disabled = input(false);
  /** 顯示每個群組的全選／全不選按鈕。 */
  readonly batch = input(true);
  readonly selectedChange = output<string[]>();
  /** 超過這個數量的卡片獨佔一列：半欄寬只排得下一欄選項，再多就會比旁邊的卡片長出一截。 */
  readonly wideAfter = 6;
  readonly chosen = computed(() => new Set(this.selected()));
  readonly summary = computed(() => {
    const chosen = this.chosen();
    return this.groups()
      .filter((group) => group.options.length > 0)
      .map((group) => {
        const checked = group.options.filter((option) => chosen.has(option.id)).length;
        return { ...group, checked, all: checked === group.options.length };
      });
  });
  toggle(id: string, checked: boolean) {
    this.change([id], checked);
  }
  setAll(options: readonly ChoiceOption[], checked: boolean) {
    this.change(
      options.map((option) => option.id),
      checked,
    );
  }
  /** 缺少的 id 加到末端、或移除；其餘保持原順序。 */
  private change(ids: readonly string[], checked: boolean) {
    const chosen = this.chosen();
    this.selectedChange.emit(
      checked
        ? [...this.selected(), ...ids.filter((id) => !chosen.has(id))]
        : this.selected().filter((id) => !ids.includes(id)),
    );
  }
}
