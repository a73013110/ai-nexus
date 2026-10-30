import type { ProductTourDefinition } from '@shared/ui/product-tour';

const PAGE = 'nx-monitoring-page';
const DRAWER = `${PAGE} nx-detail-drawer`;

/**
 * 即時監控頁的導覽。步驟指向每 3 秒更新時不會重建的區塊容器，不指向列項；
 * 第一份快照到達前儀表板不在畫面上，這些步驟會自動略過。
 */
export function monitoringTour(options: {
  /** 伺服器回報的更新間隔；尚未收到快照時為 null。 */
  refreshSeconds: number | null;
  /** 系統日誌的顯示名稱；沒有 `diagnostics.logs` 授權時為 null（操作動態不顯示追查連結）。 */
  logs: string | null;
  /** 活動稽核的顯示名稱；沒有 `audit` 授權時為 null。 */
  audit: string | null;
}): ProductTourDefinition {
  const every = options.refreshSeconds ? `每 ${options.refreshSeconds} 秒` : '每隔幾秒';
  return {
    steps: [
      {
        target: `${PAGE} .feature-title`,
        title: '這個執行個體正常嗎',
        description:
          '只觀測目前這個應用程式執行個體，不主動探測。\n保留最近 15 分鐘，重啟後重新採集。\n頁尾「觀測範圍與資料定義」說明各數字怎麼量。',
        side: 'bottom',
      },
      {
        target: `${PAGE} nx-monitoring-hero`,
        title: '一句話回答健康',
        description:
          '依錯誤率、5xx、延遲、程序資源、周邊服務與子系統，判定正常、需要注意或嚴重問題，並列出問題。\n與周邊服務或子系統有關的問題，可點開詳情。',
        side: 'bottom',
      },
      {
        target: `${PAGE} .monitor-actions nx-view-switch`,
        title: '時間範圍',
        description:
          '關鍵指標固定為最近 1 分鐘；走勢、周邊服務、熱門 API 與操作動態依這裡選的 1／5／15 分鐘。',
        side: 'bottom',
      },
      {
        target: `${PAGE} .monitor-pause`,
        title: '自動更新',
        description: `畫面${every}自動更新，暫停後停在目前快照；斷線時保留最近快照並自動重連，背景分頁會停止訂閱。`,
        side: 'bottom',
      },
      {
        target: `${PAGE} .monitor-export`,
        title: '匯出快照',
        description: `把目前快照下載成 JSON，方便回報或留存${
          options.audit ? `；匯出會留下紀錄，可在「${options.audit}」查到` : ''
        }。`,
        side: 'bottom',
      },
      {
        target: `${PAGE} nx-monitoring-service-map`,
        title: '周邊服務',
        description:
          'SQL 與 HTTP 依賴依所選範圍內的實際呼叫判定狀態；「尚無觀測」不代表故障。點選服務看呼叫數、耗時與失敗。',
      },
      {
        target: `${PAGE} nx-monitoring-subsystems`,
        title: '子系統',
        description:
          '生成佇列、背景工作、郵件、日誌管線、知識索引、儲存空間等，由各模組在背景回報，頻率從數秒到數分鐘不等。\n讀取失敗或逾時，也列為需要注意。\n點卡片看指標。',
      },
      {
        target: `${PAGE} nx-monitoring-people`,
        title: '線上人員',
        description:
          '以瀏覽器心跳判定在線，一個分頁算一個工作階段。\n可依功能、狀態篩選與搜尋，點選看工作階段詳情。',
      },
      {
        target: `${PAGE} nx-monitoring-activity`,
        title: '操作動態與熱門 API',
        description: options.logs
          ? `列出所選範圍內的異動、錯誤與中止，以及最常呼叫的 API；「追查流程」依 trace ID 開啟${options.logs}。`
          : '列出所選範圍內的異動、錯誤與中止，以及最常呼叫的 API 與平均耗時。',
      },
    ],
  };
}

/** 監控詳情抽屜的導覽；工作階段多了可複製的識別與上下筆。 */
export function monitoringDrawerTour(
  kind: 'session' | 'dependency' | 'subsystem',
): ProductTourDefinition {
  const session = kind === 'session';
  const data = {
    session:
      '身分驗證、目前功能、裝置與最近操作，以及這個工作階段累計的請求、錯誤與傳輸量。\n不收集輸入內容。',
    dependency:
      '所選範圍內的呼叫數、速率、平均與 P95 耗時、失敗與執行中的呼叫；不含 SQL 指令或資料內容。',
    subsystem: '模組回報的指標與最近讀取時間；讀取失敗或逾時時列為需要注意。',
  }[kind];
  return {
    steps: [
      {
        target: `${DRAWER} .ui-drawer-summary`,
        title: '狀態',
        description: '狀態與時間跟著快照即時更新；項目離開快照後只保留名稱。',
        side: 'bottom',
      },
      {
        target: `${DRAWER} nx-monitoring-inspector .ui-description-list`,
        title: '主要數據',
        description: data,
      },
      session && {
        target: `${DRAWER} nx-monitoring-inspector nx-copy-value`,
        title: '連線位址與識別',
        description: '可直接複製；IP 可能是代理位址，工作階段識別是隨機的分頁代碼。',
      },
      session && {
        target: `${DRAWER} nx-drawer-pager`,
        title: '上一筆／下一筆',
        description: '在線上人員表格目前這一頁內切換工作階段。',
        side: 'bottom',
      },
      {
        target: `${DRAWER} .ui-raw-record`,
        title: '原始資料',
        description: '展開查看這一項在快照中的完整 JSON。',
      },
    ],
  };
}
