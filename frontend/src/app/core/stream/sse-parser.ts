export interface SseFrame {
  id: string;
  event: string;
  data: string;
}

// Frames may span arbitrary network chunks; comments and CRLF are valid SSE.
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
