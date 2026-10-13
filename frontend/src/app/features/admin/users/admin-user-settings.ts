import { NgTemplateOutlet } from '@angular/common';
import { ChangeDetectionStrategy, Component, inject, input } from '@angular/core';
import { Field } from '../../../shared/ui/field';
import { Notice } from '../../../shared/ui/notice';
import { AdminStore } from '../admin-store';
import { StorageUsage } from '../../files/storage-usage';
import { ModelPolicyEditor } from '../model-policy-editor';
import { AdminUserStore, type UserSettingsTab } from './admin-user-store';
import { UserAccountFields } from './user-account-fields';
import { UserRoleFields } from './user-role-fields';

/**
 * The inspector's editable tabs. Each is its own form with its own save, error and unsaved state;
 * the fields scroll and the save bar stays at the foot.
 */
@Component({
  selector: 'nx-admin-user-settings',
  imports: [
    Field,
    ModelPolicyEditor,
    NgTemplateOutlet,
    Notice,
    StorageUsage,
    UserAccountFields,
    UserRoleFields,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  styleUrls: ['../form-sections.scss', './admin-user-settings.scss'],
  templateUrl: './admin-user-settings.html',
})
export class AdminUserSettings {
  readonly store = inject(AdminUserStore);
  readonly admin = inject(AdminStore);
  readonly tab = input.required<UserSettingsTab>();
  submit(event: Event) {
    event.preventDefault();
    const tab = this.tab();
    if (tab === 'account') void this.store.saveAccount();
    if (tab === 'roles') void this.store.saveRoles();
    if (tab === 'models') void this.store.saveModels();
    if (tab === 'storage') void this.store.saveStorage();
  }
}
