import { ChangeDetectionStrategy, Component, input, output } from '@angular/core';
import { StatusBadge } from '@shared/ui/status-badge';
import type { PresentedForm } from '../attendance-presentation';

/**
 * 出勤紀錄一天的假單與出差單：一張單一張小卡，第一行單號與流程狀態，第二行假別（出差寫「出差單」）與起訖，
 * 出差另附事由、流程中的單再寫停在誰。同一天多張單直排，寬版與手機卡片都一樣；流程中的卡用虛線框，和核准的分得開。
 * 單號是按鈕，點了開啟表單的完整內容（`open` 送出單號，由頁面開啟 `GdFormDialog`）。
 */
@Component({
  selector: 'nx-gd-form-list',
  imports: [StatusBadge],
  changeDetection: ChangeDetectionStrategy.OnPush,
  styleUrl: './gd-form-list.scss',
  template: `<ul>
    @for (item of forms(); track item.form.desNo + item.form.startsAt) {
      <li [attr.data-flow]="item.form.flowStatus" [attr.aria-label]="label(item)">
        <span class="form-head">
          <button
            type="button"
            class="form-no"
            [attr.aria-label]="'開啟表單 ' + item.form.desNo"
            title="開啟表單"
            (click)="open.emit(item.form.desNo)"
          >
            {{ item.form.desNo }}
          </button>
          <nx-status-badge [tone]="item.flow.tone">{{ item.flow.label }}</nx-status-badge>
        </span>
        <span class="form-body" [title]="item.span">
          <strong>{{ item.form.kind === 'Travel' ? item.kind : item.form.title }}</strong>
          <time>{{ item.time }}</time>
        </span>
        @if (item.form.kind === 'Travel' && item.form.title) {
          <small class="form-note" [title]="item.form.title">{{ item.form.title }}</small>
        }
        @if (item.waiting) {
          <small class="form-waiting">{{ item.waiting }}</small>
        }
      </li>
    }
  </ul>`,
})
export class GdFormList {
  readonly forms = input.required<readonly PresentedForm[]>();
  readonly open = output<string>();

  /** 報讀時一張單一句話，不必逐格讀小卡裡的字。 */
  label(item: PresentedForm) {
    const title = item.form.kind === 'Travel' ? `${item.kind} ${item.form.title}` : item.form.title;
    return [item.form.desNo, item.flow.label, title, item.span, item.waiting]
      .filter(Boolean)
      .join('，');
  }
}
