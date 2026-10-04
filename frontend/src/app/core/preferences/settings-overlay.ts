import { Injectable, signal } from '@angular/core';

/** One settings surface, shared by every account menu without changing the current route. */
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
