/**
 * 鎖定畫面小鎖的物理與情緒：純計算，不碰 DOM，元件每幀把量到的版面交進來、把結果畫出去。
 * 座標是鎖定層的 px，小鎖的位置以腳底中心為準，站上平台就是對齊卡片頂緣。
 */

/** 小鎖的盒子（px），與 SVG 的 viewBox 相同；樣式表的寬高要跟著改。 */
export const PET_WIDTH = 56;
export const PET_HEIGHT = 60;
/** 沒有使用者動靜這麼久，站在卡片上的小鎖就睡著；睡著時元件停掉逐幀更新。 */
export const SLEEP_AFTER_MS = 6000;

const GRAVITY = 1900;
/** 60px 算 1 公尺：甩過半個筆電螢幕大約 10 公尺，數字讀起來像真的。 */
const PX_PER_METER = 60;
const MAX_THROW = 2600;
/** 放開時速度低於這個值是放下，不算丟，也不報距離。 */
const MIN_THROW = 250;
/** 放開前停住超過這麼久就是輕輕放下。 */
const STILL_MS = 90;
const TAP_DISTANCE = 6;
const TAP_MS = 280;
/** 落到卡片頂緣兩端這麼近的地方算沒踩到，會掉下去。 */
const LANDING_EDGE = 14;
/** 站在卡片上時離兩端至少這麼遠，免得一半懸空。 */
const PERCH_INSET = 24;
const WANDER_INSET = 30;
const WALK_SPEED = 42;
/** 地面離視窗底緣的距離。 */
const FLOOR_GAP = 4;
const WALL_BOUNCE = 0.6;
/** 落地速度高於這個值會彈起來，否則直接站住。 */
const BOUNCE_SPEED = 320;
/** 落地速度高於這個值會摔暈。 */
const HARD_LANDING = 1100;
const DIZZY_MS = 1500;
/** 摔在地上先愣一下，再跳回卡片。 */
const GROUND_PAUSE_MS = 900;
/** 跳回卡片時的拋物線頂點高出卡片頂緣多少。 */
const HOP_BACK_APEX = 90;
/** 進場時從卡片頂緣上方多高落下。 */
const ENTRANCE_DROP = 180;

const clamp = (value: number, min: number, max: number) => Math.max(min, Math.min(max, value));
/** 轉角收回 ±180° 內，落地後回正時走近的那一邊，不會倒轉好幾圈。 */
const normalizeAngle = (degrees: number) => ((((degrees + 180) % 360) + 360) % 360) - 180;
/** 落地越重壓得越扁。 */
const impactSquash = (speed: number) => clamp(1 - speed / 2600, 0.62, 0.95);

export interface PetStage {
  width: number;
  height: number;
  /** 卡片頂緣是單向平台，只有從上方落下才站得住。 */
  card: { left: number; right: number; top: number };
}

export type PetMode = 'perch' | 'air' | 'ground' | 'held';
/**
 * 表情。`turned` 是密碼顯示出來時轉身背對；`sign` 是大寫鎖定開著時舉牌（閉著眼，照樣不看密碼）；
 * `stretch`／`yawn` 是依時段做的一次性動作。
 */
export type PetMood =
  'idle' | 'fly' | 'dizzy' | 'turned' | 'sign' | 'shy' | 'stretch' | 'yawn' | 'happy' | 'sleep';
/** 一次性動作：早上醒來伸懶腰、晚上醒來打哈欠。 */
export type PetAction = Extract<PetMood, 'stretch' | 'yawn'>;

export interface PetLanding {
  on: 'perch' | 'ground';
  /** 被丟出去才有飛行距離（公尺，取一位小數）。 */
  meters: number | null;
}

/** 卡片上方放得下小鎖、卡片也夠寬：矮視窗或彈出螢幕鍵盤時牠會被切到，先躲起來。 */
export function hasRoom(stage: PetStage) {
  return stage.card.top >= PET_HEIGHT + 8 && stage.card.right - stage.card.left >= PERCH_INSET * 4;
}

