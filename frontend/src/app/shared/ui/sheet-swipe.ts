import { requestDialogClose } from '../browser/dialog-close';
import { bindSwipe, swipeSettleTime } from '../browser/swipe';

const SHEET = 'dialog[open].platform-dialog';
/** Where a sheet can be grabbed: its header, or its own padding around the content. */
const HANDLE = '.dialog-bar, header, [data-sheet-handle]';

/**
 * Drag a phone bottom sheet down by its header to dismiss it, like Escape: the dialog receives a
 * cancelable `cancel` event and closes unless its owner prevents it (busy, unsaved changes), in
 * which case it springs back. Bound once on the document so every `.platform-dialog` gets it; dialogs.scss marks the sheet layout with `--dialog-sheet: 1`.
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
          // Dropping the inline position lets the CSS transitions run from where the finger left it.
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
