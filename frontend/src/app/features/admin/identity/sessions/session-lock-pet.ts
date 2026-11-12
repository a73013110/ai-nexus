import {
  ChangeDetectionStrategy,
  Component,
  DestroyRef,
  ElementRef,
  afterNextRender,
  computed,
  effect,
  inject,
  input,
  signal,
  untracked,
  viewChild,
} from '@angular/core';
import { ThemeService } from '@core/preferences/theme-service';
import { TRANSIENT_HOLD_MS } from '@shared/browser/motion-tokens';
import { LockPetArt } from './lock-pet-art';
import { nextLookChange, petLook } from './lock-pet-calendar';
import {
  Fling,
  PET_HEIGHT,
  PET_WIDTH,
  PetBody,
  SLEEP_AFTER_MS,
  hasRoom,
  petMood,
  type PetAction,
  type PetMood,
  type PetStage,
} from './lock-pet-physics';
import { readFarthest, writeFarthest } from './lock-pet-record';

/** 跳起來的初速（px/s）：被吵醒、按下解鎖、密碼輸錯被震到、被點一下。 */
const HOP = { wake: 320, cheer: 460, stumble: 380, tap: 420 } as const;
/** 滑鼠在牠身上滑過這麼長（px）算摸一下。 */
const RUB_DISTANCE = 140;
/** 分頁切回來或停了很久之後，第一幀不補算中間的時間。 */
const MAX_STEP = 0.05;
const DIZZY_MS = 1200;
const HAPPY_MS = 1400;
/** 時段動作的長度，與 lock-pet-art.scss 的動畫一致。 */
const ACTION_MS: Record<PetAction, number> = { stretch: 1400, yawn: 1600 };
/** 從倒數島掉下來時從一半大小長回原尺寸，島下掛著的那隻是半尺寸。 */
const GROW_MS = 400;

interface Heart {
  id: number;
  x: number;
  y: number;
}

/**
 * 鎖定畫面上的小鎖：站在鎖定卡頂緣的裝飾角色。可以拎起來丟、摸摸會冒愛心；
 * 密碼欄有字時遮眼、密碼顯示出來時轉身、大寫鎖定開著時舉牌，解鎖成功時自己的鎖扣也一起彈開。
 * 純裝飾：整層 `aria-hidden`、不能聚焦，按下時不搶走密碼欄的焦點；大寫鎖定的正式提示在密碼欄下方。
 * 指標事件與逐幀更新直接掛原生監聽、直接寫樣式，不經變更偵測；只有表情、打扮、泡泡與愛心是 signal。
 */
@Component({
  selector: 'nx-session-lock-pet',
  imports: [LockPetArt],
  templateUrl: './session-lock-pet.html',
  styleUrl: './session-lock-pet.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: { 'aria-hidden': 'true', '[class.is-away]': '!room()' },
})
export class SessionLockPet {
  /** 站的地方：鎖定卡看得到的外框（變形時跟著縮放的那一層）。 */
  readonly card = input.required<HTMLElement>();
  /** 從哪裡掉下來（視窗座標的腳底位置）；`null` 是從卡片上方落下。 */
  readonly origin = input<{ x: number; y: number } | null>(null);
  /** 密碼欄有字：遮住眼睛。 */
  readonly shy = input(false);
  /** 密碼顯示出來了：轉身背對。 */
  readonly revealed = input(false);
  /** 大寫鎖定開著：舉牌提醒。 */
  readonly capsLock = input(false);
  /** 正在驗證密碼：跳一下替你加油。 */
  readonly busy = input(false);
  /** 解鎖成功：跳起來、鎖扣彈開，之後不再站回卡片。 */
  readonly unlocked = input(false);
  /** 輸錯的次數；每多一次，卡片抖動時牠也被震暈。 */
  readonly rejections = input(0);
  /** 最遠紀錄依帳號存在這台瀏覽器；沒有帳號就只記在這次鎖定。 */
  readonly owner = input<string | null>(null);

  protected readonly mood = signal<PetMood>('idle');
  protected readonly look = signal(petLook(new Date()));
  protected readonly saying = signal('');
  protected readonly hearts = signal<readonly Heart[]>([]);
  protected readonly room = signal(true);
  /** 舉牌時泡泡變成牌子；有別的話要說時先說完。 */
  protected readonly signing = computed(() => !this.saying() && this.mood() === 'sign');
  protected readonly speech = computed(
    () => this.saying() || (this.signing() ? '大寫鎖定開著' : ''),
  );

