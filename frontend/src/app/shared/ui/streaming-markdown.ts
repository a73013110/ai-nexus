import { markdownBlockStructure } from './markdown';

export interface StreamingBlock {
  id: number;
  content: string;
  /** A long list split at item boundaries; the next block carries on the same list. */
  continues?: boolean;
}

/** An unclosed top-level code fence, shown as plain text in line chunks until it closes. */
export interface StreamingFence {
  language: string;
  chunks: readonly { id: number; text: string }[];
  tail: string;
}

/** Above this size an unfinished list commits its finished items instead of reparsing them. */
const LONG_BLOCK = 1200;
const FENCE_CHUNK_LINES = 24;
const FENCE_OPEN = /^(?:[ \t]*\n)*(`{3,}|~{3,})([^\n]*)\n/;

/**
 * Only the unfinished top-level block is reparsed; committed blocks retain their DOM. Long
 * lists commit finished items, and an open fence is followed by scanning only its new lines,
 * so the per-frame cost stays flat however long the answer grows.
 */
export class StreamingMarkdown {
  private previous = '';
  private committed = 0;
  private blocks: StreamingBlock[] = [];
  private fence: {
    close: RegExp;
    language: string;
    body: number;
    scanned: number;
    chunked: number;
    chunks: { id: number; text: string }[];
  } | null = null;
  update(content: string): {
    blocks: StreamingBlock[];
    tail: string;
    fence: StreamingFence | null;
  } {
    if (!content.startsWith(this.previous)) {
      this.committed = 0;
      this.blocks = [];
      this.fence = null;
    }
    this.previous = content;
    if (this.fence && this.fenceStillOpen(content)) return this.result(content);
    this.fence = null;
    const pending = content.slice(this.committed);
    const { starts, items } = markdownBlockStructure(pending);
    const offsets = [0];
    for (let i = 0; i < pending.length; i++) if (pending[i] === '\n') offsets.push(i + 1);
    // A following top-level block establishes a stable boundary for lists, tables and fences.
    for (let i = 0; i < starts.length - 1; i++) {
      this.blocks.push({
        id: this.blocks.length,
        content: pending.slice(i === 0 ? 0 : offsets[starts[i]], offsets[starts[i + 1]]),
      });
    }
    let last = starts.length > 1 ? offsets[starts[starts.length - 1]] : 0;
    // Within a long trailing list, every item followed by a sibling is finished.
    if (items.length > 1 && pending.length - last > LONG_BLOCK) {
      const end = offsets[items[items.length - 1]];
      this.blocks.push({
        id: this.blocks.length,
        content: pending.slice(last, end),
        continues: true,
      });
      last = end;
    }
    this.committed += last;
    this.openFence(content);
    return this.result(content);
  }
  private openFence(content: string) {
    const opener = FENCE_OPEN.exec(content.slice(this.committed));
    if (!opener || (opener[1][0] === '`' && opener[2].includes('`'))) return;
    const body = this.committed + opener[0].length;
    const language = opener[2].trim().split(/\s+/)[0].toLowerCase();
    const marker = opener[1];
    this.fence = {
      close: new RegExp(`^ {0,3}${marker[0] === '`' ? '`' : '~'}{${marker.length},}[ \\t]*$`),
      language: /^[\w#+-]{1,24}$/.test(language) ? language : 'text',
      body,
      scanned: body,
      chunked: body,
      chunks: [],
    };
    if (!this.fenceStillOpen(content)) this.fence = null;
  }
  /** Scans only lines added since the last frame for a closing fence. */
  private fenceStillOpen(content: string) {
    const fence = this.fence!;
    let start = fence.scanned;
    for (let end = content.indexOf('\n', start); ; end = content.indexOf('\n', start)) {
      // The unfinished last line counts too: a bare marker may already close the fence.
      if (fence.close.test(content.slice(start, end < 0 ? content.length : end))) return false;
      if (end < 0) break;
      start = end + 1;
      fence.scanned = start;
    }
    let lines = 0;
    for (let i = fence.chunked; i < fence.scanned; i++)
      if (content[i] === '\n' && ++lines === FENCE_CHUNK_LINES) {
        fence.chunks = [
          ...fence.chunks,
          { id: fence.chunks.length, text: content.slice(fence.chunked, i + 1) },
        ];
        fence.chunked = i + 1;
        lines = 0;
      }
    return true;
  }
  private result(content: string) {
    const fence = this.fence;
    return {
      blocks: [...this.blocks],
      tail: fence ? '' : content.slice(this.committed),
      fence: fence && {
        language: fence.language,
        chunks: fence.chunks,
        tail: content.slice(fence.chunked),
      },
    };
  }
}

/** Presentation only: close partial inline syntax without ever changing the saved response. */
export function completeStreamingInline(content: string) {
  const tokens = content.match(/^ {0,3}(`{3,}|~{3,})/gm) ?? [];
  if (tokens.length % 2) return content;
  // A link cannot become active until its destination is complete and goes through sanitization.
  let display = content.replace(/(?<!!)\[([^\]\n]*)\]\([^)\n]*$/, '$1');
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
