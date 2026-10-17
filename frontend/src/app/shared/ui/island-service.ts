import { computed, DestroyRef, inject, Injectable, signal, type Signal } from '@angular/core';

export type IslandTone = 'info' | 'warning' | 'danger' | 'live';

export interface IslandAction {
  label: string;
  /** 只顯示圖示；`label` 會成為無障礙名稱。 */
  icon?: string;
  disabled?: boolean;
  run: () => void;
}

export interface IslandItem {
  /** 條件成立期間保持穩定，讓島能分辨新事件與舊事件。 */
  id: string;
  /** 數字越大越前面。 */
  priority: number;
  tone: IslandTone;
  icon: string;
  /** 精簡膠囊顯示的幾個字。 */
  label: string;
  /** 展開時顯示的完整句子；預設為 `label`。 */
  message?: string;
  /** 出現時展開，直到使用者收合為止。 */
  urgent?: boolean;
  action?: IslandAction;
  link?: { label: string; commands: readonly unknown[] };
}

/**
 * `NexusIsland` 背後的優先佇列。功能會註冊由其狀態推導出的通知 signal；島永遠反映目前的集合，
 * 因此不需要手動顯示或隱藏任何東西。
 */
@Injectable({ providedIn: 'root' })
export class IslandService {
  private readonly sources = signal<readonly Signal<readonly IslandItem[]>[]>([]);
  readonly items = computed(() =>
    this.sources()
      .flatMap((source) => source())
      .sort((first, second) => second.priority - first.priority),
  );

  /** 在呼叫端的生命週期內註冊；在 injection context 中呼叫，或傳入 `destroyRef`。 */
  register(source: Signal<readonly IslandItem[]>, destroyRef = inject(DestroyRef)) {
    this.sources.update((sources) => [...sources, source]);
    destroyRef.onDestroy(() =>
      this.sources.update((sources) => sources.filter((item) => item !== source)),
    );
  }
}
