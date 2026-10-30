import { NgTemplateOutlet } from '@angular/common';
import { SearchField } from '@shared/ui/search-field';
import { ViewSwitch } from '@shared/ui/view-switch';
import { FloatingToolbar } from '@shared/ui/floating-toolbar';
import { IdleChrome, IdleChromePart } from '@shared/ui/idle-chrome';
import { CompactDialog } from '@shared/ui/compact-dialog';
import {
  ChangeDetectionStrategy,
  Component,
  DestroyRef,
  ElementRef,
  Injector,
  afterNextRender,
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
import type { DocumentDto, DocumentPageDto, JobDto } from '@core/api/schema';
import { apiHref } from '@core/api/api-client';
import { WorkspaceSession } from '@core/auth/workspace-session';
import { Icon } from '@shared/ui/icon';
import { Select } from '@shared/ui/select';
import { TextHighlight } from '@shared/ui/text-highlight';
import { ViewScope } from '@shared/browser/view-scope';
import { hotkey } from '@shared/browser/hotkeys';
import { SHORTCUTS } from '@core/layout/keyboard-shortcuts';
import { JobToasts } from '@features/work/jobs/job-toasts';
import { jobFailure } from '@features/work/jobs/job-kind';
import { Island } from '@shared/ui/island';
import { IslandService, type IslandItem } from '@shared/ui/island-service';
import { KnowledgeApi } from '../knowledge-api';
import {
  originalOnlyHref,
  readerUrl,
  remoteFileContent,
  scopedFileHref,
  type ReaderTarget,
} from '@shared/browser/reader-overlay';
import { attachmentContent, FilesApi } from '@features/work/files/files-api';
import { RemoteFilesApi } from '@features/platform/remote-files/remote-files-api';
import { MarkdownView } from '@shared/markdown/markdown-view';
import { below } from '@shared/browser/breakpoints';
import { mediaQuery } from '@shared/browser/media-query';
import { isActive } from '@core/api/generation-status';
import { HtmlPreview } from './html-preview';
import { HtmlScriptGate } from './html-script-gate';
import { richKind } from './html-scripts';
import { documentBusy } from './document-status';
import { documentReaderTour } from './document-reader-tour';
import { ProductTourButton } from '@shared/ui/product-tour-button';

/** 不屬於知識庫的原檔（預覽圖片、分享檔案、遠端檔案）以已就緒文件的形狀顯示，沒有工作也不能編輯。 */
const readyDocument = (
  id: string,
  file: { fileName: string; contentType: string },
  pageCount = 1,
): DocumentDto => ({
  id,
  fileName: file.fileName,
  contentType: file.contentType,
  collectionId: null,
  status: 'ready',
  pageCount,
  chunkCount: 0,
  warning: null,
  jobId: null,
  canEdit: false,
  hasOriginal: true,
  textVersion: 0,
});

@Component({
  selector: 'nx-document-viewer',
  imports: [
    NgTemplateOutlet,
    SearchField,
    ViewSwitch,
    FloatingToolbar,
    IdleChromePart,
    Icon,
    Select,
    TextHighlight,
    Island,
    MarkdownView,
    HtmlPreview,
    ProductTourButton,
  ],
  host: { '(keydown)': 'shortcut($event)' },
  // 標題列（放大時）與浮動工具列共用同一套出現／淡出規則。
  hostDirectives: [IdleChrome],
  // 自備島的佇列：只顯示這份文件的狀態，不和底下對話 composer 的島互相混入。
  providers: [ViewScope, IslandService],
  changeDetection: ChangeDetectionStrategy.OnPush,
  styleUrl: './document-viewer.scss',
  templateUrl: './document-viewer.html',
})
export class DocumentViewer {
  private readonly api = inject(KnowledgeApi);
  private readonly jobToasts = inject(JobToasts);
  private readonly session = inject(WorkspaceSession);
  private readonly scope = inject(ViewScope);
  private readonly uploads = inject(FilesApi);
  private readonly remoteFiles = inject(RemoteFilesApi);
  private readonly host = inject<ElementRef<HTMLElement>>(ElementRef);
  private readonly injector = inject(Injector);
  readonly target = input.required<ReaderTarget>();
  readonly embedded = input(false);
  readonly closeRequested = output<void>();
  /** 嵌在 `ReaderDialog` 時可放大；放大後標題列浮在內容上、閒置淡出。 */
  readonly dialog = inject(CompactDialog, { optional: true });
  readonly maximized = computed(() => !!this.dialog?.maximized());
  private readonly chrome = inject(IdleChrome);
  readonly document = signal<DocumentDto | null>(null);
  readonly pages = signal<DocumentPageDto[]>([]);
  readonly job = signal<JobDto | null>(null);
  readonly canControl = signal(false);
  readonly page = signal(1);
  readonly zoom = signal('1');
  readonly mode = signal('original');
  readonly query = signal('');
  readonly searchOpen = signal(false);
  readonly rawText = signal('');
  readonly gate = new HtmlScriptGate();
  private readonly narrow = mediaQuery(below('tablet'));
  readonly loading = signal(true);
  readonly rendering = signal(false);
  readonly error = signal('');
  readonly renderError = signal('');
  readonly busy = signal(false);
  readonly pdfPages = signal(0);
  private readonly pdfRevision = signal(0);
  private readonly viewportWidth = signal(800);
  readonly canvas = viewChild<ElementRef<HTMLCanvasElement>>('pdfCanvas');
  private readonly modeSwitch = viewChild<ViewSwitch, ElementRef<HTMLElement>>(ViewSwitch, {
    read: ElementRef,
  });
  readonly total = computed(() => this.pdfPages() || this.document()?.pageCount || 1);
  readonly pageOptions = computed(() =>
    Array.from({ length: this.total() }, (_, index) => ({
      value: String(index + 1),
      label: `第 ${index + 1} 頁`,
    })),
  );
  readonly zoomOptions = ['0.75', '1', '1.25', '1.5', '2'].map((value) => ({
    value,
    label: `${Number(value) * 100}%`,
  }));
  readonly text = computed(() => this.pages().find((item) => item.pageNumber === this.page()));
  readonly matches = computed(() =>
    this.query().trim()
      ? this.pages().filter((item) =>
          item.text.toLowerCase().includes(this.query().trim().toLowerCase()),
        )
      : [],
  );
  readonly isPdf = computed(() => this.document()?.contentType === 'application/pdf');
  readonly isImage = computed(() => this.document()?.contentType.startsWith('image/') ?? false);
  readonly kind = computed(() => {
    const info = this.document();
    return info ? richKind(info.fileName, info.contentType) : 'other';
  });
  readonly isRich = computed(() => this.kind() !== 'other');
  /** 並排檢視需要寬度；低於平板斷點時退回預覽。 */
  readonly view = computed(() =>
    this.mode() === 'split' && this.narrow() ? 'preview' : this.mode(),
  );
  readonly searchDisabled = computed(() => this.kind() === 'html' && this.view() === 'preview');
  readonly markdownText = computed(
    () =>
      this.rawText() ||
      this.pages()
        .map((item) => item.text)
        .join('\n\n'),
  );
  readonly format = computed(() =>
    this.isPdf() ? 'PDF 文件' : this.isImage() ? '圖片' : '文字文件',
  );
  readonly imageLoading = signal(false);
  readonly imageZoom = signal('fit');
  readonly imageSize = signal({ width: 0, height: 0 });
  readonly imageZoomOptions = [{ value: 'fit', label: '符合視窗' }, ...this.zoomOptions];
  readonly hasText = computed(() => this.pages().some((page) => page.text.trim()));
  /** 多頁的 PDF 或擷取文字才需要頁碼；Markdown／HTML 預覽一次顯示全文。 */
  readonly paged = computed(() => this.total() > 1 && !this.isRich() && !this.isImage());
  /** 浮動工具列只在有內容功能時出現（純文字至少要有可搜尋的文字）。 */
  readonly hasTools = computed(
    () =>
      !!this.document() &&
      (this.isPdf() || this.isImage() || this.isRich() || this.paged() || this.hasText()),
  );
  readonly rawImage = signal(false);
  /** 遠端檔案只有原檔：沒有擷取文字，也就沒有檢視方式可切換。 */
  readonly switchable = computed(
    () => !this.target().remote && (this.isPdf() || this.isImage() || this.isRich()),
  );
  readonly modeOptions = computed(() =>
    this.isRich()
      ? [
          { value: 'preview', label: '預覽' },
          { value: 'text', label: '原始碼' },
          ...(this.narrow() ? [] : [{ value: 'split', label: '並排' }]),
        ]
      : this.fileModes(),
  );
  private readonly fileModes = computed(() => [
    { value: 'original', label: this.isImage() ? '原始圖片' : '原始頁面', icon: 'eye' },
    {
      value: 'text',
      label: this.rawImage() ? '辨識文字' : '擷取文字',
      icon: 'lines',
      disabled: this.busy() || (!!this.target().scope && !this.hasText()),
    },
  ]);
  readonly contentUrl = computed(() => {
    const { id, scope, remote } = this.target();
    if (remote) return remoteFileContent(remote.ticket);
    if (scope) return scopedFileHref(scope, id);
    return this.rawImage()
      ? attachmentContent(id)
      : apiHref('/api/v1/documents/{id}/content', { path: { id: this.document()?.id || '' } });
  });
  readonly downloadUrl = computed(() => {
    const remote = this.target().remote;
    return remote ? remoteFileContent(remote.ticket, true) : this.contentUrl() + '?download=true';
  });
  readonly htmlUrl = computed(() => {
    const { id, scope } = this.target();
    if (scope) return scopedFileHref(scope, id, 'render');
    return this.rawImage()
      ? apiHref('/api/v1/attachments/{id}/render', { path: { id } })
      : apiHref('/api/v1/documents/{id}/render', { path: { id: this.document()?.id || '' } });
  });
  readonly tour = computed(() => {
    const job = this.job();
    return documentReaderTour({
      view: this.isPdf() || this.isImage() ? 'original' : this.isRich() ? 'rich' : 'text',
      image: this.isImage(),
      paged: this.paged(),
      searchable: this.hasText(),
      processing: !!this.document() && !!job && job.status !== 'completed',
      embedded: this.embedded(),
    });
  });
  /** 遠端檔案沒有閱讀器網址，新分頁直接開原檔。 */
  readonly standaloneUrl = computed(
    () => originalOnlyHref(this.target()) ?? readerUrl(this.target(), this.page()),
  );
  /** 這頁沒有擷取文字時的說明；工作已失敗或停止就不說「會自動顯示」。 */
  readonly missingText = computed(() => {
    if (this.target().remote) return '這種檔案無法在這裡預覽，請下載原檔。';
    const scope = this.target().scope;
    if (scope)
      return `${scope.kind === 'share' ? '這份分享' : '這個附件'}尚無此頁的擷取文字。可預覽原檔或下載。`;
    if (this.busy()) return '正在建立文字辨識工作…';
    const status = this.job()?.status;
    if (status === 'failed' || status === 'cancelled')
      return this.canControl() ? '此頁尚無擷取文字，可從下方重新處理。' : '此頁尚無擷取文字。';
    return '此頁尚無擷取文字。處理完成後會自動顯示。';
  });
  /**
   * 處理進度、失敗與錯誤都進檢視器的動態島：浮在內容上、不佔版面（放大時也一樣），
   * 失敗會展開並就地提供重試，不必到背景任務頁。
   */
  private readonly status = computed(() => {
    const items: IslandItem[] = [];
    const busy = this.busy();
    const error = this.error();
    if (error)
      items.push({
        id: 'error',
        priority: 90,
        tone: 'danger',
        icon: 'info',
        label: '無法完成',
        message: error,
        urgent: true,
        action: { label: '重新載入', disabled: busy, run: () => void this.load(this.target()) },
      });
    const renderError = this.renderError();
    if (renderError)
      items.push({
        id: 'render',
        priority: 85,
        tone: 'warning',
        icon: this.isImage() ? 'image' : 'document',
        label: '無法顯示原始內容',
        message: renderError,
        urgent: true,
        action: this.hasText()
          ? { label: '改看文字', run: () => void this.setMode('text') }
          : undefined,
      });
    const job = this.document() ? this.job() : null;
    if (!job || job.status === 'completed') return items;
    const control = this.canControl();
    if (isActive(job.status))
      items.push({
        id: 'job-active',
        priority: 50,
        tone: 'live',
        icon: 'activity',
        label: job.cancelRequested
          ? '正在停止…'
          : job.totalUnits
            ? `${job.stage} · ${job.completedUnits} / ${job.totalUnits}`
            : job.stage,
        action: control
          ? {
              label: '停止處理',
              icon: 'stop',
              disabled: busy || job.cancelRequested,
              run: () => void this.control(false),
            }
          : undefined,
      });
    else {
      const failed = job.status === 'failed';
      items.push({
        // 每次嘗試是新的事件：重試後再失敗會重新展開。
        id: `job-${job.status}-${job.attempt}`,
        priority: 80,
        tone: failed ? 'danger' : 'info',
        icon: failed ? 'info' : 'stop',
        label: failed ? '處理失敗' : '已停止處理',
        message: failed
          ? job.errorMessage
            ? jobFailure(job)
            : '文件處理未完成。'
          : '文件處理已停止。',
        urgent: failed,
        action: control
          ? { label: '重新處理', disabled: busy, run: () => void this.control(true) }
          : undefined,
      });
    }
    return items;
  });
  private pdf: PDFDocumentProxy | null = null;
  private pdfLoad: PDFDocumentLoadingTask | null = null;
  private renderTask: RenderTask | null = null;
  private observer?: ResizeObserver;
  private controller?: AbortController;
  private version = 0;
  private renderVersion = 0;
  constructor() {
    inject(IslandService).register(this.status);
    effect(() => {
      const target = this.target();
      untracked(() => {
        this.chrome.reveal();
        void this.load(target);
      });
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
    this.searchOpen.set(false);
    this.rawText.set('');
    this.gate.reset();
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
      if (target.remote) return await this.loadRemote(target.remote, valid);
      if (attachment && !target.scope) {
        const file = await this.uploads.attachment(id);
        if (!valid()) return;
        // 預覽點陣圖片不會排入 OCR，也不會產生模型呼叫。
        if (file.isImage) {
          this.rawImage.set(true);
          this.document.set(readyDocument(id, file));
          this.imageLoading.set(true);
          return;
        }
      }
      const shared = target.scope
        ? await this.uploads.scopedPreview(target.scope, id, this.controller.signal)
        : null;
      const info: DocumentDto = shared
        ? readyDocument(id, shared.file, shared.pages.length || 1)
        : await (attachment
            ? this.api.readAttachment(id, this.controller.signal)
            : this.api.document(id, this.controller.signal));
      if (!valid()) return;
      this.document.set(info);
      this.imageLoading.set(info.contentType.startsWith('image/'));
      const kind = richKind(info.fileName, info.contentType);
      this.mode.set(
        info.contentType === 'application/pdf' || info.contentType.startsWith('image/')
          ? 'original'
          : kind !== 'other'
            ? 'preview'
            : 'text',
      );
      this.loading.set(false);
      if (kind !== 'other') void this.loadRaw(info, target, kind === 'html', valid);
      if (shared) {
        this.pages.set(shared.pages);
        // 分享可能被撤銷，定期重讀以便收回畫面；稽核檢視的每次讀取都會寫稽核，不輪詢。
        if (target.scope?.kind === 'share') this.watchShare(target, valid);
      } else await this.refresh(info.id, valid);
      if (info.contentType === 'application/pdf')
        await this.openPdf(
          target.scope
            ? this.uploads.scopedOriginal(target.scope, id, this.controller.signal)
            : this.api.original(info.id, this.controller.signal),
          valid,
        );
    } catch (error) {
      if (valid()) this.error.set(this.scope.message(error));
    } finally {
      if (valid()) this.loading.set(false);
    }
  }
  /**
   * 遠端檔案（例如公文系統的附件）：只有原檔，PDF 以 pdf.js 繪製、圖片直接載入；其他種類只提供下載。
   * 憑證過期或原系統暫時取不到時，錯誤訊息進動態島，「重新載入」無法換新憑證，所以訊息請使用者重開表單。
   */
  private async loadRemote(remote: NonNullable<ReaderTarget['remote']>, valid: () => boolean) {
    const info = readyDocument(remote.ticket, remote);
    this.document.set(info);
    const pdf = info.contentType === 'application/pdf',
      image = info.contentType.startsWith('image/');
    this.mode.set(pdf || image ? 'original' : 'text');
    this.imageLoading.set(image);
    if (!pdf) return;
    this.loading.set(false);
    await this.openPdf(this.remoteFiles.content(remote.ticket, this.controller?.signal), valid);
  }
  /** 以 pdf.js 開啟 PDF 原檔，開好後由 `afterRenderEffect` 繪製目前頁。 */
  private async openPdf(source: Promise<Response>, valid: () => boolean) {
    const bytes = await (await source).arrayBuffer();
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
    this.pdfRevision.update((item) => item + 1);
  }
  /** Markdown 預覽與 HTML 腳本檢查使用的原始文字（同源 GET）。 */
  private async loadRaw(
    info: DocumentDto,
    target: ReaderTarget,
    html: boolean,
    valid: () => boolean,
  ) {
    try {
      const response = await (target.scope
          ? this.uploads.scopedOriginal(target.scope, target.id, this.controller?.signal)
          : this.api.original(info.id, this.controller?.signal)),
        text = await response.text();
      if (!valid()) return;
      this.rawText.set(text);
      if (html) this.gate.inspect(text);
    } catch {
      // 沒有文字就無法審查 HTML，因此它會留在無腳本的沙箱中。
      if (valid() && html) this.gate.inspect('');
    }
  }
  shortcut(event: KeyboardEvent) {
    if (hotkey(event) !== SHORTCUTS.findInDocument.keys) return;
    if (!this.hasText() || this.searchDisabled()) return;
    event.preventDefault();
    this.openSearch();
  }
  openSearch() {
    this.searchOpen.set(true);
    setTimeout(() =>
      this.host.nativeElement.querySelector<HTMLInputElement>('.viewer-search input')?.focus(),
    );
  }
  toggleSearch() {
    if (!this.searchOpen()) return this.openSearch();
    this.query.set('');
    this.searchOpen.set(false);
  }
  private watchShare(target: ReaderTarget, valid: () => boolean) {
    this.scope.later(
      () => {
        if (!valid() || !target.scope) return;
        void this.uploads
          .scopedPreview(target.scope, target.id)
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
      this.jobToasts.observeDocument(info);
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
      if (documentBusy(info.status))
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
  openMatch(pageNumber: number) {
    this.navigate(pageNumber);
    void this.setMode('text');
  }
  navigate(number: number) {
    this.page.set(Math.max(1, Math.min(number, this.total())));
  }
  /**
   * 從指令碼提示改看原始碼：提示與按鈕隨預覽移除，焦點改落在浮動工具列已選的「原始碼」，
   * 留在檢視器內（Ctrl+F 照常開啟搜尋），也能直接切回預覽。
   */
  showSource() {
    void this.setMode('text');
    afterNextRender(
      () =>
        this.modeSwitch()
          ?.nativeElement.querySelector<HTMLButtonElement>('[aria-pressed="true"]')
          ?.focus(),
      { injector: this.injector },
    );
  }
  async setMode(mode: string) {
    this.mode.set(mode);
    if (this.target().scope || mode !== 'text' || !this.rawImage() || this.busy()) return;
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
      const value = await this.jobToasts.control(job.id, retry);
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
