import {
  afterNextRender,
  ChangeDetectionStrategy,
  Component,
  DestroyRef,
  ElementRef,
  effect,
  inject,
  signal,
  viewChild,
} from '@angular/core';
import { ThemeService } from '../../core/preferences/theme-service';
import { harmonics, reconstruct, sampleOutline } from '../../shared/graphics/fourier';
import { Icon } from '../../shared/ui/icon';

// An original continuous N outline: two rails joined by a diagonal, with deliberately rounded harmonics.
const spectrum = harmonics(
  sampleOutline(
    [
      { x: -0.78, y: 0.82 },
      { x: -0.78, y: -0.82 },
      { x: -0.46, y: -0.82 },
      { x: 0.46, y: 0.32 },
      { x: 0.46, y: -0.82 },
      { x: 0.78, y: -0.82 },
      { x: 0.78, y: 0.82 },
      { x: 0.46, y: 0.82 },
      { x: -0.46, y: -0.32 },
      { x: -0.46, y: 0.82 },
    ],
    128,
  ),
).slice(0, 48);
const trail = Array.from({ length: 641 }, (_, i) => reconstruct(spectrum, (i / 640) * Math.PI * 2));
const duration = 4200;

@Component({
  selector: 'nx-fourier-mark',
  imports: [Icon],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `<div class="fourier-stage">
      <canvas #canvas aria-hidden="true"></canvas
      ><span class="fourier-coordinate" aria-hidden="true">N / 01</span>
    </div>
    <div class="fourier-caption">
      <span>一道訊號，連起思考。</span
      ><button
        type="button"
        class="icon-button"
        [disabled]="themes.reducedMotion()"
        [attr.aria-label]="complete() ? '重播標誌動畫' : '跳過標誌動畫'"
        [attr.title]="complete() ? '重播標誌動畫' : '跳過標誌動畫'"
        (click)="complete() ? play() : finish()"
      >
        <nx-icon [name]="complete() ? 'repeat' : 'stop'" />
      </button>
    </div>`,
})
export class FourierMark {
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
  constructor() {
    const destroy = inject(DestroyRef);
    effect(() => {
      const reduced = this.themes.reducedMotion();
      if (this.ctx) {
        if (reduced) this.finish();
        else this.paint(this.elapsed);
      }
    });
    afterNextRender(() => {
      const element = this.canvas().nativeElement;
      this.ctx = element.getContext('2d') ?? undefined;
      if (!this.ctx) {
        this.complete.set(true);
        return;
      }
      const resize = new ResizeObserver(() => {
        this.size = Math.min(element.clientWidth, element.clientHeight);
        const dpr = Math.min(devicePixelRatio || 1, 2);
        element.width = Math.round(element.clientWidth * dpr);
        element.height = Math.round(element.clientHeight * dpr);
        this.ctx!.setTransform(dpr, 0, 0, dpr, 0, 0);
        this.colors();
        this.paint(this.elapsed);
      });
      resize.observe(element);
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
        resize.disconnect();
        theme.disconnect();
        document.removeEventListener('visibilitychange', visibility);
      });
    });
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
    const progress = Math.min(1, elapsed / 3100),
      settle = Math.min(1, Math.max(0, (elapsed - 3100) / 1100));
    const fade = 1 - settle * settle * (3 - 2 * settle),
      unit = this.size * (0.24 - 0.015 * settle),
      cx = w / 2,
      cy = h / 2;
    ctx.clearRect(0, 0, w, h);
    ctx.lineWidth = 1;
    ctx.strokeStyle = this.line;
    ctx.globalAlpha = 0.65;
    for (let i = 0; i < 4; i++) {
      const r = this.size * (0.17 + i * 0.09);
      ctx.beginPath();
      ctx.arc(cx, cy, r, 0, Math.PI * 2);
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
      ctx.globalAlpha = 0.045 + 0.095 * settle;
      ctx.fillStyle = this.ink;
      ctx.fill();
      ctx.globalAlpha = 1;
    }
    ctx.lineWidth = 2;
    ctx.lineJoin = 'round';
    ctx.lineCap = 'round';
    ctx.strokeStyle = this.ink;
    ctx.stroke();
    if (progress < 1) {
      ctx.fillStyle = this.signal;
      ctx.beginPath();
      ctx.arc(x, y, 3.5, 0, Math.PI * 2);
      ctx.fill();
    }
    ctx.globalAlpha = 0.8;
    ctx.fillStyle = this.signal;
    [
      [-0.78, -0.82],
      [0.78, 0.82],
    ].forEach(([dx, dy]) => {
      ctx.beginPath();
      ctx.arc(cx + dx * unit, cy + dy * unit, 3, 0, Math.PI * 2);
      ctx.fill();
    });
    ctx.globalAlpha = 1;
  }
}
