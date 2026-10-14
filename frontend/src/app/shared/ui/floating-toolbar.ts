import { ChangeDetectionStrategy, Component, ElementRef, inject, input } from '@angular/core';
import { idleChromePart } from './idle-chrome';

/**
 * 浮在檢視器內容上方的內容工具（模式、頁碼、縮放、搜尋），讓對話框標題列只留視窗層級動作。
 * 放在包住內容、`position: relative` 的容器裡，並在外層（檢視器或對話框）加 `nxIdleChrome`：
 * 何時出現與淡出由該範圍統一決定（見 `IdleChrome`），放大的對話框標題列也跟著同一規則。
 */
@Component({
  selector: 'nx-floating-toolbar',
  changeDetection: ChangeDetectionStrategy.OnPush,
  styleUrl: './floating-toolbar.scss',
  host: { '[attr.data-chrome]': 'state()' },
  template: `<div
    class="floating-toolbar ui-density-compact"
    role="group"
    [attr.aria-label]="label()"
  >
    <ng-content />
  </div>`,
})
export class FloatingToolbar {
  readonly label = input.required<string>();
  /** 保持顯示，例如搜尋中有輸入時。 */
  readonly pinned = input(false);
  protected readonly state = idleChromePart(
    inject<ElementRef<HTMLElement>>(ElementRef).nativeElement,
    () => true,
    () => this.pinned(),
  );
}
