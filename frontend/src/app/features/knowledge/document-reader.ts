import {
  ChangeDetectionStrategy,
  Component,
  DestroyRef,
  ElementRef,
  afterRenderEffect,
  computed,
  inject,
  signal,
  viewChild,
} from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { ActivatedRoute } from '@angular/router';
import type { PDFDocumentProxy, PDFDocumentLoadingTask, RenderTask } from 'pdfjs-dist';
import type { DocumentInfo, DocumentPage, Job } from '../../core/api/types';
import { WorkspaceSession } from '../../core/auth/workspace-session';
import { FeaturePage } from '../../shared/ui/feature-page';
import { Icon } from '../../shared/ui/icon';
import { Select } from '../../shared/ui/select';
import { TextHighlight } from '../../shared/ui/text-highlight';
import { JobProgress } from '../../shared/ui/job-progress';
import { ViewScope } from '../../shared/browser/view-scope';
import { JobsApi } from '../tasks/jobs-api';
import { KnowledgeApi } from './knowledge-api';
import {
  ReaderNavigation,
  readerReturnLabel,
  readerReturnUrl,
} from '../../shared/browser/reader-navigation';
import { WORKSPACE_HOME } from '../../core/workspace-home';

@Component({
  selector: 'nx-document-reader',
  imports: [FeaturePage, Icon, Select, TextHighlight, JobProgress],
  providers: [ViewScope],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './document-reader.html',
})
export class DocumentReader {
  private readonly api = inject(KnowledgeApi);
  private readonly jobs = inject(JobsApi);
  private readonly session = inject(WorkspaceSession);
  private readonly scope = inject(ViewScope);
  private readonly route = inject(ActivatedRoute);
  private readonly navigation = inject(ReaderNavigation);
  readonly returnTo = signal(WORKSPACE_HOME);
  readonly returnLabel = computed(() => readerReturnLabel(this.returnTo()));
  readonly document = signal<DocumentInfo | null>(null);
  readonly pages = signal<DocumentPage[]>([]);
  readonly job = signal<Job | null>(null);
  readonly canControl = signal(false);
  readonly page = signal(1);
  readonly zoom = signal('1');
  readonly mode = signal('original');
  readonly query = signal('');
  readonly loading = signal(true);
  readonly rendering = signal(false);
  readonly error = signal('');
  readonly renderError = signal('');
  readonly busy = signal(false);
  readonly pdfPages = signal(0);
  private readonly pdfRevision = signal(0);
  private readonly viewportWidth = signal(800);
  readonly canvas = viewChild<ElementRef<HTMLCanvasElement>>('pdfCanvas');
  readonly total = computed(() => this.pdfPages() || this.document()?.pageCount || 1);
  readonly pageOptions = computed(() =>
    Array.from({ length: this.total() }, (_, i) => ({
      value: String(i + 1),
      label: `第 ${i + 1} 頁`,
    })),
  );
  readonly zoomOptions = ['0.75', '1', '1.25', '1.5', '2'].map((value) => ({
    value,
    label: `${Number(value) * 100}%`,
  }));
  readonly text = computed(() => this.pages().find((x) => x.pageNumber === this.page()));
  readonly matches = computed(() =>
    this.query().trim()
      ? this.pages().filter((x) => x.text.toLowerCase().includes(this.query().trim().toLowerCase()))
      : [],
  );
  readonly isPdf = computed(() => this.document()?.contentType === 'application/pdf');
  readonly isImage = computed(() => this.document()?.contentType.startsWith('image/') ?? false);
  readonly format = computed(() =>
    this.isPdf() ? 'PDF 文件' : this.isImage() ? '圖片' : '文字文件',
  );
  readonly imageLoading = signal(false);
  readonly hasText = computed(() => this.pages().some((page) => page.text.trim()));
  readonly contentUrl = computed(() => `/api/v1/documents/${this.document()?.id}/content`);
  private pdf: PDFDocumentProxy | null = null;
  private pdfLoad: PDFDocumentLoadingTask | null = null;
  private renderTask: RenderTask | null = null;
  private observer?: ResizeObserver;
  private controller?: AbortController;
  private version = 0;
  private renderVersion = 0;
  constructor() {
    this.route.paramMap
      .pipe(takeUntilDestroyed())
      .subscribe(
        (params) =>
          void this.load(
            params.get('id')!,
            this.route.snapshot.routeConfig?.path?.includes('attachment') ?? false,
          ),
      );
    afterRenderEffect(() => {
      const canvas = this.canvas()?.nativeElement,
        page = this.page(),
        zoom = Number(this.zoom()),
        revision = this.pdfRevision(),
        width = this.viewportWidth();
      if (!canvas && this.observer) {
        this.observer.disconnect();
        this.observer = undefined;
      }
      if (canvas && revision && this.pdf && this.mode() === 'original') {
        if (!this.observer) {
          this.observer = new ResizeObserver((entries) =>
            this.viewportWidth.set(entries[0].contentRect.width),
          );
          this.observer.observe(canvas.parentElement!);
        }
        void this.render(canvas, page, zoom, width);
      }
    });
    inject(DestroyRef).onDestroy(() => {
      this.version++;
      this.controller?.abort();
      this.observer?.disconnect();
      this.renderTask?.cancel();
      void this.pdfLoad?.destroy();
    });
  }
  private async load(id: string, attachment: boolean) {
    const version = ++this.version,
      guard = this.scope.guard(),
      valid = () => guard() && version === this.version;
    this.renderVersion++;
    this.controller?.abort();
    this.controller = new AbortController();
    this.renderTask?.cancel();
    this.observer?.disconnect();
    this.observer = undefined;
    void this.pdfLoad?.destroy();
    this.pdf = null;
    this.pdfLoad = null;
    this.loading.set(true);
    const returnTo = readerReturnUrl(this.route.snapshot.queryParamMap.get('returnTo'));
    this.returnTo.set(returnTo ?? WORKSPACE_HOME);
    this.navigation.enter(returnTo);
    this.imageLoading.set(false);
    this.error.set('');
    this.renderError.set('');
    this.document.set(null);
    this.pages.set([]);
    this.job.set(null);
    this.canControl.set(false);
    this.pdfPages.set(0);
    this.pdfRevision.set(0);
    this.page.set(Number(this.route.snapshot.queryParamMap.get('page')) || 1);
    try {
      await this.session.load();
      if (!valid() || !this.session.me()) return;
      const info = await (attachment
        ? this.api.readAttachment(id, this.controller.signal)
        : this.api.document(id, this.controller.signal));
      if (!valid()) return;
      this.document.set(info);
      if (!returnTo && info.collectionId) this.returnTo.set('/knowledge');
      this.imageLoading.set(info.contentType.startsWith('image/'));
      this.mode.set(
        info.contentType === 'application/pdf' || info.contentType.startsWith('image/')
          ? 'original'
          : 'text',
      );
      this.loading.set(false);
      await this.refresh(info.id, valid);
      if (info.contentType === 'application/pdf') {
        const response = await this.api.original(info.id, this.controller.signal),
          bytes = await response.arrayBuffer();
        if (!valid()) return;
        const lib = await import('pdfjs-dist');
        if (!valid()) return;
        lib.GlobalWorkerOptions.workerSrc = '/vendor/pdfjs/pdf.worker.min.mjs';
        const task = (this.pdfLoad = lib.getDocument({
          data: new Uint8Array(bytes),
          useWasm: false,
          disableFontFace: true,
          cMapUrl: '/vendor/pdfjs/cmaps/',
          cMapPacked: true,
          standardFontDataUrl: '/vendor/pdfjs/standard_fonts/',
          wasmUrl: '/vendor/pdfjs/wasm/',
        }));
        const pdf = await task.promise;
        if (!valid()) {
          await task.destroy();
          return;
        }
        this.pdf = pdf;
        this.pdfPages.set(pdf.numPages);
        this.page.set(Math.max(1, Math.min(this.page(), pdf.numPages)));
        this.pdfRevision.update((x) => x + 1);
      }
    } catch (error) {
      if (valid()) this.error.set(this.scope.message(error));
    } finally {
      if (valid()) this.loading.set(false);
    }
  }
  private async refresh(id: string, valid: () => boolean) {
    try {
      const info = await this.api.document(id);
      if (!valid()) return;
      this.document.set(info);
      const pages = await this.api.pages(id);
      if (!valid()) return;
      this.pages.set(pages);
      if (info.jobId) {
        const value = await this.api.job(id);
        if (!valid()) return;
        this.job.set(value.job);
        this.canControl.set(value.canControl);
      }
      if (info.status !== 'ready' && ['queued', 'running', 'processing'].includes(info.status))
        this.scope.later(() => void this.refresh(id, valid), 2500, 'reader-poll');
    } catch (error) {
      if (valid()) this.error.set(this.scope.message(error));
    }
  }
  private async render(canvas: HTMLCanvasElement, number: number, zoom: number, available: number) {
    const version = ++this.renderVersion,
      guard = this.scope.guard(),
      pdf = this.pdf;
    this.renderTask?.cancel();
    await this.renderTask?.promise.catch(() => {});
    if (!pdf || !guard() || version !== this.renderVersion) return;
    this.rendering.set(true);
    this.renderError.set('');
    try {
      const page = await pdf.getPage(number);
      if (!guard() || version !== this.renderVersion) return;
      const base = page.getViewport({ scale: 1 }),
        scale = Math.min(available / base.width, 1.5) * zoom,
        viewport = page.getViewport({ scale: Math.max(0.25, scale) }),
        dpr = Math.min(devicePixelRatio || 1, 2);
      if (
        viewport.width * viewport.height * dpr * dpr > 16000000 ||
        viewport.width > 16000 ||
        viewport.height > 16000
      )
        throw new Error('Canvas size limit.');
      canvas.width = Math.floor(viewport.width * dpr);
      canvas.height = Math.floor(viewport.height * dpr);
      canvas.style.width = `${viewport.width}px`;
      canvas.style.height = `${viewport.height}px`;
      this.renderTask = page.render({ canvas, viewport, transform: [dpr, 0, 0, dpr, 0, 0] });
      await this.renderTask.promise;
    } catch (error) {
      if (
        guard() &&
        version === this.renderVersion &&
        !(error instanceof Error && error.name === 'RenderingCancelledException')
      )
        this.renderError.set('原始頁面無法顯示，請下載原檔或使用擷取文字檢視。');
    } finally {
      if (guard() && version === this.renderVersion) this.rendering.set(false);
    }
  }
  navigate(number: number) {
    this.page.set(Math.max(1, Math.min(number, this.total())));
  }
  back(event: MouseEvent) {
    if (event.button !== 0 || event.ctrlKey || event.metaKey || event.shiftKey || event.altKey)
      return;
    event.preventDefault();
    this.navigation.back(this.returnTo());
  }
  imageFailed() {
    this.imageLoading.set(false);
    this.renderError.set('原始圖片無法顯示，請重新開啟或下載原檔。');
  }
  async control(retry: boolean) {
    const job = this.job();
    if (!job || this.busy()) return;
    const valid = this.scope.guard(),
      id = this.document()?.id;
    this.busy.set(true);
    this.error.set('');
    try {
      const value = await (retry ? this.jobs.retry(job.id) : this.jobs.cancel(job.id));
      if (valid() && this.document()?.id === id) {
        this.job.set(value);
        await this.refresh(id!, valid);
      }
    } catch (error) {
      if (valid()) this.error.set(this.scope.message(error));
    } finally {
      if (valid()) this.busy.set(false);
    }
  }
}
