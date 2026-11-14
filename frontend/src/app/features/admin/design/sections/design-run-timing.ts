import { ChangeDetectionStrategy, Component, computed, inject } from '@angular/core';
import { ServerClock } from '@shared/browser/server-clock';
import { RunState, type RunStateKind } from '@shared/ui/run-state';
import { TickingTime } from '@shared/ui/ticking-time';
import { TimeLane, type TimeMark, type TimeMarkKind } from '@shared/ui/time-lane';
import { TimeLanes } from '@shared/ui/time-lanes';
import { DesignExample, DesignSection } from './design-section';

const MINUTE = 60_000;
const HOUR = 60 * MINUTE;
const STATE_NAMES: Record<RunStateKind, string> = {
  queued: '排隊中',
  running: '執行中',
  succeeded: '成功',
  idle: '沒有事做',
  failed: '失敗',
  cancelled: '已停止',
  interrupted: '中斷',
  paused: '暫停',
};

/** 往前往後每 `step` 一個記號；`past` 依序循環當作結果。 */
function rhythm(now: number, from: number, to: number, step: number, past: TimeMarkKind[]) {
  const marks: TimeMark[] = [];
  let index = 0;
  for (let at = from + step; at < now; at += step)
    marks.push({ at, kind: past[index++ % past.length] });
  for (let at = now + step; at <= to; at += step) marks.push({ at, kind: 'planned' });
  return marks;
}

/** 執行與計時：泳道時間軸、執行狀態與計時，都讀同一個伺服器校正的時鐘。資料是本機假資料。 */
@Component({
  selector: 'nx-design-run-timing',
  imports: [DesignExample, DesignSection, RunState, TickingTime, TimeLane, TimeLanes],
  changeDetection: ChangeDetectionStrategy.OnPush,
  styles: `
    :host {
      display: contents;
    }
    .design-run-row {
      display: flex;
      flex-wrap: wrap;
      align-items: center;
      gap: var(--p-space-2) var(--p-space-3);
    }
    .design-run-clock {
      font-size: var(--p-font-lg);
      font-weight: 600;
    }
    nx-time-lanes {
      --time-lane-label: 9rem;
      --time-lane-end: 0px;
      width: 100%;
    }
    [lane-label] {
      font-size: var(--text-ui);
    }
  `,
  template: `<nx-design-section
    id="design-run-timing"
    title="執行與計時"
    api="TimeLanes · TimeLane · RunState · TickingTime"
    [stacked]="true"
    description="排程與背景工作的狀態。泳道把過去的結果與未來的預定放在同一條時間上，刻度、底色與現在線只畫一份。滑過記號看詳情，軌道另給報讀器一句摘要。"
  >
    <nx-design-example label="TimeLanes">
      <nx-time-lanes
        label="範例排程的時間軸"
        heading="任務"
        [from]="range().from"
        [to]="range().to"
        [shades]="range().shades"
      >
        <div role="list">
          @for (lane of lanes(); track lane.name) {
            <nx-time-lane [marks]="lane.marks" [summary]="lane.summary" [note]="lane.note">
              <span lane-label>{{ lane.name }}</span>
            </nx-time-lane>
          }
        </div>
      </nx-time-lanes>
    </nx-design-example>
    <nx-design-example label="RunState">
      <span class="design-run-row">
        @for (state of states; track state) {
          <span class="design-run-row"
            ><nx-run-state [state]="state" />{{ stateNames[state] }}</span
          >
        }
      </span>
    </nx-design-example>
    <nx-design-example label="TickingTime">
      <span class="design-run-row">
        <span class="design-run-clock"><nx-ticking-time [to]="later" /></span>
        <span class="design-run-clock"><nx-ticking-time [from]="since" /></span>
        <nx-ticking-time unknown="未排定" />
      </span>
    </nx-design-example>
  </nx-design-section>`,
})
export class DesignRunTiming {
  private readonly clock = inject(ServerClock);
  private readonly started = this.clock.now();
  readonly states = Object.keys(STATE_NAMES) as RunStateKind[];
  readonly stateNames = STATE_NAMES;
  readonly later = new Date(this.started + 2 * HOUR + 15 * MINUTE).toISOString();
  readonly since = new Date(this.started - 47_000).toISOString();

  readonly range = computed(() => {
    const from = Math.floor(this.started / HOUR) * HOUR - 4 * HOUR;
    const to = from + 24 * HOUR;
    return { from, to, shades: [{ from: from + 9 * HOUR, to: from + 15 * HOUR }] };
  });
  readonly lanes = computed(() => {
    const { from, to } = this.range();
    const now = this.started;
    return [
      {
        name: '暫存檔清理',
        summary: '每 10 分鐘一輪，大多沒有事做，一輪失敗',
        note: '',
        marks: rhythm(now, from, to, 10 * MINUTE, ['neutral', 'neutral', 'success', 'danger']),
      },
      {
        name: '報表彙整',
        summary: '每 3 小時一輪，目前執行中',
        note: '',
        marks: [
          ...rhythm(now - 2 * MINUTE, from, to, 3 * HOUR, ['success']).filter(
            (mark) => mark.kind !== 'planned',
          ),
          { at: now - 2 * MINUTE, kind: 'active', title: '執行中' },
          ...rhythm(now, from, to, 3 * HOUR, []).filter((mark) => mark.kind === 'planned'),
        ] as TimeMark[],
      },
      {
        name: '索引重建',
        summary: '每天一次，下次 03:15',
        note: '',
        marks: [
          { at: from + HOUR, kind: 'success', title: '昨天的一輪・成功' },
          { at: from + 11 * HOUR + 15 * MINUTE, kind: 'planned', label: '03:15' },
        ] as TimeMark[],
      },
      {
        name: '資料同步',
        summary: '已暫停，區間內沒有預定',
        note: '暫停中，不依排程執行',
        marks: [] as TimeMark[],
      },
    ];
  });
}
