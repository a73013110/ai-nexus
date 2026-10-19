import type { JobDto } from '@core/api/schema';
import {
  newLocalIssue,
  recordedProblem,
  systemProblem,
  validIssueCode,
} from '@core/errors/safe-errors';
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

/** 使用者閱讀的背景工作類型名稱。 */
export function jobKindName(kind: string) {
  return KINDS[kind]?.name || '背景處理';
}

/** 背景工作卡片依其類型顯示的圖示。 */
export function jobKindIcon(kind: string) {
  return KINDS[kind]?.icon || 'tasks';
}

/** 背景工作依其狀態顯示的徽章。 */
export function jobStatus(status: string) {
  return STATUSES[status] ?? { label: '背景處理', tone: 'neutral' as BadgeTone };
}

/** 舊資料沒有伺服器查證代碼時，同一工作沿用同一個本機代碼，重繪或換處顯示都不會變。 */
const legacyIssues = new Map<string, string>();

/** 失敗工作給使用者看的訊息：只用審閱過的提示與查證代碼，不顯示儲存的例外訊息。 */
export function jobFailure(job: Pick<JobDto, 'id' | 'errorCode' | 'issueCode'>) {
  if (validIssueCode(job.issueCode)) return recordedProblem(job.errorCode, job.issueCode);
  let code = legacyIssues.get(job.id);
  if (!code) legacyIssues.set(job.id, (code = newLocalIssue()));
  return systemProblem(code);
}
