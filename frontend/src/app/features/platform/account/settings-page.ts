import { NgComponentOutlet } from '@angular/common';
import {
  ChangeDetectionStrategy,
  Component,
  ElementRef,
  computed,
  effect,
  inject,
  output,
  signal,
} from '@angular/core';
import { requestDialogClose } from '@shared/browser/dialog-close';
import { ViewScope } from '@shared/browser/view-scope';
import { DialogSaveBar } from '@shared/ui/dialog-save-bar';
import { Icon } from '@shared/ui/icon';
import { Notice } from '@shared/ui/notice';
import { SearchField } from '@shared/ui/search-field';
import { ViewMotion } from '@shared/ui/view-motion';
import { SETTINGS_TABS } from './settings-tabs';
import { SettingsStore } from './settings-store';

/**
 * 個人設定對話框的內容：左側分類、右側「標題｜捲動的分頁內容｜儲存列」三段。分頁清單在
 * `SETTINGS_TABS`，草稿與儲存在 `SettingsStore`，這裡只負責切換與版面。
 */
@Component({
  selector: 'nx-settings-page',
  imports: [DialogSaveBar, Icon, NgComponentOutlet, Notice, SearchField, ViewMotion],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './settings-page.html',
  styleUrl: './settings-page.scss',
  providers: [ViewScope, SettingsStore],
})
export class SettingsPage {
  /** 對話框關閉前要知道有沒有未儲存的變更、是否正在儲存。 */
  readonly stateChange = output<{ changed: boolean; saving: boolean }>();
  readonly store = inject(SettingsStore);
  private readonly host = inject<ElementRef<HTMLElement>>(ElementRef);
  readonly tabs = SETTINGS_TABS;
  readonly search = signal('');
  readonly tabId = signal(SETTINGS_TABS[0].id);
  readonly current = computed(
    () => this.tabs.find((tab) => tab.id === this.tabId()) ?? this.tabs[0],
  );
  readonly visibleTabs = computed(() => {
    const query = this.search().trim().toLocaleLowerCase();
    return this.tabs.filter((tab) =>
      `${tab.name} ${tab.keywords}`.toLocaleLowerCase().includes(query),
    );
  });
  /** 唯讀分頁平常沒有儲存列；在別的分頁改了還沒存時仍顯示，才不會忘了儲存。 */
  readonly showSaveBar = computed(() => this.current().saved || this.store.changed());
  readonly note = computed(
    () => this.store.notice() || (this.store.changed() ? '' : '設定依帳號保存，其他裝置也會套用。'),
  );

  constructor() {
    effect(() =>
      this.stateChange.emit({ changed: this.store.changed(), saving: this.store.saving() }),
    );
  }

  searchChanged(value: string) {
    this.search.set(value);
    const visible = this.visibleTabs();
    if (visible[0] && !visible.some((tab) => tab.id === this.tabId()))
      this.tabId.set(visible[0].id);
  }

  close() {
    const dialog = this.host.nativeElement.closest('dialog');
    if (dialog) requestDialogClose(dialog);
  }

  save(event: Event) {
    event.preventDefault();
    if (this.store.changed() && !this.store.saving()) void this.store.save();
  }

  /** 還原後兩個按鈕都會停用，焦點交給目前的分類，不掉到 `<body>`。 */
  revert() {
    this.store.revert();
    this.host.nativeElement
      .querySelector<HTMLElement>('.settings-categories [aria-current]')
      ?.focus();
  }
}
