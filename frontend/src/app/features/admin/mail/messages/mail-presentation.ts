import type { MailMessageDto } from '@core/api/schema';
import type { BadgeTone } from '@shared/ui/count-badge';

export interface MailStatusView {
  label: string;
  tone: BadgeTone;
}

/**
 * 列表與詳情的狀態。等待重試不是伺服器的狀態：queued 且已嘗試過、下次時間在未來。
 * `now` 由呼叫端傳入，測試才能固定時間。
 */
export function mailStatus(
  message: Pick<MailMessageDto, 'status' | 'attempts' | 'nextAttemptAt'>,
  now = Date.now(),
): MailStatusView {
  switch (message.status) {
    case 'queued':
      return message.attempts > 0 && Date.parse(message.nextAttemptAt) > now
        ? { label: '等待重試', tone: 'warning' }
        : { label: '等待寄送', tone: 'info' };
    case 'sending':
      return { label: '寄送中', tone: 'info' };
    case 'sent':
      return { label: '已寄出', tone: 'success' };
    case 'failed':
      return { label: '寄送失敗', tone: 'danger' };
    case 'cancelled':
      return { label: '已取消', tone: 'neutral' };
    default:
      return { label: message.status, tone: 'neutral' };
  }
}

/** 寄送結果的分類代碼（`MailFailures`、`MailErrors`）；不認得的代碼原樣顯示。 */
const errors: Record<string, string> = {
  mail_rejected: '收件端拒收',
  mail_expired: '已過有效時間，未寄出',
  mail_gave_up: '超過放棄時間，未寄出',
  mail_transport_unavailable: '郵件伺服器無法使用',
  mail_timeout: '連線逾時',
  mail_connection_failed: '無法連線郵件伺服器',
  mail_deferred: '伺服器要求稍後再試',
  mail_delivery_failed: '非預期錯誤',
  mail_address_invalid: '收件地址格式不正確',
  mail_recipient_blocked: '收件網域不在允許清單',
  mail_content_unavailable: '內文已無法取得',
  mail_recipient_unavailable: '收件地址已清除',
};
export const mailError = (code: string | null | undefined) => (code ? errors[code] || code : '');

const outcomes: Record<string, MailStatusView> = {
  sent: { label: '已寄出', tone: 'success' },
  transient: { label: '暫時失敗', tone: 'warning' },
  permanent: { label: '永久失敗', tone: 'danger' },
  transport: { label: '傳輸層故障', tone: 'danger' },
  abandoned: { label: '寄送中斷', tone: 'warning' },
};
export const mailOutcome = (outcome: string): MailStatusView =>
  outcomes[outcome] ?? { label: outcome, tone: 'neutral' };

const priorities: Record<string, string> = { security: '安全', normal: '一般', bulk: '大量' };
export const mailPriority = (priority: string) => priorities[priority] || priority;

/** 不能重寄的原因（`resendBlockedBy`）。 */
export const resendBlocked = (code: string | null | undefined) =>
  code === 'mail_content_unavailable'
    ? '敏感信或內文已依保存期限刪除，不能重寄。'
    : code === 'mail_recipient_unavailable'
      ? '收件地址已依保存期限清除，不能重寄。'
      : '';
