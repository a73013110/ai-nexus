import { ChangeDetectionStrategy, Component, computed, inject } from '@angular/core';
import { catalogFeature } from '@core/api/catalog';
import type { FeatureDto, NavigationNodeDto, NavigationSectionDto } from '@core/api/schema';
import { WorkspaceSession } from '@core/auth/workspace-session';
import { navigationSections } from '@core/layout/navigation-tree';
import { FeatureSummary } from '@features/admin/access-control/roles/feature-summary';
import { SettingGroup } from '@shared/ui/setting-row';

/**
 * 自己的授權依自己看到的側欄分組（`/me` 的選單已依此帳號修剪，就是使用者熟悉的位置），
 * 動作接在所屬頁面之後。管理端分組「別人」的授權時不能這樣做，見 `featureSections`。
 */
export function ownFeatureSections(
  navigation: readonly NavigationNodeDto[],
  features: readonly FeatureDto[],
): NavigationSectionDto[] {
  const actions = features.filter((feature) => catalogFeature(feature.id)?.kind === 'action');
  return navigationSections(navigation).map((section) => ({
    id: section.id,
    path: section.path ? [section.path] : [],
    featureIds: section.entries.flatMap((node) =>
      node.featureId
        ? [
            node.featureId,
            ...actions
              .filter((action) => action.id.startsWith(node.featureId + '.'))
              .map((action) => action.id),
          ]
        : [],
    ),
  }));
}

/** 帳號分頁的「角色與可用功能」：唯讀摘要，不提供連結（換頁會關掉設定）。 */
@Component({
  selector: 'nx-account-access',
  imports: [FeatureSummary, SettingGroup],
  changeDetection: ChangeDetectionStrategy.OnPush,
  styles: `
    .access-block {
      display: grid;
      gap: var(--p-space-2);
      min-width: 0;
      padding-top: var(--p-space-3);
      border-top: 1px solid var(--line);
    }
    h3 {
      margin: 0;
      font-size: var(--text-ui);
      font-weight: 550;
    }
  `,
  template: `<nx-setting-group
    heading="角色與可用功能"
    description="可用功能由角色決定，每次操作伺服器都會再次驗證；需要更多權限時請聯絡管理員。"
  >
    @if (session.me(); as me) {
      <div class="access-block">
        <h3>角色</h3>
        <div class="resource-badges" aria-label="角色">
          @for (role of me.access.roles; track role.id) {
            <span>{{ role.name }}</span>
          } @empty {
            <span>尚未分派角色</span>
          }
        </div>
      </div>
      <div class="access-block">
        <h3>可用功能</h3>
        <nx-feature-summary [features]="me.access.features" [navigation]="sections()" />
      </div>
    }
  </nx-setting-group>`,
})
export class AccountAccess {
  readonly session = inject(WorkspaceSession);
  readonly sections = computed(() => {
    const access = this.session.me()?.access;
    return access ? ownFeatureSections(access.navigation, access.features) : [];
  });
}
