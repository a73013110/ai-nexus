import { describe, expect, it } from 'vitest';
import { SseParser } from './sse-parser';
describe('SSE framing', () => {
  it('preserves a frame split inside the data field and CRLF boundary', () => {
    const parser = new SseParser();
    expect(parser.feed('id: 8\r\nevent: run\r\ndata: {"del')).toEqual([]);
    expect(parser.feed('ta":"中文"}\r\n\r')).toEqual([]);
    expect(parser.feed('\n')).toEqual([{ id: '8', event: 'run', data: '{"delta":"中文"}' }]);
  });
  it('ignores heartbeats and handles multiple frames and data lines', () => {
    const frames = new SseParser().feed(
      ': heartbeat\n\nid: 1\ndata: a\ndata: b\n\nid: 2\ndata: c\n\n',
    );
    expect(frames.map((x) => x.data)).toEqual(['a\nb', 'c']);
  });
  it('bounds memory when the server never terminates a frame', () => {
    expect(() => new SseParser().feed('x'.repeat(1048577))).toThrow();
  });
});
