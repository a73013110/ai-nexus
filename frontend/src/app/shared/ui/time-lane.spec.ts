import { Component, signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { afterEach, describe, expect, it } from 'vitest';
import { TimeLane, lanePaths, nearestMark, type TimeMark } from './time-lane';
import { TimeLanes } from './time-lanes';

const FROM = Date.parse('2026-11-08T00:00:00Z');
const SPAN = 100_000;
const TO = FROM + SPAN;
const at = (fraction: number) => FROM + SPAN * fraction;

describe('lanePaths', () => {
  it('draws one path per kind, most important last', () => {
    const paths = lanePaths(
      [
        { at: at(0.5), kind: 'danger' },
        { at: at(0.1), kind: 'success' },
        { at: at(0.2), kind: 'success' },
      ],
      FROM,
      TO,
    );
    expect(paths.map((path) => path.kind)).toEqual(['success', 'danger']);
    expect(paths[0].d).toBe('M100 11V29M200 11V29');
  });

  it('merges dense quiet marks into a band but never hides results', () => {
    const dense = [0.1, 0.104, 0.108].map((value): TimeMark => ({
      at: at(value),
      kind: 'planned',
    }));
    const lone: TimeMark = { at: at(0.5), kind: 'planned' };
    const results = [0.2, 0.204].map((value): TimeMark => ({ at: at(value), kind: 'danger' }));
    const paths = lanePaths([...dense, ...results, lone], FROM, TO);
    expect(paths.find((path) => path.kind === 'planned-band')?.d).toBe('M100 20H108');
    expect(paths.find((path) => path.kind === 'planned')?.d).toBe('M500 16V24');
    expect(paths.find((path) => path.kind === 'danger')?.d.match(/M/g)).toHaveLength(2);
  });

  it('skips marks outside the range and marks drawn as labelled flags', () => {
    const paths = lanePaths(
      [
        { at: FROM - 1, kind: 'success' },
        { at: TO + 1, kind: 'success' },
        { at: at(0.5), kind: 'planned', label: '03:15' },
      ],
      FROM,
      TO,
    );
    expect(paths).toEqual([]);
  });
});

describe('nearestMark', () => {
  const marks: TimeMark[] = [100, 200, 400].map((value) => ({ at: value, kind: 'success' }));

  it('finds the closest mark on either side', () => {
    expect(nearestMark(marks, 190, 50)?.at).toBe(200);
    expect(nearestMark(marks, 310, 200)?.at).toBe(400);
    expect(nearestMark(marks, 1000, 700)?.at).toBe(400);
  });

  it('finds nothing beyond the reach or in an empty lane', () => {
    expect(nearestMark(marks, 300, 50)).toBeNull();
    expect(nearestMark([], 300, 50)).toBeNull();
  });
});

@Component({
  imports: [TimeLane, TimeLanes],
  template: `<nx-time-lanes label="時間軸" [from]="from" [to]="to">
    <nx-time-lane [marks]="marks()" summary="區間內 1 輪" note="暫停中">
      <span lane-label>任務甲</span>
      <button lane-end type="button">執行</button>
    </nx-time-lane>
  </nx-time-lanes>`,
})
class Host {
  readonly from = FROM;
  readonly to = TO;
  readonly marks = signal<TimeMark[]>([
    { at: at(0.5), kind: 'success', title: '成功・5 秒' },
    { at: at(0.9), kind: 'planned', label: '15:00' },
  ]);
}

describe('TimeLane', () => {
  afterEach(() => TestBed.resetTestingModule());

  function render() {
    const fixture = TestBed.createComponent(Host);
    fixture.detectChanges();
    return { fixture, host: fixture.nativeElement as HTMLElement };
  }

  it('projects the label and actions and describes the track as one image', () => {
    const { host } = render();
    expect(host.querySelector('.lane-label')?.textContent).toContain('任務甲');
    expect(host.querySelector('.lane-end button')).not.toBeNull();
    const track = host.querySelector('.lane-track');
    expect(track?.getAttribute('role')).toBe('img');
    expect(track?.getAttribute('aria-label')).toBe('區間內 1 輪');
    expect(host.querySelector('.lane-note')?.textContent).toBe('暫停中');
    expect(host.querySelector('.lane-flag')?.textContent).toBe('15:00');
    expect(host.querySelector('nx-time-lane')?.getAttribute('role')).toBe('listitem');
  });

  it('shows one shared tip for the mark under the pointer', () => {
    const { fixture, host } = render();
    const track = host.querySelector<HTMLElement>('.lane-track')!;
    track.getBoundingClientRect = () => ({ left: 0, top: 0, width: 1000 }) as DOMRect;
    track.dispatchEvent(new PointerEvent('pointermove', { clientX: 503 }));
    fixture.detectChanges();
    expect(host.querySelector('.time-tip')?.textContent).toBe('成功・5 秒');

    track.dispatchEvent(new PointerEvent('pointermove', { clientX: 700 }));
    fixture.detectChanges();
    expect(host.querySelector('.time-tip')).toBeNull();
  });
});
