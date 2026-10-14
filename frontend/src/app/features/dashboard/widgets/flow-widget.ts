import { ChangeDetectionStrategy, Component, computed, inject } from '@angular/core';
import { NgTemplateOutlet } from '@angular/common';
import { RouterLink } from '@angular/router';
import { Icon } from '@shared/ui/icon';
import { NumberTicker } from '@shared/ui/number-ticker';
import { DashboardStore } from '../dashboard-store';
import { metricText } from '@shared/browser/format';
import { workspaceLink } from '../dashboard-format';

interface Stage {
  section: string;
  icon: string;
  metric: string;
  unit: string;
  meta: (store: DashboardStore) => string;
  live?: string;
}

/** The path information takes, from saved originals to shared results; each stage is its feature. */
const STAGES: Stage[] = [
  {
    section: 'files',
    icon: 'files',
    metric: 'files',
    unit: '個原檔',
    meta: (s) => `期間保存 ${metricText(s.metric('files', 'uploads'), 'count')} 個`,
  },
  {
    section: 'tasks',
    icon: 'tasks',
    metric: 'active',
    unit: '件處理中',
    live: 'active',
    meta: (s) => `期間完成 ${metricText(s.metric('tasks', 'completed'), 'count')} 件`,
  },
  {
    section: 'knowledge',
    icon: 'library',
    metric: 'documents',
    unit: '份文件',
    meta: (s) => `${metricText(s.metric('knowledge', 'chunks'), 'count')} 段索引`,
  },
  {
    section: 'chat',
    icon: 'chat',
    metric: 'replies',
    unit: '則回覆',
    live: 'generating',
    meta: (s) => `${metricText(s.metric('chat', 'generating'), 'count')} 件生成中`,
  },
  {
    section: 'artifacts',
    icon: 'document',
    metric: 'versions',
    unit: '個版本',
    meta: (s) => `${metricText(s.metric('artifacts', 'artifacts'), 'count')} 份成果文件`,
  },
  {
    section: 'shared',
    icon: 'share',
    metric: 'sent',
    unit: '個有效分享',
    meta: (s) => `${metricText(s.metric('shared', 'expiring'), 'count')} 個 7 天內到期`,
  },
];

@Component({
  selector: 'nx-flow-widget',
  imports: [Icon, NgTemplateOutlet, NumberTicker, RouterLink],
  changeDetection: ChangeDetectionStrategy.OnPush,
  styleUrl: './flow-widget.scss',
  template: `<ol class="flow" [style.--stages]="stages().length">
      @for (stage of stages(); track stage.section) {
        <li [class.is-live]="stage.live" [style.--i]="$index">
          @if (!$first) {
            <span class="flow-link" [class.is-flowing]="stage.flowing" aria-hidden="true"></span>
          }
          @if (stage.link; as link) {
            <a class="flow-node" [routerLink]="link.path" [queryParams]="link.query">
              <ng-container
                [ngTemplateOutlet]="node"
                [ngTemplateOutletContext]="{ $implicit: stage }"
              />
            </a>
          } @else {
            <div class="flow-node">
              <ng-container
                [ngTemplateOutlet]="node"
                [ngTemplateOutletContext]="{ $implicit: stage }"
              />
            </div>
          }
        </li>
      }
    </ol>
    <ng-template #node let-stage>
      <span class="flow-icon"><nx-icon [name]="stage.icon" /></span>
      <span class="flow-label">{{ stage.title }}</span>
      <strong><nx-number-ticker [value]="stage.value" /></strong>
      <small>{{ stage.unit }}</small>
      <small class="flow-meta">{{ stage.meta }}</small>
    </ng-template>
    @if (store.scope() === 'platform') {
      <p class="flow-note">數字是所選範圍的統計；入口開啟目前帳號有權限的工作區。</p>
    }`,
})
export class FlowWidget {
  readonly store = inject(DashboardStore);
  readonly stages = computed(() =>
    STAGES.filter((stage) => this.store.section(stage.section)).map((stage) => {
      const section = this.store.section(stage.section)!;
      const live = stage.live ? (this.store.metric(stage.section, stage.live) ?? 0) > 0 : false;
      return {
        section: stage.section,
        icon: stage.icon,
        title: section.title,
        value: this.store.metric(stage.section, stage.metric),
        unit: stage.unit,
        meta: stage.meta(this.store),
        live,
        flowing: live || this.store.activeWork() > 0,
        link: this.store.session.has(section.feature) ? workspaceLink(section.route) : null,
      };
    }),
  );
}
