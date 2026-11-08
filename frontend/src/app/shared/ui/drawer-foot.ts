import { ChangeDetectionStrategy, Component, input } from '@angular/core';

/**
 * 詳情抽屜底部固定的動作列：說明在左、按鈕在右，不隨內文捲動。
 * 直接放在 `nx-detail-drawer` 裡；要依條件出現時用單層 `@if` 包住，巢狀的控制流程投影不到底部。
 */
@Component({
  selector: 'nx-drawer-foot',
  changeDetection: ChangeDetectionStrategy.OnPush,
  styles: `
    :host {
      display: flex;
      flex: none;
      align-items: center;
      justify-content: flex-end;
      gap: var(--p-space-3);
      padding: var(--p-space-2) var(--drawer-gutter);
      border-top: 1px solid var(--line);
      background: var(--surface-subtle);
    }
  `,
  template: `@if (note()) {
      <p class="dialog-note">{{ note() }}</p>
    }
    <ng-content />`,
})
export class DrawerFoot {
  /** 按鈕左側的一句說明。 */
  readonly note = input('');
}
