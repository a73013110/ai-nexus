import type { ParamMap } from '@angular/router';
import type { QueryFieldSpec } from './query-state';
import {
  CUSTOM_RANGE,
  timeRangeLabel,
  type TimeRange,
  type TimeRangePreset,
} from './time-range-picker';

/** 選項；`param` 是網址上的寫法，沒寫時與 `value` 相同（例：值 `Unexplained` 在網址寫成 `unexplained`）。 */
export interface QueryFieldOption<V extends string> {
  readonly value: V;
  readonly label: string;
  readonly param?: string;
}

const DAY = /^\d{4}-\d{2}-\d{2}$/;
const trimmed = (params: ParamMap, name: string) => (params.get(name) ?? '').trim();

/**
 * 常見查詢條件的宣告；每個欄位只寫一次參數名稱、格式與 chip 名稱，預設值的省略由 `QueryState` 統一處理。
 * 寫不進這幾種的條件（同一欄寫兩個參數、相容舊連結）直接寫一個 `QueryFieldSpec` 物件。
 */
export const queryField = {
  /**
   * 文字：網址與 chip 都用去空白後的值；`normalize` 把常見寫法補成正式格式（例：`20261001` → `20261001.txt`），
   * 連結的值不符合 `pattern` 時忽略，超過 `max` 時截斷。有 `chip` 才顯示成 chip（`chip：值`）。
   */
  text(
    param: string,
    options: {
      max?: number;
      pattern?: RegExp;
      normalize?: (value: string) => string;
      chip?: string;
    } = {},
  ): QueryFieldSpec<string> {
    const clean = (value: string) => {
      const text = value.trim();
      return text && options.normalize ? options.normalize(text) : text;
    };
    return {
      initial: () => '',
      read: (params) => {
        const value = clean(trimmed(params, param)).slice(0, options.max);
        return value && (!options.pattern || options.pattern.test(value)) ? value : undefined;
      },
      write: (value) => ({ [param]: clean(value) }),
      chip: (value) => (options.chip && clean(value) ? `${options.chip}：${clean(value)}` : ''),
    };
  },

  /** 從固定選項選一個，第一個選項是預設值（通常是「全部」）；連結的值不在選項裡時沿用預設值。 */
  choice<V extends string>(
    param: string,
    choices: readonly QueryFieldOption<V>[],
    options: { chip?: string } = {},
  ): QueryFieldSpec<V> {
    return {
      initial: () => choices[0].value,
      read: (params) =>
        choices.find((item) => (item.param ?? item.value) === params.get(param))?.value,
      write: (value) => {
        const item = choices.find((choice) => choice.value === value);
        return { [param]: item ? (item.param ?? item.value) : '' };
      },
      chip: (value) =>
        options.chip
          ? `${options.chip}：${choices.find((item) => item.value === value)?.label ?? value}`
          : '',
    };
  },

  /** 從固定選項選多個（`?result=a&result=b`），順序依選項。 */
  choices<V extends string>(
    param: string,
    choices: readonly QueryFieldOption<V>[],
    options: { chip?: string } = {},
  ): QueryFieldSpec<V[]> {
    return {
      initial: () => [],
      read: (params) => {
        const values = params.getAll(param);
        const picked = choices.filter((item) => values.includes(item.param ?? item.value));
        return picked.length ? picked.map((item) => item.value) : undefined;
      },
      write: (value) => ({
        [param]: choices
          .filter((item) => value.includes(item.value))
          .map((item) => item.param ?? item.value),
      }),
      chip: (value) =>
        options.chip
          ? `${options.chip}：${choices
              .filter((item) => value.includes(item.value))
              .map((item) => item.label)
              .join('、')}`
          : '',
    };
  },

  /** 勾選：勾了才寫 `param=true`；`chip` 是勾選時的 chip 文字。 */
  flag(param: string, chip = ''): QueryFieldSpec<boolean> {
    return {
      initial: () => false,
      read: (params) => (params.get(param) === 'true' ? true : undefined),
      write: () => ({ [param]: 'true' }),
      chip: () => chip,
    };
  },

  /**
   * `TimeRangePicker` 的區間：預設區間寫 `range=<值>`，自訂起訖寫 `from`＋`to`（結束端參數名由 `to` 指定），日期只收 `YYYY-MM-DD`。
   * chip 寫 `chip：區間摘要`；摘要預設是 `timeRangeLabel`，要附上實際起訖時給 `label`。
   */
  range(
    presets: readonly TimeRangePreset[],
    options: { initial: string; chip: string; to?: string; label?: (range: TimeRange) => string },
  ): QueryFieldSpec<TimeRange> {
    const to = options.to ?? 'to';
    return {
      initial: () => ({ preset: options.initial, from: '', to: '' }),
      read: (params) => {
        const day = (name: string) => (DAY.test(params.get(name) ?? '') ? params.get(name)! : '');
        const range = { from: day('from'), to: day(to) };
        if (range.from || range.to) return { preset: CUSTOM_RANGE, ...range };
        const preset = params.get('range');
        return presets.some((item) => item.value === preset)
          ? { preset: preset!, from: '', to: '' }
          : undefined;
      },
      write: (range) =>
        range.preset === CUSTOM_RANGE
          ? { from: range.from, [to]: range.to }
          : { range: range.preset },
      chip: (range) =>
        `${options.chip}：${options.label ? options.label(range) : timeRangeLabel(range, presets)}`,
    };
  },
};
