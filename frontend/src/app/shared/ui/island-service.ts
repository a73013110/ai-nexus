import { computed, DestroyRef, inject, Injectable, signal, type Signal } from '@angular/core';

export type IslandTone = 'info' | 'warning' | 'danger' | 'live';

export interface IslandAction {
  label: string;
  /** Shows only the icon; `label` becomes the accessible name. */
  icon?: string;
  disabled?: boolean;
  run: () => void;
}

export interface IslandItem {
  /** Stable while the condition lasts, so the island can tell new events from old ones. */
  id: string;
  /** Higher first. */
  priority: number;
  tone: IslandTone;
  icon: string;
  /** A few words for the compact pill. */
  label: string;
  /** The full sentence when expanded; defaults to `label`. */
  message?: string;
  /** Expands the island when it appears, until the person collapses it. */
  urgent?: boolean;
  action?: IslandAction;
  link?: { label: string; commands: readonly unknown[] };
}

/**
 * The priority queue behind `NexusIsland`. Features register signals of the notices their state
 * implies; the island always reflects the current set, so nothing has to be shown or hidden by hand.
 */
@Injectable({ providedIn: 'root' })
export class IslandService {
  private readonly sources = signal<readonly Signal<readonly IslandItem[]>[]>([]);
  readonly items = computed(() =>
    this.sources()
      .flatMap((source) => source())
      .sort((a, b) => b.priority - a.priority),
  );

  /** Registers for the caller's lifetime; call it in an injection context or pass `destroyRef`. */
  register(source: Signal<readonly IslandItem[]>, destroyRef = inject(DestroyRef)) {
    this.sources.update((sources) => [...sources, source]);
    destroyRef.onDestroy(() =>
      this.sources.update((sources) => sources.filter((item) => item !== source)),
    );
  }
}
