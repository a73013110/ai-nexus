import { fenceLanguage, markdownBlockStructure } from './markdown';

export interface StreamingBlock {
  id: number;
  content: string;
  /** 長列表在項目邊界切開；下一個區塊會接續同一個列表。 */
  continues?: boolean;
}

/** 尚未關閉的最外層程式碼圍欄，在關閉前以純文字逐行顯示。 */
export interface StreamingFence {
  language: string;
  chunks: readonly { id: number; text: string }[];
  tail: string;
}

/** 超過此大小時，未完成的列表會先提交已完成的項目，不再重新解析。 */
const LONG_BLOCK = 1200;
const FENCE_CHUNK_LINES = 24;
const FENCE_OPEN = /^(?:[ \t]*\n)*(`{3,}|~{3,})([^\n]*)\n/;

/**
 * 只重新解析未完成的最外層區塊；已提交的區塊保留其 DOM。長列表會提交已完成的項目，
 * 開啟中的圍欄只掃描新增的行，因此不論回答多長，每幀成本都維持不變。
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
    for (let index = 0; index < pending.length; index++)
      if (pending[index] === '\n') offsets.push(index + 1);
    // 後面接著的最外層區塊，會替列表、表格與圍欄建立穩定的邊界。
    for (let index2 = 0; index2 < starts.length - 1; index2++) {
      this.blocks.push({
        id: this.blocks.length,
        content: pending.slice(
          index2 === 0 ? 0 : offsets[starts[index2]],
          offsets[starts[index2 + 1]],
        ),
      });
    }
    let last = starts.length > 1 ? offsets[starts[starts.length - 1]] : 0;
    // 尾端的長列表中，只要後面還有同層項目，前面的項目就已完成。
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
    const marker = opener[1];
    this.fence = {
      close: new RegExp(`^ {0,3}${marker[0] === '`' ? '`' : '~'}{${marker.length},}[ \\t]*$`),
      language: fenceLanguage(opener[2]),
      body,
      scanned: body,
      chunked: body,
      chunks: [],
    };
    if (!this.fenceStillOpen(content)) this.fence = null;
  }
  /** 只掃描上一幀之後新增的行，尋找關閉的圍欄。 */
  private fenceStillOpen(content: string) {
    const fence = this.fence!;
    let start = fence.scanned;
    for (let end = content.indexOf('\n', start); ; end = content.indexOf('\n', start)) {
      // 未完成的最後一行也要計入：單獨的標記可能已經關閉了圍欄。
      if (fence.close.test(content.slice(start, end < 0 ? content.length : end))) return false;
      if (end < 0) break;
      start = end + 1;
      fence.scanned = start;
    }
    let lines = 0;
    for (let index3 = fence.chunked; index3 < fence.scanned; index3++)
      if (content[index3] === '\n' && ++lines === FENCE_CHUNK_LINES) {
        fence.chunks = [
          ...fence.chunks,
          { id: fence.chunks.length, text: content.slice(fence.chunked, index3 + 1) },
        ];
        fence.chunked = index3 + 1;
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

/** 僅供呈現：補上未完成的行內語法，但絕不改變已儲存的回應。 */
export function completeStreamingInline(content: string) {
  const tokens = content.match(/^ {0,3}(`{3,}|~{3,})/gm) ?? [];
  if (tokens.length % 2) return content;
  // 連結要等目的地完整並通過消毒後才會生效。
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
