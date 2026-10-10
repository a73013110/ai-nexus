import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';
import type { ChargeDto } from '../../core/api/schema';
import { InfoPopover } from '../../shared/ui/info-popover';
import { chargeKind, money } from './billing-api';

@Component({
  selector: 'nx-charge-label',
  imports: [InfoPopover],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `<nx-info-popover [label]="label()" icon="money" description="本次模型呼叫的費用與用量">
    <div class="charge-content">
      <strong>{{ kind() }}</strong>
      <p>
        輸入 {{ charge().inputTokens?.toLocaleString() ?? '未回報' }} · 輸出
        {{ charge().outputTokens?.toLocaleString() ?? '未回報' }} tokens
      </p>
      @if (charge().cachedInputTokens) {
        <p>輸入包含 {{ charge().cachedInputTokens?.toLocaleString() }} 個快取 tokens。</p>
      }
      @if (charge().reasoningTokens) {
        <p>輸出包含 {{ charge().reasoningTokens?.toLocaleString() }} 個思考 tokens。</p>
      }
      <p>{{ explanation() }}</p>
    </div>
  </nx-info-popover>`,
})
export class ChargeLabel {
  readonly charge = input.required<ChargeDto>();
  readonly kind = computed(() => chargeKind(this.charge().kind));
  readonly label = computed(() => {
    const c = this.charge();
    if (c.state === 'pending') return '費用待結算';
    if (c.state === 'not_started') return '未呼叫模型';
    if (c.state === 'unpriced') return '未設定價格';
    if (c.amount == null) return '用量未完整回報';
    return money(c.amount, c.currency) + (c.kind === 'internal' ? ' · 內部' : '');
  });
  readonly explanation = computed(() =>
    this.charge().kind === 'internal'
      ? '依管理員設定的內部單價估算，不代表對外付款。'
      : this.charge().state === 'unpriced'
        ? '管理員可新增價格版本；歷史未定價呼叫不會被回填成零。'
        : '依本次呼叫的價格版本與供應商回報用量計算。帳單仍以供應商為準。',
  );
}
