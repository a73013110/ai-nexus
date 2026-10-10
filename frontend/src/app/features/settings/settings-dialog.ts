import { CompactDialog } from '../../shared/ui/compact-dialog';
import {
  ChangeDetectionStrategy,
  Component,
  ElementRef,
  effect,
  inject,
  signal,
  viewChild,
} from '@angular/core';
import { SettingsOverlay } from '../../core/preferences/settings-overlay';
import { AuthService } from '../../core/auth/auth-service';
import { SettingsPage } from './settings-page';
import { ConfirmDialog } from '../../shared/ui/confirm-dialog';

@Component({
  selector: 'nx-settings-dialog',
  styleUrl: './settings-dialog.scss',
  imports: [CompactDialog, SettingsPage, ConfirmDialog],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `<dialog
      nxCompactDialog
      #dialog
      class="platform-dialog settings-dialog"
      aria-label="個人設定"
      (cancel)="$event.preventDefault(); requestClose()"
      (close)="overlay.close()"
    >
      @defer (when overlay.opened()) {
        @if (overlay.opened()) {
          <nx-settings-page
            [embedded]="true"
            (closeRequested)="requestClose()"
            (stateChange)="state.set($event)"
          />
        }
      } @placeholder {
        <p class="settings-loading" role="status">正在開啟設定…</p>
      }
    </dialog>
    <nx-confirm-dialog #discard />`,
})
export class SettingsDialog {
  readonly overlay = inject(SettingsOverlay);
  private readonly auth = inject(AuthService);
  readonly state = signal({ changed: false, saving: false });
  private readonly dialog = viewChild<ElementRef<HTMLDialogElement>>('dialog');
  private readonly discard = viewChild.required<ConfirmDialog>('discard');
  constructor() {
    effect(() => {
      const dialog = this.dialog()?.nativeElement;
      if (!dialog) return;
      if (this.overlay.opened() && !dialog.open) {
        this.state.set({ changed: false, saving: false });
        dialog.showModal();
      } else if (!this.overlay.opened() && dialog.open) dialog.close();
    });
    let generation = this.auth.generation();
    effect(() => {
      const next = this.auth.generation();
      if (next !== generation) {
        generation = next;
        this.discard().answer(false);
        this.overlay.close();
      }
    });
  }
  async requestClose() {
    if (this.state().saving) return;
    if (
      !this.state().changed ||
      (await this.discard().ask({
        title: '放棄尚未儲存的設定？',
        message: '會恢復已儲存的偏好，原頁面的內容與草稿仍保留。',
        confirm: '放棄變更',
      }))
    )
      this.overlay.close();
  }
}
