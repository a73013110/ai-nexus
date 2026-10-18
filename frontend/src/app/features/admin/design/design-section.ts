import { ChangeDetectionStrategy, Component, input } from '@angular/core';

/**
 * 元件工作台的一筆：標題列（名稱、一行用途、來源元件），
 * 與一個有框的舞台，舞台中放即時範例。以 `[design-notes]` 投影的說明放在舞台下方，
 * 頁面的索引連結到 `id`。
 */
@Component({
  selector: 'nx-design-section',
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: { class: 'design-section', '[attr.aria-labelledby]': "id() + '-title'", role: 'region' },
  template: `<header>
      <h3 [id]="id() + '-title'" tabindex="-1">{{ title() }}</h3>
      @if (api()) {
        <code>{{ api() }}</code>
      }
      @if (description()) {
        <p>{{ description() }}</p>
      }
    </header>
    <div class="design-stage" [class.is-stacked]="stacked()"><ng-content /></div>
    <ng-content select="[design-notes]" />`,
  styles: `
    :host {
      display: block;
      scroll-margin-top: var(--p-space-4);
    }
    header {
      display: flex;
      flex-wrap: wrap;
      align-items: baseline;
      column-gap: var(--p-space-2);
      margin-bottom: var(--p-space-2);
    }
    h3 {
      margin: 0;
      font-size: var(--text-ui);
      font-weight: 600;
      outline-offset: 2px;
    }
    code {
      padding: 0 var(--p-space-1);
      border-radius: var(--p-radius-sm);
      background: var(--hover);
      color: var(--secondary);
      font: var(--p-font-xs) var(--font-code);
    }
    p {
      flex-basis: 100%;
      margin: 2px 0 0;
      color: var(--secondary);
      font-size: var(--text-caption);
    }
    .design-stage {
      display: flex;
      flex-wrap: wrap;
      align-items: center;
      gap: var(--p-space-3) var(--p-space-4);
      min-width: 0;
      padding: var(--p-space-4);
      border: 1px solid var(--line);
      border-radius: var(--p-radius-lg);
      background: var(--surface);
    }
    .design-stage.is-stacked {
      flex-direction: column;
      align-items: stretch;
    }
  `,
})
export class DesignSection {
  readonly id = input.required<string>();
  readonly title = input.required<string>();
  readonly description = input('');
  /** 範例使用的正式元件或類別。 */
  readonly api = input('');
  /** 範例每行一個，而不是並排換行。 */
  readonly stacked = input(false);
}

/** 舞台內有標籤的範例：上方是變體名稱，下方是即時元件。 */
@Component({
  selector: 'nx-design-example',
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: { class: 'design-example' },
  template: `<span class="design-example-label">{{ label() }}</span
    ><ng-content />`,
  styles: `
    :host {
      display: inline-flex;
      flex-direction: column;
      align-items: flex-start;
      gap: var(--p-space-2);
      min-width: 0;
      max-width: 100%;
    }
    .design-example-label {
      color: var(--muted);
      font-size: var(--p-font-xs);
    }
  `,
})
export class DesignExample {
  readonly label = input.required<string>();
}
