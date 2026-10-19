import { describe, expect, it } from 'vitest';
import { featureTrail } from '../layout/feature-trail';
import {
  catalogFeature,
  catalogSections,
  featureForUrl,
  isFeatureId,
  modulePages,
  routeOf,
} from './catalog';

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
    expect(modulePages('access-control').map((page) => page.id)).toEqual([
      'access-control.roles',
      'access-control.groups',
      'access-control.features',
      'access-control.navigation',
    ]);
  });

  it('groups features by area in catalog order, unknown ids last without a heading', () => {
    const ids = ['retired', 'diagnostics.logs.export', 'audit', 'chat', 'diagnostics.logs'];
    expect(
      catalogSections(ids.map((id) => ({ id }))).map((section) => [
        section.id,
        section.name,
        section.features.map((feature) => feature.id),
      ]),
    ).toEqual([
      ['work', 'AI 工作台', ['chat']],
      ['admin', '系統管理', ['audit', 'diagnostics.logs', 'diagnostics.logs.export']],
      ['', '', ['retired']],
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
