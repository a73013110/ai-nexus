import {
  ChangeDetectionStrategy,
  Component,
  ViewEncapsulation,
  computed,
  input,
  output,
} from '@angular/core';
import { Icon } from './icon';
import { IssueCode } from './issue-code';
import type { BadgeTone } from './count-badge';

/** One compact feedback surface for page, dialog, composer and global notices. */
@Component({
  selector: 'nx-notice',
  imports: [Icon, IssueCode],
  changeDetection: ChangeDetectionStrategy.OnPush,
  encapsulation: ViewEncapsulation.None,
  styleUrl: './notice.scss',
  host: {
    class: 'ui-notice',
    '[attr.data-tone]': 'tone()',
    '[attr.role]': 'tone() === "danger" ? "alert" : "status"',
  },
  template: `<nx-icon class="ui-notice-icon" [name]="resolvedIcon()" />
    <div class="ui-notice-content">
      @if (message()) {
        <span>{{ message() }}</span>
      }
      <ng-content />
    </div>
    @if (message()) {
      <nx-issue-code [message]="message()" />
    }
    <div class="ui-notice-actions">
      <ng-content select="[notice-actions]" />
      @if (dismissible()) {
        <button
          type="button"
          class="icon-button"
          [attr.aria-label]="dismissLabel()"
          [title]="dismissLabel()"
          (click)="dismissed.emit()"
        >
          <nx-icon name="close" />
        </button>
      }
    </div>`,
})
export class Notice {
  readonly tone = input<BadgeTone>('danger');
  readonly message = input<string | null | undefined>();
  readonly dismissible = input(false);
  readonly dismissLabel = input('關閉提示');
  readonly dismissed = output<void>();
  readonly icon = input('');
  readonly resolvedIcon = computed(
    () => this.icon() || (this.tone() === 'success' ? 'check' : 'info'),
  );
}
