import type { BadgeTone } from '@shared/ui/count-badge';

const STATUSES: Record<string, { label: string; tone: BadgeTone }> = {
  queued: { label: '排隊中', tone: 'neutral' },
  running: { label: '處理中', tone: 'info' },
  completed: { label: '已完成', tone: 'success' },
  failed: { label: '處理失敗', tone: 'danger' },
  cancelled: { label: '已停止', tone: 'neutral' },
};

const KINDS: Record<string, { name: string; icon: string }> = {
  'document-ingest': { name: '文件辨識與索引', icon: 'document' },
  'document-embedding': { name: '文件批次向量化', icon: 'database' },
  'embedding-reindex': { name: '檢索索引重建', icon: 'database' },
  'retrieval-eval': { name: '檢索品質評測', icon: 'chart' },
  evaluation: { name: '品質評測', icon: 'lines' },
  'repository-review': { name: '程式碼 Review', icon: 'git' },
  'repository-import': { name: '程式庫匯入', icon: 'git' },
  'integration-import': { name: '資料來源匯入', icon: 'integrations' },
};

/** The name a person reads for a background job's kind. */
export function jobKindName(kind: string) {
  return KINDS[kind]?.name || '背景處理';
}

/** The icon a background job's card shows for its kind. */
export function jobKindIcon(kind: string) {
  return KINDS[kind]?.icon || 'tasks';
}

/** The badge a background job shows for its status. */
export function jobStatus(status: string) {
  return STATUSES[status] ?? { label: '背景處理', tone: 'neutral' as BadgeTone };
}
