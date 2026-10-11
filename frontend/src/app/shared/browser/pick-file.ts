/**
 * Opens the system file chooser without a permanent hidden input, so any button or command can
 * ask for a file. Resolves null when the person cancels; call it from a user gesture.
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
