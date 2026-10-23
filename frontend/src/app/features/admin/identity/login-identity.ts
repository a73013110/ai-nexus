import { ChangeDetectionStrategy, Component, input, output } from '@angular/core';
import type { SignInIdentityDto } from '@core/api/schema';
import { nameInitial } from '@shared/browser/format';
import { rovingIndex } from '@shared/browser/roving-index';
import { Icon } from '@shared/ui/icon';

const KINDS: Readonly<Record<string, string>> = {
  personal: '個人',
  organization: '組織',
  department: '部門',
  delegate: '代理',
};

/**
 * 帳號有多個身分時，登入後的步驟。選項會沿著表單離開時的同一軸向依序出現；
 * 方向鍵在它們之間移動。
 */
@Component({
  selector: 'nx-login-identity',
  styleUrl: './login-identity.scss',
  imports: [Icon],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `<h2 id="identity-heading">選擇登入身分</h2>
    <p class="login-description">
      這個帳號有 {{ identities().length }} 個身分，選擇這次要使用的身分。
    </p>
    <ul class="identity-list" aria-labelledby="identity-heading">
      @for (identity of identities(); track identity.id; let i = $index) {
        <li [style.--i]="i">
          <button
            type="button"
            class="identity-option"
            [class.is-chosen]="choosing() === identity.id"
            [attr.aria-current]="identity.id === current() ? 'true' : null"
            [attr.aria-busy]="choosing() === identity.id || null"
            [disabled]="!!choosing()"
            (click)="chosen.emit(identity.id)"
            (keydown)="move($event)"
          >
            <span
              class="avatar identity-avatar"
              [attr.data-kind]="identity.kind"
              aria-hidden="true"
              >{{ initial(identity.name) }}</span
            >
            <span class="identity-text"
              ><strong>{{ identity.name }}</strong
              ><small
                >{{ kind(identity.kind) }}
                @if (identity.detail) {
                  · {{ identity.detail }}
                }
                @if (identity.id === current()) {
                  · 目前
                }
              </small></span
            >
            <nx-icon [name]="choosing() === identity.id ? 'loading' : 'chevron'" />
          </button>
        </li>
      }
    </ul>
    <button
      type="button"
      class="secondary-button identity-back"
      [disabled]="!!choosing()"
      (click)="back.emit()"
    >
      使用其他帳號
    </button>`,
})
export class LoginIdentity {
  readonly identities = input.required<readonly SignInIdentityDto[]>();
  readonly current = input<string | null>(null);
  readonly choosing = input<string | null>(null);
  readonly chosen = output<string>();
  readonly back = output<void>();
  protected readonly initial = nameInitial;
  kind(kind: string) {
    return KINDS[kind] ?? '身分';
  }
  move(event: KeyboardEvent) {
    const options = [
      ...((event.currentTarget as HTMLElement)
        .closest('ul')
        ?.querySelectorAll<HTMLButtonElement>('button') ?? []),
    ];
    const next = rovingIndex(
      event.key,
      options.indexOf(document.activeElement as HTMLButtonElement),
      options.length,
    );
    if (next === null) return;
    event.preventDefault();
    options[next]?.focus();
  }
}
