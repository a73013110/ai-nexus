import { describe, expect, it } from 'vitest';
import { featureTrail } from '../layout/feature-trail';
import { catalogFeature, featureForUrl, featureSections, isFeatureId, routeOf } from './catalog';

describe('feature catalog helpers', () => {
  it('finds the page or reader that owns a URL by the longest whole-segment prefix', () => {
    expect(featureForUrl('/work/knowledge')?.id).toBe('knowledge');
    expect(featureForUrl('/work/knowledge/collection-1?tab=x#y')?.id).toBe('knowledge');
    expect(featureForUrl('/work/knowledge/retrieval')?.id).toBe('knowledge.retrieval');
    expect(featureForUrl('/work/knowledge/reader/doc-1')?.id).toBe('knowledge.reader');
    expect(featureForUrl('/work/chat;draft=1/abc')?.id).toBe('chat');
    expect(featureForUrl('/work/chatter')).toBeUndefined();
    expect(featureForUrl('/admin/identity')).toBeUndefined();
    expect(featureForUrl('/settings')).toBeUndefined();
  });

  it('treats pages and actions as grants and readers as routes only', () => {
    expect(isFeatureId('diagnostics.logs.export')).toBe(true);
    expect(isFeatureId('files.reader')).toBe(false);
    expect(isFeatureId('admin')).toBe(false);
    expect(catalogFeature('diagnostics.logs.export')?.route).toBeNull();
    expect(routeOf('sharing.reader')).toBe('/work/sharing/reader');
  });

  it('groups features by their place in the full menu, unplaced and unknown ids last', () => {
    const ids = [
      'retired',
      'diagnostics.logs.export',
      'quality',
      'audit',
      'chat',
      'diagnostics.logs',
      'knowledge.retrieval',
      'dashboard',
    ];
    const navigation = [
      { id: '', path: [], featureIds: ['dashboard'] },
      { id: 'work', path: ['AI 工作台'], featureIds: ['chat'] },
      {
        id: 'system',
        path: ['系統管理'],
        featureIds: [
          'knowledge.retrieval',
          'diagnostics.logs',
          'diagnostics.logs.detail',
          'diagnostics.logs.export',
          'audit',
        ],
      },
      { id: 'system.access', path: ['系統管理', '身分與權限'], featureIds: ['identity.users'] },
    ];
    expect(
      featureSections(
        ids.map((id) => ({ id })),
        navigation,
      ).map((section) => [section.id, section.name, section.features.map((feature) => feature.id)]),
    ).toEqual([
      ['menu', '最上層', ['dashboard']],
      ['menu:work', 'AI 工作台', ['chat']],
      [
        'menu:system',
        '系統管理',
        ['knowledge.retrieval', 'diagnostics.logs', 'diagnostics.logs.export', 'audit'],
      ],
      ['unplaced', '未放入選單', ['quality']],
      ['unknown', '其他', ['retired']],
    ]);
  });

  it('names the area, the module and the page without repeating a single-page module', () => {
    const name = (id: string) => (id === 'chat' ? '聊天' : (catalogFeature(id)?.name ?? id));
    expect(featureTrail('/work/chat/abc', name)).toEqual(['AI 工作台', '聊天']);
    expect(featureTrail('/work/knowledge/retrieval', name)).toEqual([
      'AI 工作台',
      '知識庫',
      '檢索管理',
    ]);
    expect(featureTrail('/admin/access-control/roles', name)).toEqual([
      '系統管理',
      '存取控制',
      '角色',
    ]);
    expect(featureTrail('/elsewhere', name)).toEqual([]);
  });
});
