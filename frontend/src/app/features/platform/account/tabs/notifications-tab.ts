import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { ViewScope } from '@shared/browser/view-scope';
import { SettingGroup, SettingRow } from '@shared/ui/setting-row';
import { SettingsStore } from '../settings-store';

type Permission = NotificationPermission | 'unsupported';

/** 回覆完成時的瀏覽器通知；開關之前要先取得瀏覽器的通知權限。 */
@Component({
  selector: 'nx-notifications-tab',
  imports: [SettingGroup, SettingRow],
  changeDetection: ChangeDetectionStrategy.OnPush,
  providers: [ViewScope],
  styles: `
    :host {
      display: flex;
      flex-direction: column;
      gap: var(--section-gap);
    }
  `,
  template: `<nx-setting-group heading="回覆完成">
    <nx-setting-row
      label="回覆完成通知"
      description="工作區在背景時提醒你回覆已完成；通知不含提問或回覆內容。"
      [checked]="store.draft().notifyOnCompletion"
      [disabled]="permission() !== 'granted'"
      (checkedChange)="store.update('notifyOnCompletion', $event)"
    >
      @if (permission() === 'default') {
        <button type="button" class="secondary-button" (click)="requestPermission()">
          允許瀏覽器通知
        </button>
      }
      @if (permission() === 'denied') {
        <span setting-detail> 瀏覽器已封鎖通知，可在網址列的網站權限中重新允許。 </span>
      }
      @if (permission() === 'unsupported') {
        <span setting-detail>
          這個環境無法使用瀏覽器通知；需要支援通知的瀏覽器，並以 HTTPS 或 localhost 開啟。
        </span>
      }
    </nx-setting-row>
  </nx-setting-group>`,
})
export class NotificationsTab {
  readonly store = inject(SettingsStore);
  private readonly scope = inject(ViewScope);
  readonly permission = signal<Permission>(
    'Notification' in window ? Notification.permission : 'unsupported',
  );
  async requestPermission() {
    if (!('Notification' in window)) return;
    const current = this.scope.guard();
    const permission = await Notification.requestPermission();
    if (!current()) return;
    this.permission.set(permission);
    if (permission === 'granted') this.store.update('notifyOnCompletion', true);
  }
}
