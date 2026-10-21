import { describe, expect, it } from 'vitest';
import { tourSteps } from '@shared/ui/product-tour';
import { documentReaderTour } from './document-reader-tour';

type Options = Parameters<typeof documentReaderTour>[0];
const base: Options = {
  view: 'original',
  image: false,
  paged: true,
  searchable: true,
  processing: false,
  embedded: false,
};
const steps = (options: Partial<Options>) => tourSteps(documentReaderTour({ ...base, ...options }));
const titles = (options: Partial<Options>) => steps(options).map((step) => step.title);

describe('documentReaderTour', () => {
  it('covers original pages, paging, zoom, search and returning on the reader page', () => {
    expect(titles({})).toEqual([
      '核對來源原文',
      '原始頁面與擷取文字',
      '切換頁面',
      '縮放',
      '搜尋內文',
      '下載原檔',
      '回到來源頁',
    ]);
  });

  it('talks about recognised text for images', () => {
    const image = steps({ image: true, paged: false });
    expect(image.map((step) => step.title)).toEqual([
      '核對來源原文',
      '原始圖片與辨識文字',
      '縮放',
      '搜尋內文',
      '下載原檔',
      '回到來源頁',
    ]);
    expect(image[2].description).toContain('符合視窗');
  });

  it('switches to preview and source for Markdown and HTML', () => {
    const rich = steps({ view: 'rich', paged: false });
    expect(rich.map((step) => step.title)).toEqual([
      '核對來源原文',
      '預覽與原始碼',
      '搜尋內文',
      '下載原檔',
      '回到來源頁',
    ]);
    expect(rich[2].description).toContain('切到原始碼');
  });

  it('keeps plain text to paging and search, and shows progress while processing', () => {
    expect(titles({ view: 'text', searchable: false, processing: true })).toEqual([
      '核對來源原文',
      '切換頁面',
      '處理進度',
      '下載原檔',
      '回到來源頁',
    ]);
  });

  it('replaces returning with maximise and new tab in the preview dialog', () => {
    const embedded = titles({ embedded: true });
    expect(embedded).toContain('放大或另開分頁');
    expect(embedded).not.toContain('回到來源頁');
  });

  it('scopes every target to the viewer, or the reader page for returning', () => {
    for (const embedded of [false, true])
      for (const step of steps({ embedded, processing: true }))
        expect(step.target).toMatch(
          step.title === '回到來源頁' ? /^nx-document-reader / : /^nx-document-viewer /,
        );
  });
});
