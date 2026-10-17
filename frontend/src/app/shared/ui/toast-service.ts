import { effect, inject, Injectable, signal, untracked } from '@angular/core';
import { AuthService } from '@core/auth/auth-service';

export type ToastTone = 'success' | 'info' | 'warning' | 'danger';

export const toastIcon = (tone: ToastTone) => (tone === 'success' ? 'check' : 'info');

export interface ToastOptions {
  /** 再次以同一 id 顯示時會取代該提示並重新計時（例如上傳 → 完成）。 */
  id?: string;
  tone?: ToastTone;
  title: string;
  description?: string;
  action?: { label: string; run: () => void };
  /** 毫秒；`0` 代表直到關閉才消失。預設值依語氣與動作而定。 */
  duration?: number;
}

export interface Toast {
  id: string;
  tone: ToastTone;
  title: string;
  description?: string;
  action?: { label: string; run: () => void };
  duration: number;
  /** 同一 id 再次顯示時遞增，讓 toaster 能重播進場動畫。 */
  version: number;
  /** 此提示寫入的工作階段紀錄 key；進行中的提示還沒有紀錄。 */
  record?: number;
}

/** 結束後保留在工作階段中的提示，讓逾時前沒讀到的提示仍找得到。 */
export interface ToastRecord {
  key: number;
  tone: ToastTone;
  title: string;
  description?: string;
  /** 執行一次後清掉（不論從提示或歷史執行），避免重複執行。 */
  action?: { label: string; run: () => void };
  /** 最後顯示的 ISO 時間。 */
  at: string;
}

/** 最新的在前；超過此數量的較舊提示會離開堆疊。 */
const LIMIT = 5;
/** 工作階段歷史只存在記憶體中，絕不送到伺服器。 */
export const HISTORY_LIMIT = 20;
let sequence = 0;
let records = 0;

/**
 * 使用者剛完成的操作之暫時確認，由殼層右下角的 `Toaster` 顯示。失敗、風險與必須持續可讀的資訊，
 * 仍用頁內的 `Notice`；提示不能是資訊的唯一來源。已結束的提示（不含 `duration: 0` 的進行中提示）
 * 也會保存在此分頁的 `history` 中（新的在前），供通知中心的工作階段列表使用。
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
    // 已登出或切換帳號後，不能再看到前一個帳號的確認訊息。
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
    // 原地更新的提示只保留一筆紀錄；之後重用同一 id 的提示會開新紀錄。
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
  /** 執行此提示的動作一次（同時影響其紀錄），然後關閉它。 */
  act(id: string) {
    const toast = this.items().find((item) => item.id === id);
    if (!toast) return;
    this.dismiss(id);
    if (toast.record !== undefined) this.spend(toast.record);
    toast.action?.run();
  }
  /** 之後從歷史執行一筆已記錄的動作；畫面上仍在的提示也會一起離開。 */
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
  /** 滑鼠懸停、聚焦或頁面隱藏時會暫停所有計時器，因為堆疊是一起讀取的。 */
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
