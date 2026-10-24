import { ChangeDetectionStrategy, Component, computed, input, output, signal } from '@angular/core';
import { ChoiceGroups, type ChoiceGroup, type ChoiceOption } from './choice-groups';
import { SearchField } from './search-field';
import { Select, type SelectOption } from './select';

/** 可挑選的一個人；`group` 是分組名稱（例如主要所屬部門），同組的人排在同一張卡片。 */
export interface BrowsePerson {
  id: string;
  name: string;
  /** 名稱下方的一行，通常是帳號。 */
  detail: string;
  group: string;
}

/**
 * 從整份名單挑人（可多選）：依分組列出全部的人，上方以文字（姓名、帳號、分組）與分組篩選，勾選即選取。
 * 用在「看到才認得出來」的情境（例如把部門同事加入單位）；已知道要找誰時用 `PersonPicker` 搜尋。
 * 名單由呼叫端載入並傳入，元件只負責篩選與呈現；分組依名單中第一次出現的順序排列。
 * 篩選掉的人若已勾選仍保持選取（`ChoiceGroups` 保留群組以外的 id）。
 */
@Component({
  selector: 'nx-person-browser',
  imports: [ChoiceGroups, SearchField, Select],
  changeDetection: ChangeDetectionStrategy.OnPush,
  styleUrl: './person-browser.scss',
  template: `@if (loading() || people().length) {
      <div class="person-browser-filters">
        <nx-search-field
          label="篩選人員"
          placeholder="姓名、帳號或部門"
          [value]="text()"
          [disabled]="disabled() || loading()"
          (valueChange)="text.set($event)"
        />
        <nx-select
          label="分組"
          [options]="groupOptions()"
          [value]="group()"
          [searchable]="groupOptions().length > 8"
          [disabled]="disabled() || loading()"
          (valueChange)="group.set($event)"
        />
      </div>
    }
    @if (loading()) {
      <div class="person-browser-skeleton" role="status">
        @for (width of skeleton; track $index) {
          <span class="skeleton" [style.width.%]="width"></span>
        }
        <span class="sr-only">正在載入名單…</span>
      </div>
    } @else if (groups().length) {
      <nx-choice-groups
        [label]="label()"
        [groups]="groups()"
        [selected]="selected()"
        [disabled]="disabled()"
        (selectedChange)="selectedChange.emit($event)"
      />
    } @else {
      <p class="form-note" role="status">
        {{ people().length ? '沒有符合篩選條件的人。' : emptyText() }}
      </p>
    }`,
})
export class PersonBrowser {
  /** 整份清單的無障礙名稱，例如「可加入的人員」。 */
  readonly label = input.required<string>();
  readonly people = input.required<readonly BrowsePerson[]>();
  readonly selected = input.required<readonly string[]>();
  readonly loading = input(false);
  readonly disabled = input(false);
  /** 名單本身是空的（不是篩選後沒有）時的說明。 */
  readonly emptyText = input('沒有可選的人。');
  readonly selectedChange = output<string[]>();

  readonly text = signal('');
  /** 篩選的分組；空字串為全部。 */
  readonly group = signal('');
  /** 骨架列的寬度（%）。 */
  protected readonly skeleton = [100, 100, 64];

  readonly groupOptions = computed<SelectOption[]>(() => {
    const counts = new Map<string, number>();
    for (const person of this.people())
      counts.set(person.group, (counts.get(person.group) ?? 0) + 1);
    return [
      { value: '', label: `全部（${this.people().length}）` },
      ...[...counts].map(([name, count]) => ({ value: name, label: `${name}（${count}）` })),
    ];
  });
  readonly groups = computed<ChoiceGroup[]>(() => {
    const query = this.text().trim().toLowerCase();
    const only = this.group();
    const groups = new Map<string, { id: string; name: string; options: ChoiceOption[] }>();
    for (const person of this.people()) {
      if (only && person.group !== only) continue;
      if (
        query &&
        !`${person.name}\n${person.detail}\n${person.group}`.toLowerCase().includes(query)
      )
        continue;
      let group = groups.get(person.group);
      if (!group)
        groups.set(person.group, (group = { id: person.group, name: person.group, options: [] }));
      group.options.push({ id: person.id, label: person.name, description: person.detail });
    }
    return [...groups.values()];
  });
}
