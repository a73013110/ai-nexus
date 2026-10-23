import { apiResource } from '@core/api/api-resource';
import { Notice } from '@shared/ui/notice';
import { CompactDialog } from '@shared/ui/compact-dialog';
import {
  ChangeDetectionStrategy,
  Component,
  ElementRef,
  inject,
  input,
  output,
  signal,
  viewChild,
  linkedSignal,
  computed,
} from '@angular/core';
import type { DirectoryUserDto, ResourceAclDto } from '@core/api/schema';
import { CollaborationApi, type SharedResourceKind } from './collaboration-api';
import { directorySearch } from './directory-search';
import { ViewScope } from '@shared/browser/view-scope';
import { DialogHeader } from '@shared/ui/dialog-header';
import { Icon } from '@shared/ui/icon';
import { Select } from '@shared/ui/select';
import { ChoiceGroups, type ChoiceGroup } from '@shared/ui/choice-groups';
import { SearchField } from '@shared/ui/search-field';
import { WorkspaceSession } from '@core/auth/workspace-session';
import { resourceSharingTour } from './resource-sharing-tour';

/** 資源種類在文案中的名詞，與對方找到它的列表功能。 */
const kindLabels: Record<SharedResourceKind, { noun: string; feature: string }> = {
  collection: { noun: '知識庫', feature: 'knowledge' },
  project: { noun: '專案', feature: 'projects' },
  artifact: { noun: '成果文件', feature: 'artifacts' },
  evaluationSet: { noun: '評測集', feature: 'quality' },
};

