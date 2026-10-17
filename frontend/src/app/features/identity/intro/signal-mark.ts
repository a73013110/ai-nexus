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
  viewChildren,
} from '@angular/core';
import { ThemeService } from '@core/preferences/theme-service';
import {
  nexusHalfPaths,
  nexusHalves,
  nexusPath,
  nexusTips,
  pointAlong,
} from '@shared/graphics/nexus-logo';
import { Icon } from '@shared/ui/icon';

/** 兩條訊號從主幹尖端射出，沿著外框走，直到在另一端相遇。 */
const DRAW = 1500;
/** 相遇後，顏色從兩端湧入，並各自留下一個光環。 */
const MEET = 1950;
/** 接著標記落到字標的 N 上。 */
const LAND = 2900;
const TAIL = 0.16;
const playedKey = 'nexus.login-mark-played';

const clamp = (count: number) => Math.min(1, Math.max(0, count));
const inOut = (count2: number) => (count2 < 0.5 ? 4 * count2 ** 3 : 1 - (-2 * count2 + 2) ** 3 / 2);
const out = (count3: number) => 1 - (1 - count3) ** 3;
/** 強調：快速開始、緩慢落定，與登入的 morph 相同（motion.scss）。 */
const land = (count4: number) => 1 - (1 - count4) ** 4;

/**
 * 繪製動畫在一個分頁中只播放一次；之後的造訪（例如登出後）從已停靠的標記開始，
 * 讓頁面能在第一幀就渲染完成的字標。
 */
export function markPlayed() {
  try {
    return !!sessionStorage.getItem(playedKey);
  } catch {
    return false;
  }
}
function firstVisitThisSession() {
  try {
    if (sessionStorage.getItem(playedKey)) return false;
    sessionStorage.setItem(playedKey, '1');
  } catch {
    // 沒有儲存空間時每次造訪都會播放；繪製動畫仍可略過。
  }
  return true;
}

interface Box {
  x: number;
  y: number;
  size: number;
}

/**
 * 登入標記。它在 `origin`（頁面留出的任何空間）中繪製，並落到 `anchor`，即字標的 N，
 * 停靠後由字標接手。空間不夠時不會播放。
 */
