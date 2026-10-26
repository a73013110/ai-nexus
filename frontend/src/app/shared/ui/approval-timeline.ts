import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';
import { Icon } from './icon';

export type ApprovalState = 'done' | 'current' | 'pending';

/**
 * 簽核的一關：`person` 與 `detail`（單位、職稱）是處理的人，`title` 是關卡（送出、串簽、決行…），
 * `note` 是附註（代理），`waiting` 是目前關卡已等多久（顯示在時間的位置），
 * `time` 是顯示用的時間字樣、`dateTime` 是給 `<time>` 的 ISO 值。
 */
export interface ApprovalStep {
  key: string;
  state: ApprovalState;
  title: string;
  person?: string | null;
  detail?: string | null;
  note?: string | null;
  waiting?: string | null;
  time?: string | null;
  dateTime?: string | null;
  memo?: string | null;
}

const STATE_LABEL: Record<ApprovalState, string> = {
  done: '已完成',
  current: '目前關卡',
  pending: '尚未輪到',
};

/**
 * 直式的簽核流程，上方一條進度（第幾關、共幾關）：已完成的實心打勾、目前關卡外圈加光暈、未到的空心；
 * 每關一行寫處理人、單位職稱、關卡與時間，簽核意見放在下方的淺底框。
 * 狀態另有報讀文字，不只靠圖形與顏色。公文系統的表單、日後其他有簽核流程的單據共用。
 */
@Component({
  selector: 'nx-approval-timeline',
  imports: [Icon],
  changeDetection: ChangeDetectionStrategy.OnPush,
  styleUrl: './approval-timeline.scss',
  template: `<div class="ui-approval-top">
      <h3 class="ui-approval-heading">{{ label() }}</h3>
      @if (progress(); as progress) {
        <span class="ui-approval-progress">{{ progress.text }}</span>
        <span class="ui-approval-meter" aria-hidden="true">
          <i class="is-done" [style.width.%]="progress.done"></i>
          <i class="is-current" [style.width.%]="progress.current"></i>
        </span>
      }
    </div>
    <ol class="ui-approval" [attr.aria-label]="label()">
      @for (step of steps(); track step.key) {
        <li class="ui-approval-step" [attr.data-state]="step.state">
          <span class="ui-approval-mark" aria-hidden="true">
            @if (step.state === 'done') {
              <nx-icon name="check" />
            } @else if (step.state === 'current') {
              <i></i>
            }
          </span>
          <div class="ui-approval-body">
            <div class="ui-approval-head">
              <strong>{{ step.person || step.title }}</strong>
              <span class="sr-only">（{{ stateLabel[step.state] }}）</span>
              @if (step.detail) {
                <span class="ui-approval-detail">{{ step.detail }}</span>
              }
              @if (step.person) {
                <span class="ui-approval-kind is-title">{{ step.title }}</span>
              }
              @if (step.note) {
                <span class="ui-approval-kind">{{ step.note }}</span>
              }
              @if (step.waiting) {
                <span class="ui-approval-end ui-approval-waiting" [title]="step.time || ''">{{
                  step.waiting
                }}</span>
              } @else if (step.time) {
                <time class="ui-approval-end" [attr.datetime]="step.dateTime || null">{{
                  step.time
                }}</time>
              }
            </div>
            @if (step.memo) {
              <p class="ui-approval-memo">{{ step.memo }}</p>
            }
          </div>
        </li>
      } @empty {
        <li class="ui-approval-empty">{{ empty() }}</li>
      }
    </ol>`,
})
export class ApprovalTimeline {
  readonly steps = input.required<readonly ApprovalStep[]>();
  /** 區塊標題與清單的報讀名稱。 */
  readonly label = input('簽核流程');
  /** 標題旁的進度說明（例：「第 4 關，共 6 關」）；空字串時不畫進度條。 */
  readonly summary = input('');
  /** 沒有任何關卡時的說明。 */
  readonly empty = input('沒有簽核紀錄。');
  readonly stateLabel = STATE_LABEL;
  /** 進度條：完成的關卡一段、目前關卡一段，各占全部關卡的比例。 */
  readonly progress = computed(() => {
    const steps = this.steps(),
      text = this.summary();
    if (!text || !steps.length) return null;
    const share = (state: ApprovalState) =>
      (steps.filter((step) => step.state === state).length / steps.length) * 100;
    return { text, done: share('done'), current: share('current') };
  });
}