  private readonly theme = inject(ThemeService);
  private readonly host = inject<ElementRef<HTMLElement>>(ElementRef).nativeElement;
  private readonly figure = viewChild.required<ElementRef<HTMLElement>>('figure');
  private readonly bubble = viewChild.required<ElementRef<HTMLElement>>('bubble');
  private readonly body = new PetBody();
  private readonly fling = new Fling();
  private farthest = 0;
  private ready = false;
  private entered = false;
  private enteredAt = 0;
  private perched = false;
  private holding = false;
  private frame = 0;
  private lastFrame = 0;
  private timer: ReturnType<typeof setTimeout> | undefined;
  private sayTimer: ReturnType<typeof setTimeout> | undefined;
  private lastActivity = performance.now();
  private nextLook = nextLookChange(new Date()).getTime();
  private action: { mood: PetAction; until: number } | null = null;
  /** 這一層在視窗中的位置：指標與倒數島的視窗座標要換成這一層的座標。 */
  private offset = { x: 0, y: 0 };
  /** 指標還沒動過時是 `null`，眼睛直視前方。 */
  private pointer: { x: number; y: number } | null = null;
  private bubbleWidth = 0;
  private rubbed = 0;
  private heartId = 0;
  private said = new Set<string>();

  constructor() {
    const destroy = inject(DestroyRef);
    // 輸入框有字、顯示密碼、大寫鎖定、送出、輸錯都表示人還在：叫醒牠，讓牠接著反應。
    effect(() => {
      this.shy();
      this.revealed();
      this.capsLock();
      untracked(() => this.stir());
    });
    effect(() => {
      if (this.busy() || this.unlocked()) untracked(() => this.cheer());
    });
    let rejected = untracked(this.rejections);
    effect(() => {
      const count = this.rejections();
      if (count > rejected) untracked(() => this.stumble());
      rejected = count;
    });
    effect(() => {
      this.theme.reducedMotion();
      untracked(() => this.kick());
    });
    afterNextRender(() => {
      this.farthest = readFarthest(this.owner());
      // 原生監聽而不是 host／範本事件：pointermove 每秒幾十次，不該每次都排一輪變更偵測。
      const listening = new AbortController();
      const options = { signal: listening.signal };
      document.addEventListener('pointermove', (event) => this.track(event), options);
      document.addEventListener('pointerdown', () => this.stir(), options);
      // 鎖定卡在可捲動的舞台裡：矮視窗捲動、螢幕鍵盤推動版面時要重新站好（scroll 不冒泡，所以用捕獲）。
      document.addEventListener('scroll', () => this.kick(), { ...options, capture: true });
      const figure = this.figure().nativeElement;
      figure.addEventListener('pointerdown', (event) => this.grab(event), options);
      figure.addEventListener('pointermove', (event) => this.drag(event), options);
      figure.addEventListener('pointerup', (event) => this.release(event), options);
      figure.addEventListener('pointercancel', (event) => this.release(event), options);
      figure.addEventListener('pointerenter', () => this.hint(), options);
      const resize = new ResizeObserver(() => this.kick());
      resize.observe(this.host);
      resize.observe(this.card());
      destroy.onDestroy(() => {
        listening.abort();
        resize.disconnect();
        cancelAnimationFrame(this.frame);
        clearTimeout(this.timer);
        clearTimeout(this.sayTimer);
      });
      this.ready = true;
      this.kick();
    });
  }

  protected fade(id: number) {
    this.hearts.update((hearts) => hearts.filter((heart) => heart.id !== id));
  }

  /** 排下一幀；已經排了就不重複。睡著或減少動態時逐幀更新會停，靠這裡在有事時再畫一幀。 */
  private kick() {
    clearTimeout(this.timer);
    if (!this.ready || this.frame) return;
    this.frame = requestAnimationFrame((now) => this.tick(now));
  }

