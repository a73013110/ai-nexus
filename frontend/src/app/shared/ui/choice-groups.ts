import { ChangeDetectionStrategy, Component, computed, input, output } from '@angular/core';
import { Checkbox } from './checkbox';

export interface ChoiceOption {
  id: string;
  label: string;
  /** A second line under the label, e.g. the stable id. */
  description?: string;
}
export interface ChoiceGroup {
  id: string;
  name: string;
  options: readonly ChoiceOption[];
}
let sequence = 0;

/**
 * A multi-select list split by category: each category is its own framed card with its name, the
 * selected count and one "select all / none" button, and the cards share the width (two columns
 * where they fit, one on narrow screens). No outer fieldset: the cards are the sections.
 * `selected` may hold ids outside these groups; changes keep them and emit the whole selection.
 */
@Component({
  selector: 'nx-choice-groups',
  imports: [Checkbox],
  changeDetection: ChangeDetectionStrategy.OnPush,
  styleUrl: './choice-groups.scss',
  host: { role: 'group', '[attr.aria-label]': 'label()' },
  template: `@for (group of summary(); track group.id) {
    <section class="choice-group" role="group" [attr.aria-labelledby]="id + '-' + group.id">
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
  /** Shows the per-group select all / none button. */
  readonly batch = input(true);
  readonly selectedChange = output<string[]>();
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
  /** Adds missing ids at the end or removes them; the rest keeps its order. */
  private change(ids: readonly string[], checked: boolean) {
    const chosen = this.chosen();
    this.selectedChange.emit(
      checked
        ? [...this.selected(), ...ids.filter((id) => !chosen.has(id))]
        : this.selected().filter((id) => !ids.includes(id)),
    );
  }
}
