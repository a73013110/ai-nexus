/**
 * Close a dialog the way Escape does: it receives a cancelable `cancel` event first and closes
 * unless its owner prevents it (busy, unsaved changes, a confirm that answers itself).
 */
export function requestDialogClose(dialog: HTMLDialogElement) {
  if (dialog.dispatchEvent(new Event('cancel', { cancelable: true }))) dialog.close();
}