  private tick(now: number) {
    this.frame = 0;
    const dt = this.lastFrame ? Math.min(MAX_STEP, (now - this.lastFrame) / 1000) : 0;
    this.lastFrame = now;
    if (Date.now() >= this.nextLook) this.refreshLook();
    // 先讀版面再寫樣式，同一幀裡不強迫重新排版。
    const stage = this.measure();
    const reduced = this.theme.reducedMotion();
    if (!this.entered && !this.enter(stage, now, reduced)) return this.hide();
    // 站著時卡片上方放不下就躲起來；空中（含從倒數島掉下來、卡片還在撐開）與解鎖離場時不管。
    const standing = this.body.mode === 'perch' || this.body.mode === 'ground';
    if (standing && !this.unlocked() && !hasRoom(stage)) return this.hide();
    this.room.set(true);
    const before = this.mood();
    if (this.action && now >= this.action.until) this.action = null;
    if (reduced) this.body.rest(stage);
    else {
      const calm = ['sleep', 'shy', 'sign', 'turned', 'stretch', 'yawn'].includes(before);
      const landing = this.body.step(dt, now, stage, !calm);
      if (landing) this.touchDown(landing.on, landing.meters, now);
    }
    const asleep = now - this.lastActivity > SLEEP_AFTER_MS;
    let mood = this.moodAt(now, asleep);
    if (before === 'sleep' && mood !== 'sleep') {
      if (!this.wakeUp(now, reduced)) this.body.hop(HOP.wake);
      mood = this.moodAt(now, asleep);
    }
    this.firstTime(mood);
    this.mood.set(mood);
    this.render(stage, mood, now);
    if (!reduced && !(mood === 'sleep' && this.body.resting)) return this.kick();
    // 停下來之後，表情還會在固定時間改變（開心結束、該睡了、換時段），到時再畫一幀；這一幀裡已經有人排了下一幀就不必。
    this.lastFrame = 0;
    if (this.frame) return;
    const next = Math.min(
      ...[
        this.body.happyUntil,
        this.body.dizzyUntil,
        this.action?.until ?? 0,
        this.lastActivity + SLEEP_AFTER_MS,
        now + (this.nextLook - Date.now()),
      ].filter((time) => time > now),
    );
    if (Number.isFinite(next)) this.timer = setTimeout(() => this.kick(), next - now + 16);
  }

  /** 第一幀：從倒數島掉下來，或從卡片上方落下（卡片上方放不下就先不出來）。 */
  private enter(stage: PetStage, now: number, reduced: boolean) {
    const from = this.origin();
    if (!from && !hasRoom(stage)) return false;
    this.entered = true;
    this.enteredAt = now;
    if (reduced) this.body.rest(stage);
    else
      this.body.drop(
        stage,
        now,
        from ? { x: from.x - this.offset.x, y: from.y - this.offset.y } : undefined,
      );
    return true;
  }

  private hide() {
    this.room.set(false);
    this.entered = false;
    this.perched = false;
    this.lastFrame = 0;
  }

  private moodAt(now: number, asleep: boolean) {
    return petMood(this.body, now, {
      shy: this.shy(),
      turned: this.revealed() && this.shy(),
      sign: this.capsLock(),
      asleep,
      action: this.action?.mood ?? null,
    });
  }

  /** 每種狀態第一次出現時說一句，之後就不再重複。 */
  private firstTime(mood: PetMood) {
    const line = mood === 'shy' ? '我不會偷看' : mood === 'turned' ? '我轉過去了' : '';
    if (!line || this.said.has(line)) return;
    this.said.add(line);
    this.say(line);
  }

  /** 早上醒來伸懶腰、晚上醒來打哈欠；做了就回傳 `true`。 */
  private wakeUp(now: number, reduced: boolean) {
    if (reduced) return false;
    const moment = this.look().moment;
    const mood: PetAction | null =
      moment === 'morning' ? 'stretch' : moment === 'evening' ? 'yawn' : null;
    if (!mood) return false;
    this.action = { mood, until: now + ACTION_MS[mood] };
    return true;
  }

  private touchDown(on: 'perch' | 'ground', meters: number | null, now: number) {
    if (on === 'perch' && !this.perched) {
      // 進場第一次站上卡片：照時段伸懶腰或打哈欠。
      this.perched = true;
      this.wakeUp(now, false);
    }
    if (meters === null) return;
    const record = meters > this.farthest;
    if (record) {
      this.farthest = meters;
      writeFarthest(this.owner(), meters);
    }
    const distance = `飛了 ${meters.toFixed(1)} 公尺`;
    this.say(record ? `新紀錄！${distance}` : distance);
  }

  private refreshLook() {
    const date = new Date();
    this.look.set(petLook(date));
    this.nextLook = nextLookChange(date).getTime();
  }

  private measure(): PetStage {
    const layer = this.host.getBoundingClientRect();
    const card = this.card().getBoundingClientRect();
    this.offset = { x: layer.left, y: layer.top };
    if (this.speech()) this.bubbleWidth = this.bubble().nativeElement.offsetWidth;
    return {
      width: layer.width,
      height: layer.height,
      card: {
        left: card.left - layer.left,
        right: card.right - layer.left,
        top: card.top - layer.top,
      },
    };
  }

