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
import { groupFeatures, FEATURE_ICONS } from '../../core/feature-groups';
@Component({
  selector: 'nx-workspace-navigation',
  imports: [RouterLink, RouterLinkActive, Icon],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `<nav
    class="workspace-navigation"
    aria-label="工作區功能"
    [class.collapsible]="collapsible()"
  >
    @if (collapsible() && !compact()) {
      <button
        type="button"
        class="workspace-disclosure"
        [attr.aria-expanded]="expanded()"
        (click)="expanded.update(toggle)"
      >
        <nx-icon name="workspace" /><strong>工作區</strong> <nx-icon name="chevron" />
      </button>
    }
    @if (compact() || !collapsible() || expanded()) {
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
                    feature.name
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
  readonly compact = input(false);
  readonly activated = output<void>();
  readonly expanded = signal(false);
  readonly toggle = (value: boolean) => !value;
  readonly visibleGroups = computed(() => groupFeatures(this.session.me()?.access.features || []));
  readonly icons = FEATURE_ICONS;
}
