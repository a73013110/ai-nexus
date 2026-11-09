import {
  ChangeDetectionStrategy,
  Component,
  Directive,
  effect,
  inject,
  input,
  model,
  signal,
} from '@angular/core';
import { EyeOff } from 'lucide';
import { uniqueId } from '../browser/unique-id';
import { EXTRA_ICONS, Icon } from './icon';

/**
 * 輸入目前密碼的外框：顯示切換與大寫鎖定提示。輸入框由呼叫端放進來並標 `nxPasswordInput`，
 * 表單綁定、樣式與 `id` 都留在呼叫端。標籤用 `<label for>` 指向輸入框，不要把外框包進 label：
 * 顯示鈕的名稱會併進欄位的無障礙名稱。
 */
@Component({
  selector: 'nx-password-field',
  imports: [Icon],
  providers: [{ provide: EXTRA_ICONS, useValue: { 'eye-off': EyeOff } }],
  changeDetection: ChangeDetectionStrategy.OnPush,
  styles: `
    :host {
      display: block;
    }
    .password-box {
      position: relative;
    }
    button {
      position: absolute;
      inset-block: 0;
      inset-inline-end: 1px;
      margin-block: auto;
    }
    .password-caps {
      margin: var(--p-space-1) 0 0;
      color: var(--warning);
      font-size: var(--text-caption);
      animation: arrive var(--motion) var(--ease-spring);
    }
  `,
  template: `<div class="password-box">
      <ng-content />
      <button
        type="button"
        class="icon-button"
        [attr.aria-pressed]="visible()"
        [attr.aria-label]="visible() ? '隱藏密碼' : '顯示密碼'"
        [attr.title]="visible() ? '隱藏密碼' : '顯示密碼'"
        [disabled]="disabled()"
        (click)="visible.set(!visible())"
      >
        <nx-icon [name]="visible() ? 'eye-off' : 'eye'" />
      </button>
    </div>
    @if (capsLock()) {
      <p class="password-caps" role="status" [id]="hintId">大寫鎖定已開啟</p>
    }`,
})
export class PasswordField {
  readonly visible = model(false);
  /** 送出中傳 `true`：同時收起顯示，密碼不會在等待回應時留在畫面上。 */
  readonly disabled = input(false);
  readonly capsLock = signal(false);
  readonly hintId = uniqueId('nx-password-caps');

  constructor() {
    effect(() => {
      if (this.disabled()) this.visible.set(false);
    });
  }
}

/** 接上 `PasswordField` 的輸入框。 */
@Directive({
  selector: 'input[nxPasswordInput]',
  host: {
    class: 'password-input',
    autocomplete: 'current-password',
    '[type]': 'field.visible() ? "text" : "password"',
    // 寫在元素上才贏得過各頁對 input 的內距設定；右側留給顯示鈕。
    style: 'padding-inline-end: calc(var(--button-target) + var(--p-space-1))',
    '[attr.aria-describedby]': 'field.capsLock() ? field.hintId : null',
    '(keydown)': 'readCapsLock($event)',
    '(keyup)': 'readCapsLock($event)',
    '(blur)': 'field.capsLock.set(false)',
  },
})
export class PasswordInput {
  protected readonly field = inject(PasswordField);
  /** CapsLock 沒有自己的事件，只能從欄位裡的每個按鍵讀狀態。 */
  protected readCapsLock(event: KeyboardEvent) {
    this.field.capsLock.set(event.getModifierState?.('CapsLock') ?? false);
  }
}
