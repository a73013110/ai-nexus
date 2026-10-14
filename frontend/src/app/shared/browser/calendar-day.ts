import { DestroyRef, inject, signal } from '@angular/core';

const DAY = 86_400_000;

function startOfDay(time: number) {
  const date = new Date(time);
  date.setHours(0, 0, 0, 0);
  return date.getTime();
}

/** Whole local calendar days from `from` to `to`; rounding absorbs daylight-saving hours. */
export const calendarDaysBetween = (from: number, to: number) =>
  Math.round((startOfDay(to) - startOfDay(from)) / DAY);

/**
 * The start of the viewer's local day as a signal for the calling component's lifetime. It moves
 * at midnight and whenever the page becomes visible again, because timers sleep with the device.
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
