import { cursorPaginationStep, queryBarStep } from '@shared/ui/data-workspace-tour';
import type { ProductTourDefinition, TourStep } from '@shared/ui/product-tour';
import { MAX_REIMPORT_DAYS } from './imports/imports-query';

const CARDS = 'nx-attendance-cards-page';
const CARD_DRAWER = `${CARDS} nx-detail-drawer`;
const IMPORTS = 'nx-attendance-imports-page';
const REIMPORT = 'nx-reimport-dialog';
const CALENDAR = 'nx-attendance-calendar-page';
const DAY_DIALOG = 'nx-calendar-day-dialog';
const RECORDS = 'nx-attendance-records-page';

/** 每頁第一步指向頁首標題，先說這頁的資料從哪來、影響什麼。 */
const purposeStep = (host: string, title: string, description: string): TourStep => ({
  target: `${host} .feature-title`,
  title,
  description,
  side: 'bottom',
});

/** 出勤管理頁共用的條件：是否有寫入動作。 */
interface WriteAccess {
  /** 有該頁的寫入動作（例如 `attendance.cards.maintain`）。 */
  maintain: boolean;
}

/** 卡號對應頁：一張卡號清單，狀態切換篩持有中、無人持有或隱藏。 */
export function attendanceCardsTour(options: WriteAccess): ProductTourDefinition {
  const write = options.maintain;
  return {
    steps: [
      purposeStep(
        CARDS,
        '刷卡依持有期間歸戶',
        '打卡機只記卡號。\n卡號指派了持有人與期間，期間內的刷卡才算進這個人的出勤。\n打卡檔出現的新卡號會自動列進來。',
      ),
      write && {
        target: `${CARDS} .cards-create`,
        title: '新增卡號',
        description:
          '刷過的卡號匯入時會自動出現。\n先發卡、人還沒報到時，可先新增沒刷過的卡並指派持有人。',
        side: 'bottom',
      },
      {
        target: `${CARDS} .cards-status`,
        title: '持有中、無人持有與隱藏',
        description:
          '・無人持有：現在沒有持有人的卡號，例如新人的卡還沒指派，或前一位已結束持有。\n・待指派：有刷卡卻沒有持有人，這些刷卡不會出現在任何人的出勤紀錄。\n・隱藏：HR 看全公司出勤時略過的卡，例如訪客卡。',
        side: 'bottom',
      },
      queryBarStep(CARDS, { fields: '卡號、備註或持有人姓名' }),
      {
        target: `${CARDS} .cards-table .cards-number`,
        title: '卡片詳情',
        description: write
          ? '點卡號開啟詳情。\n可指派持有人、補指派沒有持有人的刷卡，並設定是否在出勤紀錄顯示。'
          : '點卡號開啟詳情，查看這張卡歷任的持有人與期間。',
      },
    ],
  };
}

/** 卡片詳情抽屜：寫入步驟只在有維護權限時出現。 */
export function cardDetailTour(options: WriteAccess): ProductTourDefinition {
  const write = options.maintain;
  return {
    steps: [
      {
        target: `${CARD_DRAWER} .cards-holder`,
        title: '目前持有人',
        description: '目前持有人是今天落在持有期間內的人。',
        side: 'bottom',
      },
      write && {
        target: `${CARD_DRAWER} .cards-assign`,
        title: '指派或轉給其他人',
        description:
          '搜尋平台使用者；新進員工還沒有帳號時，選「使用姓名」。\n卡片已有人持有時就是轉給其他人，前一段在新的開始時間結束。',
        side: 'bottom',
      },
      {
        target: `${CARD_DRAWER} .cards-visibility`,
        title: '在出勤紀錄顯示',
        description:
          '關閉後，HR 看全公司出勤與匯出時會略過這張卡（例如訪客卡、董事）。\n這不是權限：本人與主管照常看得到，指定這個人或卡號時也查得到。',
        side: 'bottom',
      },
      {
        target: `${CARD_DRAWER} .cards-history`,
        title: '持有歷史',
        description:
          '同一張卡的期間不可重疊。\n「尚無帳號」是只填姓名的期間，綁定帳號後才比對得到假單。\n每段的「⋯」可編輯或刪除。',
      },
      write && {
        target: `${CARD_DRAWER} .cards-end`,
        title: '結束持有',
        description:
          '換卡或離職，用「結束持有」，歷史會保留。\n刪除期間會讓這段刷卡變回待指派，只在指派錯誤時使用。',
        side: 'left',
      },
      {
        target: `${CARD_DRAWER} .cards-punches`,
        title: '刷卡紀錄',
        description:
          '從卡片的角度，看這張卡刷過的每一筆與當時歸給誰。\n「無持有人」的刷卡不會出現在出勤紀錄。\n這裡不判定遲到早退，請到出勤紀錄看。',
      },
    ],
  };
}

