const KINDS: Record<string, { name: string; icon: string }> = {
  'document-ingest': { name: '文件辨識與索引', icon: 'document' },
  'document-embedding': { name: '文件批次向量化', icon: 'database' },
  'embedding-reindex': { name: '檢索索引重建', icon: 'database' },
  'retrieval-eval': { name: '檢索品質評測', icon: 'chart' },
  evaluation: { name: '品質評測', icon: 'lines' },
  'repository-review': { name: '程式碼 Review', icon: 'git' },
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
