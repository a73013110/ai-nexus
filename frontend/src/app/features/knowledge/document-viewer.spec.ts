import { Component } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { AuthService } from '../../core/auth/auth-service';
import { WorkspaceSession } from '../../core/auth/workspace-session';
import { SharingApi } from '../sharing/sharing-api';
import { JobsApi } from '../tasks/jobs-api';
import { JobToasts } from '../tasks/job-toasts';
import { WorkspaceApi } from '../workspace/workspace-api';
import { DocumentViewer } from './document-viewer';
import { KnowledgeApi } from './knowledge-api';

const html = '<h1>報告</h1><script>alert(1)</script>';
const info = {
  id: 'd1',
  fileName: '報告.html',
  contentType: 'text/html',
  collectionId: 'c1',
  status: 'ready',
  pageCount: 1,
  chunkCount: 1,
  warning: null,
  jobId: null,
  canEdit: false,
  hasOriginal: true,
  textVersion: 1,
};

@Component({
  selector: 'nx-document-viewer-host',
  imports: [DocumentViewer],
  template: `<nx-document-viewer [target]="{ id: 'd1', attachment: false }" />`,
})
class Host {}

async function setup() {
  vi.stubGlobal('matchMedia', (query: string) => ({
    matches: false,
    media: query,
    addEventListener() {},
    removeEventListener() {},
  }));
  vi.stubGlobal(
    'ResizeObserver',
    class {
      observe() {}
      disconnect() {}
    },
  );
  TestBed.configureTestingModule({
    providers: [
      { provide: AuthService, useValue: { guard: () => () => true } },
      { provide: WorkspaceSession, useValue: { load: async () => {}, me: () => ({ id: 'u1' }) } },
      {
        provide: KnowledgeApi,
        useValue: {
          document: async () => info,
          original: async () => new Response(html),
          pages: async () => [
            { pageNumber: 1, text: html, extraction: 'native', needsReview: false },
          ],
        },
      },
      { provide: JobToasts, useValue: { observeDocument: vi.fn() } },
      { provide: SharingApi, useValue: {} },
      { provide: JobsApi, useValue: {} },
      { provide: WorkspaceApi, useValue: {} },
    ],
  });
  const fixture = TestBed.createComponent(Host);
  const root = fixture.nativeElement as HTMLElement;
  await vi.waitFor(async () => {
    await fixture.whenStable();
    if (!root.querySelector('.html-gate')) throw new Error('script notice not shown yet');
  });
  return { fixture, root };
}

describe('DocumentViewer', () => {
  afterEach(() => {
    TestBed.resetTestingModule();
    vi.unstubAllGlobals();
  });

  it('keeps focus in the viewer when the script notice switches to the source', async () => {
    const { fixture, root } = await setup();
    const source = [...root.querySelectorAll<HTMLButtonElement>('.html-gate button')].find(
      (button) => button.textContent?.trim() === '檢視原始碼',
    )!;
    source.focus();
    source.click();
    await fixture.whenStable();
    expect(root.querySelector('nx-html-preview')).toBeNull();
    expect(root.querySelector('.reader-text pre')?.textContent).toContain('<h1>報告</h1>');
    // 「檢視原始碼」隨提示移除；焦點落在工具列已選的「原始碼」，不是 <body>。
    const focused = document.activeElement as HTMLElement;
    expect(focused.closest('nx-floating-toolbar')).not.toBeNull();
    expect(focused.getAttribute('aria-pressed')).toBe('true');
    expect(focused.textContent?.trim()).toBe('原始碼');
    // 從這裡按 Ctrl+F 仍由檢視器處理，開啟文字搜尋。
    focused.dispatchEvent(new KeyboardEvent('keydown', { key: 'f', ctrlKey: true, bubbles: true }));
    await fixture.whenStable();
    expect(root.querySelector('.viewer-search')).not.toBeNull();
  });
});
