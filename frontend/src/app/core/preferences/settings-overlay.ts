import { Injectable, signal } from '@angular/core';

/** 單一設定表面，由每個帳號選單共用，且不改變目前路由。 */
@Injectable({ providedIn: 'root' })
export class SettingsOverlay {
  readonly opened = signal(false);
  open() {
    this.opened.set(true);
  }
  close() {
    this.opened.set(false);
  }
}
