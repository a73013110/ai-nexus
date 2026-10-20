import { ChangeDetectionStrategy, Component, input } from '@angular/core';
import { nameInitial } from '@shared/browser/format';

/** 依一天中的當地時間問候。 */
export function salutation(hour: number) {
  if (hour >= 5 && hour < 11) return '早安';
  if (hour >= 11 && hour < 14) return '午安';
  if (hour >= 14 && hour < 18) return '下午好';
  return '晚上好';
}

/** 工作區開啟時，置中字標下方的問候語；頭像會飛離。 */
@Component({
  selector: 'nx-login-welcome',
  styleUrl: './login-welcome.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: { role: 'status' },
  template: `<span class="avatar welcome-avatar" aria-hidden="true">{{ initial(name()) }}</span>
    <h2>{{ greeting }}，{{ name() }}</h2>
    <p>{{ identity() ? '以「' + identity() + '」身分，' : '' }}正在開啟你的工作區</p>`,
})
export class LoginWelcome {
  readonly name = input('');
  /** 上一步所選的身分，帳號需要做選擇時才會有。 */
  readonly identity = input<string | null>(null);
  readonly greeting = salutation(new Date().getHours());
  protected readonly initial = nameInitial;
}
