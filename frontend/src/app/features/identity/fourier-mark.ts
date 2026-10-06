import {
  afterNextRender,
  ChangeDetectionStrategy,
  Component,
  DestroyRef,
  ElementRef,
  effect,
  inject,
  input,
  output,
  signal,
  viewChild,
} from '@angular/core';
import { ThemeService } from '../../core/preferences/theme-service';
import { harmonics, reconstruct, sampleOutline } from '../../shared/graphics/fourier';
import { nexusOutline } from '../../shared/graphics/nexus-logo';
import { Icon } from '../../shared/ui/icon';

const spectrum = harmonics(sampleOutline(nexusOutline, 128)).slice(0, 48);
const trail = Array.from({ length: 641 }, (_, i) => reconstruct(spectrum, (i / 640) * Math.PI * 2));
const duration = 4200;

@Component({
  selector: 'nx-fourier-mark',
  imports: [Icon],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `<div class="fourier-stage">
      <canvas #canvas aria-hidden="true"></canvas>
    </div>
    <div class="fourier-caption">
      <span [class.fourier-copy-complete]="complete()">一道訊號，連起思考。</span
      ><button
        type="button"
        class="icon-button"
        [disabled]="themes.reducedMotion()"
        [attr.aria-label]="complete() ? '重播標誌動畫' : '跳過標誌動畫'"
        [attr.title]="complete() ? '重播標誌動畫' : '跳過標誌動畫'"
        (click)="complete() ? play() : finish()"
      >
        <nx-icon [name]="complete() ? 'refresh' : 'chevron'" />
      </button>
    </div>`,
})
export class FourierMark {
  readonly anchor = input<HTMLElement | null>(null);
  readonly finishedChange = output<boolean>();
  readonly themes = inject(ThemeService);
  readonly complete = signal(false);
  private readonly canvas = viewChild.required<ElementRef<HTMLCanvasElement>>('canvas');
  private ctx?: CanvasRenderingContext2D;
  private frame = 0;
  private elapsed = 0;
  private previous = 0;
  private size = 0;
  private ink = '';
  private signal = '';
  private line = '';
  private visible = true;
  private destination = { x: 0, y: 0, unit: 0 };
  private resize?: ResizeObserver;
  constructor() {
    const destroy = inject(DestroyRef);
    effect(() => {
      const reduced = this.themes.reducedMotion();
      if (this.ctx) {
        if (reduced) this.finish();
        else this.paint(this.elapsed);
      }
    });
    effect(() => {
      const anchor = this.anchor();
      if (this.resize) {
        this.observeAnchor(anchor);
        this.measure();
      }
    });
    afterNextRender(() => {
      const element = this.canvas().nativeElement;
      this.ctx = element.getContext('2d') ?? undefined;
      if (!this.ctx) {
        this.complete.set(true);
        this.finishedChange.emit(true);
        return;
      }
      this.resize = new ResizeObserver(() => this.measure());
      this.observeAnchor(this.anchor());
      this.measure();
      const theme = new MutationObserver(() => {
        this.colors();
        this.paint(this.elapsed);
      });
      theme.observe(document.documentElement, {
        attributes: true,
        attributeFilter: ['data-theme'],
      });
      const visibility = () => {
        this.visible = !document.hidden;
        cancelAnimationFrame(this.frame);
        this.previous = 0;
        if (this.visible && !this.complete())
          this.frame = requestAnimationFrame((t) => this.tick(t));
      };
      document.addEventListener('visibilitychange', visibility);
      this.colors();
      this.play();
      destroy.onDestroy(() => {
        cancelAnimationFrame(this.frame);
        this.resize?.disconnect();
        theme.disconnect();
        document.removeEventListener('visibilitychange', visibility);
      });
    });
  }
  private observeAnchor(anchor: HTMLElement | null) {
    this.resize?.disconnect();
    this.resize?.observe(this.canvas().nativeElement);
    if (anchor) {
      this.resize?.observe(anchor);
      // Font metrics can move the N even when the canvas size stays unchanged.
      if (anchor.parentElement) this.resize?.observe(anchor.parentElement);
    }
  }
  private measure() {
    const element = this.canvas().nativeElement;
    this.size = Math.min(element.clientWidth, element.clientHeight);
    const anchor = this.anchor()?.getBoundingClientRect(),
      bounds = element.getBoundingClientRect();
    if (anchor)
      this.destination = {
        x: anchor.left - bounds.left + anchor.width / 2,
        y: anchor.top - bounds.top + anchor.height / 2,
        unit: Math.min(anchor.width, anchor.height) / 2,
      };
    const dpr = Math.min(devicePixelRatio || 1, 2);
    element.width = Math.round(element.clientWidth * dpr);
    element.height = Math.round(element.clientHeight * dpr);
    this.ctx!.setTransform(dpr, 0, 0, dpr, 0, 0);
    this.colors();
    this.paint(this.elapsed);
  }
  private colors() {
    const tokens = getComputedStyle(this.canvas().nativeElement);
    this.ink = tokens.getPropertyValue('--accent').trim();
    this.signal = tokens.getPropertyValue('--signal').trim();
    this.line = tokens.getPropertyValue('--line').trim();
  }
  play() {
    cancelAnimationFrame(this.frame);
    this.elapsed = 0;
    this.previous = 0;
    this.complete.set(false);
    this.finishedChange.emit(false);
    if (this.themes.reducedMotion()) {
      this.finish();
      return;
    }
    if (!document.hidden && this.ctx) this.frame = requestAnimationFrame((t) => this.tick(t));
  }
  finish() {
    cancelAnimationFrame(this.frame);
    this.elapsed = duration;
    this.complete.set(true);
    this.finishedChange.emit(true);
    this.paint(duration);
  }
  private tick(time: number) {
    if (!this.ctx || !this.visible) return;
    if (this.previous) this.elapsed += Math.min(time - this.previous, 80);
    this.previous = time;
    this.paint(this.elapsed);
    if (this.elapsed >= duration) this.finish();
    else this.frame = requestAnimationFrame((t) => this.tick(t));
  }
  private paint(elapsed: number) {
    const ctx = this.ctx;
    if (!ctx || !this.size) return;
    const element = this.canvas().nativeElement,
      w = element.clientWidth,
      h = element.clientHeight;
    ctx.clearRect(0, 0, w, h);
    // Hand the completed drawing to the SVG in the wordmark. It stays crisp at every size.
    if (this.complete()) return;
    const progress = Math.min(1, elapsed / 3100),
      settle = Math.min(1, Math.max(0, (elapsed - 3100) / 1100));
    const eased = settle * settle * (3 - 2 * settle);
    const fade = 1 - eased;
    const destination = this.destination;
    const unit = this.size * 0.24 * fade + destination.unit * eased,
      cx = (w / 2) * fade + destination.x * eased,
      cy = (h / 2) * fade + destination.y * eased;
    ctx.lineWidth = 1;
    ctx.strokeStyle = this.line;
    ctx.globalAlpha = 0.5 * fade;
    for (let i = 0; i < 4; i++) {
      const r = this.size * (0.17 + i * 0.09);
      ctx.beginPath();
      ctx.arc(w / 2, h / 2, r, 0, Math.PI * 2);
      ctx.stroke();
    }
    ctx.globalAlpha = 1;
    let x = cx,
      y = cy;
    spectrum.forEach((harmonic, i) => {
      const a = harmonic.frequency * progress * Math.PI * 2 + harmonic.phase,
        radius = harmonic.amplitude * unit;
      const nextX = x + radius * Math.cos(a),
        nextY = y + radius * Math.sin(a);
      if (i < 12 && radius > 1 && fade > 0) {
        ctx.globalAlpha = 0.28 * fade;
        ctx.strokeStyle = this.ink;
        ctx.beginPath();
        ctx.arc(x, y, radius, 0, Math.PI * 2);
        ctx.stroke();
        ctx.beginPath();
        ctx.moveTo(x, y);
        ctx.lineTo(nextX, nextY);
        ctx.stroke();
      }
      x = nextX;
      y = nextY;
    });
    ctx.globalAlpha = 1;
    ctx.beginPath();
    const last = Math.floor(progress * 640);
    for (let i = 0; i <= last; i++) {
      const p = trail[i],
        px = cx + p.x * unit,
        py = cy + p.y * unit;
      if (i === 0) ctx.moveTo(px, py);
      else ctx.lineTo(px, py);
    }
    if (progress === 1) {
      ctx.closePath();
      ctx.globalAlpha = 0.045 + 0.955 * eased;
      ctx.fillStyle = this.ink;
      ctx.fill();
      ctx.globalAlpha = 1;
    }
    ctx.lineWidth = 2 * fade;
    ctx.lineJoin = 'round';
    ctx.lineCap = 'round';
    ctx.strokeStyle = this.ink;
    if (fade > 0) ctx.stroke();
    if (progress < 1) {
      ctx.fillStyle = this.signal;
      ctx.beginPath();
      ctx.arc(x, y, 3.5, 0, Math.PI * 2);
      ctx.fill();
    }
    ctx.globalAlpha = 0.8 * fade;
    ctx.fillStyle = this.signal;
    [
      [-0.72, -0.9],
      [0.72, 0.9],
    ].forEach(([dx, dy]) => {
      ctx.beginPath();
      ctx.arc(cx + dx * unit, cy + dy * unit, 3, 0, Math.PI * 2);
      ctx.fill();
    });
    ctx.globalAlpha = 1;
  }
}
