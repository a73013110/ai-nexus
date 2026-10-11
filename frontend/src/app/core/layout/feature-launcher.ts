import {
  ChangeDetectionStrategy,
  Component,
  computed,
  inject,
  input,
  output,
  signal,
} from '@angular/core';
import { RouterLink } from '@angular/router';
import { Icon } from '../../shared/ui/icon';
import { ViewSwitch } from '../../shared/ui/view-switch';
import { FEATURE_ICONS, groupFeatures } from './feature-groups';
import { PIN_LIMIT, NavigationPreferences } from './navigation-preferences';
import { WorkspaceCommands } from './workspace-commands';

/**
 * Every feature the account can open, in the shared groups as three-column icons or one per line.
 * Pinned features already sit above it, so it lists the rest; editing shows them all as pin
 * toggles.
 */
@Component({
  selector: 'nx-feature-launcher',
  imports: [RouterLink, Icon, ViewSwitch],
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
      <nx-view-switch
        label="功能顯示方式"
        [iconOnly]="true"
        [options]="displays"
        [value]="pins.display()"
        (valueChange)="pins.setDisplay($any($event))"
      />
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
    <div class="workspace-groups">
      @for (group of groups(); track group.id) {
        <section class="workspace-group" [attr.aria-label]="group.name">
          <h3>{{ group.name }}</h3>
          <div class="workspace-icons" [class.is-list]="pins.display() === 'list'">
            @for (feature of group.features; track feature.id) {
              @if (editing()) {
                <button
                  type="button"
                  [attr.aria-pressed]="pins.has(feature.id)"
                  [disabled]="!pins.has(feature.id) && pins.full()"
                  [attr.aria-label]="'釘選' + feature.name"
                  (click)="pins.toggle(feature.id)"
                >
                  <nx-icon [name]="icons[feature.id] || 'document'" /><span>{{
                    feature.name
                  }}</span>
                </button>
              } @else {
                <a
                  [routerLink]="feature.route"
                  [class.current]="active() === feature.id"
                  [attr.aria-current]="active() === feature.id ? 'page' : null"
                  [attr.title]="feature.name"
                  [attr.aria-label]="feature.name"
                  (click)="activated.emit()"
                >
                  <nx-icon [name]="icons[feature.id] || 'document'" /><span>{{
                    feature.name
                  }}</span>
                </a>
              }
            }
          </div>
        </section>
      } @empty {
        <p class="launcher-note">所有功能都已釘選在上方。</p>
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
  readonly displays = [
    { value: 'icons', label: '圖示顯示', icon: 'apps' },
    { value: 'list', label: '條列顯示', icon: 'list' },
  ];
  readonly icons = FEATURE_ICONS;
  readonly groups = computed(() =>
    groupFeatures(
      this.editing()
        ? this.pins.features()
        : this.pins.features().filter((feature) => !this.pins.has(feature.id)),
    ),
  );
}
