import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';
import type { ChargeDto } from '@core/api/schema';
import { formatNumber } from '@shared/browser/format';
import { InfoPopover } from '@shared/ui/info-popover';
import { chargeKind, money } from './billing-api';

@Component({
  selector: 'nx-charge-label',
  imports: [InfoPopover],
  changeDetection: ChangeDetectionStrategy.OnPush,
  styleUrl: './charge-label.scss',
  template: `<nx-info-popover [label]="label()" icon="money" description="本次模型呼叫的費用與用量">
    <div class="charge-content">
      <strong>{{ kind() }}</strong>
      <p>
        輸入 {{ tokens(charge().inputTokens) }} · 輸出 {{ tokens(charge().outputTokens) }} tokens
      </p>
      @if (charge().cachedInputTokens; as cached) {
        <p>輸入包含 {{ tokens(cached) }} 個快取 tokens。</p>
      }
      @if (charge().reasoningTokens; as reasoning) {
        <p>輸出包含 {{ tokens(reasoning) }} 個思考 tokens。</p>
      }
      <p>{{ explanation() }}</p>
    </div>
  </nx-info-popover>`,
})
export class ChargeLabel {
  readonly charge = input.required<ChargeDto>();
  readonly kind = computed(() => chargeKind(this.charge().kind));
  /** 供應商沒回報的用量顯示「未回報」，不當成零。 */
  tokens(value: number | null | undefined) {
    return value == null ? '未回報' : formatNumber(value);
  }
  readonly label = computed(() => {
    const card = this.charge();
    if (card.state === 'pending') return '費用待結算';
    if (card.state === 'not_started') return '未呼叫模型';
    if (card.state === 'unpriced') return '未設定價格';
    if (card.amount == null) return '用量未完整回報';
    return money(card.amount, card.currency) + (card.kind === 'internal' ? ' · 內部' : '');
  });
  readonly explanation = computed(() =>
    this.charge().kind === 'internal'
      ? '依管理員設定的內部單價估算，不代表對外付款。'
      : this.charge().state === 'unpriced'
        ? '管理員可新增價格版本；歷史未定價呼叫不會被回填成零。'
        : '依本次呼叫的價格版本與供應商回報用量計算。帳單仍以供應商為準。',
  );
}
