import {
  ChangeDetectionStrategy,
  Component,
  computed,
  inject,
  input,
  output,
  signal,
} from '@angular/core';
import { RouterLink, RouterLinkActive } from '@angular/router';
import { WorkspaceSession } from '../../core/auth/workspace-session';
import { Icon } from './icon';
import { groupFeatures } from '../../core/feature-groups';
@Component({
  selector: 'nx-workspace-navigation',
  imports: [RouterLink, RouterLinkActive, Icon],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `<nav
    class="workspace-navigation"
    aria-label="工作區功能"
    [class.collapsible]="collapsible()"
  >
    @if (collapsible()) {
      <button
        type="button"
        class="workspace-disclosure"
        [attr.aria-expanded]="expanded()"
        (click)="expanded.update(toggle)"
      >
        <nx-icon name="workspace" /><strong>工作區</strong> <nx-icon name="chevron" />
      </button>
    }
    @if (!collapsible() || expanded()) {
      <div class="workspace-groups">
        @for (group of visibleGroups(); track group.id) {
          <section class="workspace-group" [attr.aria-label]="group.name">
            <h3>{{ group.name }}</h3>
            <div class="workspace-icons">
              @for (feature of group.features; track feature.id) {
                <a
                  [routerLink]="feature.route"
                  routerLinkActive="current"
                  ariaCurrentWhenActive="page"
                  [attr.title]="feature.name"
                  [attr.aria-label]="feature.name"
                  (click)="activated.emit()"
                >
                  <nx-icon [name]="icons[feature.id] || 'document'" /><span>{{
                    shortNames[feature.id] || feature.name
                  }}</span>
                </a>
              }
            </div>
          </section>
        }
      </div>
    }
  </nav>`,
})
export class WorkspaceNavigation {
  private readonly session = inject(WorkspaceSession);
  readonly collapsible = input(false);
  readonly activated = output<void>();
  readonly expanded = signal(false);
  readonly toggle = (value: boolean) => !value;
  readonly visibleGroups = computed(() => {
    const features = this.session.me()?.access.features || [];
    const hasFiles = features.some((feature) =>
      ['chat', 'knowledge', 'projects'].includes(feature.id),
    );
    return groupFeatures(
      hasFiles
        ? [
            ...features.slice(0, 2),
            { id: 'files', name: '檔案庫', route: '/files' },
            ...features.slice(2),
          ]
        : features,
    );
  });
  readonly shortNames: Record<string, string> = {
    files: '檔案庫',
    dashboard: '總覽',
    repositories: '程式庫',
    chat: '對話',
    projects: '專案',
    knowledge: '知識',
    artifacts: '成果',
    tasks: '任務',
    quality: '評測',
    admin: '管理',
    integrations: '來源',
    shared: '分享',
  };
  readonly icons: Record<string, string> = {
    files: 'document',
    dashboard: 'dashboard',
    repositories: 'git',
    chat: 'lines',
    projects: 'projects',
    knowledge: 'library',
    artifacts: 'document',
    tasks: 'tasks',
    quality: 'shield',
    admin: 'lock',
    integrations: 'integrations',
    shared: 'share',
  };
}
