/** `keys` 用 `hotkey()` 的格式（`mod` 是 Ctrl 或 ⌘），顯示時由 `KeyCombo`／`keyText` 依平台換字。 */
export interface Shortcut {
  keys: string;
  label: string;
}

export interface ShortcutGroup {
  id: 'global' | 'chat' | 'documents';
  name: string;
  description?: string;
  items: readonly Shortcut[];
}

/**
 * 全站快捷鍵的唯一來源：`bindHotkeys` 綁定的鍵、Ctrl+K 指令旁的按鍵與個人設定的「鍵盤快捷鍵」
 * 都由這裡產生，改鍵只改一處。F2、Esc 由各元件在焦點所在處處理（`InlineTitle`、對話框），
 * 這裡只列出說明。
 */
export const SHORTCUTS = {
  commandPalette: { keys: 'mod+k', label: '快捷指令：搜尋功能、動作與對話' },
  newChat: { keys: 'mod+alt+n', label: '開始新對話' },
  promptLibrary: { keys: 'mod+shift+l', label: '開啟提示詞範本' },
  renameConversation: { keys: 'f2', label: '修改目前對話名稱（焦點在標題時）' },
  saveArtifact: { keys: 'mod+s', label: '儲存編輯中的成果文件' },
  findInDocument: { keys: 'mod+f', label: '搜尋文件內文（閱讀器內）' },
  dismiss: { keys: 'escape', label: '關閉選單或對話框、取消編輯' },
} as const satisfies Record<string, Shortcut>;

/** 個人設定列出的分組，依使用頻率排列；送出訊息的鍵依個人設定而定，由設定頁加進 `chat`。 */
export const SHORTCUT_GROUPS: readonly ShortcutGroup[] = [
  {
    id: 'global',
    name: '全站',
    description: '選單與清單都可用方向鍵、Home／End 移動，Enter 選取。',
    items: [SHORTCUTS.commandPalette, SHORTCUTS.dismiss],
  },
  {
    id: 'chat',
    name: '對話',
    items: [SHORTCUTS.newChat, SHORTCUTS.promptLibrary, SHORTCUTS.renameConversation],
  },
  { id: 'documents', name: '文件', items: [SHORTCUTS.saveArtifact, SHORTCUTS.findInDocument] },
];
