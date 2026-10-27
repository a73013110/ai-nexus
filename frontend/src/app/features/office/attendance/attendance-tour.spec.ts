import { describe, expect, it } from 'vitest';
import { cursorPaginationStep } from '@shared/ui/data-workspace-tour';
import { tourSteps, type ProductTourDefinition } from '@shared/ui/product-tour';
import {
  attendanceCalendarTour,
  attendanceCardsTour,
  attendanceImportsTour,
  attendanceRecordsTour,
  CALENDAR_DAY_TOUR,
  cardDetailTour,
  reimportTour,
} from './attendance-tour';

const titles = (definition: ProductTourDefinition) =>
  tourSteps(definition).map((step) => step.title);
const copy = (definition: ProductTourDefinition) =>
  tourSteps(definition)
    .map((step) => step.description)
    .join('\n');
const scoped = (definition: ProductTourDefinition, host: string) => {
  for (const step of tourSteps(definition)) expect(step.target.startsWith(host + ' ')).toBe(true);
};

describe('attendanceCardsTour', () => {
  it('offers the write steps only to maintainers', () => {
    expect(titles(attendanceCardsTour({ maintain: true }))).toEqual([
      '刷卡依持有期間歸戶',
      '新增卡號',
      '持有中、無人持有與隱藏',
      '查詢條件',
      '卡片詳情',
    ]);
    expect(titles(attendanceCardsTour({ maintain: false }))).toEqual([
      '刷卡依持有期間歸戶',
      '持有中、無人持有與隱藏',
      '查詢條件',
      '卡片詳情',
    ]);
  });

  it('introduces assigning and hiding in the drawer', () => {
    expect(titles(cardDetailTour({ maintain: true }))).toEqual([
      '目前持有人',
      '指派或轉給其他人',
      '在出勤紀錄顯示',
      '持有歷史',
      '結束持有',
      '刷卡紀錄',
    ]);
    expect(copy(cardDetailTour({ maintain: true }))).toContain('本人與主管照常看得到');
    expect(titles(cardDetailTour({ maintain: false }))).toEqual([
      '目前持有人',
      '在出勤紀錄顯示',
      '持有歷史',
      '刷卡紀錄',
    ]);
  });

  it('scopes page steps to the page and drawer steps to the drawer', () => {
    scoped(attendanceCardsTour({ maintain: true }), 'nx-attendance-cards-page');
    scoped(cardDetailTour({ maintain: true }), 'nx-attendance-cards-page nx-detail-drawer');
  });
});

describe('attendanceImportsTour', () => {
  it('walks purpose, reimport, filters, the list and paging', () => {
    const tour = attendanceImportsTour({ maintain: true });
    expect(titles(tour)).toEqual([
      '每天一個打卡檔',
      '手動重匯',
      '先看失敗的檔',
      '查詢條件',
      '匯入紀錄',
      '翻頁',
    ]);
    expect(copy(tour)).toContain('最多 31 天');
    expect(tourSteps(tour).at(-1)).toEqual(
      cursorPaginationStep('nx-attendance-imports-page', { sortable: false }),
    );
    scoped(tour, 'nx-attendance-imports-page');
  });

  it('skips reimport without the action', () => {
    expect(titles(attendanceImportsTour({ maintain: false }))).not.toContain('手動重匯');
  });

  it('introduces the date fields of the chosen reimport mode', () => {
    const single = reimportTour({ mode: 'single' });
    const range = reimportTour({ mode: 'range' });
    expect(titles(single)).toEqual(['單一檔案或日期區間', '檔名日期', '檔名日期不是出勤日']);
    expect(titles(range)).toEqual(['單一檔案或日期區間', '開始與結束', '檔名日期不是出勤日']);
    expect(copy(range)).toContain('超過 31 天請分次');
    for (const definition of [single, range]) scoped(definition, 'nx-reimport-dialog');
  });
});

describe('attendanceCalendarTour', () => {
  const options = { maintain: true, gdReadOnly: false };

  it('explains half days and missing dates on the calendar, and the list as a second view', () => {
    const calendar = attendanceCalendarTour({ ...options, view: 'calendar' });
    expect(titles(calendar)).toEqual([
      '假日來自公文系統',
      '切換月份',
      '月曆或清單',
      '點日期修改',
      '未建立的日期',
    ]);
    expect(copy(calendar)).toContain('週日開始');
    expect(copy(calendar)).toContain('上半格');
    expect(titles(attendanceCalendarTour({ ...options, view: 'list' }))).toEqual([
      '假日來自公文系統',
      '切換月份',
      '月曆或清單',
      '逐日清單',
    ]);
    for (const view of ['calendar', 'list'] as const)
      scoped(attendanceCalendarTour({ ...options, view }), 'nx-attendance-calendar-page');
  });

  it('offers editing only when the day can actually be changed', () => {
    for (const blocked of [
      { maintain: false, gdReadOnly: false },
      { maintain: true, gdReadOnly: true },
    ]) {
      const tour = attendanceCalendarTour({ ...blocked, view: 'calendar' });
      expect(titles(tour)).toContain('讀月曆');
      expect(copy(tour)).not.toContain('點日期開啟編輯');
    }
  });

  it('walks the day dialog from the four options to saving into the document system', () => {
    expect(titles(CALENDAR_DAY_TOUR)).toEqual(['四選一的放假時段', '備註', '儲存至公文系統']);
    scoped(CALENDAR_DAY_TOUR, 'nx-calendar-day-dialog');
  });
});

describe('attendanceRecordsTour', () => {
  it('covers the rules, query, gap summary, grouped list and Excel export', () => {
    const tour = attendanceRecordsTour({ people: true, units: true });
    expect(titles(tour)).toEqual([
      '每天的出勤判定',
      '判定規則',
      '查詢條件',
      '表單補足',
      '依人分組',
      '匯出 Excel',
    ]);
    expect(copy(tour)).toContain('最長一年');
    expect(copy(tour)).toContain('單位（含下層）');
    expect(copy(tour)).toContain('含放假日');
    expect(copy(tour)).toContain('已補足');
    const people = copy(attendanceRecordsTour({ people: true, units: false }));
    expect(people).not.toContain('單位');
    expect(people).toContain('區間（最長一年）、人員與是否含放假日');
    // 只看得到自己時查詢列沒有人員。
    expect(copy(attendanceRecordsTour({ people: false, units: false }))).toContain(
      '區間（最長一年）與是否含放假日',
    );
    scoped(tour, 'nx-attendance-records-page');
  });
});
