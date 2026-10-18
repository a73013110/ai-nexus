import type { BadgeTone } from '@shared/ui/count-badge';

const labels: Record<string, string> = {
  ready: '已就緒',
  queued: '等待處理',
  running: '處理中',
  processing: '處理中',
  failed: '需要重試',
  cancelled: '已取消',
};

/** 來源文件的處理狀態，在知識庫與專案中以相同的用語與色調呈現。 */
export function documentStatus(status: string): { label: string; tone: BadgeTone } {
  return {
    label: labels[status] ?? status,
    tone: status === 'ready' ? 'success' : status === 'failed' ? 'danger' : 'neutral',
  };
}

/** 仍在讀取或建立索引中的文件；其列表會輪詢，直到它們完成。 */
export const documentBusy = (status: string) =>
  ['queued', 'running', 'processing'].includes(status);
