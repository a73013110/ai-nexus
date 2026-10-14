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

/** Two signals leave the stem tips and trace the outline until they meet at the other tip. */
const DRAW = 1500;
/** Where they meet, colour floods in from both tips and a ring leaves each. */
const MEET = 1950;
/** Then the mark lands on the wordmark N. */
const LAND = 2900;
const TAIL = 0.16;
const playedKey = 'nexus.login-mark-played';

const clamp = (n: number) => Math.min(1, Math.max(0, n));
const inOut = (n: number) => (n < 0.5 ? 4 * n ** 3 : 1 - (-2 * n + 2) ** 3 / 2);
const out = (n: number) => 1 - (1 - n) ** 3;
/** Emphasised: a quick start that settles slowly, like the sign-in morphs (motion.scss). */
const land = (n: number) => 1 - (1 - n) ** 4;

/**
 * The drawing greets a tab once; later visits (after sign-out, say) start from the docked mark,
 * so a page can render the finished wordmark from its first frame.
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
    // Without storage every visit plays; the drawing stays skippable.
  }
  return true;
}

interface Box {
  x: number;
  y: number;
  size: number;
}

/**
 * The sign-in mark. It draws in `origin` (any box the page leaves free) and lands on `anchor`,
 * the N of the wordmark, which takes over once it docks. Without that room it does not play.
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
          this.frame = requestAnimationFrame((t) => this.tick(t));
      };
      document.addEventListener('visibilitychange', visibility);
      // Layout changes while the mark waits or flies keep it on its boxes.
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
    // Small screens leave no room to draw; the wordmark simply appears.
    if (this.themes.reducedMotion() || !this.box(this.origin())) {
      this.finish();
      return;
    }
    this.paint(0);
    if (!document.hidden) this.frame = requestAnimationFrame((t) => this.tick(t));
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
    this.frame = requestAnimationFrame((t) => this.tick(t));
  }
  /** Boxes in host pixels; the anchor is read every frame so a resize mid-flight is followed. */
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
      x = start.x + (end.x - start.x) * moved,
      y = start.y + (end.y - start.y) * moved;
    const stage = this.stage().nativeElement;
    stage.style.width = stage.style.height = `${size}px`;
    stage.style.translate = `${x - size / 2}px ${y - size / 2}px`;
    // Pixel sizes in viewBox units, so the light keeps its size while the mark scales.
    const px = 2 / size;
    stage.style.setProperty('--px', `${px}`);
    // Each flood reaches past the centre, so together they cover the mark.
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
    this.heads().forEach(({ nativeElement: head }, i) => {
      const at = pointAlong(nexusHalves[i], drawn);
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
