export interface LruCacheOptions<K, V> {
  /** Most entries kept. */
  maxEntries: number;
  /** Most total weight kept (for example characters); a single heavier value is not cached. */
  maxWeight?: number;
  weigh?: (value: V, key: K) => number;
  /** Called when a value leaves the cache by eviction, replacement or `clear`. */
  evicted?: (value: V, key: K) => void;
}

/** Bounded least-recently-used map. `get` refreshes recency; the oldest entries go first. */
export class LruCache<K, V> {
  private readonly entries = new Map<K, { value: V; weight: number }>();
  private readonly options: LruCacheOptions<K, V>;
  private total = 0;
  constructor(options: LruCacheOptions<K, V>) {
    this.options = options;
  }
  get size() {
    return this.entries.size;
  }
  get weight() {
    return this.total;
  }
  get(key: K): V | undefined {
    const entry = this.entries.get(key);
    if (!entry) return undefined;
    // Map iteration follows insertion order; re-inserting marks the entry most recent.
    this.entries.delete(key);
    this.entries.set(key, entry);
    return entry.value;
  }
  set(key: K, value: V) {
    const weight = this.options.weigh?.(value, key) ?? 0;
    this.delete(key);
    if (weight > (this.options.maxWeight ?? Infinity)) return;
    this.entries.set(key, { value, weight });
    this.total += weight;
    for (const oldest of this.entries.keys()) {
      if (
        this.entries.size <= this.options.maxEntries &&
        this.total <= (this.options.maxWeight ?? Infinity)
      )
        break;
      this.delete(oldest);
    }
  }
  delete(key: K) {
    const entry = this.entries.get(key);
    if (!entry) return false;
    this.entries.delete(key);
    this.total -= entry.weight;
    this.options.evicted?.(entry.value, key);
    return true;
  }
  clear() {
    for (const key of [...this.entries.keys()]) this.delete(key);
  }
}
