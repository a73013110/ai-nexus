import { apiResource } from '../../core/api/api-resource';
import { ChangeDetectionStrategy, Component, inject, input, computed } from '@angular/core';
import type { ConversationSpendDto } from '../../core/api/schema';
import { ViewScope } from '../../shared/browser/view-scope';
import { InfoPopover } from '../../shared/ui/info-popover';
import { Icon } from '../../shared/ui/icon';
import { BillingApi, chargeKind, money } from './billing-api';

@Component({
  selector: 'nx-conversation-spend',
  imports: [InfoPopover, Icon],
  providers: [ViewScope],
  changeDetection: ChangeDetectionStrategy.OnPush,
  styleUrl: './conversation-spend.scss',
  template: `@if (data(); as spend) {
      <nx-info-popover
        [label]="summary(spend)"
        icon="money"
        description="檢視全對話費用"
        align="right"
      >
        <div class="charge-content">
          <strong>全對話 · {{ spend.requests }} 次呼叫</strong>
          <p>包含所有分支、重新生成與網路搜尋，不只目前顯示的回答。</p>
          @for (total of spend.totals; track total.currency + total.kind) {
            <p>
              {{ kind(total.kind) }} ·
              {{ total.currency ? money(total.amount, total.currency) : '未定價' }}
              @if (total.unknownCalls) {
                <span> · {{ total.unknownCalls }} 次費用未知</span>
              }
            </p>
          }
          @for (model of spend.models; track model.label + model.currency + model.kind) {
            <div class="spend-breakdown">
              <span>{{ model.label }}</span
              ><span
                >{{ model.requests }} 次 ·
                {{ model.currency ? money(model.amount, model.currency) : '未定價' }}</span
              >
            </div>
          }
          @if (spend.pendingCalls) {
            <p>{{ spend.pendingCalls }} 次待結算。</p>
          }
          @if (spend.legacyCalls) {
            <p>{{ spend.legacyCalls }} 次早期呼叫尚無價格紀錄。</p>
          }
        </div>
      </nx-info-popover>
    } @else if (failed()) {
      <button
        type="button"
        class="quiet-button"
        (click)="load()"
        title="費用暫時無法取得，點擊重試"
      >
        <nx-icon name="money" />費用
      </button>
    }`,
})
export class ConversationSpendView {
  readonly id = input.required<string>();
  readonly revision = input('');
  private readonly api = inject(BillingApi);
  /** Read again for each new revision of the conversation (a finished run adds spending). */
  private readonly spendRead = apiResource({
    params: () => ({ id: this.id(), revision: this.revision() }),
    loader: async ({ id }) => ({ id, spend: await this.api.conversation(id) }),
  });
  readonly data = computed<ConversationSpendDto | null>(() => {
    const value = this.spendRead.value();
    return value && value.id === this.id() ? value.spend : null;
  });
  readonly failed = computed(() => !!this.spendRead.error());
  readonly money = money;
  readonly kind = chargeKind;
  load() {
    this.spendRead.reload();
  }
  summary(spend: ConversationSpendDto) {
    const priced = spend.totals.filter((x) => !!x.currency);
    if (!priced.length) return spend.requests ? '費用未定價' : '全對話費用';
    if (priced.length === 1)
      return (
        (priced[0].knownCalls ? money(priced[0].amount, priced[0].currency) : '費用待確認') +
        (spend.totals.some((x) => x.unknownCalls > 0) || spend.legacyCalls || spend.pendingCalls
          ? ' +'
          : '')
      );
    return `${priced.length} 種計費總額`;
  }
}
