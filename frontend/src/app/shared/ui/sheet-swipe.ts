import { requestDialogClose } from '../browser/dialog-close';
import { bindSwipe, swipeSettleTime } from '../browser/swipe';

const SHEET = 'dialog[open].platform-dialog';
/** 面板可以抓取的位置：標題，或內容周圍自己的內距。 */
const HANDLE = '.dialog-bar, header, [data-sheet-handle]';

/**
 * 在手機底部面板上從標題往下拖曳即可關閉，與 Escape 相同：對話框會收到可取消的 `cancel` 事件，
 * 並關閉，除非擁有者阻止它（忙碌中、有未儲存的變更）；那種情況下面板會彈回。綁定一次於 document，
 * 讓每個 `.platform-dialog` 都能使用；
 * dialogs.scss 會以 `--dialog-sheet: 1` 標記面板版面。
 */
export function bindSheetSwipe() {
  return bindSwipe(document, {
    axis: 'y',
    direction: 1,
    begin(event) {
      const target = event.target as Element;
      const sheet = target.closest?.<HTMLDialogElement>(SHEET);
      if (!sheet || getComputedStyle(sheet).getPropertyValue('--dialog-sheet').trim() !== '1')
        return null;
      const handle = target === sheet || target.closest(HANDLE)?.closest(SHEET) === sheet;
      if (!handle) return null;
      const height = sheet.offsetHeight;
      return {
        distance: Math.min(120, height / 3),
        move(offset) {
          sheet.dataset['swipe'] = 'drag';
          sheet.style.translate = `0 ${offset}px`;
          sheet.style.setProperty('--swipe-progress', String(Math.max(0, offset) / height));
        },
        end(dismiss) {
          // 拿掉行內位置，讓 CSS 過場從手指離開的位置開始。
          sheet.style.removeProperty('translate');
          sheet.style.removeProperty('--swipe-progress');
          delete sheet.dataset['swipe'];
          if (dismiss) requestDialogClose(sheet);
          if (!sheet.open) return;
          sheet.dataset['swipe'] = 'return';
          window.setTimeout(() => {
            if (sheet.dataset['swipe'] === 'return') delete sheet.dataset['swipe'];
          }, swipeSettleTime(sheet));
        },
      };
    },
  });
}
