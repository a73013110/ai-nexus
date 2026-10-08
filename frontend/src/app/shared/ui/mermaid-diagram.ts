import {
  afterRenderEffect,
  ChangeDetectionStrategy,
  Component,
  computed,
  DestroyRef,
  ElementRef,
  inject,
  input,
  output,
  signal,
  untracked,
  viewChild,
  ViewEncapsulation,
} from '@angular/core';
import { NgTemplateOutlet } from '@angular/common';
import { CopyFeedback } from '../browser/copy-feedback';
import { downloadBlob } from '../browser/download';
import { CompactDialog } from './compact-dialog';
import { Icon, EXTRA_ICONS } from './icon';
import { SquareCode, ZoomIn, ZoomOut, Scan } from 'lucide';
import { ViewSwitch } from './view-switch';
import { renderDiagram } from './mermaid-renderer';
import type { sanitizeDiagramSvg } from './mermaid-svg';

type Diagram = ReturnType<typeof sanitizeDiagramSvg>;

@Component({
  selector: 'nx-diagram-canvas',
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: { class: 'mermaid-canvas-host', '[class.is-expanded]': 'expanded()' },
  template: `<div
    #canvas
    class="mermaid-canvas"
    role="region"
    aria-label="圖表畫布；可捲動或拖曳，使用加減鍵縮放、0 重設"
    tabindex="0"
    [style.height.px]="expanded() ? null : canvasHeight()"
    (keydown)="key($event)"
    (pointerdown)="panStart($event)"
    (pointermove)="pan($event)"
    (pointerup)="panEnd()"
    (pointercancel)="panEnd()"
    (lostpointercapture)="panEnd()"
  >
    <div class="mermaid-paper">
      <img
        [src]="url()"
        [alt]="diagram().title + (diagram().description ? '：' + diagram().description : '')"
        [style.width.px]="diagram().width * scale()"
        [style.height.px]="diagram().height * scale()"
        draggable="false"
      />
    </div>
  </div>`,
})
export class DiagramCanvas {
  readonly diagram = input.required<Diagram>();
  readonly url = input.required<string>();
  readonly zoom = input.required<number>();
  readonly expanded = input(false);
  readonly zoomChange = output<number>();
  private readonly canvas = viewChild.required<ElementRef<HTMLElement>>('canvas');
  private readonly width = signal(640);
  readonly scale = computed(
    () => Math.min(1, Math.max(1, this.width() - 32) / this.diagram().width) * this.zoom(),
  );
  readonly canvasHeight = computed(() =>
    Math.min(420, Math.max(180, this.diagram().height * this.scale() + 32)),
  );
  private drag?: { x: number; y: number; left: number; top: number };
  constructor() {
    afterRenderEffect((cleanup) => {
      const canvas = this.canvas().nativeElement;
      const observer = new ResizeObserver(() => {
        if (canvas.clientWidth) this.width.set(canvas.clientWidth);
      });
      observer.observe(canvas);
      cleanup(() => observer.disconnect());
    });
  }
  key(event: KeyboardEvent) {
    if (event.ctrlKey || event.metaKey || event.altKey) return;
    const value =
      event.key === '+' || event.key === '='
        ? this.zoom() * 1.25
        : event.key === '-'
          ? this.zoom() / 1.25
          : event.key === '0'
            ? 1
            : null;
    if (value !== null) {
      event.preventDefault();
      this.zoomChange.emit(value);
    }
  }
  panStart(event: PointerEvent) {
    if (event.pointerType !== 'mouse' || event.button !== 0) return;
    const canvas = this.canvas().nativeElement;
    this.drag = {
      x: event.clientX,
      y: event.clientY,
      left: canvas.scrollLeft,
      top: canvas.scrollTop,
    };
    canvas.setPointerCapture(event.pointerId);
    canvas.classList.add('is-panning');
    event.preventDefault();
  }
  pan(event: PointerEvent) {
    if (!this.drag) return;
    const canvas = this.canvas().nativeElement;
    canvas.scrollLeft = this.drag.left + this.drag.x - event.clientX;
    canvas.scrollTop = this.drag.top + this.drag.y - event.clientY;
  }
  panEnd() {
    this.drag = undefined;
    this.canvas().nativeElement.classList.remove('is-panning');
  }
}

