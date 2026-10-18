import { Component, afterNextRender, inject } from '@angular/core';
import { Router } from '@angular/router';
import { SettingsOverlay } from '@core/preferences/settings-overlay';
import { WORKSPACE_HOME } from '@core/layout/workspace-home';

/** 為書籤保留的相容性；一般入口會在目前頁面上開啟。 */
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
