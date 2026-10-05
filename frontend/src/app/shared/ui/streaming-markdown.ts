import { markdownBlockStarts } from './markdown';

/** Only the unfinished top-level block is reparsed; committed blocks retain their DOM. */
export class StreamingMarkdown {
  private previous = '';
  private committed = 0;
  private blocks: { id: number; content: string }[] = [];
  update(content: string) {
    if (!content.startsWith(this.previous)) {
      this.committed = 0;
      this.blocks = [];
    }
    this.previous = content;
    const pending = content.slice(this.committed);
    const starts = markdownBlockStarts(pending);
    const offsets = [0];
    for (let i = 0; i < pending.length; i++) if (pending[i] === '\n') offsets.push(i + 1);
    // A following top-level block establishes a stable boundary for lists, tables and fences.
    for (let i = 0; i < starts.length - 1; i++) {
      this.blocks.push({
        id: this.blocks.length,
        content: pending.slice(i === 0 ? 0 : offsets[starts[i]], offsets[starts[i + 1]]),
      });
    }
    if (starts.length > 1) this.committed += offsets[starts[starts.length - 1]];
    return { blocks: [...this.blocks], tail: content.slice(this.committed) };
  }
}

/** Presentation only: close partial inline syntax without ever changing the saved response. */
export function completeStreamingInline(content: string) {
  const tokens = content.match(/^ {0,3}(`{3,}|~{3,})/gm) ?? [];
  if (tokens.length % 2) return content;
  // A link cannot become active until its destination is complete and goes through sanitization.
  let display = content.replace(/(?<!!)\[([^\]\n]*)\]\([^\)\n]*$/, '$1');
  const ticks = display.match(/(?<!\\)(`+)/g) ?? [];
  if (ticks.length % 2) {
    const delimiter = ticks[ticks.length - 1];
    return display.endsWith(delimiter) ? display.slice(0, -delimiter.length) : display + delimiter;
  }
  for (const delimiter of ['**', '__']) {
    const count = display.split(delimiter).length - 1;
    if (count % 2)
      display = display.endsWith(delimiter)
        ? display.slice(0, -delimiter.length)
        : display + delimiter;
  }
  return display;
}
