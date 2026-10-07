import { ErrorHandler, Injectable, Injector, effect, inject, signal } from '@angular/core';
import { AuthService } from '../auth/auth-service';
import { ApiTransport } from './api-transport';
import {
  ApiError,
  ClientValidationError,
  isCancellation,
  safeMessage,
  systemProblem,
  validIssueCode,
} from './safe-errors';

@Injectable({ providedIn: 'root' })
export class ClientIssues implements ErrorHandler {
  private readonly injector = inject(Injector);
  private get transport() {
    return this.injector.get(ApiTransport);
  }
  private get auth() {
    return this.injector.get(AuthService);
  }
  readonly notice = signal('');
  private readonly reported = new Map<string, { at: number; message: string }>();
  private calls: number[] = [];
  private epoch: number | null = null;
  constructor() {
    effect(() => {
      const generation = this.auth.generation();
      if (this.epoch !== null && generation !== this.epoch) {
        this.reported.clear();
        this.calls = [];
        this.notice.set('');
      }
      this.epoch = generation;
    });
  }
  handleError(error: unknown) {
    void this.report(error, 'exception');
  }
  async report(error: unknown, kind: 'exception' | 'rejection') {
    if (isCancellation(error) || error instanceof ClientValidationError) return;
    const generation = this.auth.generation();
    if (this.epoch !== null && generation !== this.epoch) {
      this.reported.clear();
      this.calls = [];
    }
    this.epoch = generation;
    if (error instanceof ApiError) {
      this.notice.set(safeMessage(error));
      return;
    }
    // Never transmit message, stack, page URL, prompt, document or arbitrary rejection values.
    const type =
      error instanceof Error && /^[A-Za-z]{1,40}$/.test(error.name) ? error.name : 'Unknown';
    const now = Date.now();
    const key = kind + ':' + type;
    const previous = this.reported.get(key);
    if (previous && now - previous.at < 60_000) {
      this.notice.set(previous.message);
      return;
    }
    const localMessage = safeMessage(error);
    this.notice.set(localMessage);
    this.calls = this.calls.filter((time) => now - time < 60_000);
    if (this.calls.length >= 5) return;
    this.calls.push(now);
    this.reported.set(key, { at: now, message: localMessage });
    if (this.reported.size > 32) this.reported.delete(this.reported.keys().next().value!);
    try {
      const hash = new Uint8Array(
        await crypto.subtle.digest('SHA-256', new TextEncoder().encode(key)),
      );
      const fingerprint = Array.from(hash, (byte) => byte.toString(16).padStart(2, '0')).join('');
      const result = await this.transport.json<{ issueCode: string; accepted: boolean }>(
        '/client-issues',
        'POST',
        { kind, fingerprint },
      );
      if (
        generation === this.auth.generation() &&
        result.accepted &&
        validIssueCode(result.issueCode)
      ) {
        const message = systemProblem(result.issueCode);
        this.reported.set(key, { at: now, message });
        this.notice.set(message);
      }
    } catch {
      /* Keep the explicitly local code. A failed report must never report itself. */
    }
  }
}