export class PetBody {
  x = 0;
  y = 0;
  /** 角度（deg）。 */
  rot = 0;
  /** 垂直方向的壓扁比例；水平方向由畫的一端依此撐開。 */
  squash = 1;
  mode: PetMode = 'air';
  dizzyUntil = 0;
  happyUntil = 0;
  private vx = 0;
  private vy = 0;
  private vr = 0;
  private squashSpeed = 0;
  private walkTo: number | null = null;
  private nextWander = 0;
  private groundAt = 0;
  private hard = false;
  /** 被丟出去之後累計的路徑長度；`null` 表示這趟不是丟的。 */
  private flight: number | null = null;
  private placed = false;
  private grip = { x: 0, y: 0 };
  private target = { x: 0, y: 0 };
  private tilt = 0;
  private heldFrom: PetMode = 'perch';

  constructor(private readonly random: () => number = Math.random) {}

  /** 正被丟在空中：表情睜大眼。 */
  get thrown() {
    return this.mode === 'air' && this.flight !== null;
  }

  /** 站好不動、也沒有回彈：睡著時可以停掉逐幀更新。 */
  get resting() {
    return (
      this.mode === 'perch' &&
      this.walkTo === null &&
      Math.abs(this.rot) < 0.05 &&
      Math.abs(1 - this.squash) < 0.001 &&
      Math.abs(this.squashSpeed) < 0.01
    );
  }

  /**
   * 落下進場：預設從卡片上方，空間不夠躲起來之後再出現也一樣；`from` 是腳底的起點，
   * 從倒數島掉下來時就是牠掛在島下的位置。
   */
  drop(stage: PetStage, now: number, from?: { x: number; y: number }) {
    const { card } = stage;
    this.x = from?.x ?? card.left + (card.right - card.left) * 0.74;
    this.y = Math.max(PET_HEIGHT, from?.y ?? card.top - ENTRANCE_DROP);
    this.vx = this.vy = this.vr = this.rot = 0;
    this.flight = null;
    this.mode = 'air';
    this.placed = true;
    this.nextWander = now + 2500;
  }

  /** 直接站在卡片上、沒有物理：減少動態時用。 */
  rest(stage: PetStage) {
    const { card } = stage;
    if (!this.placed) this.x = card.left + (card.right - card.left) * 0.74;
    this.placed = true;
    this.x = clamp(this.x, card.left + PERCH_INSET, card.right - PERCH_INSET);
    this.y = card.top;
    this.mode = 'perch';
    this.vx = this.vy = this.vr = this.rot = this.squashSpeed = 0;
    this.squash = 1;
    this.walkTo = null;
    this.flight = null;
  }

  /** 原地跳一下；只有站著時跳得起來。 */
  hop(speed: number) {
    if (this.mode !== 'perch' && this.mode !== 'ground') return;
    this.launch(0, -speed, 0);
    this.squeeze(0.8);
  }

  /** 被拎起來；`x`、`y` 是指標位置，之後的 `drag` 以同一個握點跟著走。 */
  grab(x: number, y: number) {
    this.heldFrom = this.mode;
    this.mode = 'held';
    this.grip = { x: x - this.x, y: y - this.y };
    this.target = { x: this.x, y: this.y };
    this.vx = this.vy = this.vr = this.tilt = 0;
    this.walkTo = null;
    this.flight = null;
  }

  /** `speed` 是水平拖曳速度（px/s），讓牠往拖的方向歪。 */
  drag(x: number, y: number, speed: number) {
    this.target = { x: x - this.grip.x, y: y - this.grip.y };
    this.tilt = clamp(speed * 0.02, -35, 35);
  }

  /** 只是點一下：放回原本的狀態。 */
  letGo() {
    if (this.mode === 'held') this.mode = this.heldFrom;
  }

  /** 放開：夠快就是丟出去，之後落地時回報飛了多遠。 */
  release(velocity: { vx: number; vy: number }) {
    if (this.mode !== 'held') return;
    this.launch(velocity.vx, velocity.vy, velocity.vx * 0.5);
    if (Math.hypot(velocity.vx, velocity.vy) > MIN_THROW) this.flight = 0;
  }

