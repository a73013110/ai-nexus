import { ChangeDetectionStrategy, Component, inject, input } from '@angular/core';
import { ClientIssues } from '../../core/errors/client-issues';
import { Icon } from './icon';
import { ProductTour, type ProductTourDefinition } from './product-tour';

@Component({
  selector: 'nx-product-tour-button',
  imports: [Icon],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `<button
    type="button"
    [class]="compact() ? 'icon-button tour-trigger' : 'quiet-button tour-trigger'"
    aria-label="操作導覽"
    title="操作導覽 · 可隨時跳過或重播"
    [disabled]="tours.loading() || tours.active()"
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
  async start() {
    try {
      await this.tours.start(this.tour());
    } catch (error) {
      this.issues.handleError(error);
    }
  }
}