@Component({
  selector: 'nx-resource-sharing',
  imports: [Notice, CompactDialog, DialogHeader, Icon, Select, ChoiceGroups, SearchField],
  providers: [ViewScope],
  changeDetection: ChangeDetectionStrategy.OnPush,
  styleUrl: './resource-sharing.scss',
  template: `<dialog
    nxCompactDialog
    size="md"
    #dialog
    class="platform-dialog"
    (cancel)="cancel($event)"
  >
    <nx-dialog-header
      heading="存取權限"
      [detail]="name()"
      closeLabel="關閉存取權限"
      [tour]="tour()"
    />
    <div class="dialog-body">
      @if (error()) {
        <nx-notice tone="danger" [message]="error()" />
      }
      @if (loading()) {
        <p role="status" class="form-note">正在載入授權…</p>
      } @else {
        <div class="platform-form">
          <label class="sharing-search"
            >加入使用者<nx-search-field
              label="尋找要授權的使用者"
              placeholder="輸入至少兩個字，搜尋已登入的 AD 帳號"
              [value]="people.text()"
              (valueChange)="people.find($event)"
          /></label>
          @if (people.searchable()) {
            <div class="person-picker-results" role="region" aria-label="符合的使用者">
              @for (user of people.results(); track user.id) {
                <button class="person-picker-user" (click)="add(user)">
                  <span
                    ><strong>{{ user.displayName }}</strong
                    ><small>{{ user.account }}</small></span
                  ><nx-icon name="plus" />
                </button>
              } @empty {
                <p class="form-note">尚無符合帳號，或使用者尚未登入過平台。</p>
              }
            </div>
          }
          <fieldset>
            <legend>具名成員</legend>
            <div class="acl-members">
              @for (member of members(); track member.userId) {
                <div class="acl-member">
                  <span
                    ><strong>{{ member.displayName }}</strong
                    ><small>{{ member.account }}</small></span
                  ><nx-select
                    [label]="member.displayName + '的權限'"
                    [options]="roles"
                    [value]="member.role"
                    (valueChange)="role(member.userId, $event)"
                  /><button
                    class="icon-button member-remove"
                    [attr.aria-label]="'移除成員：' + member.displayName"
                    (click)="remove(member.userId)"
                  >
                    <nx-icon name="close" />
                  </button>
                </div>
              } @empty {
                <p class="form-note">目前僅擁有者可以存取。</p>
              }
            </div>
          </fieldset>
          @if (allowGroups() && groups().length) {
            <p class="form-note">群組授權只允許閱讀。協作編輯請加入具名成員。</p>
            <nx-choice-groups
              class="sharing-groups"
              label="可檢視的群組"
              [groups]="groupChoices()"
              [selected]="groupIds()"
              [disabled]="saving()"
              (selectedChange)="groupIds.set($event)"
            />
          }
        </div>
      }
    </div>
    @if (!loading()) {
      <footer class="dialog-foot">
        <p class="dialog-note">擁有者保留完整權限</p>
        <button class="secondary-button" [disabled]="saving()" (click)="dialog.close()">
          取消
        </button>
        <button class="primary-button sharing-save" [disabled]="saving()" (click)="save()">
          {{ saving() ? '正在儲存…' : '儲存權限' }}
        </button>
      </footer>
    }
  </dialog>`,
})
export class ResourceSharing {
  readonly kind = input.required<SharedResourceKind>();
  readonly resourceId = input.required<string>();
  readonly name = input.required<string>();
  readonly allowGroups = input(true);
  readonly saved = output<void>();
  private readonly api = inject(CollaborationApi);
  private readonly scope = inject(ViewScope);
  private readonly session = inject(WorkspaceSession);
  readonly dialog = viewChild.required<ElementRef<HTMLDialogElement>>('dialog');
  /** 每次開啟都會讀取目前的權限；編輯在儲存前只留在本地。 */
  private readonly opened = signal(0);
  private readonly aclRead = apiResource({
    params: () =>
      this.opened()
        ? { kind: this.kind(), id: this.resourceId(), opened: this.opened() }
        : undefined,
    loader: async ({ kind, id }) => ({
      acl: await this.api.access(kind, id),
      groups: this.allowGroups() ? await this.api.groups() : [],
    }),
  });
  readonly members = linkedSignal<ResourceAclDto['members']>(
    () => this.aclRead.value()?.acl.members ?? [],
  );
  readonly groupIds = linkedSignal<string[]>(() => this.aclRead.value()?.acl.groupIds ?? []);
  readonly groups = computed(() => this.aclRead.value()?.groups ?? []);
  /** 群組沒有分類，收成一張卡片。 */
  readonly groupChoices = computed<ChoiceGroup[]>(() => [
    {
      id: 'groups',
      name: '群組可檢視',
      options: this.groups().map((group) => ({ id: group.id, label: group.name })),
    },
  ]);
  readonly people = directorySearch(() => this.members().map((item) => item.userId));
  readonly saveError = signal('');
  readonly error = computed(() => this.saveError() || this.aclRead.error() || this.people.error());
  readonly loading = this.aclRead.loading;
  readonly saving = signal(false);
  readonly roles = [
    { value: 'viewer', label: '可檢視' },
    { value: 'editor', label: '可編輯' },
  ];
  /** 載入中畫面上只有標題列，先不給導覽。 */
  readonly tour = computed(() => {
    if (this.loading()) return null;
    const label = kindLabels[this.kind()];
    return resourceSharingTour({
      noun: label.noun,
      listName: this.session.featureName(label.feature),
      sharingName: this.session.featureName('sharing'),
      members: this.members().length > 0,
      groups: this.allowGroups() && this.groups().length > 0,
      testing: this.session.testing(),
    });
  });
  open() {
    this.dialog().nativeElement.showModal();
    this.saveError.set('');
    this.people.clear();
    this.opened.update((value) => value + 1);
  }
  add(user: DirectoryUserDto) {
    if (this.members().some((item) => item.userId === user.id)) return;
    this.members.update((rows) => [
      ...rows,
      { userId: user.id, account: user.account, displayName: user.displayName, role: 'viewer' },
    ]);
    this.people.clear();
  }
  remove(id: string) {
    this.members.update((rows) => rows.filter((item) => item.userId !== id));
  }
  role(id: string, role: string) {
    this.members.update((rows) =>
      rows.map((item) => (item.userId === id ? { ...item, role } : item)),
    );
  }
  cancel(event: Event) {
    if (this.saving()) event.preventDefault();
  }
  async save() {
    if (this.saving()) return;
    const valid = this.scope.guard(),
      kind = this.kind(),
      id = this.resourceId();
    this.saving.set(true);
    this.saveError.set('');
    try {
      await this.api.saveAccess(kind, id, {
        members: this.members().map((item) => ({ userId: item.userId, role: item.role })),
        groupIds: this.groupIds(),
      });
      if (valid() && this.kind() === kind && this.resourceId() === id) {
        this.dialog().nativeElement.close();
        this.saved.emit();
      }
    } catch (error) {
      if (valid()) this.saveError.set(this.scope.message(error));
    } finally {
      if (valid()) this.saving.set(false);
    }
  }
}
