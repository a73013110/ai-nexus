import { ChangeDetectionStrategy, Component, input } from '@angular/core';

/** 一個欄位的變更；`before` 為 null 表示原本沒有值（新增），`after` 為 null 表示會清空。 */
export interface FieldChange {
  label: string;
  before: string | null;
  after: string | null;
}

/**
 * 欄位層級的「舊值 → 新值」清單：預覽寫入、同步差異、稽核前後值共用。舊值刪除線、新值以淡底標出；
 * 空值寫「（無）」。報讀時每列讀成「欄位：舊值 改為 新值」，不依賴刪除線與顏色。
 */
@Component({
  selector: 'nx-change-list',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `<dl class="ui-change-list">
    @for (change of changes(); track change.label) {
      <div class="ui-change">
        <dt>{{ change.label }}</dt>
        <dd>
          @if (change.before !== null) {
            <del>{{ change.before || empty }}</del
            ><span class="ui-change-arrow" aria-hidden="true">→</span
            ><span class="sr-only">改為</span>
          }
          <ins>{{ change.after || empty }}</ins>
        </dd>
      </div>
    }
  </dl>`,
  styles: `
    :host {
      display: block;
      min-width: 0;
    }
    .ui-change-list {
      display: grid;
      gap: var(--p-space-1);
      margin: 0;
    }
    .ui-change {
      display: grid;
      grid-template-columns: minmax(4rem, max-content) minmax(0, 1fr);
      gap: var(--p-space-3);
      align-items: baseline;
    }
    dt {
      color: var(--secondary);
      font-size: var(--text-caption);
    }
    dd {
      margin: 0;
      min-width: 0;
      overflow-wrap: anywhere;
    }
    del {
      color: var(--muted);
      text-decoration-color: color-mix(in srgb, var(--muted) 70%, transparent);
    }
    .ui-change-arrow {
      margin-inline: var(--p-space-2);
      color: var(--muted);
    }
    ins {
      padding: 0 var(--p-space-1);
      border-radius: var(--radius-mark);
      background: var(--accent-soft);
      font-weight: 500;
      text-decoration: none;
      box-decoration-break: clone;
    }
  `,
})
export class ChangeList {
  readonly changes = input.required<readonly FieldChange[]>();
  protected readonly empty = '（無）';
}
