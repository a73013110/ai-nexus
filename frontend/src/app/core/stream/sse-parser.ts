export interface SseFrame {
  id: string;
  event: string;
  data: string;
}

// 幀可能跨越任意的網路區塊；註解與 CRLF 都是合法的 SSE。
export class SseParser {
  private buffer = '';
  feed(chunk: string): SseFrame[] {
    this.buffer += chunk;
    if (this.buffer.length > 1048576) throw new Error('事件框架過大');
    const frames: SseFrame[] = [];
    let match: RegExpExecArray | null;
    while ((match = /\r?\n\r?\n/.exec(this.buffer))) {
      const block = this.buffer.slice(0, match.index);
      this.buffer = this.buffer.slice(match.index + match[0].length);
      let id = '',
        event = 'message';
      const data: string[] = [];
      for (const line of block.split(/\r?\n/)) {
        if (line.startsWith(':')) continue;
        const colon = line.indexOf(':');
        const field = colon < 0 ? line : line.slice(0, colon);
        const value = colon < 0 ? '' : line.slice(colon + 1).replace(/^ /, '');
        if (field === 'id' && !value.includes('\0')) id = value;
        else if (field === 'event') event = value;
        else if (field === 'data') data.push(value);
      }
      if (data.length > 0) frames.push({ id, event, data: data.join('\n') });
    }
    return frames;
  }
}

/**
 * 逐幀讀取 SSE 回應主體，每次讀取前檢查 `signal`。消費端提前離開（`break`、`throw`）或讀完時，
 * 都會先取消並釋放讀取器，連線才不會在背景繼續佔用。
 */
export async function* sseFrames(
  body: ReadableStream<Uint8Array>,
  signal: AbortSignal,
): AsyncGenerator<SseFrame> {
  const reader = body.getReader();
  const parser = new SseParser();
  const decoder = new TextDecoder();
  try {
    while (!signal.aborted) {
      const chunk = await reader.read();
      if (chunk.done) return;
      yield* parser.feed(decoder.decode(chunk.value, { stream: true }));
    }
  } finally {
    await reader.cancel().catch(() => undefined);
    reader.releaseLock();
  }
}
