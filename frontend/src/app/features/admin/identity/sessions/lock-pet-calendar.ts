/**
 * 小鎖今天的打扮與作息：依這台電腦的本地時間決定，純前端，不需要伺服器。
 * 節日的道具優先於時段的道具（春節拿紅包就不拿便當）。
 */

/** 早上伸懶腰、中午吃便當、下班後睡覺蓋被子、被吵醒會打哈欠。 */
export type PetMoment = 'morning' | 'lunch' | 'evening';
export type PetHat = 'santa' | 'party';
export type PetHeld = 'bento' | 'red-envelope' | 'mooncake';

export interface PetLook {
  moment: PetMoment | null;
  hat: PetHat | null;
  held: PetHeld | null;
}

/** 時段的分界（當天的分鐘數）；跨過任何一個都可能換打扮。 */
const MORNING = 6 * 60;
const LATE_MORNING = 10 * 60;
const LUNCH = 11 * 60 + 30;
const AFTER_LUNCH = 13 * 60 + 30;
const EVENING = 18 * 60;
const BOUNDARIES = [MORNING, LATE_MORNING, LUNCH, AFTER_LUNCH, EVENING];

// 農曆用瀏覽器內建的曆法換算；閏月的月份是「6bis」這類字串，轉成數字是 NaN，不會被當成節日。
const lunarFormat = new Intl.DateTimeFormat('en-u-ca-chinese', {
  month: 'numeric',
  day: 'numeric',
});

function lunar(date: Date) {
  const parts = lunarFormat.formatToParts(date);
  const value = (type: Intl.DateTimeFormatPartTypes) =>
    Number(parts.find((part) => part.type === type)?.value);
  return { month: value('month'), day: value('day') };
}

function addDays(date: Date, days: number) {
  const next = new Date(date);
  next.setDate(next.getDate() + days);
  return next;
}

function momentOf(date: Date): PetMoment | null {
  const minutes = date.getHours() * 60 + date.getMinutes();
  if (minutes >= MORNING && minutes < LATE_MORNING) return 'morning';
  if (minutes >= LUNCH && minutes < AFTER_LUNCH) return 'lunch';
  if (minutes >= EVENING || minutes < MORNING) return 'evening';
  return null;
}

function hatOf(date: Date): PetHat | null {
  const month = date.getMonth() + 1;
  const day = date.getDate();
  if (month === 12 && (day === 24 || day === 25)) return 'santa';
  if (month === 1 && day === 1) return 'party';
  return null;
}

function holidayHeld(date: Date): PetHeld | null {
  const today = lunar(date);
  // 除夕（明天是正月初一）到初五發紅包。
  const tomorrow = lunar(addDays(date, 1));
  if ((today.month === 1 && today.day <= 5) || (tomorrow.month === 1 && tomorrow.day === 1))
    return 'red-envelope';
  if (today.month === 8 && today.day === 15) return 'mooncake';
  return null;
}

export function petLook(date: Date): PetLook {
  const moment = momentOf(date);
  return {
    moment,
    hat: hatOf(date),
    held: holidayHeld(date) ?? (moment === 'lunch' ? 'bento' : null),
  };
}

/** 下一次可能換打扮的時間：今天下一個時段分界，或明天零時（換日可能換節日）。 */
export function nextLookChange(date: Date): Date {
  const minutes = date.getHours() * 60 + date.getMinutes();
  const next = BOUNDARIES.find((boundary) => boundary > minutes);
  const at = new Date(date);
  if (next === undefined) {
    at.setDate(at.getDate() + 1);
    at.setHours(0, 0, 0, 0);
  } else at.setHours(Math.floor(next / 60), next % 60, 0, 0);
  return at;
}
