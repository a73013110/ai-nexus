import {
  ChangeDetectionStrategy,
  Component,
  ElementRef,
  computed,
  inject,
  input,
  signal,
  viewChild,
} from '@angular/core';
import type { MailDiagnosisDto } from '@core/api/schema';
import { safeMessage } from '@core/errors/safe-errors';
import { formatDate } from '@shared/browser/format';
import { CompactDialog } from '@shared/ui/compact-dialog';
import { CopyValue } from '@shared/ui/copy-value';
import { DialogHeader } from '@shared/ui/dialog-header';
import { Field } from '@shared/ui/field';
import { Notice } from '@shared/ui/notice';
import { StatusBadge } from '@shared/ui/status-badge';
import { ViewMotion } from '@shared/ui/view-motion';
import { MailApi } from '../mail-api';
import {
  diagnosisStatus,
  diagnosisStep,
  diagnosisVerdict,
  groupedThumbprint,
} from './mail-diagnosis';

/**
 * 連線診斷：以目前設定跑一次 SMTP 對話（不寄出信），逐步顯示到哪裡失敗、伺服器憑證與對話紀錄（AUTH 已遮罩）。
 * 憑證的 SHA-256 指紋可直接複製到 `Mail:Smtp:ServerCertificateThumbprint`。伺服器每人每分鐘限 5 次。
 */
@Component({
  selector: 'nx-mail-diagnosis-dialog',
  imports: [CompactDialog, CopyValue, DialogHeader, Field, Notice, StatusBadge, ViewMotion],
  changeDetection: ChangeDetectionStrategy.OnPush,
  styleUrl: './mail-diagnosis-dialog.scss',
  templateUrl: './mail-diagnosis-dialog.html',
})
export class MailDiagnosisDialog {
  private readonly api = inject(MailApi);
  /** 頁首顯示的伺服器（主機:埠（Security）），讓人確認診斷的是哪一台。 */
  readonly server = input('');
  readonly to = signal('');
  readonly busy = signal(false);
  readonly error = signal('');
  readonly result = signal<MailDiagnosisDto | null>(null);
  readonly view = computed(() => {
    const result = this.result();
    if (!result) return null;
    return {
      result,
      verdict: diagnosisVerdict(result),
      steps: result.steps.map((step) => ({
        step,
        label: diagnosisStep(step.id),
        status: diagnosisStatus(step.status),
      })),
      certificate: result.certificate
        ? {
            ...result.certificate,
            validity: `${formatDate(result.certificate.notBefore)} – ${formatDate(result.certificate.notAfter)}`,
            grouped: groupedThumbprint(result.certificate.sha256),
          }
        : null,
    };
  });
  private readonly dialog = viewChild.required<ElementRef<HTMLDialogElement>>('dialog');

  open() {
    this.error.set('');
    this.result.set(null);
    this.dialog().nativeElement.showModal();
  }
  cancel(event: Event) {
    if (this.busy()) event.preventDefault();
  }
  async run(event: Event) {
    event.preventDefault();
    if (this.busy()) return;
    this.busy.set(true);
    this.error.set('');
    try {
      this.result.set(await this.api.diagnose(this.to().trim()));
    } catch (error) {
      this.error.set(safeMessage(error));
    } finally {
      this.busy.set(false);
    }
  }
}
