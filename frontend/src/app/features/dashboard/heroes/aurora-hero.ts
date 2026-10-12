import { ChangeDetectionStrategy, Component, computed, inject } from '@angular/core';
import { RouterLink } from '@angular/router';
import { Icon } from '../../../shared/ui/icon';
import { NumberTicker } from '../../../shared/ui/number-ticker';
import { DashboardStore } from '../dashboard-store';
import { compactNumber } from '../dashboard-format';

interface Ring {
  key: string;
  label: string;
  /** 0–100, or null when there is nothing to measure yet. */
  value: number | null;
  color: string;
  hint: string;
}

/** Greets the viewer over a drifting aurora, says what is happening now and shows three rings. */
@Component({
  selector: 'nx-aurora-hero',
  imports: [Icon, NumberTicker, RouterLink],
  changeDetection: ChangeDetectionStrategy.OnPush,
  styleUrl: './aurora-hero.scss',
  template: `<div class="aurora" aria-hidden="true">
      <i></i><i></i><i></i><i></i>
      <svg class="grain">
        <filter id="aurora-grain">
          <feTurbulence
            type="fractalNoise"
            baseFrequency="0.9"
            numOctaves="2"
            stitchTiles="stitch"
          />
          <feColorMatrix values="0 0 0 0 0.5  0 0 0 0 0.5  0 0 0 0 0.5  0 0 0 0.6 0" />
        </filter>
        <rect width="100%" height="100%" filter="url(#aurora-grain)" />
      </svg>
    </div>
    <div class="aurora-layout">
      <div class="aurora-copy">
        <span class="aurora-eyebrow"
          >{{ store.scopeLabel() }} · {{ store.appliedFrom() }} – {{ store.appliedThrough() }}</span
        >
        <h2>{{ greeting() }}</h2>
        <p class="aurora-status" [attr.data-state]="status().state">
          <i aria-hidden="true"></i>{{ status().text }}
        </p>
        <div class="aurora-actions">
          @for (action of actions(); track action.route) {
            <a [routerLink]="action.route"><nx-icon [name]="action.icon" />{{ action.label }}</a>
          }
        </div>
      </div>
      <div class="aurora-rings">
        <svg viewBox="0 0 120 120" aria-hidden="true">
          @for (ring of rings(); track ring.key) {
            <circle
              class="ring-track"
              cx="60"
              cy="60"
              [attr.r]="52 - $index * 13"
              [style.stroke]="ring.color"
            />
            <circle
              class="ring-value"
              cx="60"
              cy="60"
              pathLength="100"
              [attr.r]="52 - $index * 13"
              [style.stroke]="ring.color"
              [style.stroke-dasharray]="(ring.value ?? 0) + ' 100'"
              [style.animation-delay.ms]="200 + $index * 160"
            />
          }
        </svg>
        <div class="ring-center">
          <strong
            ><nx-number-ticker [value]="store.data()?.spend?.requests ?? 0" [format]="compact"
          /></strong>
          <span>次呼叫</span>
        </div>
        <ul class="ring-legend">
          @for (ring of rings(); track ring.key) {
            <li [title]="ring.hint">
              <i [style.background]="ring.color"></i>{{ ring.label }}
              <b>{{ ring.value === null ? '—' : ring.value + '%' }}</b>
            </li>
          }
        </ul>
      </div>
    </div>`,
})
export class AuroraHero {
  readonly store = inject(DashboardStore);
  readonly compact = compactNumber;
  readonly greeting = computed(() => {
    const hour = new Date().getHours();
    const part =
      hour < 5 ? '夜深了' : hour < 11 ? '早安' : hour < 14 ? '午安' : hour < 18 ? '下午好' : '晚安';
    const name = this.store.session.me()?.displayName;
    return name ? `${part}，${name}` : part;
  });
  readonly status = computed(() => {
    const active = this.store.activeWork(),
      attention = this.store.attention();
    if (active) return { state: 'active', text: `目前有 ${active} 件工作處理中` };
    if (attention.length)
      return {
        state: 'attention',
        text: `${attention.map((x) => x.title).join('、')} 需要留意`,
      };
    return { state: 'idle', text: '一切就緒，沒有進行中的工作' };
  });
  readonly actions = computed(() =>
    [
      { feature: 'chat', route: '/chat', icon: 'plus', label: '新對話' },
      { feature: 'files', route: '/files', icon: 'upload', label: '檔案庫' },
      { feature: 'knowledge', route: '/knowledge', icon: 'library', label: '知識庫' },
      { feature: 'tasks', route: '/tasks', icon: 'tasks', label: '背景任務' },
    ].filter((x) => this.store.session.has(x.feature)),
  );
  readonly rings = computed<Ring[]>(() => {
    const percent = (part: number | null, whole: number | null) =>
      part === null || !whole ? null : Math.round((part / whole) * 100);
    const replies = this.store.metric('chat', 'replies'),
      failed = this.store.metric('chat', 'failed') ?? 0;
    const documents = this.store.metric('knowledge', 'documents'),
      broken = this.store.metric('knowledge', 'failed') ?? 0,
      processing = this.store.metric('knowledge', 'processing') ?? 0;
    return [
      {
        key: 'replies',
        label: '回覆完成率',
        value: percent(replies, (replies ?? 0) + failed),
        color: 'var(--series-4)',
        hint: '期間完成的回覆，相對於完成與失敗合計',
      },
      {
        key: 'helpful',
        label: '回覆有幫助',
        value:
          this.store.metric('quality', 'helpful') === null
            ? null
            : Math.round(this.store.metric('quality', 'helpful')!),
        color: 'var(--series-6)',
        hint: '期間評分中「有幫助」的比例',
      },
      {
        key: 'ready',
        label: '文件可查詢',
        value: percent(documents === null ? null : documents - broken - processing, documents),
        color: 'var(--series-1)',
        hint: '知識庫文件中已完成處理的比例',
      },
    ];
  });
}
