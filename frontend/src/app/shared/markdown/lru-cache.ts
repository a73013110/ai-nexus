export interface LruCacheOptions<K, V> {
  /** 保留的最多項目數。 */
  maxEntries: number;
  /** 保留的最大總權重（例如字元數）；單一過重的值不會被快取。 */
  maxWeight?: number;
  weigh?: (value: V, key: K) => number;
  /** 值因淘汰、替換或 `clear` 離開快取時呼叫。 */
  evicted?: (value: V, key: K) => void;
}

/** 有上限的最近最少使用對應表。`get` 會更新新近度；最舊的項目先被淘汰。 */
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
    // Map 的迭代依插入順序；重新插入會把該項目標記為最新。
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