  private render(stage: PetStage, mood: PetMood, now: number) {
    const { x, y, rot, squash } = this.body;
    const stretch = 1 + (1 - squash) * 0.7;
    const grow = this.origin() ? Math.min(1, 0.5 + ((now - this.enteredAt) / GROW_MS) * 0.5) : 1;
    const figure = this.figure().nativeElement;
    figure.style.transform =
      `translate(${x - PET_WIDTH / 2}px, ${y - PET_HEIGHT}px) rotate(${rot}deg) ` +
      `scale(${stretch * grow}, ${squash * grow})`;
    if (this.pointer && (mood === 'idle' || mood === 'fly')) {
      // 眼睛看向指標，最多偏 1.8px，離得近時偏得少。
      const dx = this.pointer.x - x;
      const dy = this.pointer.y - (y - 20);
      const distance = Math.hypot(dx, dy) || 1;
      const reach = Math.min(1.8, distance / 30);
      figure.style.setProperty(
        '--pet-look',
        `${(dx / distance) * reach}px ${(dy / distance) * reach}px`,
      );
    }
    if (this.speech()) {
      const half = this.bubbleWidth / 2 + 8;
      const left = Math.max(half, Math.min(stage.width - half, x));
      this.bubble().nativeElement.style.translate = `${left}px ${y - PET_HEIGHT - 6}px`;
    }
  }

  /** 使用者有動靜：重設睡意並畫下一幀（睡著的會被吵醒）。 */
  private stir() {
    this.lastActivity = performance.now();
    this.kick();
  }

  private track(event: PointerEvent) {
    this.pointer = this.local(event);
    this.stir();
  }

  private grab(event: PointerEvent) {
    if (event.button !== 0) return;
    // 不讓焦點離開密碼欄，觸控時也不產生相容的滑鼠事件。
    event.preventDefault();
    if (this.theme.reducedMotion() || this.unlocked()) {
      this.pat(false);
      return;
    }
    this.figure().nativeElement.setPointerCapture(event.pointerId);
    const point = this.local(event);
    this.holding = true;
    this.body.grab(point.x, point.y);
    this.fling.begin(point.x, point.y, event.timeStamp);
    this.stir();
  }

  private drag(event: PointerEvent) {
    const point = this.local(event);
    if (this.holding) {
      this.fling.move(point.x, point.y, event.timeStamp);
      this.body.drag(point.x, point.y, this.fling.speed);
      return;
    }
    // 觸控沒有滑過，只有拎起來；`pointer` 還是上一次的位置，document 的監聽在這之後才更新。
    if (event.pointerType === 'touch' || !this.pointer) return;
    this.rubbed += Math.hypot(point.x - this.pointer.x, point.y - this.pointer.y);
    if (this.rubbed < RUB_DISTANCE) return;
    this.rubbed = 0;
    this.pat(false);
  }

  private release(event: PointerEvent) {
    if (!this.holding) return;
    this.holding = false;
    // 被系統收走（捲動、切換程式）時座標不可靠：原地放下，不算丟也不算點。
    if (event.type === 'pointercancel') {
      this.body.release({ vx: 0, vy: 0 });
      this.kick();
      return;
    }
    const point = this.local(event);
    if (this.fling.isTap(point.x, point.y, event.timeStamp)) {
      this.body.letGo();
      this.pat(true);
      return;
    }
    this.body.release(this.fling.velocity(event.timeStamp));
    this.kick();
  }

  /** 第一次湊過去時教一下怎麼玩；丟過的人就不再提。 */
  private hint() {
    if (this.farthest || this.theme.reducedMotion()) return;
    if (this.said.has('拎起來丟丟看')) return;
    this.said.add('拎起來丟丟看');
    this.say('拎起來丟丟看');
  }

  /** 摸摸或點一下：開心、冒愛心；點的會跳一下。 */
  private pat(hop: boolean) {
    this.body.happyUntil = performance.now() + HAPPY_MS;
    if (!this.theme.reducedMotion()) {
      if (hop) this.body.hop(HOP.tap);
      this.heart();
    }
    this.stir();
  }

  private cheer() {
    this.body.happyUntil = performance.now() + HAPPY_MS;
    if (!this.theme.reducedMotion()) this.body.hop(HOP.cheer);
    this.stir();
  }

  private stumble() {
    this.body.dizzyUntil = performance.now() + DIZZY_MS;
    if (!this.theme.reducedMotion()) this.body.hop(HOP.stumble);
    this.stir();
  }

  private heart() {
    const { x, y } = this.body;
    const id = ++this.heartId;
    this.hearts.update((hearts) => [
      ...hearts,
      { id, x: x + (Math.random() - 0.5) * 24, y: y - PET_HEIGHT - 4 },
    ]);
  }

  private say(text: string) {
    this.saying.set(text);
    clearTimeout(this.sayTimer);
    this.sayTimer = setTimeout(() => this.saying.set(''), TRANSIENT_HOLD_MS);
    this.kick();
  }

  private local(event: PointerEvent) {
    return { x: event.clientX - this.offset.x, y: event.clientY - this.offset.y };
  }
}