/** 打卡匯入紀錄頁：手動重匯步驟只給有 `attendance.imports.reimport` 的人。 */
export function attendanceImportsTour(options: WriteAccess): ProductTourDefinition {
  return {
    steps: [
      purposeStep(
        IMPORTS,
        '每天一個打卡檔',
        '系統定時掃描打卡機每天輸出的檔，檔案有變動就重新匯入，每次留一筆紀錄。\n任何一行格式錯誤，整個檔就失敗，刷卡維持上次成功的內容。',
      ),
      options.maintain && {
        target: `${IMPORTS} .imports-reimport`,
        title: '手動重匯',
        description: `修正打卡檔或補放遺漏的檔之後使用。
可選單一檔案，或最多 ${MAX_REIMPORT_DAYS} 天的日期區間。
不論檔案有沒有變，都會重新匯入。`,
        side: 'bottom',
      },
      {
        target: `${IMPORTS} nx-query-bar nx-view-switch`,
        title: '先看失敗的檔',
        description: '切到「失敗」，只列匯入失敗的檔，原因寫在結果欄。\n點選立即套用。',
        side: 'bottom',
      },
      queryBarStep(IMPORTS, { fields: '檔名（例如 20261001）與匯入時間' }),
      {
        target: `${IMPORTS} .imports-table .ui-table-toolbar`,
        title: '匯入紀錄',
        description:
          '點一筆看這次匯入寫入的刷卡。\n觸發是排程或手動，手動會標示操作者。\n找不到檔的日期不留紀錄。',
        side: 'bottom',
      },
      cursorPaginationStep(IMPORTS, { sortable: false }),
    ],
  };
}

/** 手動重匯對話框：單一檔案與日期區間各自的日期欄位。 */
export function reimportTour(options: { mode: 'single' | 'range' }): ProductTourDefinition {
  return {
    steps: [
      {
        target: `${REIMPORT} nx-view-switch`,
        title: '單一檔案或日期區間',
        description: `補一天用單一檔案。
打卡機或資料夾停擺幾天，用日期區間：一次最多 ${MAX_REIMPORT_DAYS} 天，由舊到新逐檔重匯。`,
        side: 'bottom',
      },
      options.mode === 'single'
        ? {
            target: `${REIMPORT} nx-date-time-picker`,
            title: '檔名日期',
            description: '選打卡機輸出檔名上的日期，不能晚於今天。\n下方會列出要重匯的檔名。',
            side: 'bottom',
          }
        : {
            target: `${REIMPORT} .reimport-range`,
            title: '開始與結束',
            description: `含頭尾，結束不能早於開始；超過 ${MAX_REIMPORT_DAYS} 天請分次。
區間內找不到的檔會略過，完成後一起列出。`,
            side: 'bottom',
          },
      {
        target: `${REIMPORT} .reimport-note`,
        title: '檔名日期不是出勤日',
        description:
          '檔內可能有前一天深夜的刷卡，出勤日依刷卡時間，不看檔名。\n成功的檔以通知回報；失敗的檔與原因留在頁面上方。',
        side: 'top',
      },
    ],
  };
}

/** 假日設定頁：步驟依月曆或清單切換；能不能點日期修改取決於維護權限與公文系統是否唯讀。 */
export function attendanceCalendarTour(
  options: WriteAccess & {
    view: 'calendar' | 'list';
    /** 公文系統的連線設為唯讀（回寫被拒過），暫時不能修改。 */
    gdReadOnly: boolean;
  },
): ProductTourDefinition {
  const calendar = options.view === 'calendar';
  const editable = options.maintain && !options.gdReadOnly;
  return {
    steps: [
      purposeStep(
        CALENDAR,
        '假日來自公文系統',
        '這裡讀寫的是公文系統的假日行事曆，平台不另存一份。\n出勤判定依每天上午、下午是否放假。',
      ),
      {
        target: `${CALENDAR} .calendar-nav`,
        title: '切換月份',
        description:
          '月份寫進網址，重新整理或分享連結都會停在同一個月。\n「本月」回到今天所在的月份。',
        side: 'bottom',
      },
      {
        target: `${CALENDAR} .calendar-view-switch`,
        title: '月曆或清單',
        description:
          '月曆一眼看出整月的放假。\n清單逐日列出上午、下午與備註；窄螢幕的月曆只留底色，要看細節請改用清單。',
        side: 'bottom',
      },
      calendar && {
        target: `${CALENDAR} nx-month-calendar`,
        title: editable ? '點日期修改' : '讀月曆',
        description: `一週從週日開始。
放假的日子塗底色；半天放假只塗上半格（上午）或下半格（下午）。
週末上班會標出來。${editable ? '\n點日期開啟編輯；方向鍵移動，Enter 開啟。' : ''}`,
        side: 'top',
      },
      calendar && {
        target: `${CALENDAR} .holiday-legend`,
        title: '未建立的日期',
        description:
          '公文系統沒有這一天，就標「未建立」。\n不能在這裡新增，請到公文系統補上。\n出勤判定時，週末當放假，平日無法判定。',
        side: 'top',
      },
      !calendar && {
        target: `${CALENDAR} .calendar-table .ui-table-toolbar`,
        title: '逐日清單',
        description: `每天一列。
「未建立」是公文系統沒有的日期，不能修改。${editable ? '\n點日期開啟編輯。' : ''}`,
        side: 'bottom',
      },
    ],
  };
}

