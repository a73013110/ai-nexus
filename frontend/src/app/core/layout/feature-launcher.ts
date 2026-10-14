import {
  ChangeDetectionStrategy,
  Component,
  computed,
  inject,
  input,
  output,
  signal,
} from '@angular/core';
import { NgTemplateOutlet } from '@angular/common';
import { RouterLink } from '@angular/router';
import type { FeatureDto } from '@core/api/schema';
import { Icon } from '@shared/ui/icon';
import { featureIcon, type NavigationNode } from './navigation-tree';
import { PIN_LIMIT, NavigationPreferences, type NavigationDisplay } from './navigation-preferences';
import { WorkspaceCommands } from './workspace-commands';

/**
 * Every feature the account can open, in the administrators' menu groups (`navigationTree`) as
 * three-column icons or one per line: top-level features first, then each group with its
 * subgroups nested inside. Pinned features stay in their group with a pin mark, so the catalogue
 * never reshuffles; editing turns every entry into a pin toggle. A pinned current page is marked
 * once, on its pinned row.
 */
@Component({
  selector: 'nx-feature-launcher',
  imports: [RouterLink, NgTemplateOutlet, Icon],
  changeDetection: ChangeDetectionStrategy.OnPush,
  styleUrl: './feature-launcher.scss',
  template: `<div class="launcher-tools">
      <button
        type="button"
        class="quiet-button"
        [attr.aria-pressed]="editing()"
        (click)="editing.set(!editing())"
      >
        <nx-icon [name]="editing() ? 'check' : 'pin'" />{{ editing() ? '完成' : '自訂釘選' }}
      </button>
      <button
        type="button"
        class="icon-button display-toggle"
        [attr.data-display]="pins.display()"
        [attr.aria-label]="nextDisplay().label"
        [attr.title]="nextDisplay().label"
        (click)="pins.setDisplay(nextDisplay().value)"
      >
        <nx-icon name="apps" /><nx-icon name="list" />
      </button>
      <button
        type="button"
        class="icon-button"
        aria-label="搜尋全部功能"
        title="搜尋功能 Ctrl + K"
        (click)="commands.open()"
      >
        <nx-icon name="search" />
      </button>
    </div>
    @if (editing()) {
      <p class="launcher-note" role="status">
        已釘選 {{ pins.ids().length }}／{{ limit }} 項，選擇要放在上方的功能。
      </p>
    }
    <ng-template #entries let-features>
      <div class="workspace-icons" [class.is-list]="pins.display() === 'list'">
        @for (feature of asFeatures(features); track feature.id) {
          @if (editing()) {
            <button
              type="button"
              [attr.aria-pressed]="pins.has(feature.id)"
              [disabled]="!pins.has(feature.id) && pins.full()"
              [attr.aria-label]="'釘選' + feature.name"
              (click)="pins.toggle(feature.id)"
            >
              <nx-icon [name]="icon(feature.icon)" /><span>{{ feature.name }}</span>
            </button>
          } @else {
            <a
              [routerLink]="feature.route"
              [class.current]="active() === feature.id && !pins.has(feature.id)"
              [attr.aria-current]="active() === feature.id && !pins.has(feature.id) ? 'page' : null"
              [attr.title]="pins.has(feature.id) ? feature.name + '（已釘選）' : feature.name"
              [attr.aria-label]="feature.name"
              (click)="activated.emit()"
            >
              <nx-icon [name]="icon(feature.icon)" /><span>{{ feature.name }}</span>
              @if (pins.has(feature.id)) {
                <nx-icon class="pin-mark" name="pin" aria-hidden="true" />
              }
            </a>
          }
        }
      </div>
    </ng-template>
    <!-- A group nests its subgroups as sections inside its own, below its features. -->
    <ng-template #section let-node let-depth="depth">
      <section
        class="workspace-group"
        [class.is-nested]="depth > 0"
        [attr.aria-label]="asNode(node).group.name"
      >
        @if (depth === 0) {
          <h3>{{ asNode(node).group.name }}</h3>
        } @else {
          <h4>{{ asNode(node).group.name }}</h4>
        }
        @if (asNode(node).features.length) {
          <ng-container
            [ngTemplateOutlet]="entries"
            [ngTemplateOutletContext]="{ $implicit: asNode(node).features }"
          />
        }
        @for (child of asNode(node).groups; track child.group.id) {
          <ng-container
            [ngTemplateOutlet]="section"
            [ngTemplateOutletContext]="{ $implicit: child, depth: depth + 1 }"
          />
        }
      </section>
    </ng-template>
    <div class="workspace-groups">
      @if (pins.tree().features.length) {
        <div class="workspace-group">
          <ng-container
            [ngTemplateOutlet]="entries"
            [ngTemplateOutletContext]="{ $implicit: pins.tree().features }"
          />
        </div>
      }
      @for (node of pins.tree().groups; track node.group.id) {
        <ng-container
          [ngTemplateOutlet]="section"
          [ngTemplateOutletContext]="{ $implicit: node, depth: 0 }"
        />
      }
    </div>`,
})
export class FeatureLauncher {
  readonly pins = inject(NavigationPreferences);
  readonly commands = inject(WorkspaceCommands);
  readonly active = input<string | null>(null);
  readonly activated = output<void>();
  readonly editing = signal(false);
  readonly limit = PIN_LIMIT;
  /**
   * One button flips between the two displays, so the toolbar keeps a single cell. It shows the
   * display it switches to, the convention of a toggle whose two states are both views.
   */
  readonly nextDisplay = computed<{ value: NavigationDisplay; label: string }>(() =>
    this.pins.display() === 'icons'
      ? { value: 'list', label: '切換為條列顯示' }
      : { value: 'icons', label: '切換為圖示顯示' },
  );
  readonly icon = featureIcon;
  /** Template contexts are untyped; these restore the types for the recursive sections. */
  asFeatures = (features: unknown) => features as FeatureDto[];
  asNode = (node: unknown) => node as NavigationNode<FeatureDto>;
}