  /** 前進 `dt` 秒。`wander` 為否時站著不亂走（睡著、遮著眼睛）。落地的那一幀回傳落點。 */
  step(dt: number, now: number, stage: PetStage, wander: boolean): PetLanding | null {
    let landing: PetLanding | null = null;
    if (this.mode === 'held') this.follow(dt);
    else if (this.mode === 'air') landing = this.fly(dt, now, stage);
    else if (this.mode === 'perch') this.stand(dt, now, stage, wander);
    else this.sit(dt, now, stage);
    // 壓扁是阻尼彈簧，落地後抖兩下回到原形。
    this.squashSpeed += ((1 - this.squash) * 380 - this.squashSpeed * 16) * dt;
    this.squash += this.squashSpeed * dt;
    return landing;
  }

  private follow(dt: number) {
    const pull = Math.min(1, dt * 30);
    this.x += (this.target.x - this.x) * pull;
    this.y += (this.target.y - this.y) * pull;
    this.rot += (this.tilt - this.rot) * Math.min(1, dt * 10);
  }

  private fly(dt: number, now: number, stage: PetStage): PetLanding | null {
    const { card } = stage;
    const half = PET_WIDTH / 2;
    const floor = stage.height - FLOOR_GAP;
    const fromX = this.x;
    const fromY = this.y;
    this.vy += GRAVITY * dt;
    this.x += this.vx * dt;
    this.y += this.vy * dt;
    this.rot += this.vr * dt;
    if (this.flight !== null) this.flight += Math.hypot(this.x - fromX, this.y - fromY);
    if (this.x < half) {
      this.x = half;
      this.bounceOffWall(1);
    } else if (this.x > stage.width - half) {
      this.x = stage.width - half;
      this.bounceOffWall(-1);
    }
    if (this.y < PET_HEIGHT) {
      this.y = PET_HEIGHT;
      this.vy = Math.abs(this.vy) * 0.4;
    }
    const overCard = this.x > card.left + LANDING_EDGE && this.x < card.right - LANDING_EDGE;
    if (this.vy > 0 && fromY <= card.top && this.y >= card.top && overCard)
      return this.land('perch', card.top, now);
    if (this.y < floor) return null;
    if (this.vy > HARD_LANDING) this.hard = true;
    if (this.vy <= BOUNCE_SPEED) return this.land('ground', floor, now);
    this.squeeze(impactSquash(this.vy));
    this.y = floor;
    this.vy *= -0.42;
    this.vx *= 0.7;
    this.vr *= 0.5;
    return null;
  }

  private bounceOffWall(direction: 1 | -1) {
    this.vx = direction * Math.abs(this.vx) * WALL_BOUNCE;
    this.vr *= -WALL_BOUNCE;
    this.squeeze(0.86);
  }

  private land(on: PetLanding['on'], y: number, now: number): PetLanding {
    this.squeeze(impactSquash(this.vy));
    this.mode = on;
    this.y = y;
    this.vx = this.vy = this.vr = 0;
    this.rot = normalizeAngle(this.rot);
    if (on === 'ground') {
      this.groundAt = now;
      if (this.hard) this.dizzyUntil = now + DIZZY_MS;
    }
    this.hard = false;
    this.nextWander = now + 2000;
    const meters = this.flight === null ? null : Math.round((this.flight / PX_PER_METER) * 10) / 10;
    this.flight = null;
    return { on, meters };
  }

  private stand(dt: number, now: number, stage: PetStage, wander: boolean) {
    const { card } = stage;
    // 卡片會跟著視窗、捲動與螢幕鍵盤移動，每幀重新站上頂緣。
    this.y = card.top;
    this.x = clamp(this.x, card.left + PERCH_INSET, card.right - PERCH_INSET);
    if (!wander) this.walkTo = null;
    else if (this.walkTo === null && now > this.nextWander)
      this.walkTo =
        card.left + WANDER_INSET + this.random() * (card.right - card.left - WANDER_INSET * 2);
    if (this.walkTo === null) {
      this.rot -= this.rot * Math.min(1, dt * 12);
      if (Math.abs(this.rot) < 0.05) this.rot = 0;
      return;
    }
    const distance = this.walkTo - this.x;
    this.x += Math.sign(distance) * Math.min(Math.abs(distance), WALK_SPEED * dt);
    this.rot = Math.sin(now / 85) * 7;
    if (Math.abs(distance) < 1) {
      this.walkTo = null;
      this.nextWander = now + 3000 + this.random() * 4000;
    }
  }

