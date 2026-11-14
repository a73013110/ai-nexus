import {
  ChangeDetectionStrategy,
  Component,
  computed,
  inject,
  input,
  output,
  signal,
  viewChild,
} from '@angular/core';
import { RouterLink } from '@angular/router';
import { apiResource } from '@core/api/api-resource';
import { routeOf } from '@core/api/catalog';
import type { RepositoryCollaboratorDto, RepositoryGrantDto } from '@core/api/schema';
import { action } from '@shared/browser/action';
import { formatDate, nameInitial } from '@shared/browser/format';
import { ActionMenu, type MenuAction } from '@shared/ui/action-menu';
import { ConfirmDialog } from '@shared/ui/confirm-dialog';
import { DetailDrawer } from '@shared/ui/detail-drawer';
import { DrawerPager } from '@shared/ui/drawer-pager';
import { DrawerSummary } from '@shared/ui/drawer-summary';
import { Icon } from '@shared/ui/icon';
import { Notice } from '@shared/ui/notice';
import { PendingButton } from '@shared/ui/pending-button';
import { StatusBadge } from '@shared/ui/status-badge';
import { ToastService } from '@shared/ui/toast-service';
import { RepositoriesApi } from '../repositories-api';
import {
  canRequest,
  expiryText,
  grantOrigin,
  myAccess,
  permissionName,
  repositoryTags,
  syncState,
} from './repository-access-presentation';
import { RepositoryGrantDialog } from './repository-grant-dialog';
import { RepositoryRequestDialog } from './repository-request-dialog';
import { RepositorySettingsDialog } from './repository-settings-dialog';

/**
 * 一個儲存庫的權限：我的權限與申請，負責人與系統管理員另看到授權名單、審核中的申請，
 * 以及在 Gitea 上直接加、平台沒有記錄的協作者（可轉成平台授權）。
 * 抽屜自己讀詳情：名單只給管理者，清單上的摘要不含。
 */
