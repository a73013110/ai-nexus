import { describe, expect, it } from 'vitest';
import {
  Fling,
  PET_HEIGHT,
  PetBody,
  hasRoom,
  petMood,
  type PetLanding,
  type PetStage,
} from './lock-pet-physics';

// 1280×768 的視窗，24rem 的鎖定卡置中。
const stage: PetStage = {
  width: 1280,
  height: 768,
  card: { left: 448, right: 832, top: 200 },
};
const STEP = 1 / 60;

/** 以 60fps 跑 `seconds` 秒，收集落點。 */
function run(body: PetBody, seconds: number, start = 0, wander = false, on = stage) {
  const landings: PetLanding[] = [];
  let now = start;
  for (let frame = 0; frame < seconds * 60; frame++) {
    now += STEP * 1000;
    const landing = body.step(STEP, now, on, wander);
    if (landing) landings.push(landing);
  }
  return { landings, now };
}

function untilLanding(body: PetBody) {
  let now = 0;
  for (let frame = 0; frame < 600; frame++) {
    now += STEP * 1000;
    const landing = body.step(STEP, now, stage, false);
    if (landing) return { landing, now };
  }
  throw new Error('沒有落地');
}

/** `random` 固定，閒晃與跳回卡片的落點可預期。 */
function perched() {
  const body = new PetBody(() => 0.5);
  body.drop(stage, 0);
  run(body, 1);
  return body;
}

/** 拎到卡片旁邊的空中，等牠跟上指標。 */
function heldBeside(body: PetBody, x: number, y: number) {
  body.grab(body.x, body.y);
  body.drag(x, y, 0);
  run(body, 0.5);
}

describe('PetBody', () => {
  it('drops onto the card top and comes to rest there', () => {
    const body = new PetBody(() => 0.5);
    body.drop(stage, 0);
    const { landings } = run(body, 2);
    expect(landings).toEqual([{ on: 'perch', meters: null }]);
    expect(body.mode).toBe('perch');
    expect(body.y).toBe(stage.card.top);
    expect(body.x).toBeGreaterThan(stage.card.left);
    expect(body.x).toBeLessThan(stage.card.right);
    expect(body.resting).toBe(true);
  });

  it('hops straight up and lands back on the same spot', () => {
    const body = perched();
    const x = body.x;
    body.hop(420);
    expect(body.mode).toBe('air');
    expect(run(body, 1).landings).toEqual([{ on: 'perch', meters: null }]);
    expect(body.x).toBe(x);
  });

  it('starts the entrance from a given point, as when falling from the countdown island', () => {
    const body = new PetBody(() => 0.5);
    body.drop(stage, 0, { x: 700, y: 90 });
    expect([body.x, body.y]).toEqual([700, 90]);
    expect(untilLanding(body).landing).toEqual({ on: 'perch', meters: null });
    expect([body.x, body.y]).toEqual([700, stage.card.top]);
  });

  it('only hops while standing', () => {
    const tall = { ...stage, card: { ...stage.card, top: 400 } };
    const body = new PetBody();
    body.drop(tall, 0);
    const start = body.y;
    body.hop(420);
    run(body, 0.05, 0, false, tall);
    expect(body.y).toBeGreaterThan(start);
  });

  it('reports how far a throw flew, then hops back onto the card by itself', () => {
    const body = perched();
    heldBeside(body, 900, 150);
    body.release({ vx: 1500, vy: 200 });
    expect(body.thrown).toBe(true);
    const { landings } = run(body, 6);
    const thrown = landings.find((landing) => landing.meters !== null)!;
    expect(thrown.on).toBe('ground');
    expect(thrown.meters).toBeGreaterThan(5);
    // 摔在地上之後自己跳回去，落點在卡片頂緣上。
    expect(landings.at(-1)).toEqual({ on: 'perch', meters: null });
    expect(body.mode).toBe('perch');
    expect(body.x).toBeGreaterThan(stage.card.left);
    expect(body.x).toBeLessThan(stage.card.right);
  });

  it('stands back on the card when let go just above it', () => {
    const body = perched();
    heldBeside(body, 640, 150);
    body.release({ vx: 0, vy: 0 });
    expect(untilLanding(body).landing).toEqual({ on: 'perch', meters: null });
  });

  it('does not count a gentle release as a throw', () => {
    const body = perched();
    heldBeside(body, 1000, 600);
    body.release({ vx: 100, vy: 0 });
    expect(body.thrown).toBe(false);
    expect(untilLanding(body).landing).toEqual({ on: 'ground', meters: null });
  });

  it('treats the card top as one way: rising from below passes through it', () => {
    const body = perched();
    heldBeside(body, stage.card.left + 100, stage.height - 10);
    body.release({ vx: 0, vy: -1600 });
    let crossed = false;
    let now = 0;
    for (let frame = 0; frame < 60 && !crossed; frame++) {
      now += STEP * 1000;
      body.step(STEP, now, stage, false);
      crossed = body.mode === 'air' && body.y < stage.card.top;
    }
    expect(crossed).toBe(true);
  });

  it('gets dizzy after a hard landing beside the card', () => {
    const body = perched();
    heldBeside(body, 1000, 300);
    body.release({ vx: 0, vy: 2600 });
    const { landing, now } = untilLanding(body);
    expect(landing.on).toBe('ground');
    expect(petMood(body, now, { shy: false, asleep: false })).toBe('dizzy');
  });

  it('stands still on the card without physics when motion is reduced', () => {
    const body = new PetBody();
    body.drop(stage, 0);
    body.rest(stage);
    expect(body.mode).toBe('perch');
    expect(body.y).toBe(stage.card.top);
    expect(body.resting).toBe(true);
  });

  it('wanders along the card while awake and stays on it', () => {
    const body = perched();
    const x = body.x;
    run(body, 5, 1000, true);
    expect(body.x).not.toBe(x);
    expect(body.x).toBeGreaterThanOrEqual(stage.card.left + 24);
    expect(body.x).toBeLessThanOrEqual(stage.card.right - 24);
  });
});

