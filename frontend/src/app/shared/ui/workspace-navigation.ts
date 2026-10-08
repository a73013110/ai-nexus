import { ChangeDetectionStrategy, Component, computed, inject, input, output } from '@angular/core';
import { NavigationEnd, PRIMARY_OUTLET, Router, RouterLink } from '@angular/router';
import { toSignal } from '@angular/core/rxjs-interop';
import { filter } from 'rxjs';
import { WorkspaceSession } from '../../core/auth/workspace-session';
import { Icon } from './icon';
import { ScrollArea } from './scroll-area';
import { groupFeatures, FEATURE_ICONS } from '../../core/feature-groups';
@Component({
  selector: 'nx-workspace-navigation',
  imports: [RouterLink, Icon, ScrollArea],
  changeDetection: ChangeDetectionStrategy.OnPush,
  styles: ':host { display: flex; flex-direction: column; min-height: 0; }',
  template: `<nav
    class="workspace-navigation"
    aria-label="工作區功能"
    [class.collapsible]="collapsible()"
  >
    @if (collapsible() && !compact()) {
      <button
        type="button"
        class="workspace-disclosure ui-density-compact"
        [attr.aria-expanded]="expanded()"
        (click)="expandedChange.emit(!expanded())"
      >
        <nx-icon name="workspace" /><strong>工作區</strong> <nx-icon name="chevron" />
      </button>
    }
    @if (compact() || !collapsible() || expanded()) {
      <nx-scroll-area label="工作區功能">
        <div class="workspace-groups">
          @for (group of visibleGroups(); track group.id) {
            <section class="workspace-group" [attr.aria-label]="group.name">
              <h3>{{ group.name }}</h3>
              <div class="workspace-icons">
                @for (feature of group.features; track feature.id) {
                  <a
                    [routerLink]="feature.route"
                    [class.current]="activeFeature() === feature.id"
                    [attr.aria-current]="activeFeature() === feature.id ? 'page' : null"
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
      </nx-scroll-area>
    }
  </nav>`,
})
export class WorkspaceNavigation {
  private readonly session = inject(WorkspaceSession);
  private readonly router = inject(Router);
  private readonly navigation = toSignal(
    this.router.events.pipe(filter((event) => event instanceof NavigationEnd)),
  );
  readonly collapsible = input(false);
  readonly compact = input(false);
  readonly activated = output<void>();
  readonly expanded = input(false);
  readonly expandedChange = output<boolean>();
  readonly visibleGroups = computed(() =>
    groupFeatures((this.session.me()?.access.features || []).filter((feature) => !!feature.route)),
  );
  /** Select the deepest matching feature; its own detail routes still belong to it. */
  readonly activeFeature = computed(() => {
    this.navigation();
    let active: string | null = null;
    let depth = -1;
    for (const feature of this.visibleGroups().flatMap((group) => group.features)) {
      const tree = this.router.parseUrl(feature.route!);
      if (
        !this.router.isActive(tree, {
          paths: 'subset',
          queryParams: 'ignored',
          matrixParams: 'ignored',
          fragment: 'ignored',
        })
      )
        continue;
      const candidateDepth = tree.root.children[PRIMARY_OUTLET]?.segments.length ?? 0;
      if (candidateDepth > depth) {
        active = feature.id;
        depth = candidateDepth;
      }
    }
    return active;
  });
  readonly icons = FEATURE_ICONS;
}
