import { Component, afterNextRender, inject } from '@angular/core';
import { Router } from '@angular/router';
import { SettingsOverlay } from '@core/preferences/settings-overlay';
import { WORKSPACE_HOME } from '@core/layout/workspace-home';

/**
 * `/settings`：個人設定唯一能放進書籤、說明文件或通知的網址。設定一律以對話框開在目前頁面上，
 * 這個路由本身沒有畫面，所以先換到首頁再開啟對話框；成本只有這個空元件，因此保留。
 */
@Component({ template: '' })
export class SettingsEntry {
  constructor() {
    const router = inject(Router),
      overlay = inject(SettingsOverlay);
    afterNextRender(async () => {
      await router.navigateByUrl(WORKSPACE_HOME, { replaceUrl: true });
      overlay.open();
    });
  }
}
