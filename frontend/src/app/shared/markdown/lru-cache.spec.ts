import { describe, expect, it } from 'vitest';
import { LruCache } from './lru-cache';

describe('LruCache', () => {
  it('returns hits, misses and keeps stored null values', () => {
    const cache = new LruCache<string, string | null>({ maxEntries: 2 });
    cache.set('a', 'A');
    cache.set('none', null);
    expect(cache.get('a')).toBe('A');
    expect(cache.get('none')).toBeNull();
    expect(cache.get('missing')).toBeUndefined();
  });

  it('evicts the least recently used entry by count', () => {
    const evicted: string[] = [];
    const cache = new LruCache<string, number>({
      maxEntries: 2,
      evicted: (_, key) => evicted.push(key),
    });
    cache.set('a', 1);
    cache.set('b', 2);
    cache.get('a');
    cache.set('c', 3);
    expect(cache.get('b')).toBeUndefined();
    expect(cache.get('a')).toBe(1);
    expect(cache.get('c')).toBe(3);
    expect(evicted).toEqual(['b']);
  });

  it('evicts by total weight, skips values heavier than the bound and updates replaced weight', () => {
    const cache = new LruCache<string, string>({
      maxEntries: 10,
      maxWeight: 10,
      weigh: (value) => value.length,
    });
    cache.set('a', 'xxxx');
    cache.set('b', 'xxxx');
    cache.set('c', 'xxxx');
    expect(cache.get('a')).toBeUndefined();
    expect(cache.weight).toBe(8);
    cache.set('huge', 'x'.repeat(11));
    expect(cache.get('huge')).toBeUndefined();
    expect(cache.size).toBe(2);
    cache.set('b', 'x');
    expect(cache.weight).toBe(5);
    cache.clear();
    expect(cache.size).toBe(0);
    expect(cache.weight).toBe(0);
  });
});
