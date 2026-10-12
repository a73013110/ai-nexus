/** Movement before a press becomes a drag, so taps and clicks on a handle still work. */
const SLOP = 6;
/** Release speed (px/ms) that dismisses a short, quick flick. */
const FLING = 0.45;
/** Only the last stretch of movement counts toward the release speed. */
const WINDOW = 100;

/**
 * Follows one pointer along an axis. Toward the way out the surface tracks the pointer; the other
 * way it resists, since that is not a way out. It dismisses past a distance or on a fast flick.
 */
export class SwipeTracker {
  offset = 0;
  private readonly samples: [position: number, time: number][];
  constructor(
    private readonly direction: 1 | -1,
    private readonly origin: number,
    time: number,
  ) {
    this.samples = [[origin, time]];
  }
  move(position: number, time: number) {
    const delta = position - this.origin;
    this.offset = delta * this.direction > 0 ? delta : delta / 6;
    this.samples.push([position, time]);
    while (this.samples.length > 2 && time - this.samples[0][1] > WINDOW) this.samples.shift();
    return this.offset;
  }
  /** Velocity toward the way out at `time`; a pointer that paused before release has none. */
  velocity(time: number) {
    const recent = this.samples.filter(([, at]) => time - at <= WINDOW);
    if (recent.length < 2) return 0;
    const [from, at] = recent[0],
      [to, until] = recent.at(-1)!;
    return until > at ? ((to - from) / (until - at)) * this.direction : 0;
  }
  dismisses(distance: number, time: number) {
    const along = this.offset * this.direction;
    return along >= distance || (along > SLOP && this.velocity(time) > FLING);
  }
}

export interface SwipeSession {
  /** Distance past which a release dismisses. */
  distance: number;
  /** Called with the offset on every move once the press has become a drag. */
  move(offset: number): void;
  /** Called once a drag ends: released past the distance or flung, or not (also on cancel). */
  end(dismiss: boolean): void;
}

export interface SwipeGesture {
  axis: 'x' | 'y';
  direction: 1 | -1;
  /** Starts a session for this press, or null when it is not on a handle right now. */
  begin(event: PointerEvent): SwipeSession | null;
}

/** How long a spring back runs (`--motion-panel`) before the surface takes its usual transitions. */
export function swipeSettleTime(element: HTMLElement) {
  return parseFloat(getComputedStyle(element).getPropertyValue('--motion-panel')) || 240;
}

const EDITABLE = 'input, textarea, select, [contenteditable]:not([contenteditable="false"])';

/**
 * Swipe-to-dismiss on handles only (sheet headers, the drawer heading), so scrolling and text
 * selection elsewhere stay with the browser. Handles set `touch-action: none` in CSS; a press only
 * becomes a drag after the slop along the axis, and the click that follows a drag is swallowed.
 */
export function bindSwipe(target: HTMLElement | Document, gesture: SwipeGesture) {
  const at = (event: PointerEvent) => (gesture.axis === 'x' ? event.clientX : event.clientY);
  const across = (event: PointerEvent) => (gesture.axis === 'x' ? event.clientY : event.clientX);
  const down = (event: Event) => {
    const press = event as PointerEvent;
    if (!press.isPrimary || press.button !== 0) return;
    if ((press.target as Element).closest?.(EDITABLE)) return;
    const session = gesture.begin(press);
    if (!session) return;
    let tracker: SwipeTracker | null = null;
    const move = (event: PointerEvent) => {
      if (event.pointerId !== press.pointerId) return;
      if (!tracker) {
        const along = at(event) - at(press),
          aside = across(event) - across(press);
        if (Math.hypot(along, aside) < SLOP) return;
        if (Math.abs(aside) > Math.abs(along)) return stop();
        tracker = new SwipeTracker(gesture.direction, at(press), press.timeStamp);
        try {
          (press.target as Element).setPointerCapture(press.pointerId);
        } catch {
          // The element left the DOM; the document listeners still follow the pointer.
        }
        document.getSelection()?.removeAllRanges();
      }
      event.preventDefault();
      session.move(tracker.move(at(event), event.timeStamp));
    };
    const up = (event: PointerEvent) => {
      if (event.pointerId !== press.pointerId) return;
      stop();
      if (!tracker) return;
      swallowClick();
      session.end(
        event.type === 'pointerup' && tracker.dismisses(session.distance, event.timeStamp),
      );
    };
    const stop = () => {
      document.removeEventListener('pointermove', move);
      document.removeEventListener('pointerup', up);
      document.removeEventListener('pointercancel', up);
    };
    document.addEventListener('pointermove', move);
    document.addEventListener('pointerup', up);
    document.addEventListener('pointercancel', up);
  };
  target.addEventListener('pointerdown', down);
  return () => target.removeEventListener('pointerdown', down);
}

/** A drag that ends on a button must not also press it. */
function swallowClick() {
  const swallow = (event: Event) => {
    event.stopPropagation();
    event.preventDefault();
  };
  window.addEventListener('click', swallow, { capture: true, once: true });
  setTimeout(() => window.removeEventListener('click', swallow, { capture: true }));
}
