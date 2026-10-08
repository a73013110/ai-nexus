import { ApiError } from '../api/api-transport';
import { SseParser } from './sse-parser';

/** Decode bounded SSE frames through the existing parser, sharing error and cancellation semantics. */
export async function* jsonEvents<T>(
  response: Response,
  eventName: string,
  signal: AbortSignal,
): AsyncGenerator<T> {
  if (!response.body) throw new Error('事件串流無法使用');
  const reader = response.body.getReader();
  const parser = new SseParser();
  const decoder = new TextDecoder();
  try {
    while (!signal.aborted) {
      const chunk = await reader.read();
      if (chunk.done) break;
      for (const frame of parser.feed(decoder.decode(chunk.value, { stream: true }))) {
        if (frame.event === 'error') {
          const problem = JSON.parse(frame.data) as {
            status?: number;
            code?: string;
            issueCode?: string;
          };
          throw new ApiError(
            Number.isInteger(problem.status) && problem.status! >= 400 && problem.status! <= 599
              ? problem.status!
              : problem.code === 'feature_forbidden'
                ? 403
                : problem.code === 'session_revoked'
                  ? 401
                  : 503,
            problem.code ?? 'stream_failed',
            undefined,
            problem.issueCode,
          );
        }
        if (frame.event === eventName) yield JSON.parse(frame.data) as T;
      }
    }
  } finally {
    await reader.cancel().catch(() => undefined);
    reader.releaseLock();
  }
}