@Component({
  selector: 'nx-signal-mark',
  styleUrl: './signal-mark.scss',
  imports: [Icon],
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: { '[class.is-complete]': 'complete()' },
  template: `<svg #stage class="signal-stage" viewBox="-1 -1 2 2" aria-hidden="true">
      <mask id="nx-signal-flood">
        @for (tip of tips; track $index) {
          <circle #flood [attr.cx]="tip.x" [attr.cy]="tip.y" r="0" fill="#fff" />
        }
      </mask>
      <path class="signal-fill" [attr.d]="outline" mask="url(#nx-signal-flood)" />
      @for (half of halves; track $index) {
        <path #trace class="signal-trace" [attr.d]="half" pathLength="1" />
        <path #tail class="signal-tail" [attr.d]="half" pathLength="1" />
      }
      @for (tip of tips; track $index) {
        <circle #ring class="signal-ring" [attr.cx]="tip.x" [attr.cy]="tip.y" r="0" />
      }
      @for (half of halves; track $index) {
        <g #head class="signal-head">
          <circle class="signal-glow" r="0" />
          <circle class="signal-core" r="0" />
        </g>
      }
    </svg>
    <button
      type="button"
      class="icon-button signal-replay"
      [disabled]="themes.reducedMotion()"
      [attr.aria-label]="complete() ? '重播標誌動畫' : '跳過標誌動畫'"
      [attr.title]="complete() ? '重播標誌動畫' : '跳過標誌動畫'"
      (click)="complete() ? play() : finish()"
    >
      <nx-icon [name]="complete() ? 'refresh' : 'chevron'" />
    </button>`,
})
export class SignalMark {
  readonly anchor = input<HTMLElement | null>(null);
  readonly origin = input<Element | null>(null);
  readonly finishedChange = output<boolean>();
  readonly themes = inject(ThemeService);
  readonly complete = signal(false);
  readonly outline = nexusPath;
  readonly halves = nexusHalfPaths;
  readonly tips = nexusTips;
  private readonly host = inject<ElementRef<HTMLElement>>(ElementRef);
  private readonly stage = viewChild.required<ElementRef<SVGSVGElement>>('stage');
  private readonly floods = viewChildren<ElementRef<SVGCircleElement>>('flood');
  private readonly traces = viewChildren<ElementRef<SVGPathElement>>('trace');
  private readonly tails = viewChildren<ElementRef<SVGPathElement>>('tail');
  private readonly rings = viewChildren<ElementRef<SVGCircleElement>>('ring');
  private readonly heads = viewChildren<ElementRef<SVGGElement>>('head');
  private frame = 0;
  private elapsed = 0;
  private previous = 0;
  private rendered = false;
  constructor() {
    const destroy = inject(DestroyRef);
    effect(() => {
      if (this.themes.reducedMotion() && this.rendered) this.finish();
    });
    afterNextRender(() => {
      this.rendered = true;
      const visibility = () => {
        cancelAnimationFrame(this.frame);
        this.previous = 0;
        if (!document.hidden && !this.complete())
          this.frame = requestAnimationFrame((time2) => this.tick(time2));
      };
      document.addEventListener('visibilitychange', visibility);
      // 標記等待或飛行時發生的版面變化，會讓它留在自己的盒子上。
      const resize = new ResizeObserver(() => this.complete() || this.paint(this.elapsed));
      resize.observe(this.host.nativeElement);
      if (firstVisitThisSession()) this.play();
      else this.finish();
      destroy.onDestroy(() => {
        cancelAnimationFrame(this.frame);
        resize.disconnect();
        document.removeEventListener('visibilitychange', visibility);
      });
    });
  }
  play() {
    cancelAnimationFrame(this.frame);
    this.elapsed = 0;
    this.previous = 0;
    this.complete.set(false);
    this.finishedChange.emit(false);
    // 小螢幕沒有空間繪製，字標直接出現。
    if (this.themes.reducedMotion() || !this.box(this.origin())) {
      this.finish();
      return;
    }
    this.paint(0);
    if (!document.hidden) this.frame = requestAnimationFrame((time3) => this.tick(time3));
  }
  finish() {
    cancelAnimationFrame(this.frame);
    this.elapsed = LAND;
    this.complete.set(true);
    this.finishedChange.emit(true);
  }
  private tick(time: number) {
    if (this.previous) this.elapsed += Math.min(time - this.previous, 80);
    this.previous = time;
    if (this.elapsed >= LAND) return this.finish();
    this.paint(this.elapsed);
    this.frame = requestAnimationFrame((time4) => this.tick(time4));
  }
  /** 宿主像素的盒子；錨點每幀都會重新讀取，因此飛行途中的縮放會被跟上。 */
  private box(element: Element | null): Box | null {
    const rect = element?.getBoundingClientRect();
    if (!rect?.width || !rect.height) return null;
    const host = this.host.nativeElement.getBoundingClientRect();
    return {
      x: rect.left - host.left + rect.width / 2,
      y: rect.top - host.top + rect.height / 2,
      size: Math.min(rect.width, rect.height),
    };
  }
  private paint(elapsed: number) {
    const end = this.box(this.anchor());
    if (!end) return;
    const start = this.box(this.origin()) ?? end;
    const drawn = inOut(clamp(elapsed / DRAW)),
      met = clamp((elapsed - DRAW) / (MEET - DRAW)),
      flight = clamp((elapsed - MEET) / (LAND - MEET)),
      moved = land(flight);
    const size = start.size + (end.size - start.size) * moved,
      pointX = start.x + (end.x - start.x) * moved,
      pointY = start.y + (end.y - start.y) * moved;
    const stage = this.stage().nativeElement;
    stage.style.width = stage.style.height = `${size}px`;
    stage.style.translate = `${pointX - size / 2}px ${pointY - size / 2}px`;
    // 以 viewBox 單位計算像素大小，讓標記縮放時線條維持原本的粗細。
    const px = 2 / size;
    stage.style.setProperty('--px', `${px}`);
    // 每個光湧都會越過中心，所以合起來能覆蓋整個標記。
    this.floods().forEach(({ nativeElement: flood }) =>
      flood.setAttribute('r', `${inOut(met) * 1.7}`),
    );
    const fading = `${1 - clamp(flight * 2.5)}`;
    this.traces().forEach(({ nativeElement: path }) => {
      path.style.strokeDasharray = `${drawn} 2`;
      path.style.opacity = fading;
    });
    const tail = Math.min(TAIL, drawn);
    this.tails().forEach(({ nativeElement: path }) => {
      path.style.strokeDasharray = `${tail} 2`;
      path.style.strokeDashoffset = `${-(drawn - tail)}`;
      path.style.opacity = `${1 - met}`;
    });
    this.heads().forEach(({ nativeElement: head }, index) => {
      const at = pointAlong(nexusHalves[index], drawn);
      head.setAttribute('transform', `translate(${at.x} ${at.y})`);
      head.style.opacity = `${clamp(elapsed / 200) * (1 - met)}`;
      head.children[0].setAttribute('r', `${9 * px}`);
      head.children[1].setAttribute('r', `${2.75 * px}`);
    });
    this.rings().forEach(({ nativeElement: ring }) => {
      ring.setAttribute('r', `${(4 + out(met) * 34) * px}`);
      ring.style.opacity = met > 0 && met < 1 ? `${0.8 * (1 - met)}` : '0';
    });
  }
}
