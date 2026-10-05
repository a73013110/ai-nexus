import { inject, Injectable } from '@angular/core';
import { ApiError, NexusApi } from '../api/nexus-api';
import { isActive, Run, RunEvent } from '../api/types';
import { SseParser } from './sse-parser';
import { abortableDelay } from '../../shared/browser/abortable-delay';
import { FramePublisher } from './frame-publisher';

export interface StreamObserver {
  content(value: string): void;
  status(value: Run): void;
  connection(value: 'connected' | 'reconnecting'): void;
}

@Injectable({ providedIn: 'root' })
export class RunStream {
  private readonly api = inject(NexusApi);

  async follow(initial: Run, signal: AbortSignal, observer: StreamObserver): Promise<Run> {
    const content = new FramePublisher((value) => {
      if (!signal.aborted) observer.content(value);
    });
    try {
      return await this.followEvents(initial, signal, {
        ...observer,
        content: (value) => content.set(value),
        status: (value) => {
          content.flush();
          observer.status(value);
        },
      });
    } finally {
      if (!signal.aborted) content.flush();
      content.dispose();
    }
  }
  private async followEvents(
    initial: Run,
    signal: AbortSignal,
    observer: StreamObserver,
  ): Promise<Run> {
    let run = initial;
    let cursor = run.lastSequence;
    let content = run.content;
    observer.content(content);
    observer.status(run);
    for (let attempt = 0; attempt < 8 && !signal.aborted; attempt++) {
      try {
        if (!isActive(run.status)) return run;
        const response = await this.api.events(run.id, cursor, signal);
        if (!response.body) throw new Error('事件連線失敗');
        observer.connection('connected');
        const reader = response.body.getReader();
        const decoder = new TextDecoder();
        const parser = new SseParser();
        try {
          while (!signal.aborted) {
            const chunk = await reader.read();
            if (chunk.done) break;
            for (const frame of parser.feed(decoder.decode(chunk.value, { stream: true }))) {
              if (frame.event !== 'run') continue;
              const event = JSON.parse(frame.data) as RunEvent;
              if (
                event.version !== 1 ||
                event.runId !== run.id ||
                !Number.isSafeInteger(event.sequence) ||
                event.sequence <= cursor
              )
                continue;
              if (event.type === 'snapshot') content = event.delta ?? '';
              else if (event.type === 'delta') content += event.delta ?? '';
              else if (event.type !== 'status') continue;
              cursor = event.sequence;
              if (content.length > 65536) throw new Error('回答超過長度上限');
              observer.content(content);
              if (run.status !== event.status || run.errorCode !== event.errorCode) {
                run = {
                  ...run,
                  status: event.status,
                  errorCode: event.errorCode,
                  content,
                  lastSequence: cursor,
                };
                observer.status(run);
              }
            }
          }
        } finally {
          await reader.cancel().catch(() => undefined);
          reader.releaseLock();
        }
        if (!isActive(run.status)) return this.api.run(run.id, signal);
        throw new Error('串流提前結束');
      } catch (error) {
        if (signal.aborted || this.permanent(error)) throw error;
        observer.connection('reconnecting');
        await abortableDelay(Math.min(500 * 2 ** attempt, 6000), signal);
        try {
          run = await this.api.run(run.id, signal);
          cursor = run.lastSequence;
          content = run.content;
          observer.content(content);
          observer.status(run);
          if (!isActive(run.status)) return run;
        } catch (stateError) {
          if (signal.aborted || this.permanent(stateError)) throw stateError;
          /* Next attempt fetches state again; no POST is ever repeated here. */
        }
      }
    }
    throw new Error('暫時無法恢復事件連線，請按「恢復連線」。生成仍由伺服器管理。');
  }
  private permanent(error: unknown) {
    return (
      error instanceof ApiError && error.status >= 400 && error.status < 500 && error.status !== 429
    );
  }
}
