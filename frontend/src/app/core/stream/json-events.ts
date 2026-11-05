import { ApiError } from '../errors/safe-errors';
import { sseFrames } from './sse-parser';

/** 一個具名的 SSE 事件，`data` 已解析成 JSON。 */
export interface JsonEvent<T> {
  event: string;
  data: T;
}

/**
 * 透過既有的解析器解碼有界的 SSE 幀，逐一交出每個具名事件（`data` 解析成 JSON）。
 * 伺服器在串流開始後失敗時送 `error` 事件，這裡把它轉成安全的 `ApiError` 丟出；取消沿用 `signal`。
 */
export async function* sseEvents<T>(
  response: Response,
  signal: AbortSignal,
): AsyncGenerator<JsonEvent<T>> {
  if (!response.body) throw new Error('事件串流無法使用');
  for await (const frame of sseFrames(response.body, signal)) {
    if (frame.event === 'error') throw streamProblem(frame.data);
    yield { event: frame.event, data: JSON.parse(frame.data) as T };
  }
}

/** 只交出名為 `eventName` 的事件內容；其他事件略過。 */
export async function* jsonEvents<T>(
  response: Response,
  eventName: string,
  signal: AbortSignal,
): AsyncGenerator<T> {
  for await (const { event, data } of sseEvents<T>(response, signal))
    if (event === eventName) yield data;
}

function streamProblem(data: string) {
  const problem = JSON.parse(data) as {
    status?: number;
    code?: string;
    issueCode?: string;
  };
  return new ApiError(
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
