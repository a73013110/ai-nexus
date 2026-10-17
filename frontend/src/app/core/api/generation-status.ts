/** 執行與背景工作共用這兩個非終止狀態。 */
export const isActive = (state: string) => state === 'queued' || state === 'running';

export interface GenerationStatus {
  phase: 'queued' | 'preparing' | 'streaming';
  label: string;
  detail: string;
}

/** 描述觀察到的執行狀態，不從推理設定推斷模型活動。 */
export function generationStatus(status: string, hasContent: boolean): GenerationStatus | null {
  if (status === 'queued')
    return { phase: 'queued', label: '等待模型回應', detail: '已加入佇列，可隨時停止' };
  if (status === 'running')
    return hasContent
      ? { phase: 'streaming', label: '正在生成回答', detail: '回答將逐步呈現' }
      : { phase: 'preparing', label: '正在準備回答', detail: '尚未收到回答內容，可隨時停止' };
  return null;
}
