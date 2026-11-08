import {
  ChangeDetectionStrategy,
  Component,
  DestroyRef,
  computed,
  inject,
  signal,
} from '@angular/core';
import { ServerClock } from '@shared/browser/server-clock';
import { RunState, type RunStateKind } from '@shared/ui/run-state';
import { RunStrip, type RunStripItem } from '@shared/ui/run-strip';
import { TickingTime } from '@shared/ui/ticking-time';
import { TimerRing } from '@shared/ui/timer-ring';
import { DesignExample, DesignSection } from './design-section';

const COUNTDOWN = 8000;
const RUN_TIME = 4000;
const OUTCOMES: RunStateKind[] = [
  'succeeded',
  'succeeded',
  'idle',
  'failed',
  'succeeded',
  'interrupted',
];
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

/** 本機假紀錄：耗時與結果寫死，看起來像一個每幾分鐘跑一次的清理任務。 */
function sampleRuns(now: number): RunStripItem[] {
  const seconds = [3, 2, 41, 3, 2, 2, 95, 3, 4, 2, 3, 260, 2, 3];
  const statuses = ['succeeded', 'idle', 'succeeded', 'idle', 'idle', 'idle', 'failed'];
  return seconds.map((duration, index) => {
    const startedAt = now - (seconds.length - index) * 5 * 60_000;
    return {
      id: `sample-${index}`,
      status: statuses[index % statuses.length],
      startedAt: new Date(startedAt).toISOString(),
      finishedAt: new Date(startedAt + duration * 1000).toISOString(),
    };
  });
}

/** 執行與計時：倒數、圓環、執行狀態與紀錄條，以一個本機模擬的排程任務串起來。 */
@Component({
  selector: 'nx-design-run-timing',
  imports: [DesignExample, DesignSection, RunState, RunStrip, TickingTime, TimerRing],
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
    .design-run-task {
      display: grid;
      grid-template-columns: auto minmax(0, 1fr);
      align-items: center;
      gap: var(--p-space-1) var(--p-space-3);
      width: min(26rem, 100%);
    }
    .design-run-task nx-timer-ring {
      --timer-ring-size: 2.75rem;
      grid-row: span 2;
    }
    .design-run-task strong {
      font-size: var(--text-ui);
    }
    .design-run-task p {
      display: flex;
      align-items: center;
      gap: var(--p-space-2);
      margin: 0;
      color: var(--secondary);
      font-size: var(--text-caption);
    }
    .design-run-strip {
      width: min(16rem, 100%);
    }
    .design-run-clock {
      font-size: var(--p-font-lg);
      font-weight: 600;
    }
  `,
  template: `<nx-design-section
    id="design-run-timing"
    title="執行與計時"
    api="TickingTime · TimerRing · RunState · RunStrip"
    description="排程與背景工作的狀態：倒數與圓環讀同一個伺服器校正的時鐘，同一頁的數字同時跳；狀態改變時才播描繪與長出動效，第一次顯示不播。時間與狀態的文字都另給報讀器。"
  >
    <nx-design-example label="模擬任務">
      <div class="design-run-task">
        <nx-timer-ring
          [mode]="ringMode()"
          [from]="windowFrom()"
          [to]="nextAt()"
          [progress]="progress()"
        >
          <nx-run-state [state]="shownState()" />
        </nx-timer-ring>
        <strong>暫存檔清理</strong>
        <p>
          @switch (phase()) {
            @case ('running') {
              執行中・已經過 <nx-ticking-time [from]="startedAt()" />
            }
            @case ('paused') {
              已暫停
            }
            @default {
              下次 <nx-ticking-time [to]="nextAt()" (reached)="begin()" />
            }
          }
        </p>
      </div>
      <div class="design-run-strip"><nx-run-strip [runs]="runs()" [slots]="18" /></div>
      <span class="design-run-row">
        <button
          type="button"
          class="secondary-button"
          [disabled]="phase() === 'running'"
          (click)="begin()"
        >
          立即執行
        </button>
        <button type="button" class="quiet-button" (click)="togglePause()">
          {{ phase() === 'paused' ? '恢復' : '暫停' }}
        </button>
      </span>
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
    <nx-design-example label="TimerRing">
      <span class="design-run-row">
        <nx-timer-ring [from]="windowFrom()" [to]="nextAt()" />
        <nx-timer-ring mode="running" />
        <nx-timer-ring mode="running" [progress]="0.62" />
        <nx-timer-ring mode="due" />
        <nx-timer-ring mode="paused" />
        <nx-timer-ring mode="off" />
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
  readonly states: RunStateKind[] = [
    'queued',
    'running',
    'succeeded',
    'idle',
    'failed',
    'cancelled',
    'interrupted',
    'paused',
  ];
  readonly stateNames = STATE_NAMES;
  readonly later = new Date(this.clock.now() + 2 * 3_600_000 + 15 * 60_000).toISOString();
  readonly since = new Date(this.clock.now() - 47_000).toISOString();

  readonly phase = signal<'countdown' | 'running' | 'paused'>('countdown');
  readonly windowFrom = signal(new Date(this.clock.now()).toISOString());
  readonly nextAt = signal(new Date(this.clock.now() + COUNTDOWN).toISOString());
  readonly startedAt = signal<string | null>(null);
  readonly progress = signal<number | null>(null);
  readonly runs = signal<RunStripItem[]>(sampleRuns(this.clock.now()));
  private readonly lastState = signal<RunStateKind>('succeeded');
  private turn = 0;
  private timers: ReturnType<typeof setTimeout>[] = [];

  readonly ringMode = computed(() => (this.phase() === 'countdown' ? 'countdown' : this.phase()));
  readonly shownState = computed<RunStateKind>(() =>
    this.phase() === 'running'
      ? 'running'
      : this.phase() === 'paused'
        ? 'paused'
        : this.lastState(),
  );

  constructor() {
    inject(DestroyRef).onDestroy(() => this.clear());
  }

  begin() {
    if (this.phase() === 'running') return;
    this.clear();
    const startedAt = this.clock.now();
    const id = `demo-${startedAt}`;
    this.phase.set('running');
    this.startedAt.set(new Date(startedAt).toISOString());
    this.progress.set(null);
    this.runs.update((runs) => [
      ...runs,
      { id, status: 'running', startedAt: new Date(startedAt).toISOString(), finishedAt: null },
    ]);
    // 先繞行一下（還不知道有多少要做），再以進度填滿。
    for (const [delay, value] of [
      [1200, 0.25],
      [2000, 0.55],
      [2800, 0.8],
      [3500, 1],
    ] as const)
      this.timers.push(setTimeout(() => this.progress.set(value), delay));
    this.timers.push(setTimeout(() => this.finish(id), RUN_TIME));
  }

  togglePause() {
    if (this.phase() === 'paused') {
      this.schedule();
      return;
    }
    if (this.phase() === 'running') return;
    this.clear();
    this.phase.set('paused');
  }

  private finish(id: string) {
    const outcome = OUTCOMES[this.turn++ % OUTCOMES.length];
    const finishedAt = new Date(this.clock.now()).toISOString();
    this.runs.update((runs) =>
      runs.map((run) => (run.id === id ? { ...run, status: outcome, finishedAt } : run)),
    );
    this.lastState.set(outcome);
    this.schedule();
  }

  private schedule() {
    const now = this.clock.now();
    this.phase.set('countdown');
    this.windowFrom.set(new Date(now).toISOString());
    this.nextAt.set(new Date(now + COUNTDOWN).toISOString());
  }

  private clear() {
    for (const timer of this.timers) clearTimeout(timer);
    this.timers = [];
  }
}
