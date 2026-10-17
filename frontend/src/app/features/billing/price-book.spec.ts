import { describe, expect, it } from 'vitest';
import type { PriceDto, PriceTargetDto } from '@core/api/schema';
import { priceCatalog } from './price-book';

const now = Date.parse('2026-10-14T00:00:00Z');
const price = (
  id: string,
  modelId: string,
  effectiveAt: string,
  provider = 'google',
): PriceDto => ({
  id,
  provider,
  modelId,
  currency: 'USD',
  kind: 'api',
  inputPerMillion: 0.1,
  cachedInputPerMillion: 0,
  outputPerMillion: 0.4,
  perRequest: 0,
  requestCharge: 'completed',
  effectiveAt,
  note: '',
  modelDisplayName: null,
});
const targets: PriceTargetDto[] = [
  { provider: 'ollama', modelId: 'qwen3:8b', displayName: '本地助理' },
  { provider: 'google', modelId: 'gemma', displayName: '雲端助理' },
  { provider: 'searxng', modelId: 'web-search', displayName: '網路搜尋' },
];

describe('priceCatalog', () => {
  it('applies the latest version in effect, keeps the next scheduled one and flags the unpriced', () => {
    const scheduled = price('p3', 'gemma', '2026-11-01T00:00:00Z'),
      current = price('p2', 'gemma', '2026-10-01T00:00:00Z'),
      old = price('p1', 'gemma', '2026-09-01T00:00:00Z');
    const { current: rows } = priceCatalog([old, scheduled, current], targets, now);
    // 供應商依固定順序排列，再依名稱；每個已設定的目標都有一列。
    expect(rows.map((item) => item.name)).toEqual(['雲端助理', '本地助理', '網路搜尋']);
    expect(rows[0].current?.id).toBe('p2');
    expect(rows[0].next?.id).toBe('p3');
    expect(rows[1].current).toBeNull();
    expect(rows[1].next).toBeNull();
  });

  it('names each version by its target and states whether it applies, is scheduled or replaced', () => {
    const { history } = priceCatalog(
      [
        price('p1', 'gemma', '2026-09-01T00:00:00Z'),
        price('p3', 'gemma', '2026-11-01T00:00:00Z'),
        price('p2', 'gemma', '2026-10-01T00:00:00Z'),
      ],
      targets,
      now,
    );
    expect(history.map((item2) => [item2.price.id, item2.name, item2.state.label])).toEqual([
      ['p3', '雲端助理', '已排定'],
      ['p2', '雲端助理', '適用中'],
      ['p1', '雲端助理', '已取代'],
    ]);
  });

  it('keeps a priced model that is no longer configured, under its recorded display name only', () => {
    const removed = { ...price('p9', 'retired-model', '2026-01-01T00:00:00Z', 'brave') };
    const { current } = priceCatalog([removed], [], now);
    expect(current).toHaveLength(1);
    expect(current[0].name).toBe('AI 助理');
    expect(current[0].current?.id).toBe('p9');
  });
});
