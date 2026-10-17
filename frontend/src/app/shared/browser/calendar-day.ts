import { DestroyRef, inject, signal } from '@angular/core';

const DAY = 86_400_000;

function startOfDay(time: number) {
  const date = new Date(time);
  date.setHours(0, 0, 0, 0);
  return date.getTime();
}

/** 從 `from` 到 `to` 的完整本地日曆日數；夏令時間的小時差由四捨五入吸收。 */
export const calendarDaysBetween = (from: number, to: number) =>
  Math.round((startOfDay(to) - startOfDay(from)) / DAY);

/**
 * 檢視者本地日期的起點，作為呼叫元件生命週期內的 signal。它會在午夜與頁面重新可見時更新，
 * 因為計時器會隨裝置休眠。
 */
export function currentDay() {
  const day = signal(startOfDay(Date.now()));
  let timer: ReturnType<typeof setTimeout> | undefined;
  const refresh = () => {
    clearTimeout(timer);
    const now = Date.now();
    day.set(startOfDay(now));
    const tomorrow = new Date(day());
    tomorrow.setDate(tomorrow.getDate() + 1);
    timer = setTimeout(refresh, tomorrow.getTime() - now + 1000);
  };
  const visible = () => {
    if (document.visibilityState === 'visible') refresh();
  };
  refresh();
  document.addEventListener('visibilitychange', visible);
  inject(DestroyRef).onDestroy(() => {
    clearTimeout(timer);
    document.removeEventListener('visibilitychange', visible);
  });
  return day.asReadonly();
}
