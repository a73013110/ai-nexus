import { ChangeDetectionStrategy, Component, DestroyRef, inject, input } from '@angular/core';
import { ClientIssues } from '@core/errors/client-issues';
import { Icon } from './icon';
import { ProductTour, type ProductTourDefinition } from './product-tour';

/** 操作導覽的觸發鈕；`FeaturePage`、`DialogHeader` 與 `DetailDrawer` 的 `tour` 都用它，自繪標題列（文件閱讀器、個人設定）才直接放。 */
@Component({
  selector: 'nx-product-tour-button',
  imports: [Icon],
  changeDetection: ChangeDetectionStrategy.OnPush,
  // 各入口（頁首、對話框標題列、抽屜）都把它放在一列工具裡，不隨標題壓縮。
  styles: ':host { flex: none; }',
  template: `<button
    type="button"
    [class]="compact() ? 'icon-button tour-trigger' : 'quiet-button tour-trigger'"
    aria-label="操作導覽"
    title="操作導覽 · 可隨時跳過或重播"
    [attr.aria-disabled]="tours.loading() || tours.active() || null"
    [attr.aria-busy]="tours.loading() || null"
    (click)="start()"
  >
    <nx-icon [name]="tours.loading() ? 'loading' : 'tour'" />
    @if (!compact()) {
      <span>{{ tours.loading() ? '準備導覽…' : '操作導覽' }}</span>
    }
  </button>`,
})
export class ProductTourButton {
  readonly tours = inject(ProductTour);
  private readonly issues = inject(ClientIssues);
  readonly tour = input.required<ProductTourDefinition>();
  readonly compact = input(false);
  private started?: ProductTourDefinition;
  constructor() {
    // 對話框或抽屜被移除時不一定會觸發 close，導覽要跟著觸發鈕一起結束。
    inject(DestroyRef).onDestroy(() => this.started && this.tours.stop(this.started));
  }
  async start() {
    // 以 aria-disabled 擋下（焦點留在鈕上），所以這裡自己忽略準備中與導覽進行中的點擊。
    if (this.tours.loading() || this.tours.active()) return;
    try {
      await this.tours.start((this.started = this.tour()));
    } catch (error) {
      this.issues.handleError(error);
    }
  }
}
