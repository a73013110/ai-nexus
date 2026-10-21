import type { ProductTourDefinition } from '@shared/ui/product-tour';

/**
 * 價格頁的導覽；步驟依目前分頁切換（另一個分頁的表格不在畫面上）。
 * 測試身分期間新增與帶入都被停用，略過寫入步驟並改說明唯讀。
 */
export function pricesTour(options: {
  /** 目前顯示的分頁。 */
  view: 'current' | 'history';
  /** 正以測試身分檢視，頁面全部唯讀。 */
  testing: boolean;
}): ProductTourDefinition {
  const current = options.view === 'current';
  return {
    steps: [
      {
        target: 'nx-prices-page .feature-title',
        title: '費用依當時的價格計算',
        description:
          '每次模型呼叫建立時，會複製當時有效的價格版本來估算費用；之後改價只影響新的呼叫，已記錄的費用不重算。',
        side: 'bottom',
      },
      {
        target: 'nx-prices-page .price-view',
        title: '目前價格與價格歷史',
        description: options.testing
          ? '「目前價格」列出每個模型與工具適用中的版本，「價格歷史」列出各版本與狀態；測試身分期間只能查看。'
          : '「目前價格」列出每個已設定的模型與工具適用中的版本，「價格歷史」列出最新 500 個版本與狀態。',
        side: 'bottom',
      },
      !options.testing && {
        target: 'nx-prices-page .price-add',
        title: '新增價格版本',
        description:
          '版本不可修改，調價就新增下一個版本；系統不預填價格，也不推定本機模型為零成本，請依實際方案填寫。',
        side: 'bottom',
      },
      current && {
        target: 'nx-prices-page .price-current .price-unpriced',
        title: '未定價的項目',
        description:
          '沒有適用版本的模型或工具標示未定價，費用顯示為未知、不當成免費；之後補上價格也不會回頭重算。',
      },
      current && {
        target: 'nx-prices-page .price-current .price-time',
        title: '生效時間',
        description: '顯示目前版本從何時開始適用；已排定未來版本時，下方註明哪天起改用新版本。',
      },
      current &&
        !options.testing && {
          target: 'nx-prices-page .price-current .price-adjust',
          title: '調整某一項',
          description: '從列開啟對話框，以目前價格為起點；未定價的項目會直接選好該模型或工具。',
          side: 'left',
        },
      !current && {
        target: 'nx-prices-page .price-history .price-state',
        title: '版本狀態',
        description:
          '「適用中」是目前計費的版本，「已排定」在生效時間到了才套用，「已取代」只留作紀錄；備註欄記錄價格依據。',
      },
      !current &&
        !options.testing && {
          target: 'nx-prices-page .price-history .price-reuse',
          title: '帶入舊版本',
          description: '把該版本的欄位複製成新草稿，調整後存成新版本，原版本保持原樣。',
          side: 'left',
        },
    ],
  };
}

/** 新增價格版本對話框的導覽；從既有版本開啟時說明會另存新版本。 */
export function priceVersionTour(options: {
  /** 草稿帶入目前價格或歷史版本（而非空白或未定價項目）。 */
  copied: boolean;
  /** 正以測試身分檢視，不能儲存。 */
  testing: boolean;
}): ProductTourDefinition {
  const target = options.copied
    ? '已帶入既有版本的欄位；儲存會建立新版本，原版本保持原樣。'
    : '依名稱選擇已設定或曾定價的模型、知識向量模型或網路搜尋；計費以供應商與原生識別碼對應。';
  return {
    steps: [
      {
        target: 'nx-price-version-dialog .price-target-section',
        title: '計費對象',
        description: options.testing ? `${target}測試身分期間只能查看，不能儲存。` : target,
        side: 'bottom',
      },
      {
        target: 'nx-price-version-dialog .price-basis',
        title: '幣別與費用類型',
        description:
          'API 計費估算依對外方案，內部成本估算是自行制定的本機成本，免費會把費率固定為 0；不同幣別分開統計、不換算。',
        side: 'bottom',
      },
      {
        target: 'nx-price-version-dialog .price-rates',
        title: '費率的單位',
        description:
          '輸入、快取輸入與輸出以每百萬 tokens 計，最多 8 位小數，快取輸入不得高於一般輸入；每次固定費用另外加計。',
        side: 'bottom',
      },
      {
        target: 'nx-price-version-dialog .price-trigger',
        title: '單次計費時機',
        description:
          '只決定每次固定費用在成功完成時才計，或開始呼叫就計（包含失敗）；token 費一律依有效用量計算。',
        side: 'top',
      },
      {
        target: 'nx-price-version-dialog .price-effective',
        title: '生效時間',
        description:
          '以台北時區輸入；留空即儲存時生效，也可排定未來版本。只影響之後的呼叫，已記錄的費用不重算。',
        side: 'top',
      },
      {
        target: 'nx-price-version-dialog .price-note-field',
        title: '價格依據',
        description: '記下官方價格表或合約日期、內部估算方式，日後在價格歷史的備註欄可查。',
        side: 'top',
      },
    ],
  };
}
