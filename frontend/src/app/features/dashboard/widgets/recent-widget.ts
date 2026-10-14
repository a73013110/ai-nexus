import { ChangeDetectionStrategy, Component, computed, inject } from '@angular/core';
import { RouterLink } from '@angular/router';
import { EmptyState } from '@shared/ui/empty-state';
import { Icon } from '@shared/ui/icon';
import { DashboardStore } from '../dashboard-store';
import { relativeTime } from '@shared/browser/format';
import { sectionIcon, workspaceLink } from '../dashboard-format';

/** The viewer's latest work across every feature, to pick up where they left off. */
@Component({
  selector: 'nx-recent-widget',
  imports: [EmptyState, Icon, RouterLink],
  changeDetection: ChangeDetectionStrategy.OnPush,
  styleUrl: './recent-widget.scss',
  template: `@if (items().length) {
      <ol class="recent">
        @for (item of items(); track item.section.id + item.id) {
          <li [style.animation-delay.ms]="$index * 45">
            @if (item.link; as link) {
              <a [routerLink]="link.path" [queryParams]="link.query">
                <span class="recent-icon"><nx-icon [name]="item.icon" /></span>
                <span class="recent-text"
                  ><b>{{ item.title }}</b
                  ><small
                    >{{ item.section.title }}{{ item.detail ? ' · ' + item.detail : '' }}</small
                  ></span
                >
                <time [attr.datetime]="item.at">{{ item.when }}</time>
              </a>
            }
          </li>
        }
      </ol>
    } @else {
      <nx-empty-state><p>第一段對話、文件或成果會出現在這裡。</p></nx-empty-state>
    }`,
})
export class RecentWidget {
  readonly store = inject(DashboardStore);
  readonly items = computed(() =>
    this.store
      .recent()
      .slice(0, 8)
      .map((item) => ({
        ...item,
        icon: sectionIcon(item.section.id),
        link: workspaceLink(item.link),
        when: item.at ? relativeTime(item.at) : '',
      })),
  );
}
