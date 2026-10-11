import { apiResource } from '../../core/api/api-resource';
import { Notice } from '../../shared/ui/notice';
import { Field } from '../../shared/ui/field';
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
import type { MessageDto } from '../../core/api/schema';
import { ViewScope } from '../../shared/browser/view-scope';
import { Icon } from '../../shared/ui/icon';
import { Select } from '../../shared/ui/select';
import { QualityApi } from './quality-api';

@Component({
  selector: 'nx-message-feedback',
  host: { class: 'ui-density-compact' },
  imports: [Notice, Field, Icon, Select],
  providers: [ViewScope],
  changeDetection: ChangeDetectionStrategy.OnPush,
  styleUrl: './message-feedback.scss',
  template: `<div class="message-feedback">
    <div class="feedback-buttons" role="group" aria-label="回答品質回饋">
      <button
        class="quiet-button"
        [class.feedback-active]="rating() === 1"
        [attr.aria-pressed]="rating() === 1"
        aria-label="回答有幫助"
        [disabled]="busy()"
        (click)="rate(1)"
      >
        <nx-icon name="thumb-up" />
      </button>
      <button
        class="quiet-button"
        [class.feedback-active]="rating() === -1"
        [attr.aria-pressed]="rating() === -1"
        aria-label="回答待改善"
        [disabled]="busy()"
        (click)="rate(-1)"
      >
        <nx-icon name="thumb-down" />
      </button>
      @if (rating() === -1) {
        <button
          class="quiet-button"
          (click)="openDetails()"
          [attr.aria-expanded]="expanded()"
          [disabled]="busy()"
        >
          補充回饋
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
        ><button class="secondary-button" (click)="save(-1)" [disabled]="busy()">儲存補充</button>
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
  /** Supplementary details are read when the user first opens them. */
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
  private readonly saving = signal(false);
  readonly busy = computed(() => this.saving() || this.detailsRead.loading());
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
    void this.save(this.rating() === value ? 0 : value);
  }
  openDetails() {
    if (this.expanded()) this.requested.set(false);
    else if (!this.busy()) {
      this.saveError.set('');
      this.requested.set(true);
    }
  }
  async save(value: number) {
    if (this.busy()) return;
    const id = this.message().id,
      alive = this.scope.guard();
    this.saving.set(true);
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
    } catch (e) {
      if (alive() && this.message().id === id) this.saveError.set(this.scope.message(e));
    } finally {
      if (alive()) this.saving.set(false);
    }
  }
}
