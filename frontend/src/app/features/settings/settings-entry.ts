import { Component, afterNextRender, inject } from '@angular/core';
import { Router } from '@angular/router';
import { SettingsOverlay } from '../../core/preferences/settings-overlay';

/** Compatibility for bookmarks; regular entry points open over the current page. */
@Component({ template: '' })
export class SettingsEntry {
  constructor() {
    const router = inject(Router),
      overlay = inject(SettingsOverlay);
    afterNextRender(async () => {
      await router.navigateByUrl('/chat', { replaceUrl: true });
      overlay.open();
    });
  }
}
