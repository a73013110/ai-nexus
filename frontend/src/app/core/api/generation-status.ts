/** Runs and background jobs share these two non-terminal states. */
export const isActive = (state: string) => state === 'queued' || state === 'running';

export interface GenerationStatus {
  phase: 'queued' | 'preparing' | 'streaming';
  label: string;
  detail: string;
}

/** Describe observed run state, without inferring model activity from reasoning settings. */
export function generationStatus(status: string, hasContent: boolean): GenerationStatus | null {
  if (status === 'queued')
    return { phase: 'queued', label: '等待模型回應', detail: '已加入佇列，可隨時停止' };
  if (status === 'running')
    return hasContent
      ? { phase: 'streaming', label: '正在生成回答', detail: '回答將逐步呈現' }
      : { phase: 'preparing', label: '正在準備回答', detail: '尚未收到回答內容，可隨時停止' };
  return null;
}
