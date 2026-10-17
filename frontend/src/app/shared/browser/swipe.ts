/** 按下後移動多少距離才算拖曳，讓把手上的點擊仍然有效。 */
const SLOP = 6;
/** 能被視為快速輕彈而關閉的放開速度（px/ms）。 */
const FLING = 0.45;
/** 放開速度只看最後一段移動。 */
const WINDOW = 100;

/**
 * 沿著單一軸追蹤一個指標。朝離開的方向時表面跟隨指標；反方向則加上阻力，因為那不是離開。
 * 超過距離或快速輕彈時就關閉。
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
  /** `time` 時朝離開方向的速度；放開前已停住的指標速度為零。 */
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
  /** 放開時超過此距離就關閉。 */
  distance: number;
  /** 按下變成拖曳後，每次移動都以位移量呼叫。 */
  move(offset: number): void;
  /** 拖曳結束時呼叫一次：超過距離或被輕彈而放開，或沒有（取消時也會）。 */
  end(dismiss: boolean): void;
}

export interface SwipeGesture {
  axis: 'x' | 'y';
  direction: 1 | -1;
  /** 為此次按下開始一個工作階段；若目前不在把手上則為 null。 */
  begin(event: PointerEvent): SwipeSession | null;
}

/** 回彈動畫的長度（`--motion-panel`），之後表面才套用一般的過場。 */
export function swipeSettleTime(element: HTMLElement) {
  return parseFloat(getComputedStyle(element).getPropertyValue('--motion-panel')) || 240;
}

const EDITABLE = 'input, textarea, select, [contenteditable]:not([contenteditable="false"])';

/**
 * 只有把手可以滑動關閉（表單標題、抽屜標題），其他地方的捲動與文字選取仍交給瀏覽器。
 * 把手在 CSS 設定 `touch-action: none`；按下後只有沿著軸超過容許範圍才變成拖曳，
 * 拖曳後緊接的點擊會被吞掉。
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
          // 元素已離開 DOM；文件層級的監聽器仍會跟隨指標。
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

/** 在按鈕上放開的拖曳，不可以同時觸發該按鈕。 */
function swallowClick() {
  const swallow = (event: Event) => {
    event.stopPropagation();
    event.preventDefault();
  };
  window.addEventListener('click', swallow, { capture: true, once: true });
  setTimeout(() => window.removeEventListener('click', swallow, { capture: true }));
}
