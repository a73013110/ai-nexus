import { ChangeDetectionStrategy, Component, inject, signal, viewChild } from '@angular/core';
import { DraftRepository } from '@core/preferences/draft-repository';
import { ConfirmDialog } from '@shared/ui/confirm-dialog';
import { Notice } from '@shared/ui/notice';
import { SettingGroup, SettingRow } from '@shared/ui/setting-row';
import { ToastService } from '@shared/ui/toast-service';
import { SettingsStore } from '../settings-store';

/**
 * 未送出草稿在這個瀏覽器的保存方式。對話本身存在伺服器，匯出在各對話的「對話操作」，
 * 所以這裡只管本機資料。
 */
@Component({
  selector: 'nx-data-tab',
  imports: [ConfirmDialog, Notice, SettingGroup, SettingRow],
  changeDetection: ChangeDetectionStrategy.OnPush,
  styles: `
    :host {
      display: flex;
      flex-direction: column;
      gap: var(--section-gap);
    }
    /* 關閉時不佔版面，也不吃掉欄位之間的間距。 */
    nx-confirm-dialog {
      position: absolute;
    }
  `,
  template: `@if (error()) {
      <nx-notice tone="danger" [message]="error()" />
    }
    <nx-setting-group heading="此瀏覽器的草稿">
      <nx-setting-row
        class="data-autosave"
        label="自動保存草稿"
        description="依帳號保存未送出的文字與附件參照，不同步到其他裝置。共用電腦建議關閉。"
        [checked]="store.draft().saveLocalDrafts"
        (checkedChange)="store.update('saveLocalDrafts', $event)"
      />
      <nx-setting-row
        class="data-clear"
        label="清除本機草稿"
        description="移除這個瀏覽器中你所有未送出的草稿；已送出的對話不受影響。"
      >
        <button type="button" class="danger-button" (click)="clearDrafts()">清除草稿</button>
      </nx-setting-row>
    </nx-setting-group>
    <nx-confirm-dialog #confirm />`,
})
export class DataTab {
  readonly store = inject(SettingsStore);
  private readonly drafts = inject(DraftRepository);
  private readonly toast = inject(ToastService);
  private readonly confirm = viewChild.required<ConfirmDialog>('confirm');
  readonly error = signal('');
  async clearDrafts() {
    const owner = this.store.session.me()?.id;
    if (
      !owner ||
      !(await this.confirm().ask({
        title: '清除本機草稿？',
        message: '這個瀏覽器中此帳號所有未送出的文字與附件參照都會移除，無法復原。',
        confirm: '清除草稿',
        danger: true,
      }))
    )
      return;
    const cleared = this.drafts.clearAccount(owner);
    this.error.set(cleared ? '' : '瀏覽器阻擋了儲存空間存取，草稿未清除。');
    if (cleared) this.toast.success('已清除此瀏覽器的草稿');
  }
}
