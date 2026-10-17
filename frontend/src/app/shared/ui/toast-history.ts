import {
  afterNextRender,
  ChangeDetectionStrategy,
  Component,
  inject,
  Injector,
  output,
} from '@angular/core';
import { formatDateTimeInput } from '../browser/format';
import { EmptyState } from './empty-state';
import { Icon } from './icon';
import { HISTORY_LIMIT, ToastService, toastIcon, type ToastRecord } from './toast-service';

/**
 * 此分頁已結束的提示，最新的在前，讓在讀到之前就消失的提示還能找回。
 * 只存在記憶體中：重新整理或切換帳號時會清空。動作只會執行一次，可從此處或提示本身執行；
 * `activated` 讓宿主在動作把使用者帶走之前先關閉自己。
 */
@Component({
  selector: 'nx-toast-history',
  imports: [EmptyState, Icon],
  changeDetection: ChangeDetectionStrategy.OnPush,
  styleUrl: './toast-history.scss',
  template: `<p class="toast-history-note">
      只保留這個分頁最近 {{ limit }} 則操作通知，重新整理或切換帳號後清空。
    </p>
    @if (service.history().length) {
      <ol class="toast-history" aria-label="本次工作階段的操作通知">
        @for (record of service.history(); track record.key) {
          <li class="toast-record" tabindex="-1" [attr.data-tone]="record.tone">
            <nx-icon [name]="icon(record)" />
            <div class="toast-record-text">
              <strong>{{ record.title }}</strong>
              @if (record.description) {
                <span>{{ record.description }}</span>
              }
            </div>
            <time [attr.datetime]="record.at" [title]="stamp(record.at)">{{
              time(record.at)
            }}</time>
            @if (record.action; as action) {
              <button
                type="button"
                class="quiet-button"
                [attr.aria-label]="action.label + '：' + record.title"
                (click)="run(record, $event)"
              >
                {{ action.label }}
              </button>
            }
          </li>
        }
      </ol>
    } @else {
      <nx-empty-state>
        <nx-icon name="bell" />
        <p>這個分頁還沒有操作通知</p>
      </nx-empty-state>
    }`,
})
export class ToastHistory {
  readonly service = inject(ToastService);
  readonly limit = HISTORY_LIMIT;
  readonly activated = output<void>();
  private readonly injector = inject(Injector);
  icon(record: ToastRecord) {
    return toastIcon(record.tone);
  }
  stamp(at: string) {
    return formatDateTimeInput(new Date(at)).replace('T', ' ');
  }
  time(at: string) {
    return formatDateTimeInput(new Date(at)).slice(11, 16);
  }
  /** 用掉的按鈕會離開；除非宿主移動了焦點（例如關閉中的對話框），否則該列保留焦點。 */
  run(record: ToastRecord, event: Event) {
    const row = (event.currentTarget as HTMLElement).closest('li');
    this.activated.emit();
    this.service.replay(record.key);
    afterNextRender(
      () => {
        const active = document.activeElement;
        if (row?.isConnected && (!active || active === document.body)) row.focus();
      },
      { injector: this.injector },
    );
  }
}