/** 假日編輯對話框：選項卡片、備註與寫回公文系統；只有能修改時頁面才開這個對話框。 */
export const CALENDAR_DAY_TOUR: ProductTourDefinition = {
  steps: [
    {
      target: `${DAY_DIALOG} .day-kind`,
      title: '四選一的放假時段',
      description:
        '上班日、上午放假、下午放假或放假，圖形與月曆的半格相同。\n改過的話，「放假時段」旁會註明原本的設定。',
      side: 'bottom',
    },
    {
      target: `${DAY_DIALOG} .day-remark`,
      title: '備註',
      description:
        '選填，例如假日名稱或停班原因。\n公文系統以位元組計長度，快到上限時計數會變色。\n罕用字與表情符號存不下，儲存時會被拒絕。',
      side: 'top',
    },
    {
      target: `${DAY_DIALOG} nx-dialog-save-bar`,
      title: '儲存至公文系統',
      description: '直接寫回公文系統，出勤判定立即依新設定計算。\n「還原」回到開啟時的內容。',
      side: 'top',
    },
  ],
};

/** 出勤紀錄頁：查詢條件依授權出現（看得到別人才有人員，單位還要能讀組織），步驟文字跟著調整。 */
export function attendanceRecordsTour(options: {
  /** 資料範圍看得到本人以外的人，查詢列有人員選單。 */
  people: boolean;
  /** 另有組織讀取權，人員前面還有單位選單。 */
  units: boolean;
}): ProductTourDefinition {
  const fields = [
    '區間（最長一年）',
    ...(options.units ? ['單位（含下層）', '人員（選了單位只列該單位的人）'] : []),
    ...(options.people && !options.units ? ['人員'] : []),
  ];
  return {
    steps: [
      purposeStep(
        RECORDS,
        '每天的出勤判定',
        '依刷卡、假日行事曆與假單，逐日判定。\n只列出期間內持有卡的人；看得到誰，依你的資料範圍而定。',
      ),
      {
        target: `${RECORDS} .records-rules`,
        title: '判定規則',
        description: '表定、彈性上班與應下班怎麼算，以及遲到、早退與異常的條件。\n內容依目前設定。',
        side: 'bottom',
      },
      queryBarStep(RECORDS, {
        fields: `${fields.join('、')}與是否含放假日`,
      }),
      {
        target: `${RECORDS} .records-coverage`,
        title: '表單補足',
        description:
          '表單只比時數，不比時段。\n核准的假單、出差單時數會從當天工時扣掉；實際上班補得滿剩下的，就是「已補足」，應下班下方會列出扣表單後幾點可走。\n・未補足：核准的表單不夠。\n・流程中：要等流程中的單才夠。\n補足欄寫出還差多少與缺口時段，緊鄰表單欄，方便對照假單起訖。\n切到「未補足」或「流程中」，只列要追的日子。',
        side: 'bottom',
      },
      {
        target: `${RECORDS} .records-table .ui-table-toolbar`,
        title: '依人分組',
        description:
          '每人一段、每天一列。\n最左的人員欄捲動時固定，並列出遲到、早退與異常天數；遲到的上班、早退的下班，直接在時間上標色。\n順序依使用者頁設定的排序，人多時分頁。\n只有姓名的持卡人標「尚未綁帳號」；有刷卡卻沒有持有人的卡號不列，請到卡號對應頁補指派。\n沒刷卡的放假日（含只有出差單或假單的）預設不列，勾「含放假日」可看行事曆備註。\n公文系統連不上時，改為無法判定，不算異常。',
        side: 'bottom',
      },
      {
        target: `${RECORDS} .records-export`,
        title: '匯出 Excel',
        description:
          '依已套用的條件，產生整段期間的 .xlsx，格式與舊系統的「出缺勤計算」相同。\n不分頁、包含所有人；「本月」等預設區間，以按下時重算。\n沒有人可列時停用。',
        side: 'bottom',
      },
    ],
  };
}
