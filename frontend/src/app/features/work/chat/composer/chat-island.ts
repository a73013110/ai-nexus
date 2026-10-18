import { computed, inject } from '@angular/core';
import { generationStatus } from '@core/api/generation-status';
import type { IslandItem } from '@shared/ui/island-service';
import { ChatConversations } from '../state/chat-conversations';
import { ChatStore } from '../chat-store';

/**
 * 輸入框需要告訴使用者的一切，以島的通知呈現：進行中執行的進度、
 * 會阻擋或改變送出的狀態，以及草稿的警告。需在 injection context 中呼叫。
 */
export function chatIslandItems() {
  const store = inject(ChatStore);
  const conversations = inject(ChatConversations);
  const status = computed(() => {
    if (store.stopping()) return '正在停止…';
    if (store.connection() === 'reconnecting') return '連線中斷，正在恢復…';
    const run = generationStatus(store.liveRun()?.status ?? '', !!store.streamingText());
    if (run) return run.label;
    if (!store.submitting()) return '';
    return store.webSearchEnabled() ? '正在搜尋網路並準備回答…' : '正在提交訊息…';
  });
  return computed(() => {
    const items: IslandItem[] = [];
    const selected = store.selected();
    const run = store.liveRun();
    const label = status();
    if (label && (!run || run.conversationId === selected?.id))
      items.push({ id: 'generation', priority: 50, tone: 'live', icon: 'activity', label });
    if (run && run.conversationId !== selected?.id)
      items.push({
        id: 'elsewhere',
        priority: 30,
        tone: 'info',
        icon: 'chat',
        label: '另有對話生成中',
        message: '另有對話正在生成。',
        link: { label: '回到該對話', commands: ['/chat', run.conversationId] },
      });
    if (store.editing())
      items.push({
        id: 'editing',
        priority: 70,
        tone: 'info',
        icon: 'edit',
        label: '編輯中',
        message: '編輯提問 · 送出後建立新分支，原始內容保留',
        urgent: true,
        action: { label: '取消編輯', icon: 'close', run: () => store.cancelEdit() },
      });
    if (selected?.isArchived)
      items.push({
        id: 'archived',
        priority: 60,
        tone: 'info',
        icon: 'archive',
        label: '已封存',
        message: '這段對話已封存，還原後即可繼續。',
        urgent: true,
        action: {
          label: '還原對話',
          run: () => void conversations.organize(selected, { isArchived: false }),
        },
      });
    const attachmentError = store.attachments.error();
    if (attachmentError)
      items.push({
        id: 'attachment',
        priority: 90,
        tone: 'danger',
        icon: 'paperclip',
        label: '附件無法使用',
        message: attachmentError,
        urgent: true,
      });
    if (store.visionNotice())
      items.push({
        id: 'vision',
        priority: 85,
        tone: 'warning',
        icon: 'image',
        label: '模型不支援圖片',
        message: store.visionNotice(),
        urgent: true,
      });
    if (store.contextUsage()?.budgetExceeded)
      items.push({
        id: 'budget',
        priority: 84,
        tone: 'warning',
        icon: 'info',
        label: '超出 Context 預算',
        message: '本次提問與附件超出 Context 預算，請縮短內容或減少附件。',
        urgent: true,
      });
    const limit = store.policy().maxInputCharacters;
    if (store.draft().text.length > limit)
      items.push({
        id: 'length',
        priority: 83,
        tone: 'warning',
        icon: 'lines',
        label: '超過字數上限',
        message: `訊息超過系統允許的 ${limit} 個字元，請拆分內容。`,
        urgent: true,
      });
    const contextNotice = store.contextNotice();
    if (contextNotice)
      items.push({
        id: 'context',
        priority: 20,
        tone: 'warning',
        icon: 'info',
        label: 'Context 預估暫停',
        message: contextNotice,
      });
    const modelNotice = store.modelNotice();
    if (modelNotice)
      items.push({
        id: 'model',
        priority: 15,
        tone: 'info',
        icon: 'info',
        label: '模型提示',
        message: modelNotice,
        urgent: true,
      });
    if (store.webSearchEnabled())
      items.push({
        id: 'web-search',
        priority: 10,
        tone: 'info',
        icon: 'globe',
        label: '網路搜尋',
        message: '會將這次提問送往搜尋服務；附件與歷史對話不會送出。',
        urgent: true,
      });
    return items;
  });
}
