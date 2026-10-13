import {
  ErrorHandler,
  Injectable,
  Injector,
  effect,
  inject,
  isDevMode,
  signal,
} from '@angular/core';
import { AuthService } from '../auth/auth-service';
import { ApiClient } from '../api/api-client';
import {
  ApiError,
  ClientValidationError,
  isCancellation,
  newLocalIssue,
  pageProblem,
  safeMessage,
  validIssueCode,
} from './safe-errors';

/** A page exception leaves the app usable, so its notice clears itself unless something replaced it. */
const PAGE_NOTICE_MS = 12_000;

@Injectable({ providedIn: 'root' })
export class ClientIssues implements ErrorHandler {
  private readonly injector = inject(Injector);
  private get transport() {
    return this.injector.get(ApiClient);
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
  private show(message: string) {
    this.notice.set(message);
    setTimeout(() => {
      if (this.notice() === message) this.notice.set('');
    }, PAGE_NOTICE_MS);
  }
  handleError(error: unknown) {
    void this.report(error, 'exception');
  }
  async report(error: unknown, kind: 'exception' | 'rejection') {
    if (isCancellation(error) || error instanceof ClientValidationError) return;
    // The server only receives the error type; during development the stack is the only way to find the cause.
    if (isDevMode()) console.error(error);
    const current = this.auth.guard();
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
      this.show(previous.message);
      return;
    }
    const localMessage = pageProblem(newLocalIssue());
    this.show(localMessage);
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
      const result = await this.transport.post('/api/v1/client-issues', {
        body: { kind, fingerprint },
      });
      if (current() && result.accepted && validIssueCode(result.issueCode)) {
        const message = pageProblem(result.issueCode);
        this.reported.set(key, { at: now, message });
        if (this.notice() === localMessage) this.show(message);
      }
    } catch {
      /* Keep the explicitly local code. A failed report must never report itself. */
    }
  }
}
