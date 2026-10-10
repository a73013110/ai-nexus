import { inject, Injectable, signal } from '@angular/core';
import { ApiError } from '../../core/api/nexus-api';
import type { RunDto } from '../../core/api/schema';
import { RunStream } from '../../core/stream/run-stream';

/** The one generation this browser follows: its status, streamed text and connection. */
@Injectable({ providedIn: 'root' })
export class ChatRun {
  private readonly stream = inject(RunStream);
  readonly live = signal<RunDto | null>(null);
  readonly text = signal('');
  readonly connection = signal<'connected' | 'reconnecting' | 'disconnected'>('connected');
  readonly stopping = signal(false);
  private subscription: AbortController | null = null;

  /**
   * Follows events until the run settles, then lets the caller reload what it produced. A newer
   * follow, a stop or a reset ends this one silently; other failures mark the connection lost.
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

  /** Stops following after a server-side cancel, keeping the partial answer on screen. */
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
