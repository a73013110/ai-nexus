import { ChangeDetectionStrategy, Component, computed, inject, output } from '@angular/core';
import { Icon } from '@shared/ui/icon';
import { StatusBadge } from '@shared/ui/status-badge';
import { MonitoringStore } from '../monitoring-store';

export interface MonitoringTarget {
  kind: 'dependency' | 'subsystem';
  id: string;
}

/**
 * The health issues, critical first as the server orders them. An issue about a dependency or a
 * subsystem opens its details. Rows settle in and collapse away as issues come and go.
 */
@Component({
  selector: 'nx-monitoring-issues',
  imports: [Icon, StatusBadge],
  changeDetection: ChangeDetectionStrategy.OnPush,
  styles: `
    :host {
      grid-column: 1 / -1;
      display: block;
      margin-top: var(--p-space-1);
      border-top: 1px solid var(--tile-line);
    }
    ul {
      margin: 0;
      padding: 0;
      list-style: none;
    }
    li {
      display: grid;
      grid-template-rows: 1fr;
    }
    li + li .issue {
      border-top: 1px solid var(--tile-line);
    }
    .issue {
      display: grid;
      grid-template-columns: auto minmax(0, 1fr) auto;
      align-items: start;
      gap: var(--p-space-3);
      min-height: 0;
      width: 100%;
      padding: var(--p-space-3) var(--p-space-2);
      border: 0;
      border-radius: var(--p-radius-md);
      background: transparent;
      color: inherit;
      text-align: left;
    }
    button.issue {
      cursor: pointer;
      transition:
        background-color var(--motion) var(--ease),
        scale var(--motion) var(--ease-spring);
    }
    button.issue:hover {
      background: color-mix(in srgb, var(--ink) 4%, transparent);
    }
    button.issue:active {
      scale: 0.99;
    }
    nx-status-badge {
      justify-self: start;
      margin-top: 0.0625rem;
    }
    strong {
      display: block;
      font-weight: 600;
    }
    small {
      display: block;
      margin-top: 0.125rem;
      color: var(--secondary);
      font-size: var(--text-caption);
      line-height: 1.5;
    }
    nx-icon {
      --icon-size: var(--icon-sm);
      align-self: center;
      color: var(--muted);
    }
    .issue-enter {
      animation: issue-arrive var(--motion-panel) var(--ease-spring);
    }
    .issue-leave {
      overflow: hidden;
      animation: issue-depart var(--motion) var(--ease-exit) forwards;
    }
  `,
  template: `<ul aria-label="需要處理的問題">
    @for (issue of issues(); track issue.code + '|' + issue.subject) {
      <li animate.enter="issue-enter" animate.leave="issue-leave">
        @if (issue.target; as target) {
          <button type="button" class="issue" (click)="selected.emit(target)">
            <nx-status-badge [tone]="issue.tone">{{ issue.severityText }}</nx-status-badge>
            <span
              ><strong>{{ issue.title }}</strong
              ><small>{{ issue.detail }}</small></span
            >
            <nx-icon name="chevron" />
          </button>
        } @else {
          <div class="issue">
            <nx-status-badge [tone]="issue.tone">{{ issue.severityText }}</nx-status-badge>
            <span
              ><strong>{{ issue.title }}</strong
              ><small>{{ issue.detail }}</small></span
            >
          </div>
        }
      </li>
    }
  </ul>`,
})
export class MonitoringIssues {
  private readonly store = inject(MonitoringStore);
  readonly selected = output<MonitoringTarget>();
  readonly issues = computed(() => {
    const dependencies = new Set(this.store.dependencies().map((d) => d.id));
    const subsystems = new Set(this.store.subsystems().map((s) => s.id));
    return (this.store.health()?.issues ?? []).map((issue) => ({
      ...issue,
      tone: issue.severity === 'critical' ? ('danger' as const) : ('warning' as const),
      severityText: issue.severity === 'critical' ? '嚴重' : '注意',
      target: dependencies.has(issue.subject)
        ? ({ kind: 'dependency', id: issue.subject } as MonitoringTarget)
        : subsystems.has(issue.subject)
          ? ({ kind: 'subsystem', id: issue.subject } as MonitoringTarget)
          : null,
    }));
  });
}