describe('petMood', () => {
  it('ranks being held over dizzy, shy, happy and asleep', () => {
    const body = perched();
    body.dizzyUntil = 100;
    body.happyUntil = 100;
    expect(petMood(body, 50, { shy: true, asleep: true })).toBe('dizzy');
    expect(petMood(body, 150, { shy: true, asleep: true })).toBe('shy');
    body.happyUntil = 200;
    expect(petMood(body, 150, { shy: false, asleep: true })).toBe('happy');
    expect(petMood(body, 250, { shy: false, asleep: true })).toBe('sleep');
    expect(petMood(body, 250, { shy: false, asleep: false })).toBe('idle');
    body.grab(body.x, body.y);
    expect(petMood(body, 50, { shy: true, asleep: true })).toBe('fly');
  });

  it('turns its back over holding up the sign or covering its eyes, and only on the card', () => {
    const body = perched();
    const all = { shy: true, turned: true, sign: true, asleep: true, action: 'yawn' as const };
    expect(petMood(body, 0, all)).toBe('turned');
    expect(petMood(body, 0, { ...all, turned: false })).toBe('sign');
    expect(petMood(body, 0, { ...all, turned: false, sign: false })).toBe('shy');
    expect(petMood(body, 0, { ...all, turned: false, sign: false, shy: false })).toBe('yawn');
    body.hop(420);
    expect(petMood(body, 0, all)).toBe('yawn');
  });

  it('lets a time-of-day action win over a happy face', () => {
    const body = perched();
    body.happyUntil = 100;
    expect(petMood(body, 50, { shy: false, asleep: false, action: 'stretch' })).toBe('stretch');
  });

  it('neither covers its eyes nor sleeps away from the card', () => {
    const body = new PetBody();
    body.drop(stage, 0);
    expect(petMood(body, 0, { shy: true, asleep: true })).toBe('idle');
  });
});

describe('Fling', () => {
  it('measures the release speed and drops it when the pointer stopped first', () => {
    const fling = new Fling();
    fling.begin(0, 0, 0);
    fling.move(20, 0, 10);
    fling.move(40, -10, 20);
    expect(fling.velocity(25)).toEqual({ vx: 1500, vy: -500 });
    expect(fling.velocity(200)).toEqual({ vx: 0, vy: 0 });
  });

  it('caps a wild flick', () => {
    const fling = new Fling();
    fling.begin(0, 0, 0);
    fling.move(400, 0, 4);
    fling.move(800, 0, 8);
    expect(fling.velocity(8).vx).toBe(2600);
  });

  it('tells a tap from a drag', () => {
    const fling = new Fling();
    fling.begin(10, 10, 0);
    expect(fling.isTap(12, 11, 120)).toBe(true);
    expect(fling.isTap(30, 10, 120)).toBe(false);
    expect(fling.isTap(10, 10, 400)).toBe(false);
  });
});

describe('hasRoom', () => {
  it('hides the pet when the card sits too close to the top or is too narrow', () => {
    expect(hasRoom(stage)).toBe(true);
    expect(hasRoom({ ...stage, card: { ...stage.card, top: PET_HEIGHT } })).toBe(false);
    expect(hasRoom({ ...stage, card: { left: 0, right: 80, top: 200 } })).toBe(false);
  });
});
