/**
 * 以 Escape 的方式關閉對話框：它會先收到可取消的 `cancel` 事件，並關閉，
 * 除非擁有者阻止它（忙碌中、有未儲存的變更、會自行回答的確認）。
 */
export function requestDialogClose(dialog: HTMLDialogElement) {
  if (dialog.dispatchEvent(new Event('cancel', { cancelable: true }))) dialog.close();
}
