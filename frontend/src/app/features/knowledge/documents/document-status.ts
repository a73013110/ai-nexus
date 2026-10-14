import type { BadgeTone } from '@shared/ui/count-badge';

const labels: Record<string, string> = {
  ready: '已就緒',
  queued: '等待處理',
  running: '處理中',
  processing: '處理中',
  failed: '需要重試',
  cancelled: '已取消',
};

/** A source document's processing state, worded and toned the same in knowledge and projects. */
export function documentStatus(status: string): { label: string; tone: BadgeTone } {
  return {
    label: labels[status] ?? status,
    tone: status === 'ready' ? 'success' : status === 'failed' ? 'danger' : 'neutral',
  };
}

/** Documents still being read or indexed; their lists poll until these finish. */
export const documentBusy = (status: string) =>
  ['queued', 'running', 'processing'].includes(status);
