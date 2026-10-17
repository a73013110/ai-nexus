/**
 * 開啟系統的檔案選擇器，且不保留隱藏的 input，因此任何按鈕或指令都能要求檔案。
 * 使用者取消時回傳 null；需在使用者手勢中呼叫。
 */
export function pickFile(accept: string): Promise<File | null> {
  return new Promise((resolve) => {
    const input = document.createElement('input');
    input.type = 'file';
    input.accept = accept;
    input.hidden = true;
    const done = () => {
      input.remove();
      resolve(input.files?.[0] ?? null);
    };
    input.addEventListener('change', done, { once: true });
    input.addEventListener('cancel', done, { once: true });
    document.body.append(input);
    input.click();
  });
}
