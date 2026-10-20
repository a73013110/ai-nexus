import { ChangeDetectionStrategy, Component, computed, inject } from '@angular/core';
import { SHORTCUT_GROUPS } from '@core/layout/keyboard-shortcuts';
import { KeyCombo } from '@shared/ui/key-combo';
import { SettingGroup, SettingRow } from '@shared/ui/setting-row';
import { SettingsStore } from '../settings-store';

/**
 * 清單來自 `SHORTCUT_GROUPS`，與實際綁定的鍵同一份資料；送出與換行依「對話」分頁的 Enter 設定，
 * 跟著尚未儲存的草稿一起變，先看到改了之後的按法。
 */
@Component({
  selector: 'nx-shortcuts-tab',
  imports: [KeyCombo, SettingGroup, SettingRow],
  changeDetection: ChangeDetectionStrategy.OnPush,
  styles: `
    :host {
      display: flex;
      flex-direction: column;
      gap: var(--section-gap);
    }
  `,
  template: `@for (group of groups(); track group.id) {
    <nx-setting-group [heading]="group.name" [description]="group.description ?? ''">
      @for (item of group.items; track item.keys) {
        <nx-setting-row [label]="item.label"><nx-key-combo [keys]="item.keys" /></nx-setting-row>
      }
    </nx-setting-group>
  }`,
})
export class ShortcutsTab {
  private readonly store = inject(SettingsStore);
  readonly groups = computed(() => {
    const enter = this.store.draft().enterToSend;
    const typing = [
      { keys: enter ? 'enter' : 'mod+enter', label: '送出訊息' },
      { keys: enter ? 'shift+enter' : 'enter', label: '輸入框換行' },
    ];
    return SHORTCUT_GROUPS.map((group) =>
      group.id === 'chat' ? { ...group, items: [...typing, ...group.items] } : group,
    );
  });
}
