import type { ProductTourDefinition } from '../../shared/ui/product-tour';

/**
 * The overview tour. Steps for controls or tiles the viewer cannot see (no platform scope, no
 * currencies yet, a hidden tile) are skipped when the tour starts.
 */
export const DASHBOARD_TOUR: ProductTourDefinition = {
  steps: [
    {
      target: 'nx-dashboard-page .feature-title',
      title: '一句話掌握現況',
      description:
        '問候下方會說明目前有幾件工作處理中，或哪些功能需要留意。「我的工作」每 60 秒自動更新。',
      side: 'bottom',
    },
    {
      target: 'nx-dashboard-page .dashboard-scope',
      title: '切換總覽範圍',
      description:
        '選「整個平台」查看所有人的用量與使用者區間費用；這個範圍每次讀取都會留下稽核紀錄。',
      side: 'bottom',
    },
    {
      target: 'nx-dashboard-toolbar nx-view-switch',
      title: '選擇查詢期間',
      description: '選了就套用，不必按查詢；選「自訂」再輸入開始與結束日期。',
      side: 'bottom',
    },
    {
      target: 'nx-dashboard-toolbar .toolbar-unit',
      title: '幣別與費用類型分開看',
      description: '不同幣別與費用類型不會相加；在這裡切換卡片顯示的幣別與類型。',
      side: 'bottom',
    },
    {
      target: 'nx-dashboard-tile[data-id="overview"]',
      title: '先看關鍵指標',
      description: '期間內的已知費用、模型與工具呼叫、Token 與處理中工作，附每日走勢。',
    },
    {
      target: 'nx-dashboard-tile[data-id^="section:"]',
      title: '每個功能一張卡片',
      description:
        '有權限的功能各有一張卡片：目前數量、標「期間」的區間數字、每日走勢與自己最近的項目。',
    },
    {
      target: 'nx-dashboard-tile[data-id="recent"]',
      title: '從上次停下的地方繼續',
      description: '列出各功能中你最近的對話、知識庫、專案、成果與任務，點選即可回到工作。',
    },
    {
      target: 'nx-dashboard-page .dashboard-customize',
      title: '排出自己的總覽',
      description:
        '「自訂版面」可拖曳排序、調整大小、加高或隱藏卡片。版面只存在這個瀏覽器，可隨時恢復預設。',
      side: 'bottom',
    },
    {
      target: 'nx-dashboard-page .dashboard-refresh',
      title: '需要最新數字時',
      description: '按重新整理立即讀取。右上角的問號圖示可隨時重播導覽。',
      side: 'bottom',
    },
  ],
};
