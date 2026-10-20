import { describe, expect, it } from 'vitest';
import { featureForUrl, routeOf } from '@core/api/catalog';
import { shareLink } from './share-dialog';

describe('shareLink', () => {
  it('points at the sharing page from the feature catalog', () => {
    const id = '0f8fad5b-d9cb-469f-a165-70867728950e';
    const link = shareLink(id, 'https://nexus.example/');
    // 曾寫死 `/shared/{id}`，區式網址上線後落到「頁面不存在」；以目錄反查確保連結真的開到分享頁。
    expect(link).toBe(`https://nexus.example${routeOf('sharing')}/${id}`);
    expect(featureForUrl(new URL(link).pathname)?.id).toBe('sharing');
  });
});