@Component({
  selector: 'nx-repository-access-drawer',
  imports: [
    ActionMenu,
    ConfirmDialog,
    DetailDrawer,
    DrawerPager,
    DrawerSummary,
    Icon,
    Notice,
    PendingButton,
    RepositoryGrantDialog,
    RepositoryRequestDialog,
    RepositorySettingsDialog,
    RouterLink,
    StatusBadge,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  styles: `
    :host {
      display: contents;
    }
    .access-mine {
      display: flex;
      flex-wrap: wrap;
      align-items: center;
      gap: var(--p-space-2) var(--p-space-3);
    }
    .access-mine-text {
      display: grid;
      flex: 1 1 12rem;
      gap: var(--p-space-1);
      min-width: 0;
    }
    .access-mine-text small,
    .person-row-text small {
      color: var(--text-muted);
    }
    .access-reason {
      overflow-wrap: anywhere;
    }
    .access-skeleton {
      display: grid;
      gap: var(--p-space-4);
    }
    .access-skeleton .skeleton {
      height: 0.875rem;
    }
  `,
  templateUrl: './repository-access-drawer.html',
})
export class RepositoryAccessDrawer {
  private readonly api = inject(RepositoriesApi);
  private readonly toast = inject(ToastService);
  readonly routeOf = routeOf;
  readonly repositoryId = input<string | null>(null);
  readonly index = input(-1);
  readonly count = input(0);
  readonly move = output<-1 | 1>();
  readonly closed = output<void>();
  /** 申請、授予、移除或設定成功：頁面重讀清單。 */
  readonly changed = output<void>();
  private readonly drawer = viewChild.required(DetailDrawer);
  private readonly confirmation = viewChild.required(ConfirmDialog);
  private readonly requestDialog = viewChild.required(RepositoryRequestDialog);
  private readonly grantDialog = viewChild.required(RepositoryGrantDialog);
  private readonly settingsDialog = viewChild.required(RepositorySettingsDialog);
  readonly drawerId = computed(() => this.drawer().id);
  protected readonly initial = nameInitial;
  protected readonly permissionName = permissionName;
  protected readonly skeleton = [40, 88, 72, 92, 64];

  readonly read = apiResource({
    feature: 'repositories.access',
    params: () => this.repositoryId() ?? undefined,
    loader: (id, signal) => this.api.managedDetail(id, signal),
  });
  readonly detail = computed(() => {
    const value = this.read.value();
    return value && value.repository.id === this.repositoryId() ? value : null;
  });
  /** 設定時版本過期的說明。 */
  readonly notice = signal('');
  readonly acting = action({ concurrent: true });

  readonly view = computed(() => {
    const detail = this.detail();
    if (!detail) return null;
    const repository = detail.repository;
    const manager = repository.manager !== 'None';
    return {
      detail,
      repository,
      fullName: `${repository.owner}/${repository.name}`,
      tags: repositoryTags(repository),
      mine: myAccess(repository),
      canRequest: canRequest(repository),
      manager,
      administrator: repository.manager === 'Administrator',
      stewards: repository.stewards.map((person) => person.displayName).join('、'),
      grants: detail.grants.map((grant) => this.presentGrant(grant)),
      requests: detail.requests.map((request) => ({
        request,
        summary: `${permissionName(request.permission)}・${expiryText(request.expiresOn)}・${formatDate(request.submittedAt)}`,
      })),
      unregistered: detail.unregistered.map((collaborator) => ({
        collaborator,
        name: collaborator.user?.displayName ?? collaborator.login,
        summary: `${collaborator.login}・${permissionName(collaborator.permission)}`,
      })),
    };
  });
  readonly menu = computed<MenuAction[]>(() => {
    const view = this.view();
    if (!view?.manager) return [];
    const unavailable = view.repository.missing || view.repository.isArchived;
    return [
      { id: 'grant', label: '授予權限', icon: 'plus', disabled: unavailable },
      ...(view.administrator ? [{ id: 'settings', label: '儲存庫設定', icon: 'settings' }] : []),
    ];
  });

  open() {
    this.drawer().open();
  }
  close() {
    this.drawer().close();
  }
  menuAction(id: string) {
    const view = this.view();
    if (!view) return;
    if (id === 'grant') this.grantDialog().open(view.repository, view.administrator);
    else if (id === 'settings') {
      this.notice.set('');
      this.settingsDialog().open(view.detail);
    }
  }
  request() {
    const repository = this.detail()?.repository;
    if (repository) this.requestDialog().open(repository);
  }
  requested() {
    this.toast.success('已送出申請', '審核進度可在簽核頁查看。');
    this.refresh();
  }
  granted() {
    this.toast.success('已授予權限', '幾秒內會同步到 Gitea。');
    this.refresh();
  }
  settingsSaved() {
    this.toast.success('已儲存設定');
    this.refresh();
  }
  settingsStale(message: string) {
    this.notice.set(message);
    this.read.reload();
  }
  /** 把 Gitea 上直接加的協作者轉成平台授權：有對應的人就授予給人，否則以帳號授予（只有系統管理員）。 */
  adopt(collaborator: RepositoryCollaboratorDto) {
    const view = this.view();
    if (!view) return;
    this.grantDialog().open(view.repository, view.administrator, {
      user: collaborator.user,
      login: collaborator.login,
      permission: collaborator.permission,
    });
  }

  /** 移除一筆授權；`who` 為 null 表示放棄自己的權限。 */
  async revoke(grantId: string, who: string | null) {
    const view = this.view();
    if (!view) return;
    const own = who === null;
    if (
      !(await this.confirmation().ask(
        own
          ? {
              title: '放棄這個儲存庫的權限？',
              message: '幾秒內會從 Gitea 的協作者移除。\n之後需要時可以重新申請。',
              confirm: '放棄權限',
              danger: true,
            }
          : {
              title: `移除 ${who} 的權限？`,
              message: '幾秒內會從 Gitea 的協作者移除，對方會收到通知。',
              confirm: '移除',
              danger: true,
            },
      ))
    )
      return;
    const repositoryId = view.repository.id;
    const outcome = await this.acting.run(() => this.api.revokeGrant(repositoryId, grantId), {
      key: grantId,
      current: () => this.repositoryId() === repositoryId,
    });
    this.read.reload();
    if (!outcome.ok) return;
    this.toast.success(own ? '已放棄權限' : '已移除權限', view.fullName);
    this.changed.emit();
  }

  private refresh() {
    this.read.reload();
    this.changed.emit();
  }
  private presentGrant(grant: RepositoryGrantDto) {
    const ending = !!grant.endedAt;
    return {
      grant,
      name: grant.user?.displayName ?? grant.login ?? '',
      account: grant.user ? (grant.login ?? '') : 'Gitea 帳號',
      summary: `${permissionName(grant.permission)}・${expiryText(grant.expiresOn)}`,
      origin: grantOrigin(grant),
      // 已結束但還沒從 Gitea 移除的授權：同步完成前仍列出，標成移除中。
      state: ending ? { label: '移除中', tone: 'warning' as const } : syncState(grant.syncState),
      removable: !ending,
    };
  }
}
