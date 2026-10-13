import { ChangeDetectionStrategy, Component, computed, inject, input, output } from '@angular/core';
import type { AdminCatalogDto } from '../../../core/api/schema';
import { Checkbox } from '../../../shared/ui/checkbox';
import { AdminStore } from '../admin-store';
import { FeatureSummary } from '../feature-summary';

/** What a user would be able to open: enabled features of enabled groups of enabled roles. */
export function userFeatures(catalog: AdminCatalogDto | null, roleIds: string[], enabled: boolean) {
  if (!catalog || !enabled) return [];
  const groups = new Set(
    catalog.roles.filter((x) => x.enabled && roleIds.includes(x.id)).flatMap((x) => x.groupIds),
  );
  const features = new Set(
    catalog.groups.filter((x) => x.enabled && groups.has(x.id)).flatMap((x) => x.featureIds),
  );
  return catalog.features.filter((x) => x.enabled && features.has(x.id));
}

export function sameIds(a: readonly string[], b: readonly string[]) {
  return a.length === b.length && a.every((id) => b.includes(id));
}

/** Role assignment with a preview of the features it grants; shared by create and inspector. */
@Component({
  selector: 'nx-user-role-fields',
  imports: [Checkbox, FeatureSummary],
  changeDetection: ChangeDetectionStrategy.OnPush,
  styleUrls: ['../form-sections.scss'],
  styles: `
    :host {
      display: grid;
      gap: var(--p-space-3);
      min-width: 0;
    }
  `,
  template: `
    <fieldset>
      <legend>分派角色</legend>
      <div class="choice-list">
        @for (role of roles(); track role.id) {
          <nx-checkbox
            [label]="role.name + (role.enabled ? '' : '（停用）')"
            [description]="role.id"
            [checked]="roleIds().includes(role.id)"
            [disabled]="disabled()"
            (checkedChange)="toggle(role.id, $event)"
          />
        }
      </div>
    </fieldset>
    <fieldset>
      <legend>儲存後可用功能（預覽）</legend>
      @if (!enabled()) {
        <p class="form-note">使用者已停用，儲存後無法使用任何功能。</p>
      }
      <nx-feature-summary [features]="features()" />
    </fieldset>
  `,
})
export class UserRoleFields {
  private readonly store = inject(AdminStore);
  readonly roleIds = input.required<string[]>();
  /** A disabled user keeps their roles but can open nothing. */
  readonly enabled = input(true);
  readonly disabled = input(false);
  readonly roleIdsChange = output<string[]>();
  readonly roles = computed(() => this.store.catalog()?.roles ?? []);
  readonly features = computed(() =>
    userFeatures(this.store.catalog(), this.roleIds(), this.enabled()),
  );
  toggle(id: string, checked: boolean) {
    const ids = this.roleIds().filter((x) => x !== id);
    this.roleIdsChange.emit(checked ? [...ids, id] : ids);
  }
}
