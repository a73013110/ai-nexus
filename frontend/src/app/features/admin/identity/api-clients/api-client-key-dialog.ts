import { ChangeDetectionStrategy, Component, ElementRef, signal, viewChild } from '@angular/core';
import type { IssuedApiClientKeyDto } from '@core/api/schema';
import { CompactDialog } from '@shared/ui/compact-dialog';
import { CopyValue } from '@shared/ui/copy-value';
import { DialogHeader } from '@shared/ui/dialog-header';
import { Notice } from '@shared/ui/notice';

/**
 * 新增或重新產生後顯示一次金鑰。伺服器只存雜湊，關閉後再也拿不到原文，所以只在這裡出現、不進 toast 或網址；
 * 關閉時清掉，避免留在元件狀態裡。
 */
@Component({
  selector: 'nx-api-client-key-dialog',
  imports: [CompactDialog, CopyValue, DialogHeader, Notice],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `<dialog
    nxCompactDialog
    #dialog
    class="platform-dialog"
    size="md"
    aria-label="外部系統金鑰"
    (close)="issued.set(null)"
  >
    @if (issued(); as value) {
      <div class="dialog-form">
        <nx-dialog-header heading="金鑰已產生" [detail]="value.client.name" />
        <div class="dialog-body">
          <nx-notice
            tone="warning"
            message="金鑰只顯示這一次，關閉後無法再查看；遺失時請重新產生。"
          />
          <nx-copy-value
            class="api-client-key"
            label="金鑰"
            [compact]="false"
            [value]="value.key"
          />
          <p class="form-note">對方以 Authorization: Bearer 標頭帶上金鑰呼叫帳密驗證。</p>
        </div>
        <footer class="dialog-foot">
          <button type="button" class="primary-button" (click)="dialog.close()">完成</button>
        </footer>
      </div>
    }
  </dialog>`,
})
export class ApiClientKeyDialog {
  readonly issued = signal<IssuedApiClientKeyDto | null>(null);
  private readonly dialog = viewChild.required<ElementRef<HTMLDialogElement>>('dialog');
  open(issued: IssuedApiClientKeyDto) {
    this.issued.set(issued);
    this.dialog().nativeElement.showModal();
  }
}