@Component({
  selector: 'nx-mermaid-diagram',
  imports: [Icon, ViewSwitch, CompactDialog, DiagramCanvas, NgTemplateOutlet],
  providers: [
    CopyFeedback,
    {
      provide: EXTRA_ICONS,
      useValue: { code: SquareCode, 'zoom-in': ZoomIn, 'zoom-out': ZoomOut, fit: Scan },
    },
  ],
  encapsulation: ViewEncapsulation.None,
  styleUrl: '../../../styles/mermaid.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `<figure
      class="mermaid-frame"
      aria-label="Mermaid 圖表與原始碼"
      [attr.aria-busy]="loading()"
    >
      <figcaption class="mermaid-toolbar ui-density-compact">
        <span class="mermaid-label"><nx-icon name="integrations" />Mermaid</span>
        <nx-view-switch
          label="Mermaid 顯示方式"
          appearance="segment"
          [options]="views"
          [value]="view()"
          (valueChange)="view.set($event)"
        />
        <div class="mermaid-actions">
          @if (view() === 'diagram' && diagram()) {
            <ng-container [ngTemplateOutlet]="zoomTools" />
            <button
              class="icon-button"
              type="button"
              title="展開圖表"
              aria-label="展開圖表"
              (click)="expand()"
            >
              <nx-icon name="maximize" />
            </button>
          }
          <button
            class="icon-button"
            type="button"
            title="複製 Mermaid 原始碼"
            aria-label="複製 Mermaid 原始碼"
            (click)="copy.copy(source())"
          >
            <nx-icon [name]="copy.copied() ? 'check' : 'copy'" />
          </button>
          <button
            class="icon-button"
            type="button"
            title="下載 SVG 圖表"
            aria-label="下載 SVG 圖表"
            [disabled]="!diagram()"
            (click)="download()"
          >
            <nx-icon name="download" />
          </button>
        </div>
      </figcaption>
      @if (loading() && !diagram()) {
        <p class="mermaid-status" role="status"><nx-icon name="loading" />正在繪製圖表…</p>
      }
      @if (error()) {
        <div class="mermaid-error" role="status">
          <nx-icon name="info" /><span>{{ error() }}</span>
          <button class="quiet-button" type="button" (click)="retry()">
            <nx-icon name="repeat" />重試
          </button>
        </div>
      }
      @if (view() === 'source') {
        <pre class="mermaid-source"><code>{{ source() }}</code></pre>
      } @else if (diagram(); as graph) {
        <nx-diagram-canvas
          [diagram]="graph"
          [url]="url()"
          [zoom]="zoom()"
          (zoomChange)="setZoom($event)"
        />
      }
      @if (copy.error()) {
        <p class="mermaid-error" role="status">{{ copy.error() }}</p>
      }
    </figure>
    <ng-template #zoomTools>
      <div class="mermaid-zoom" role="group" aria-label="圖表縮放">
        <button
          class="icon-button"
          type="button"
          title="縮小圖表"
          aria-label="縮小圖表"
          [disabled]="zoom() <= 0.25"
          (click)="setZoom(zoom() / 1.25)"
        >
          <nx-icon name="zoom-out" />
        </button>
        <span class="mermaid-scale" aria-live="polite">{{ percent() }}%</span>
        <button
          class="icon-button"
          type="button"
          title="放大圖表"
          aria-label="放大圖表"
          [disabled]="zoom() >= 4"
          (click)="setZoom(zoom() * 1.25)"
        >
          <nx-icon name="zoom-in" />
        </button>
        <button
          class="icon-button"
          type="button"
          title="適合寬度"
          aria-label="適合寬度"
          (click)="setZoom(1)"
        >
          <nx-icon name="fit" />
        </button>
      </div>
    </ng-template>
    <dialog
      nxCompactDialog
      #dialog
      class="platform-dialog mermaid-dialog"
      aria-label="展開 Mermaid 圖表"
    >
      <header class="dialog-heading ui-density-compact">
        <h2>{{ diagram()?.title || 'Mermaid 圖表' }}</h2>
        <div class="mermaid-actions">
          <ng-container [ngTemplateOutlet]="zoomTools" />
          <button
            class="icon-button"
            type="button"
            title="下載 SVG 圖表"
            aria-label="下載 SVG 圖表"
            (click)="download()"
          >
            <nx-icon name="download" />
          </button>
          <button class="icon-button" type="button" aria-label="關閉圖表" (click)="dialog.close()">
            <nx-icon name="close" />
          </button>
        </div>
      </header>
      @if (diagram(); as graph) {
        <nx-diagram-canvas
          [diagram]="graph"
          [url]="url()"
          [zoom]="zoom()"
          [expanded]="true"
          (zoomChange)="setZoom($event)"
        />
      }
    </dialog>`,
})
export class MermaidDiagram {
  readonly source = input.required<string>();
  readonly diagram = signal<Diagram | null>(null);
  readonly url = signal('');
  readonly loading = signal(true);
  readonly error = signal('');
  readonly view = signal('diagram');
  readonly zoom = signal(1);
  readonly percent = computed(() => Math.round(this.zoom() * 100));
  readonly views = [
    { value: 'diagram', label: '圖表', icon: 'integrations' },
    { value: 'source', label: '原始碼', icon: 'code' },
  ];
  readonly copy = inject(CopyFeedback);
  private readonly host = inject<ElementRef<HTMLElement>>(ElementRef);
  private readonly dialog = viewChild.required<ElementRef<HTMLDialogElement>>('dialog');
  private readonly refresh = signal(0);
  private revision = 0;
  constructor() {
    const observer = new MutationObserver(() => this.retry());
    observer.observe(document.documentElement, {
      attributes: true,
      attributeFilter: ['data-theme', 'style'],
    });
    afterRenderEffect(() => {
      const source = this.source();
      this.refresh();
      untracked(() => void this.render(source));
    });
    inject(DestroyRef).onDestroy(() => {
      ++this.revision;
      observer.disconnect();
      if (this.url()) URL.revokeObjectURL(this.url());
    });
  }
  retry() {
    this.refresh.update((value) => value + 1);
  }
  setZoom(value: number) {
    this.zoom.set(Math.max(0.25, Math.min(4, value)));
  }
  expand() {
    this.dialog().nativeElement.showModal();
  }
  download() {
    const diagram = this.diagram();
    if (diagram)
      downloadBlob(
        new Blob([diagram.svg], { type: 'image/svg+xml;charset=utf-8' }),
        diagram.title,
        'svg',
      );
  }
  private async render(source: string) {
    const revision = ++this.revision;
    const active = () => revision === this.revision;
    const style = getComputedStyle(this.host.nativeElement);
    const color = (token: string) => style.getPropertyValue(token).trim();
    const ink = color('--ink'),
      line = color('--line'),
      surface = color('--surface');
    this.loading.set(true);
    this.error.set('');
    try {
      const result = await renderDiagram(
        source,
        {
          theme: 'base',
          look: 'neo',
          layout: 'elk',
          fontFamily: color('--font-ui'),
          flowchart: {
            htmlLabels: false,
            curve: 'basis',
            nodeSpacing: 36,
            rankSpacing: 48,
            padding: 14,
          },
          themeVariables: {
            darkMode: style.colorScheme === 'dark',
            fontFamily: color('--font-ui'),
            fontSize: style.fontSize,
            background: surface,
            primaryColor: color('--accent-soft'),
            primaryTextColor: ink,
            primaryBorderColor: line,
            nodeBorder: line,
            secondaryColor: surface,
            tertiaryColor: color('--canvas'),
            secondaryTextColor: ink,
            tertiaryTextColor: ink,
            secondaryBorderColor: line,
            tertiaryBorderColor: line,
            lineColor: color('--secondary'),
            textColor: ink,
            nodeTextColor: ink,
            clusterBkg: color('--canvas'),
            clusterBorder: line,
            edgeLabelBackground: surface,
            actorBkg: color('--accent-soft'),
            actorBorder: line,
            actorTextColor: ink,
            actorLineColor: line,
            signalColor: color('--secondary'),
            signalTextColor: ink,
            noteBkgColor: color('--canvas'),
            noteTextColor: ink,
            noteBorderColor: line,
            labelTextColor: ink,
            useGradient: false,
          },
        },
        active,
      );
      if (!active() || !result) return;
      const previous = this.url();
      this.diagram.set(result);
      this.url.set(URL.createObjectURL(new Blob([result.svg], { type: 'image/svg+xml' })));
      if (previous) URL.revokeObjectURL(previous);
    } catch {
      if (active()) {
        this.error.set('無法繪製這段 Mermaid；請檢查語法，原始碼仍可複製。');
        this.view.set('source');
      }
    } finally {
      if (active()) this.loading.set(false);
    }
  }
}
