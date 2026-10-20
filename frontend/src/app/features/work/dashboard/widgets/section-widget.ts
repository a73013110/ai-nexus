import { ChangeDetectionStrategy, Component, computed, inject, input } from '@angular/core';
import { RouterLink } from '@angular/router';
import { SERIES_COLORS } from '@shared/ui/donut-chart';
import { Icon } from '@shared/ui/icon';
import { NumberTicker } from '@shared/ui/number-ticker';
import { Sparkline } from '@shared/ui/sparkline';
import { DashboardStore } from '../dashboard-store';
import { metricFormatter, metricText, relativeTime } from '@shared/browser/format';
import { fillDays, sectionLink, tone, TONE_COLORS, workspaceLink } from '../dashboard-format';

/**
 * 後端送來的任何總覽區段：數字、第一個每日數列、分布，以及檢視者最近的項目。
 * 註冊了儀表板來源的功能會出現在這裡，不需要前端修改。
 */
@Component({
  selector: 'nx-section-widget',
  imports: [Icon, NumberTicker, RouterLink, Sparkline],
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
  private readonly formatters = new Map<string, (value: number) => string>();
  formatter(format: string) {
    let value = this.formatters.get(format);
    if (!value) this.formatters.set(format, (value = metricFormatter(format)));
    return value;
  }
  readonly series = computed(() => {
    const series = this.section()?.series[0];
    if (!series) return null;
    const values = fillDays(this.store.days(), series.points).map((item) => item.value);
    const total = values.reduce((sum, value) => sum + value, 0);
    return { label: series.label, values, total: metricText(total, 'count') + ' ' + series.unit };
  });
  readonly shares = computed(() => {
    const shares = this.section()?.shares ?? [];
    const total = shares.reduce((sum, part) => sum + part.value, 0) || 1;
    return shares.map((part, index) => ({
      ...part,
      color:
        part.tone !== 'neutral'
          ? TONE_COLORS[tone(part.tone)]
          : SERIES_COLORS[index % SERIES_COLORS.length],
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
    return section ? sectionLink(section, this.store.session) : null;
  });
}
