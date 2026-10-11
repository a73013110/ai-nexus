import { ChangeDetectionStrategy, Component, input } from '@angular/core';

/** Greets by the local time of day. */
export function salutation(hour: number) {
  if (hour >= 5 && hour < 11) return '早安';
  if (hour >= 11 && hour < 14) return '午安';
  if (hour >= 14 && hour < 18) return '下午好';
  return '晚上好';
}

/** The greeting under the centred wordmark while the workspace opens; its avatar flies on. */
@Component({
  selector: 'nx-login-welcome',
  styleUrl: './login-welcome.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: { role: 'status' },
  template: `<span class="avatar welcome-avatar" aria-hidden="true">{{
      name().slice(0, 1) || 'N'
    }}</span>
    <h2>{{ greeting }}，{{ name() }}</h2>
    <p>{{ identity() ? '以「' + identity() + '」身分，' : '' }}正在開啟你的工作區</p>`,
})
export class LoginWelcome {
  readonly name = input('');
  /** The identity chosen on the previous step, when the account had a choice to make. */
  readonly identity = input<string | null>(null);
  readonly greeting = salutation(new Date().getHours());
}
