import { effect, inject, Injectable, signal, untracked } from '@angular/core';
import { AuthService } from '@core/auth/auth-service';

export type ToastTone = 'success' | 'info' | 'warning' | 'danger';

export const toastIcon = (tone: ToastTone) => (tone === 'success' ? 'check' : 'info');

export interface ToastOptions {
  /** Showing the same id again replaces that toast and restarts its timer (e.g. upload → done). */
  id?: string;
  tone?: ToastTone;
  title: string;
  description?: string;
  action?: { label: string; run: () => void };
  /** Milliseconds; 0 keeps it until dismissed. Defaults depend on tone and action. */
  duration?: number;
}

export interface Toast {
  id: string;
  tone: ToastTone;
  title: string;
  description?: string;
  action?: { label: string; run: () => void };
  duration: number;
  /** Bumped when the same id is shown again, so the toaster can replay its arrival. */
  version: number;
  /** Key of the session record this toast writes to; in-progress toasts have none yet. */
  record?: number;
}

/** A settled toast kept for the session, so one that timed out unread can still be found. */
export interface ToastRecord {
  key: number;
  tone: ToastTone;
  title: string;
  description?: string;
  /** Cleared once run, from the toast or from the history, so it never runs twice. */
  action?: { label: string; run: () => void };
  /** ISO time it was last shown. */
  at: string;
}

/** Most recent first; older toasts beyond this leave the stack. */
const LIMIT = 5;
/** Session history kept in memory only; never sent to the server. */
export const HISTORY_LIMIT = 20;
let sequence = 0;
let records = 0;

/**
 * Transient confirmations of work the person just did, shown by `Toaster` in the shell's
 * bottom-right corner. Failures, risks and anything that must stay readable remain in-page
 * `Notice`s; toasts never carry the only copy of information. Settled toasts (not the in-progress
 * ones with `duration: 0`) are also kept in `history` for this tab, newest first, for the
 * notification center's session list.
 */
@Injectable({ providedIn: 'root' })
export class ToastService {
  private readonly items = signal<readonly Toast[]>([]);
  readonly toasts = this.items.asReadonly();
  private readonly records = signal<readonly ToastRecord[]>([]);
  readonly history = this.records.asReadonly();
  private readonly timers = new Map<
    string,
    { handle?: ReturnType<typeof setTimeout>; left: number; started: number }
  >();
  private paused = false;

  constructor() {
    // A signed-out or switched account must not see the previous account's confirmations.
    const auth = inject(AuthService);
    let generation = auth.generation();
    effect(() => {
      if (auth.generation() === generation) return;
      generation = auth.generation();
      untracked(() => {
        this.clear();
        this.records.set([]);
      });
    });
  }

  show(options: ToastOptions) {
    const id = options.id ?? `toast-${++sequence}`;
    const tone = options.tone ?? 'success';
    const duration =
      options.duration ??
      (options.action ? 8000 : tone === 'success' || tone === 'info' ? 4000 : 6000);
    const previous = this.items().find((item) => item.id === id);
    // A toast updated in place keeps one record; a later toast reusing the id starts a new one.
    const record = duration > 0 ? (previous?.record ?? ++records) : undefined;
    const toast: Toast = {
      ...options,
      id,
      tone,
      duration,
      version: (previous?.version ?? 0) + 1,
      record,
    };
    if (record !== undefined) this.remember(record, toast);
    this.items.update((items) => [toast, ...items.filter((item) => item.id !== id)]);
    for (const old of this.items().slice(LIMIT)) this.dismiss(old.id);
    this.stop(id);
    if (duration > 0) {
      this.timers.set(id, { left: duration, started: 0 });
      if (!this.paused) this.start(id);
    }
    return id;
  }
  success(title: string, description?: string) {
    return this.show({ tone: 'success', title, description });
  }
  info(title: string, description?: string) {
    return this.show({ tone: 'info', title, description });
  }
  /** Runs the toast's action once, for this toast and its record, and closes it. */
  act(id: string) {
    const toast = this.items().find((item) => item.id === id);
    if (!toast) return;
    this.dismiss(id);
    if (toast.record !== undefined) this.spend(toast.record);
    toast.action?.run();
  }
  /** Runs a recorded action later, from the history; the toast still on screen leaves too. */
  replay(key: number) {
    const action = this.records().find((record) => record.key === key)?.action;
    if (!action) return;
    const live = this.items().find((item) => item.record === key);
    if (live) this.dismiss(live.id);
    this.spend(key);
    action.run();
  }
  dismiss(id: string) {
    this.stop(id);
    this.items.update((items) => items.filter((item) => item.id !== id));
  }
  clear() {
    for (const id of [...this.timers.keys()]) this.stop(id);
    this.items.set([]);
  }
  /** Hovering, focusing or hiding the page holds every timer, as the stack is read together. */
  pause() {
    if (this.paused) return;
    this.paused = true;
    const now = Date.now();
    for (const timer of this.timers.values()) {
      if (timer.handle === undefined) continue;
      clearTimeout(timer.handle);
      timer.handle = undefined;
      timer.left -= now - timer.started;
    }
  }
  resume() {
    if (!this.paused) return;
    this.paused = false;
    for (const id of this.timers.keys()) this.start(id);
  }

  private remember(key: number, toast: Toast) {
    const record: ToastRecord = {
      key,
      tone: toast.tone,
      title: toast.title,
      description: toast.description,
      action: toast.action,
      at: new Date().toISOString(),
    };
    this.records.update((items) =>
      [record, ...items.filter((item) => item.key !== key)].slice(0, HISTORY_LIMIT),
    );
  }
  private spend(key: number) {
    this.records.update((items) =>
      items.map((item) => (item.key === key ? { ...item, action: undefined } : item)),
    );
  }
  private start(id: string) {
    const timer = this.timers.get(id);
    if (!timer) return;
    timer.started = Date.now();
    timer.handle = setTimeout(() => this.dismiss(id), Math.max(timer.left, 1000));
  }
  private stop(id: string) {
    const timer = this.timers.get(id);
    if (timer?.handle !== undefined) clearTimeout(timer.handle);
    this.timers.delete(id);
  }
}
