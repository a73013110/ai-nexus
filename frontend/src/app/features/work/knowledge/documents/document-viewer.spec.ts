import { Component, type DebugElement, signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { AuthService } from '@core/auth/auth-service';
import { WorkspaceSession } from '@core/auth/workspace-session';
import { SharingApi } from '@features/work/sharing/sharing-api';
import { JobToasts } from '@features/work/jobs/job-toasts';
import { FilesApi } from '@features/work/files/files-api';
import { ClientIssues } from '@core/errors/client-issues';
import { ProductTour, tourSteps } from '@shared/ui/product-tour';
import { DocumentViewer } from './document-viewer';
import { KnowledgeApi } from '../knowledge-api';

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

const failedJob = {
  id: 'j1',
  kind: 'document-ingest',
  subjectId: 'd1',
  label: '報告.html',
  status: 'failed',
  stage: '需要重試',
  attempt: 1,
  completedUnits: 0,
  totalUnits: 1,
  cancelRequested: false,
  errorCode: 'ocr_failed',
  errorMessage: 'Password=secret',
  issueCode: 'NX-' + 'B'.repeat(32),
  createdAt: '2026-10-20T00:00:00Z',
  updatedAt: '2026-10-20T00:00:00Z',
};

async function setup(
  ready: (root: HTMLElement) => boolean = (root) => !!root.querySelector('.html-gate'),
  overrides: {
    document?: object;
    job?: () => Promise<object>;
    control?: (id: string, retry: boolean) => Promise<object>;
  } = {},
) {
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
      unobserve() {}
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
          document: async () => ({ ...info, ...overrides.document }),
          job: overrides.job,
          original: async () => new Response(html),
          pages: async () => [
            { pageNumber: 1, text: html, extraction: 'native', needsReview: false },
          ],
        },
      },
      { provide: JobToasts, useValue: { observeDocument: vi.fn(), control: overrides.control } },
      { provide: SharingApi, useValue: {} },
      { provide: FilesApi, useValue: {} },
      { provide: ProductTour, useValue: { loading: signal(false), active: signal(false) } },
      { provide: ClientIssues, useValue: {} },
    ],
  });
  const fixture = TestBed.createComponent(Host);
  const root = fixture.nativeElement as HTMLElement;
  await vi.waitFor(async () => {
    await fixture.whenStable();
    if (!ready(root)) throw new Error('viewer not ready yet');
  });
  return { fixture, root };
}

/** 導覽每個檢視器內的目標都在畫面上（返回鈕屬於獨立閱讀器頁面，不在這個宿主內）。 */
function expectTourTargets(root: HTMLElement, fixture: { debugElement: DebugElement }) {
  const viewer = fixture.debugElement.query((element) => element.name === 'nx-document-viewer')
    .componentInstance as DocumentViewer;
  expect(root.querySelector('nx-document-viewer nx-product-tour-button')).not.toBeNull();
  const steps = tourSteps(viewer.tour()).filter((step) =>
    step.target.startsWith('nx-document-viewer '),
  );
  for (const step of steps) expect(root.querySelector(step.target), step.target).not.toBeNull();
  return steps.map((step) => step.title);
}

describe('DocumentViewer', () => {
  afterEach(() => {
    TestBed.resetTestingModule();
    vi.unstubAllGlobals();
  });

  it('offers a tour whose steps match an HTML document', async () => {
    const { fixture, root } = await setup();
    expect(expectTourTargets(root, fixture)).toEqual([
      '核對來源原文',
      '預覽與原始碼',
      '搜尋內文',
      '下載原檔',
    ]);
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

  it('shows a failed job in its own island and retries it in place', async () => {
    let job: object = failedJob;
    const control = vi.fn(async () => {
      job = {
        ...failedJob,
        status: 'queued',
        stage: '等待重試',
        errorMessage: null,
        issueCode: null,
      };
      return job;
    });
    const { fixture, root } = await setup(
      (root) => !!root.querySelector('.viewer-status .island-item'),
      {
        document: { jobId: 'j1', status: 'failed' },
        job: async () => ({ job, canControl: true }),
        control,
      },
    );
    expect(expectTourTargets(root, fixture)).toContain('處理進度');
    const status = root.querySelector('.viewer-status')!;
    // 失敗是緊急通知：直接展開，顯示查證代碼而非儲存的例外訊息。
    expect(status.getAttribute('data-mode')).toBe('expanded');
    expect(status.textContent).toContain('NX-' + 'B'.repeat(32));
    expect(status.textContent).not.toContain('Password');
    const button = [...status.querySelectorAll<HTMLButtonElement>('button')].find(
      (item) => item.textContent?.trim() === '重新處理',
    )!;
    button.click();
    await vi.waitFor(async () => {
      await fixture.whenStable();
      if (!status.textContent?.includes('等待重試')) throw new Error('job not refreshed yet');
    });
    expect(control).toHaveBeenCalledWith('j1', true);
    // 重新排入後回到精簡膠囊，顯示進度。
    expect(status.getAttribute('data-mode')).toBe('compact');
  });
});
