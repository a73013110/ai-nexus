import {
  ChangeDetectionStrategy,
  Component,
  computed,
  input,
  model,
  output,
  viewChild,
} from '@angular/core';
import type { OnlineSession } from '@core/api/schema';
import { Panel } from '@shared/ui/panel';
import { MonitoringSessions } from './monitoring-sessions';
import { monitoringFeatureNames } from '../monitoring-format';

/**
 * 誰在線上：他們開啟的功能（作為篩選），以及其後的工作階段列表。
 */
@Component({
  selector: 'nx-monitoring-people',
  imports: [MonitoringSessions, Panel],
  changeDetection: ChangeDetectionStrategy.OnPush,
  styles: `
    :host {
      display: block;
      min-width: 0;
    }
    .features {
      display: flex;
      flex-wrap: wrap;
      gap: var(--p-space-2);
      margin: 0 0 var(--p-space-4);
      padding: 0;
      list-style: none;
    }
    button {
      display: inline-flex;
      align-items: center;
      gap: var(--p-space-2);
      min-height: var(--control-height-compact);
      padding: 0 var(--p-space-3);
      border: 1px solid var(--line);
      border-radius: var(--radius-action);
      background: transparent;
      color: var(--secondary);
      font-size: var(--text-caption);
      cursor: pointer;
      transition:
        background-color var(--motion) var(--ease),
        box-shadow var(--motion) var(--ease),
        scale var(--motion) var(--ease-spring);
    }
    button:hover {
      background: var(--hover);
    }
    button:active {
      scale: 0.97;
    }
    button[aria-pressed='true'] {
      border-color: transparent;
      background: var(--surface);
      box-shadow: var(--selected-ring);
      color: var(--ink);
      font-weight: 600;
    }
    b {
      color: var(--muted);
      font-weight: 500;
      font-variant-numeric: tabular-nums;
    }
    @media (pointer: coarse) {
      button {
        min-height: var(--button-target);
      }
    }
  `,
  template: `<nx-panel heading="線上人員" [count]="sessions().length">
    <ul class="features" aria-label="依目前功能篩選工作階段">
      <li>
        <button
          type="button"
          [attr.aria-pressed]="feature() === 'all'"
          (click)="feature.set('all')"
        >
          全部<b>{{ sessions().length }}</b>
        </button>
      </li>
      @for (item of features(); track item.feature) {
        <li>
          <button
            type="button"
            [attr.aria-pressed]="feature() === item.feature"
            (click)="feature.set(feature() === item.feature ? 'all' : item.feature)"
          >
            {{ item.name }}<b>{{ item.count }}</b>
          </button>
        </li>
      }
    </ul>
    <nx-monitoring-sessions
      [sessions]="sessions()"
      [selectedId]="selectedId()"
      [(feature)]="feature"
      (selected)="selected.emit($event)"
    />
  </nx-panel>`,
})
export class MonitoringPeople {
  readonly sessions = input.required<OnlineSession[]>();
  readonly selectedId = input<string | null>(null);
  readonly feature = model('all');
  readonly selected = output<OnlineSession>();
  /** 工作階段表格；頁面的詳情抽屜依它目前這一頁逐筆瀏覽。 */
  readonly list = viewChild(MonitoringSessions);
  private readonly featureName = monitoringFeatureNames();
  readonly features = computed(() => {
    const counts = new Map<string, number>();
    for (const segment of this.sessions())
      counts.set(segment.feature, (counts.get(segment.feature) ?? 0) + 1);
    return [...counts]
      .sort((first, second) => second[1] - first[1] || first[0].localeCompare(second[0]))
      .slice(0, 8)
      .map(([feature, count]) => ({ feature, count, name: this.featureName(feature) }));
  });
}
