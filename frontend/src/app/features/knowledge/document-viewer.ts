import { Notice } from '../../shared/ui/notice';
import { EmptyState } from '../../shared/ui/empty-state';
import { SearchField } from '../../shared/ui/search-field';
import { ViewSwitch } from '../../shared/ui/view-switch';
import {
  ChangeDetectionStrategy,
  Component,
  DestroyRef,
  ElementRef,
  afterRenderEffect,
  computed,
  effect,
  inject,
  input,
  output,
  signal,
  untracked,
  viewChild,
} from '@angular/core';
import type { PDFDocumentProxy, PDFDocumentLoadingTask, RenderTask } from 'pdfjs-dist';
import type { DocumentDto, DocumentPageDto, JobDto } from '../../core/api/schema';
import { apiHref } from '../../core/api/api-client';
import { WorkspaceSession } from '../../core/auth/workspace-session';
import { Icon } from '../../shared/ui/icon';
import { Select } from '../../shared/ui/select';
import { TextHighlight } from '../../shared/ui/text-highlight';
import { JobProgress } from '../tasks/job-progress';
import { ViewScope } from '../../shared/browser/view-scope';
import { JobsApi } from '../tasks/jobs-api';
import { KnowledgeApi } from './knowledge-api';
import type { ReaderTarget } from '../../shared/browser/reader-overlay';
import { SharingApi } from '../sharing/sharing-api';
import { WorkspaceApi } from '../workspace/workspace-api';

