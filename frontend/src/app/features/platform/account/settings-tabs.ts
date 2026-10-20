import type { Type } from '@angular/core';
import { AccountTab } from './tabs/account-tab';
import { AppearanceTab } from './tabs/appearance-tab';
import { ConversationTab } from './tabs/conversation-tab';
import { DataTab } from './tabs/data-tab';
import { NotificationsTab } from './tabs/notifications-tab';
import { ShortcutsTab } from './tabs/shortcuts-tab';
import { UsageTab } from './tabs/usage-tab';

/** 個人設定的一個分頁；新增分頁只要在 `SETTINGS_TABS` 加一筆。 */
export interface SettingsTab {
  id: string;
  name: string;
  icon: string;
  /** 標題下的一句說明。 */
  description: string;
  /** 搜尋分類時除了名稱之外比對的詞，用空白分隔。 */
  keywords: string;
  /** 分頁有寫回帳號的設定：顯示儲存列。唯讀分頁只在別處有未儲存的變更時才顯示它。 */
  saved: boolean;
  component: Type<unknown>;
}

export const SETTINGS_TABS: readonly SettingsTab[] = [
  {
    id: 'appearance',
    name: '外觀與閱讀',
    icon: 'sliders',
    description: '主題、動態效果，以及對話正文的字級、行距與寬度。',
    keywords: '主題 深色 淺色 字級 字體 行距 密度 側欄 寬度 動畫 動態',
    saved: true,
    component: AppearanceTab,
  },
  {
    id: 'conversation',
    name: '對話',
    icon: 'chat',
    description: '輸入方式，以及新對話預設的模型與思考強度。',
    keywords: '模型 思考 強度 網路搜尋 SearXNG Enter 送出 換行 自動捲動 跟隨',
    saved: true,
    component: ConversationTab,
  },
  {
    id: 'notifications',
    name: '通知',
    icon: 'bell',
    description: '回覆在背景完成時，是否用瀏覽器通知提醒你。',
    keywords: '完成 回覆 瀏覽器 提醒',
    saved: true,
    component: NotificationsTab,
  },
  {
    id: 'data',
    name: '資料與草稿',
    icon: 'archive',
    description: '未送出的草稿怎麼保存在這個瀏覽器。',
    keywords: '草稿 保存 清除 本機 瀏覽器',
    saved: true,
    component: DataTab,
  },
  {
    id: 'account',
    name: '帳號與權限',
    icon: 'user',
    description: '你的身分、登入方式，以及角色開放的功能。',
    keywords: 'AD 登入 角色 功能 權限 帳號 姓名',
    saved: false,
    component: AccountTab,
  },
  {
    id: 'usage',
    name: '使用狀況',
    icon: 'chart',
    description: '今日 token 預算、最近 30 天的用量與原檔容量。',
    keywords: '用量 tokens 預算 額度 檔案 容量 統計 限制',
    saved: false,
    component: UsageTab,
  },
  {
    id: 'shortcuts',
    name: '鍵盤快捷鍵',
    icon: 'command',
    description: '常用操作的按鍵，依你的作業系統顯示。',
    keywords: '按鍵 快速 指令 Ctrl',
    saved: false,
    component: ShortcutsTab,
  },
];
