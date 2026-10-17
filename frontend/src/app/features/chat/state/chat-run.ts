import { inject, Injectable, signal } from '@angular/core';
import { ApiError } from '@core/errors/safe-errors';
import type { RunDto } from '@core/api/schema';
import { RunStream } from '@core/stream/run-stream';

/** 此瀏覽器追蹤的那一次生成：其狀態、串流文字與連線。 */
@Injectable({ providedIn: 'root' })
export class ChatRun {
  private readonly stream = inject(RunStream);
  readonly live = signal<RunDto | null>(null);
  readonly text = signal('');
  readonly connection = signal<'connected' | 'reconnecting' | 'disconnected'>('connected');
  readonly stopping = signal(false);
  private subscription: AbortController | null = null;

  /**
   * 追蹤事件直到執行結束，接著讓呼叫端重新讀取它產生的內容。較新的追蹤、停止或重設會靜
   * 默結束這次追蹤；
   * 其他失敗則把連線標為已中斷。
   */
  async follow(run: RunDto, settled: (terminal: RunDto) => Promise<void>) {
    this.subscription?.abort();
    const controller = (this.subscription = new AbortController());
    this.live.set(run);
    try {
      const terminal = await this.stream.follow(run, controller.signal, {
        content: (content) => {
          if (!controller.signal.aborted) this.text.set(content);
        },
        status: (current) => {
          if (!controller.signal.aborted) this.live.set(current);
        },
        connection: (state) => {
          if (!controller.signal.aborted) this.connection.set(state);
        },
      });
      if (!controller.signal.aborted) await settled(terminal);
    } catch (error) {
      if (controller.signal.aborted) return;
      this.connection.set('disconnected');
      if (error instanceof ApiError && [401, 403, 404].includes(error.status)) this.live.set(null);
      throw error;
    }
  }

  /** 伺服器端取消後停止追蹤，並保留畫面上的部分回答。 */
  stopped(final: RunDto) {
    this.subscription?.abort();
    this.text.set(final.content);
  }

  settle(run: RunDto) {
    if (this.live()?.id === run.id) this.live.set(null);
    this.connection.set('connected');
  }

  reset() {
    this.subscription?.abort();
    this.stopping.set(false);
    this.live.set(null);
    this.text.set('');
  }
}
