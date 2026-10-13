import { describe, expect, it } from 'vitest';
import { groupFeatures } from './feature-groups';

describe('shared feature grouping', () => {
  it('uses the same category order and keeps configured feature order within each group', () => {
    const features = ['operations', 'admin', 'knowledge', 'chat', 'quality', 'extension'].map(
      (id) => ({
        id,
        name: id,
      }),
    );
    expect(
      groupFeatures(features).map((group) => [
        group.id,
        group.features.map((feature) => feature.id),
      ]),
    ).toEqual([
      ['work', ['knowledge', 'chat']],
      ['collaboration', ['quality']],
      ['system', ['operations', 'admin']],
      ['additional', ['extension']],
    ]);
    expect(features[0].id).toBe('operations');
  });
  it('hides empty groups and preserves extension metadata', () => {
    const feature = { id: 'new-tool', enabled: true, path: '/new-tool' };
    expect(groupFeatures([feature])[0].features[0]).toBe(feature);
    expect(groupFeatures([])).toEqual([]);
  });
});
