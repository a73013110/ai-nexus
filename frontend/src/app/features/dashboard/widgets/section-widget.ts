import { ChangeDetectionStrategy, Component, computed, inject, input } from '@angular/core';
import { RouterLink } from '@angular/router';
import { SERIES_COLORS } from '../../../shared/ui/donut-chart';
import { Icon } from '../../../shared/ui/icon';
import { NumberTicker } from '../../../shared/ui/number-ticker';
import { Sparkline } from '../../../shared/ui/sparkline';
import { relativeTime } from '../../../shared/browser/format';
import { StatusBadge } from '../../../shared/ui/status-badge';
import { DashboardStore } from '../dashboard-store';
import {
  fillDays,
  metricFormatter,
  metricText,
  tone,
  TONE_COLORS,
  workspaceLink,
} from '../dashboard-format';

/**
 * Any overview section as sent by the backend: figures, the first daily series, a distribution,
 * facts and the viewer's recent items. A feature that registers a dashboard source appears here
 * without frontend changes.
 */
@Component({
  selector: 'nx-section-widget',
  imports: [Icon, NumberTicker, RouterLink, Sparkline, StatusBadge],
  changeDetection: ChangeDetectionStrategy.OnPush,
  styleUrl: './section-widget.scss',
  template: `@if (section(); as s) {
    <dl class="metrics">
      @for (m of s.metrics; track m.key) {
        <div class="metric" [attr.data-tone]="m.tone" [attr.title]="m.hint">
          <dt>
            {{ m.label }}
            @if (m.period) {
              <span class="metric-period">期間</span>
            }
          </dt>
          <dd>
            <nx-number-ticker [value]="m.value" [format]="formatter(m.format)" />
            @if (m.unit && m.value !== null) {
              <em>{{ m.unit }}</em>
            }
          </dd>
          @if (m.hint) {
            <small>{{ m.hint }}</small>
          }
        </div>
      }
    </dl>
    @if (series(); as line) {
      <div class="series">
        <span
          >{{ line.label }}<b>{{ line.total }}</b></span
        >
        <nx-sparkline [values]="line.values" />
      </div>
    }
    @if (shares().length) {
      <div class="shares">
        <div class="shares-bar" aria-hidden="true">
          @for (part of shares(); track part.key) {
            <i [style.flex-grow]="part.value" [style.background]="part.color"></i>
          }
        </div>
        <ul>
          @for (part of shares(); track part.key) {
            <li>
              <i [style.background]="part.color"></i>{{ part.label }} <b>{{ part.text }}</b>
            </li>
          }
        </ul>
      </div>
    }
    @if (s.facts.length) {
      <div class="facts">
        @for (fact of s.facts; track fact.label) {
          <span
            >{{ fact.label
            }}<nx-status-badge [tone]="toneOf(fact.tone)">{{ fact.value }}</nx-status-badge></span
          >
        }
      </div>
    }
    @if (items().length) {
      <ul class="items">
        @for (item of items(); track item.id) {
          <li [attr.data-tone]="item.tone">
            @if (item.link; as link) {
              <a [routerLink]="link.path" [queryParams]="link.query"
                ><span>{{ item.title }}</span
                ><time>{{ item.when }}</time></a
              >
            } @else {
              <p>
                <span>{{ item.title }}</span
                ><time>{{ item.when }}</time>
              </p>
            }
          </li>
        }
      </ul>
    }
    @if (route(); as link) {
      <a class="section-go" [routerLink]="link.path" [queryParams]="link.query"
        >前往{{ s.title }}<nx-icon name="chevron"
      /></a>
    }
  }`,
})
export class SectionWidget {
  readonly sectionId = input.required<string>();
  readonly store = inject(DashboardStore);
  readonly section = computed(() => this.store.section(this.sectionId()));
  readonly toneOf = tone;
  private readonly formatters = new Map<string, (value: number) => string>();
  formatter(format: string) {
    let value = this.formatters.get(format);
    if (!value) this.formatters.set(format, (value = metricFormatter(format)));
    return value;
  }
  readonly series = computed(() => {
    const series = this.section()?.series[0];
    if (!series) return null;
    const values = fillDays(this.store.days(), series.points).map((x) => x.value);
    const total = values.reduce((a, b) => a + b, 0);
    return { label: series.label, values, total: metricText(total, 'count') + ' ' + series.unit };
  });
  readonly shares = computed(() => {
    const shares = this.section()?.shares ?? [];
    const total = shares.reduce((a, b) => a + b.value, 0) || 1;
    return shares.map((part, i) => ({
      ...part,
      color:
        part.tone !== 'neutral'
          ? TONE_COLORS[tone(part.tone)]
          : SERIES_COLORS[i % SERIES_COLORS.length],
      text: `${metricText(part.value, 'count')}（${Math.round((part.value / total) * 100)}%）`,
    }));
  });
  readonly items = computed(() =>
    (this.section()?.items ?? []).slice(0, 4).map((item) => ({
      ...item,
      link: workspaceLink(item.link),
      when: item.at ? relativeTime(item.at) : '',
    })),
  );
  readonly route = computed(() => {
    const section = this.section();
    return section && this.store.session.has(section.feature) ? workspaceLink(section.route) : null;
  });
}
