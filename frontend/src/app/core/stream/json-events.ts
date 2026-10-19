import { ApiError } from '../errors/safe-errors';
import { sseFrames } from './sse-parser';

/** 透過既有的解析器解碼有界的 SSE 幀，並沿用其錯誤與取消的語意。 */
export async function* jsonEvents<T>(
  response: Response,
  eventName: string,
  signal: AbortSignal,
): AsyncGenerator<T> {
  if (!response.body) throw new Error('事件串流無法使用');
  for await (const frame of sseFrames(response.body, signal)) {
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
