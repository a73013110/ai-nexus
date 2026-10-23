import {
  ChangeDetectionStrategy,
  Component,
  effect,
  inject,
  input,
  linkedSignal,
  model,
} from '@angular/core';
import type { VerificationCodeDto } from '@core/api/schema';
import { Field } from '@shared/ui/field';
import { Icon } from '@shared/ui/icon';
import { AccountSecurityStore } from './account-security-store';

const DELIVERY: Readonly<Record<string, string>> = {
  queued: '正在排入寄送…',
  sending: '正在寄送…',
  sent: '已寄出，請查看信箱（也請檢查垃圾郵件）。',
  failed: '寄送失敗，請稍後重新寄送；持續失敗請聯絡管理員。',
  cancelled: '這組驗證碼已被新的取代。',
};
/** 不會再變化的寄送狀態；到了就停止詢問。 */
const FINAL = new Set(['sent', 'failed', 'cancelled', 'unknown']);
const POLL_MS = 1500;
const POLL_TIMES = 20;

/** 寄送狀態文字；未知狀態（紀錄已依保存期限刪除）不猜測。 */
export function deliveryText(delivery: string) {
  return DELIVERY[delivery] ?? '寄送狀態不明，沒收到信時請重新寄送。';
}

/**
 * 6 位數驗證碼欄位與寄送狀態。寄出後每 1.5 秒問一次狀態，到「已寄出／失敗」為止，
 * 讓使用者知道信到底有沒有寄出，而不是乾等。
 */
@Component({
  selector: 'nx-verification-code-field',
  imports: [Field, Icon],
  changeDetection: ChangeDetectionStrategy.OnPush,
  styles: `
    :host {
      display: grid;
      gap: var(--p-space-2);
    }
    input {
      font-family: var(--font-code);
      letter-spacing: 0.3em;
    }
    .delivery-pending {
      --icon-size: var(--icon-sm);
      vertical-align: -0.125em;
      margin-inline-end: var(--p-space-1);
    }
  `,
  template: `<p class="form-note" role="status">
      @if (!settled(current().delivery)) {
        <nx-icon name="loading" class="delivery-pending" />
      }
      驗證碼已寄到 {{ current().maskedTarget ?? '你的信箱' }}：{{ text(current().delivery) }}
    </p>
    <label
      >驗證碼<input
        nxField
        name="one-time-code"
        inputmode="numeric"
        autocomplete="one-time-code"
        pattern="[0-9]{6}"
        maxlength="6"
        required
        [value]="code()"
        (input)="code.set(digits($any($event.target).value))"
    /></label>`,
})
export class VerificationCodeField {
  private readonly store = inject(AccountSecurityStore);
  readonly challenge = input.required<VerificationCodeDto>();
  readonly code = model('');
  /** 最新的狀態；換一組驗證碼時從新的開始。 */
  readonly current = linkedSignal(() => this.challenge());
  readonly text = deliveryText;
  /** 排隊與寄送中還會變化，前面轉圈；到了最終狀態就停。 */
  readonly settled = (delivery: string) => FINAL.has(delivery) || !(delivery in DELIVERY);

  constructor() {
    effect((onCleanup) => {
      const id = this.challenge().id;
      let stopped = false;
      onCleanup(() => (stopped = true));
      void this.poll(id, () => stopped);
    });
  }

  /** 貼上「123 456」或全形數字也能用；只留前 6 個數字。 */
  digits(value: string) {
    return value
      .replace(/[０-９]/g, (digit) => String.fromCharCode(digit.charCodeAt(0) - 0xfee0))
      .replace(/\D/g, '')
      .slice(0, 6);
  }

  private async poll(id: string, stopped: () => boolean) {
    for (let attempt = 0; attempt < POLL_TIMES; attempt++) {
      if (FINAL.has(this.current().delivery)) return;
      await new Promise((resolve) => setTimeout(resolve, POLL_MS));
      if (stopped()) return;
      try {
        const status = await this.store.code(id);
        if (stopped()) return;
        this.current.set(status);
      } catch {
        return;
      }
    }
  }
}