@Component({
  selector: 'nx-document-viewer',
  imports: [Notice, EmptyState, SearchField, ViewSwitch, Icon, Select, TextHighlight, JobProgress],
  providers: [ViewScope],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './document-viewer.html',
})
export class DocumentViewer {
  private readonly api = inject(KnowledgeApi);
  private readonly shares = inject(SharingApi);
  private readonly jobs = inject(JobsApi);
  private readonly session = inject(WorkspaceSession);
  private readonly scope = inject(ViewScope);
  private readonly uploads = inject(WorkspaceApi);
  readonly target = input.required<ReaderTarget>();
  readonly embedded = input(false);
  readonly expanded = input(false);
  readonly closeRequested = output<void>();
  readonly expandRequested = output<void>();
  readonly document = signal<DocumentDto | null>(null);
  readonly pages = signal<DocumentPageDto[]>([]);
  readonly job = signal<JobDto | null>(null);
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
  readonly imageZoom = signal('fit');
  readonly imageSize = signal({ width: 0, height: 0 });
  readonly imageZoomOptions = [{ value: 'fit', label: '符合視窗' }, ...this.zoomOptions];
  readonly hasText = computed(() => this.pages().some((page) => page.text.trim()));
  readonly rawImage = signal(false);
  readonly modeOptions = computed(() => [
    { value: 'original', label: this.isImage() ? '原始圖片' : '原始頁面', icon: 'eye' },
    {
      value: 'text',
      label: this.rawImage() ? '辨識文字' : '擷取文字',
      icon: 'lines',
      disabled: this.busy() || (!!this.target().shareId && !this.hasText()),
    },
  ]);
  readonly contentUrl = computed(() => {
    const { id, shareId } = this.target();
    if (shareId)
      return apiHref('/api/v1/shares/{id}/files/{file}', { path: { id: shareId, file: id } });
    return this.rawImage()
      ? apiHref('/api/v1/attachments/{id}/content', { path: { id } })
      : apiHref('/api/v1/documents/{id}/content', { path: { id: this.document()?.id || '' } });
  });
  readonly standaloneUrl = computed(() =>
    this.target().shareId
      ? `/reader/share/${encodeURIComponent(this.target().shareId!)}/${encodeURIComponent(this.target().id)}?page=${this.page()}`
      : `${this.target().attachment ? '/reader/attachment/' : '/reader/'}${encodeURIComponent(this.target().id)}?page=${this.page()}`,
  );
  private pdf: PDFDocumentProxy | null = null;
  private pdfLoad: PDFDocumentLoadingTask | null = null;
  private renderTask: RenderTask | null = null;
  private observer?: ResizeObserver;
  private controller?: AbortController;
  private version = 0;
  private renderVersion = 0;
  constructor() {
    effect(() => {
      const target = this.target();
      untracked(() => void this.load(target));
    });
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
  private async load(target: ReaderTarget) {
    const { id, attachment } = target;
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
    this.zoom.set('1');
    this.imageZoom.set('fit');
    this.imageSize.set({ width: 0, height: 0 });
    this.query.set('');
    this.rawImage.set(false);
    this.mode.set('original');
    this.imageLoading.set(false);
    this.error.set('');
    this.renderError.set('');
    this.document.set(null);
    this.pages.set([]);
    this.job.set(null);
    this.canControl.set(false);
    this.pdfPages.set(0);
    this.pdfRevision.set(0);
    this.page.set(Math.max(1, Math.floor(target.page || 1)));
    try {
      await this.session.load();
      if (!valid() || !this.session.me()) return;
      if (attachment && !target.shareId) {
        const file = await this.uploads.attachment(id);
        if (!valid()) return;
        // Previewing a raster image does not enqueue OCR or incur a model call.
        if (file.isImage) {
          this.rawImage.set(true);
          this.document.set({
            id,
            fileName: file.fileName,
            contentType: file.contentType,
            collectionId: null,
            status: 'ready',
            pageCount: 1,
            chunkCount: 0,
            warning: null,
            jobId: null,
            canEdit: false,
            hasOriginal: true,
            textVersion: 0,
          });
          this.imageLoading.set(true);
          return;
        }
      }
      const shared = target.shareId
        ? await this.shares.preview(target.shareId, id, this.controller.signal)
        : null;
      const info: DocumentDto = shared
        ? {
            id,
            fileName: shared.file.fileName,
            contentType: shared.file.contentType,
            collectionId: null,
            status: 'ready',
            pageCount: shared.pages.length || 1,
            chunkCount: 0,
            warning: null,
            jobId: null,
            canEdit: false,
            hasOriginal: true,
            textVersion: 0,
          }
        : await (attachment
            ? this.api.readAttachment(id, this.controller.signal)
            : this.api.document(id, this.controller.signal));
      if (!valid()) return;
      this.document.set(info);
      this.imageLoading.set(info.contentType.startsWith('image/'));
      this.mode.set(
        info.contentType === 'application/pdf' || info.contentType.startsWith('image/')
          ? 'original'
          : 'text',
      );
      this.loading.set(false);
      if (shared) {
        this.pages.set(shared.pages);
        this.watchShare(target, valid);
      } else await this.refresh(info.id, valid);
      if (info.contentType === 'application/pdf') {
        const response = await (target.shareId
            ? this.shares.original(target.shareId, id, this.controller.signal)
            : this.api.original(info.id, this.controller.signal)),
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
  private watchShare(target: ReaderTarget, valid: () => boolean) {
    this.scope.later(
      () => {
        if (!valid() || !target.shareId) return;
        void this.shares
          .preview(target.shareId, target.id)
          .then((value) => {
            if (valid()) {
              this.pages.set(value.pages);
              this.watchShare(target, valid);
            }
          })
          .catch((error) => {
            if (!valid()) return;
            this.renderTask?.cancel();
            void this.pdfLoad?.destroy();
            this.pdf = null;
            this.document.set(null);
            this.pages.set([]);
            this.error.set(this.scope.message(error));
          });
      },
      30000,
      'share-preview-access',
    );
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
  async setMode(mode: string) {
    this.mode.set(mode);
    if (this.target().shareId || mode !== 'text' || !this.rawImage() || this.busy()) return;
    const version = this.version,
      guard = this.scope.guard(),
      valid = () => guard() && version === this.version;
    this.busy.set(true);
    this.error.set('');
    try {
      const info = await this.api.readAttachment(this.target().id, this.controller?.signal);
      if (!valid()) return;
      this.rawImage.set(false);
      this.document.set(info);
      await this.refresh(info.id, valid);
    } catch (error) {
      if (valid()) this.error.set(this.scope.message(error));
    } finally {
      if (valid()) this.busy.set(false);
    }
  }
  imageLoaded(event: Event) {
    const image = event.target as HTMLImageElement;
    this.imageSize.set({ width: image.naturalWidth, height: image.naturalHeight });
    this.imageLoading.set(false);
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
