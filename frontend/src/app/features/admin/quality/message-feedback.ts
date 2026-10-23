import { apiResource } from '@core/api/api-resource';
import { Notice } from '@shared/ui/notice';
import { Field } from '@shared/ui/field';
import {
  ChangeDetectionStrategy,
  Component,
  computed,
  inject,
  input,
  output,
  signal,
  linkedSignal,
} from '@angular/core';
import type { MessageDto } from '@core/api/schema';
import { ViewScope } from '@shared/browser/view-scope';
import { Icon } from '@shared/ui/icon';
import { PendingButton } from '@shared/ui/pending-button';
import { Select } from '@shared/ui/select';
import { QualityApi } from './quality-api';

@Component({
  selector: 'nx-message-feedback',
  host: { class: 'ui-density-compact' },
  imports: [Notice, Field, Icon, PendingButton, Select],
  providers: [ViewScope],
  changeDetection: ChangeDetectionStrategy.OnPush,
  styleUrl: './message-feedback.scss',
  template: `<div class="message-feedback">
    <div class="feedback-buttons" role="group" aria-label="回答品質回饋">
      <button
        nxPending
        class="quiet-button"
        [class.feedback-active]="rating() === 1"
        [attr.aria-pressed]="rating() === 1"
        aria-label="回答有幫助"
        [pending]="working() === 'up'"
        [disabled]="busy() && working() !== 'up'"
        (click)="rate(1)"
      >
        <nx-icon name="thumb-up" />
      </button>
      <button
        nxPending
        class="quiet-button"
        [class.feedback-active]="rating() === -1"
        [attr.aria-pressed]="rating() === -1"
        aria-label="回答待改善"
        [pending]="working() === 'down'"
        [disabled]="busy() && working() !== 'down'"
        (click)="rate(-1)"
      >
        <nx-icon name="thumb-down" />
      </button>
      @if (rating() === -1) {
        <button
          nxPending
          class="quiet-button"
          [attr.aria-expanded]="expanded()"
          [pending]="opening()"
          [disabled]="busy() && !opening()"
          (click)="openDetails()"
        >
          {{ opening() ? '開啟中…' : '補充回饋' }}
        </button>
      }
    </div>
    @if (expanded() && rating() === -1) {
      <div class="feedback-detail platform-form">
        <nx-select
          label="待改善原因"
          [options]="reasons"
          [value]="reason()"
          (valueChange)="reason.set($event)"
          [disabled]="busy()"
        /><textarea
          nxField
          aria-label="回饋補充說明"
          placeholder="哪裡需要改善？（選填）"
          rows="2"
          maxlength="2000"
          [value]="note()"
          (input)="note.set($any($event.target).value)"
          [disabled]="busy()"
        ></textarea
        ><button
          nxPending
          class="secondary-button"
          [pending]="working() === 'detail'"
          [disabled]="busy() && working() !== 'detail'"
          (click)="save(-1)"
        >
          {{ working() === 'detail' ? '儲存中…' : '儲存補充' }}
        </button>
      </div>
    }
    @if (error()) {
      <nx-notice tone="danger" [message]="error()" />
    }
    @if (notice()) {
      <span class="sr-only" role="status">{{ notice() }}</span>
    }
  </div>`,
})
export class MessageFeedback {
  readonly message = input.required<MessageDto>();
  readonly rated = output<{ id: string; rating: number }>();
  private readonly api = inject(QualityApi);
  private readonly scope = inject(ViewScope);
  private readonly savedRating = signal<number | null>(null);
  readonly rating = computed(() => this.savedRating() ?? this.message().feedbackRating ?? 0);
  /** 補充細節在使用者第一次開啟時才讀取。 */
  private readonly requested = signal(false);
  private readonly detailsRead = apiResource({
    params: () => (this.requested() ? this.message().id : undefined),
    loader: async (id) => ({ id, feedback: await this.api.feedbackFor(id) }),
  });
  private readonly details = computed(() => {
    const value = this.detailsRead.value();
    return value && value.id === this.message().id ? value : null;
  });
  readonly expanded = computed(() => this.requested() && !!this.details());
  /** 正在儲存的是哪個控制項：讚、待改善或補充說明；只有它轉圈。 */
  readonly working = signal<'up' | 'down' | 'detail' | null>(null);
  /** 第一次開啟補充回饋時正在讀取既有內容。 */
  readonly opening = this.detailsRead.loading;
  readonly busy = computed(() => this.working() !== null || this.detailsRead.loading());
  readonly reason = linkedSignal(() => this.details()?.feedback?.reason ?? '');
  readonly note = linkedSignal(() => this.details()?.feedback?.note ?? '');
  readonly saveError = signal('');
  readonly error = computed(() => this.saveError() || this.detailsRead.error());
  readonly notice = signal('');
  readonly reasons = [
    { value: '', label: '選擇原因（選填）' },
    { value: 'incorrect', label: '內容不正確' },
    { value: 'citation', label: '引用有問題' },
    { value: 'incomplete', label: '內容不完整' },
    { value: 'format', label: '格式不易使用' },
    { value: 'other', label: '其他' },
  ];
  rate(value: number) {
    void this.save(this.rating() === value ? 0 : value, value === 1 ? 'up' : 'down');
  }
  openDetails() {
    if (this.expanded()) this.requested.set(false);
    else if (!this.busy()) {
      this.saveError.set('');
      this.requested.set(true);
    }
  }
  async save(value: number, from: 'up' | 'down' | 'detail' = 'detail') {
    if (this.busy()) return;
    const id = this.message().id,
      alive = this.scope.guard();
    this.working.set(from);
    this.saveError.set('');
    try {
      await this.api.feedback(
        id,
        value,
        value === -1 ? this.reason() : '',
        value === -1 ? this.note() : '',
      );
      if (!alive() || this.message().id !== id) return;
      this.savedRating.set(value);
      this.rated.emit({ id, rating: value });
      this.requested.set(false);
      this.notice.set(value ? '回饋已儲存' : '回饋已移除');
    } catch (failure) {
      if (alive() && this.message().id === id) this.saveError.set(this.scope.message(failure));
    } finally {
      if (alive()) this.working.set(null);
    }
  }
}