  private sit(dt: number, now: number, stage: PetStage) {
    this.y = stage.height - FLOOR_GAP;
    this.rot -= this.rot * Math.min(1, dt * 12);
    if (now > this.dizzyUntil && now - this.groundAt > GROUND_PAUSE_MS) this.hopBack(stage);
  }

  /** 從地上翻一圈跳回卡片：先定頂點再反推初速，落點剛好是卡片頂緣、而且是往下落。 */
  private hopBack(stage: PetStage) {
    const { card } = stage;
    // 卡片貼近頂端時頂點跟著壓低，但不能是負的（開根號會變 NaN）。
    const apex = Math.max(8, Math.min(HOP_BACK_APEX, card.top - PET_HEIGHT));
    const rise = Math.max(0, this.y - card.top) + apex;
    const up = Math.sqrt(2 * GRAVITY * rise);
    const time = up / GRAVITY + Math.sqrt((2 * apex) / GRAVITY);
    const to = card.left + (card.right - card.left) * (0.2 + this.random() * 0.6);
    this.launch((to - this.x) / time, -up, ((to > this.x ? 1 : -1) * 360) / time);
    this.squeeze(0.78);
  }

  private launch(vx: number, vy: number, vr: number) {
    this.vx = vx;
    this.vy = vy;
    this.vr = vr;
    this.mode = 'air';
    this.walkTo = null;
  }

  private squeeze(value: number) {
    this.squash = value;
    this.squashSpeed = 0;
  }
}

export interface PetMoodState {
  /** 密碼欄有字。 */
  shy: boolean;
  /** 密碼顯示出來了。 */
  turned?: boolean;
  /** 大寫鎖定開著。 */
  sign?: boolean;
  asleep: boolean;
  action?: PetAction | null;
}

/**
 * 誰的表情優先：被拎著或丟出去最優先，其次摔暈；站在卡片上時，和密碼有關的（背對、舉牌、遮眼）
 * 壓過時段動作與開心，最後才是睡著。
 */
export function petMood(body: PetBody, now: number, state: PetMoodState): PetMood {
  const standing = body.mode === 'perch';
  if (body.mode === 'held' || body.thrown) return 'fly';
  if (now < body.dizzyUntil) return 'dizzy';
  if (standing && state.turned) return 'turned';
  if (standing && state.sign) return 'sign';
  if (standing && state.shy) return 'shy';
  if (state.action) return state.action;
  if (now < body.happyUntil) return 'happy';
  if (state.asleep && standing) return 'sleep';
  return 'idle';
}

/** 放開時的甩動速度（px/s）：取最近幾次移動的平滑值，放開前停住就是輕輕放下。 */
export class Fling {
  private start = { x: 0, y: 0, time: 0 };
  private last = { x: 0, y: 0, time: 0 };
  private vx = 0;
  private vy = 0;

  /** 時間用事件的 `timeStamp`（ms）。 */
  begin(x: number, y: number, time: number) {
    this.start = this.last = { x, y, time };
    this.vx = this.vy = 0;
  }

  move(x: number, y: number, time: number) {
    const elapsed = Math.max(4, time - this.last.time);
    this.vx = this.vx * 0.5 + ((x - this.last.x) / elapsed) * 500;
    this.vy = this.vy * 0.5 + ((y - this.last.y) / elapsed) * 500;
    this.last = { x, y, time };
  }

  /** 目前的水平速度，拖著時讓小鎖往那邊歪。 */
  get speed() {
    return this.vx;
  }

  /** 沒怎麼動又很快放開：是點一下，不是拖。 */
  isTap(x: number, y: number, time: number) {
    return (
      Math.hypot(x - this.start.x, y - this.start.y) < TAP_DISTANCE &&
      time - this.start.time < TAP_MS
    );
  }

  velocity(time: number) {
    if (time - this.last.time > STILL_MS) return { vx: 0, vy: 0 };
    return { vx: clamp(this.vx, -MAX_THROW, MAX_THROW), vy: clamp(this.vy, -MAX_THROW, MAX_THROW) };
  }
}
