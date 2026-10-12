import { ChangeDetectionStrategy, Component, computed, inject } from '@angular/core';
import { Icon } from '../../../shared/ui/icon';
import { StatusBadge } from '../../../shared/ui/status-badge';
import { DashboardStore } from '../dashboard-store';
import { sectionIcon, tone } from '../dashboard-format';

/**
 * Configuration and service facts from every section in one list, failures first. "Enabled"
 * means the setting is usable, not that the remote service was just verified.
 */
@Component({
  selector: 'nx-services-widget',
  imports: [Icon, StatusBadge],
  changeDetection: ChangeDetectionStrategy.OnPush,
  styles: `
    :host {
      display: block;
    }
    ul {
      display: grid;
      gap: 2px;
      margin: 0;
      padding: 0;
      list-style: none;
    }
    li {
      display: grid;
      grid-template-columns: auto minmax(0, 1fr) auto;
      align-items: center;
      gap: var(--p-space-2);
      padding: var(--p-space-2) 0;
      border-bottom: 1px solid color-mix(in srgb, var(--line) 60%, transparent);
      font-size: var(--text-caption);
      --icon-size: var(--icon-sm);
    }
    li:last-child {
      border-bottom: 0;
    }
    nx-icon {
      color: var(--muted);
    }
    span {
      overflow: hidden;
      text-overflow: ellipsis;
      white-space: nowrap;
    }
    small {
      color: var(--muted);
    }
    p {
      margin: 0;
      color: var(--muted);
      font-size: var(--text-caption);
    }
  `,
  template: `@if (facts().length) {
      <ul>
        @for (fact of facts(); track fact.section + fact.label) {
          <li>
            <nx-icon [name]="fact.icon" /><span
              >{{ fact.label }} <small>{{ fact.title }}</small></span
            ><nx-status-badge [tone]="fact.tone">{{ fact.value }}</nx-status-badge>
          </li>
        }
      </ul>
    } @else {
      <p>目前沒有可顯示的服務設定。</p>
    }`,
})
export class ServicesWidget {
  readonly store = inject(DashboardStore);
  private readonly order = ['danger', 'warning', 'info', 'neutral', 'success'];
  readonly facts = computed(() =>
    this.store
      .sections()
      .flatMap((section) =>
        section.facts.map((fact) => ({
          ...fact,
          section: section.id,
          title: section.title,
          icon: sectionIcon(section.id),
          tone: tone(fact.tone),
        })),
      )
      .sort((a, b) => this.order.indexOf(a.tone) - this.order.indexOf(b.tone)),
  );
}
