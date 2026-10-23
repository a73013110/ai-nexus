import { ChangeDetectionStrategy, Component, input, output } from '@angular/core';
import { uniqueId } from '../browser/unique-id';
import { Card } from './card';
import { Icon } from './icon';
import { Select, type SelectOption } from './select';

/**
 * 偏好設定的一張卡片：標題、選用的一句說明，以及數個 `nx-setting-row`。列與其他投影內容之間的
 * 間距由 host 的 gap 提供，投影內容不必自己留白。
 */
@Component({
  selector: 'nx-setting-group',
  hostDirectives: [Card],
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: { class: 'setting-group', role: 'group', '[attr.aria-labelledby]': 'headingId' },
  styles: `
    :host {
      container: setting-group / inline-size;
      display: flex;
      flex-direction: column;
      gap: var(--p-space-3);
      min-width: 0;
    }
    h2 {
      margin: 0;
      font-size: var(--resource-heading-size);
      font-weight: 600;
    }
    header p {
      margin: var(--p-space-1) 0 0;
      color: var(--secondary);
      font-size: var(--text-caption);
      line-height: 1.65;
    }
  `,
  template: `<header>
      <h2 [id]="headingId">{{ heading() }}</h2>
      @if (description()) {
        <p>{{ description() }}</p>
      }
    </header>
    <ng-content />`,
})
export class SettingGroup {
  readonly heading = input.required<string>();
  readonly description = input('');
  protected readonly headingId = uniqueId('nx-setting-group');
}

/**
 * 一列設定：左邊是名稱與一句說明，右邊是控制項。給 `options` 顯示下拉選單、給 `checked` 顯示開關
 * （無障礙名稱都取自 `label`）；其他控制項（按鈕、狀態）投影進來即可。投影 `[setting-detail]`
 * 的內容排在說明下方（例如狀態原因）。
 */
@Component({
  selector: 'nx-setting-row',
  imports: [Select, Icon],
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: { class: 'setting-row' },
  styles: `
    :host {
      display: flex;
      align-items: center;
      gap: var(--section-gap);
      min-width: 0;
      padding-top: var(--p-space-3);
      border-top: 1px solid var(--line);
    }
    .setting-text {
      display: grid;
      flex: 1;
      gap: var(--p-space-1);
      min-width: 0;
    }
    .setting-label {
      font-size: var(--text-ui);
      font-weight: 550;
    }
    .setting-description,
    .setting-detail {
      color: var(--secondary);
      font-size: var(--text-caption);
      line-height: 1.65;
    }
    .setting-detail:empty {
      display: none;
    }
    .setting-control {
      display: flex;
      flex: none;
      align-items: center;
      justify-content: flex-end;
      gap: var(--p-space-2);
      max-width: 60%;
    }
    .setting-control:empty {
      display: none;
    }
    nx-select {
      width: var(--setting-select-width, 11rem);
    }
    .setting-pending {
      --icon-size: var(--icon-sm);
      color: var(--secondary);
    }
    /* 窄卡片（手機）：控制項貼著說明的第一行，下拉選單縮窄，名稱才不會被擠成一字一行。 */
    @container setting-group (width <= 30rem) {
      :host {
        align-items: flex-start;
        gap: var(--p-space-3);
      }
      nx-select {
        width: var(--setting-select-width-compact, 9rem);
      }
    }
  `,
  template: `<div class="setting-text">
      <span class="setting-label" [id]="labelId">{{ label() }}</span>
      @if (description()) {
        <span class="setting-description" [id]="descriptionId">{{ description() }}</span>
      }
      <span class="setting-detail"><ng-content select="[setting-detail]" /></span>
    </div>
    <div class="setting-control">
      @if (options(); as items) {
        <nx-select
          [label]="label()"
          [options]="items"
          [value]="value()"
          [disabled]="disabled()"
          [loading]="loading()"
          (valueChange)="valueChange.emit($event)"
        />
      } @else if (checked() !== undefined) {
        @if (pending()) {
          <nx-icon name="loading" class="setting-pending" />
        }
        <button
          type="button"
          class="switch"
          role="switch"
          [attr.aria-checked]="checked()"
          [attr.aria-labelledby]="labelId"
          [attr.aria-describedby]="description() ? descriptionId : null"
          [attr.aria-busy]="pending() || null"
          [attr.aria-disabled]="pending() || null"
          [disabled]="disabled()"
          (click)="pending() || checkedChange.emit(!checked())"
        >
          <span></span>
        </button>
      }
      <ng-content />
    </div>`,
})
export class SettingRow {
  readonly label = input.required<string>();
  readonly description = input('');
  /** 下拉選單的選項；與 `checked` 擇一。 */
  readonly options = input<SelectOption[]>();
  readonly value = input('');
  /** 下拉選項讀取中（交給 `nx-select` 的 `loading`）；讀取期間傳 `[]` 當選項，列才不會先藏起來。 */
  readonly loading = input(false);
  readonly valueChange = output<string>();
  /** 開關目前的狀態；`undefined` 表示這列不是開關。 */
  readonly checked = input<boolean>();
  readonly checkedChange = output<boolean>();
  readonly disabled = input(false);
  /**
   * 開關的變更正在儲存（不可還原或很慢的設定）：開關旁轉圈並忽略再次切換；不用 `disabled`，
   * 焦點留在開關上。可還原的設定改用樂觀更新。
   */
  readonly pending = input(false);
  protected readonly labelId = uniqueId('nx-setting-label');
  protected readonly descriptionId = uniqueId('nx-setting-description');
}
