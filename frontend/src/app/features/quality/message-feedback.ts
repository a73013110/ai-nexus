import {
  ChangeDetectionStrategy,
  Component,
  computed,
  inject,
  input,
  output,
  signal,
} from '@angular/core';
import type { Message } from '../../core/api/types';
import { ViewScope } from '../../shared/browser/view-scope';
import { Icon } from '../../shared/ui/icon';
import { Select } from '../../shared/ui/select';
import { QualityApi } from './quality-api';

@Component({
  selector: 'nx-message-feedback',
  imports: [Icon, Select],
  providers: [ViewScope],
  changeDetection: ChangeDetectionStrategy.OnPush,
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
      <p role="alert" class="message-note error-note">{{ error() }}</p>
    }
    @if (notice()) {
      <span class="visually-hidden" role="status">{{ notice() }}</span>
    }
  </div>`,
  styles: `
    :host {
      display: block;
    }
    .feedback-buttons {
      display: flex;
      align-items: center;
      gap: 4px;
    }
    .feedback-detail {
      padding: 16px;
      border: 1px solid var(--line);
      border-radius: var(--p-radius-md);
      margin-top: 8px;
      max-width: 32rem;
    }
    .feedback-detail textarea {
      resize: vertical;
    }
  `,
})
export class MessageFeedback {
  readonly message = input.required<Message>();
  readonly rated = output<{ id: string; rating: number }>();
  private readonly api = inject(QualityApi);
  private readonly scope = inject(ViewScope);
  private readonly savedRating = signal<number | null>(null);
  readonly rating = computed(() => this.savedRating() ?? this.message().feedbackRating ?? 0);
  readonly expanded = signal(false);
  readonly busy = signal(false);
  readonly reason = signal('');
  readonly note = signal('');
  readonly error = signal('');
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
  async openDetails() {
    if (this.expanded()) {
      this.expanded.set(false);
      return;
    }
    if (this.busy()) return;
    const id = this.message().id,
      valid = this.scope.guard();
    this.busy.set(true);
    this.error.set('');
    try {
      const value = await this.api.feedbackFor(id);
      if (!valid() || this.message().id !== id) return;
      this.reason.set(value?.reason ?? '');
      this.note.set(value?.note ?? '');
      this.expanded.set(true);
    } catch (e) {
      if (valid()) this.error.set(this.scope.message(e));
    } finally {
      if (valid()) this.busy.set(false);
    }
  }
  async save(value: number) {
    if (this.busy()) return;
    const id = this.message().id,
      alive = this.scope.guard();
    this.busy.set(true);
    this.error.set('');
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
      this.expanded.set(false);
      this.notice.set(value ? '回饋已儲存' : '回饋已移除');
    } catch (e) {
      if (alive() && this.message().id === id) this.error.set(this.scope.message(e));
    } finally {
      if (alive()) this.busy.set(false);
    }
  }
}
