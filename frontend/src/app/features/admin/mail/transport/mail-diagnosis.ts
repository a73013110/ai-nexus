import type { MailDiagnosisDto } from '@core/api/schema';
import type { BadgeTone } from '@shared/ui/count-badge';

const steps: Record<string, string> = {
  connect: 'TCP 連線',
  ehlo: '問候與 EHLO',
  tls: 'TLS',
  certificate: '伺服器憑證',
  auth: '帳密驗證',
  sender: '寄件者（MAIL FROM）',
  recipient: '收件者（RCPT TO）',
  reset: '結束對話（RSET）',
  directory: 'Pickup 目錄',
};
export const diagnosisStep = (id: string) => steps[id] || id;

const statuses: Record<string, { label: string; tone: BadgeTone }> = {
  passed: { label: '通過', tone: 'success' },
  warning: { label: '注意', tone: 'warning' },
  failed: { label: '失敗', tone: 'danger' },
  skipped: { label: '略過', tone: 'neutral' },
};
export const diagnosisStatus = (status: string): { label: string; tone: BadgeTone } =>
  statuses[status] ?? { label: status, tone: 'neutral' };

/** 結論一句話：成功、有注意事項，或第一個失敗的步驟。 */
export function diagnosisVerdict(result: Pick<MailDiagnosisDto, 'succeeded' | 'steps'>): {
  tone: 'success' | 'warning' | 'danger';
  message: string;
} {
  const failed = result.steps.find((step) => step.status === 'failed');
  if (failed)
    return { tone: 'danger', message: `在「${diagnosisStep(failed.id)}」失敗：${failed.summary}` };
  const warned = result.steps.find((step) => step.status === 'warning');
  return warned
    ? {
        tone: 'warning',
        message: `可以寄送，但「${diagnosisStep(warned.id)}」需要注意：${warned.summary}`,
      }
    : { tone: 'success', message: '每一步都通過，可以寄送。' };
}

/** 指紋每 4 碼一組，方便對照；複製時用原值。 */
export const groupedThumbprint = (value: string) => value.match(/.{1,4}/g)?.join(' ') ?? value;
