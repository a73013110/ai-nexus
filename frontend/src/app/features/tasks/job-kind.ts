const NAMES: Record<string, string> = {
  'document-ingest': '文件辨識與索引',
  'document-embedding': '文件批次向量化',
  'embedding-reindex': '檢索索引重建',
  'retrieval-eval': '檢索品質評測',
  evaluation: '品質評測',
  'repository-review': '程式碼 Review',
  'integration-import': '資料來源匯入',
};

/** The name a person reads for a background job's kind. */
export function jobKindName(kind: string) {
  return NAMES[kind] || '背景處理';
}
