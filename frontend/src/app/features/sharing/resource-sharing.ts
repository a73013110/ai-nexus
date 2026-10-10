import { Notice } from '../../shared/ui/notice';
import { CompactDialog } from '../../shared/ui/compact-dialog';
import {
  ChangeDetectionStrategy,
  Component,
  ElementRef,
  inject,
  input,
  output,
  signal,
  viewChild,
} from '@angular/core';
import type { DirectoryUserDto, ResourceAclDto } from '../../core/api/schema';
import { ResourceApi, type SharedResourceKind } from './resource-api';
import { WorkspaceSession } from '../../core/auth/workspace-session';
import { ViewScope } from '../../shared/browser/view-scope';
import { Icon } from '../../shared/ui/icon';
import { Select } from '../../shared/ui/select';
import { Checkbox } from '../../shared/ui/checkbox';
import { SearchField } from '../../shared/ui/search-field';

@Component({
  selector: 'nx-resource-sharing',
  imports: [Notice, CompactDialog, Icon, Select, Checkbox, SearchField],
  providers: [ViewScope],
  changeDetection: ChangeDetectionStrategy.OnPush,
  styleUrl: './resource-sharing.scss',
  template: `<dialog nxCompactDialog #dialog class="platform-dialog" (cancel)="cancel($event)">
    <div class="dialog-heading">
      <div>
        <h2>存取權限</h2>
        <p>{{ name() }} · 擁有者保留完整權限</p>
      </div>
      <button
        class="icon-button"
        aria-label="關閉存取權限"
        [disabled]="saving()"
        (click)="dialog.close()"
      >
        <nx-icon name="close" />
      </button>
    </div>
    @if (error()) {
      <nx-notice tone="danger" [message]="error()" />
    }
    @if (loading()) {
      <p role="status">正在載入授權…</p>
    } @else {
      <div class="platform-form">
        <label
          >加入使用者<nx-search-field
            label="尋找要授權的使用者"
            placeholder="輸入至少兩個字，搜尋已登入的 AD 帳號"
            [value]="search()"
            (valueChange)="find($event)"
        /></label>
        @if (search().trim().length >= 2) {
          <div class="directory-results" role="region" aria-label="符合的使用者">
            @for (user of results(); track user.id) {
              <button class="directory-user" (click)="add(user)">
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
                  class="icon-button"
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
        @if (allowGroups()) {
          <fieldset>
            <legend>群組可檢視</legend>
            <p class="form-note">群組授權只允許閱讀。協作編輯請加入具名成員。</p>
            <div class="choice-list">
              @for (group of groups(); track group.id) {
                <nx-checkbox
                  [label]="group.name"
                  [checked]="groupIds().includes(group.id)"
                  [disabled]="saving()"
                  (checkedChange)="toggleGroup(group.id, $event)"
                />
              }
            </div>
          </fieldset>
        }
        <div class="dialog-actions">
          <button class="secondary-button" [disabled]="saving()" (click)="dialog.close()">
            取消</button
          ><button class="primary-button" [disabled]="saving()" (click)="save()">
            {{ saving() ? '正在儲存…' : '儲存權限' }}
          </button>
        </div>
      </div>
    }
  </dialog>`,
})
export class ResourceSharing {
  readonly kind = input.required<SharedResourceKind>();
  readonly resourceId = input.required<string>();
  readonly name = input.required<string>();
  readonly allowGroups = input(true);
  readonly saved = output<void>();
  private readonly api = inject(ResourceApi);
  private readonly scope = inject(ViewScope);
  private readonly session = inject(WorkspaceSession);
  readonly dialog = viewChild.required<ElementRef<HTMLDialogElement>>('dialog');
  readonly members = signal<ResourceAclDto['members']>([]);
  readonly groupIds = signal<string[]>([]);
  readonly groups = signal<{ id: string; name: string }[]>([]);
  readonly search = signal('');
  readonly results = signal<DirectoryUserDto[]>([]);
  readonly error = signal('');
  readonly loading = signal(false);
  readonly saving = signal(false);
  readonly roles = [
    { value: 'viewer', label: '可檢視' },
    { value: 'editor', label: '可編輯' },
  ];
  private version = 0;
  async open() {
    const kind = this.kind(),
      id = this.resourceId(),
      valid = this.scope.guard();
    this.dialog().nativeElement.showModal();
    this.loading.set(true);
    this.error.set('');
    this.search.set('');
    this.results.set([]);
    try {
      const acl = await this.api.access(kind, id),
        groups = this.allowGroups() ? await this.api.groups() : [];
      if (valid() && this.kind() === kind && this.resourceId() === id) {
        this.members.set(acl.members);
        this.groupIds.set(acl.groupIds);
        this.groups.set(groups);
      }
    } catch (error) {
      if (valid()) this.error.set(this.scope.message(error));
    } finally {
      if (valid()) this.loading.set(false);
    }
  }
  find(value: string) {
    this.search.set(value);
    const version = ++this.version;
    this.results.set([]);
    if (value.trim().length < 2) return;
    this.scope.later(
      () => {
        const valid = this.scope.guard();
        void this.api
          .directory(value.trim())
          .then((rows) => {
            if (valid() && version === this.version)
              this.results.set(
                rows.filter(
                  (x) =>
                    x.id !== this.session.me()?.id &&
                    !this.members().some((m) => m.userId === x.id),
                ),
              );
          })
          .catch((error) => {
            if (valid() && version === this.version) this.error.set(this.scope.message(error));
          });
      },
      250,
      'directory',
    );
  }
  add(user: DirectoryUserDto) {
    if (this.members().some((x) => x.userId === user.id)) return;
    this.members.update((rows) => [
      ...rows,
      { userId: user.id, account: user.account, displayName: user.displayName, role: 'viewer' },
    ]);
    this.search.set('');
    this.results.set([]);
    this.version++;
  }
  remove(id: string) {
    this.members.update((rows) => rows.filter((x) => x.userId !== id));
  }
  role(id: string, role: string) {
    this.members.update((rows) => rows.map((x) => (x.userId === id ? { ...x, role } : x)));
  }
  toggleGroup(id: string, checked: boolean) {
    this.groupIds.update((rows) => (checked ? [...rows, id] : rows.filter((x) => x !== id)));
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
    this.error.set('');
    try {
      await this.api.saveAccess(kind, id, {
        members: this.members().map((x) => ({ userId: x.userId, role: x.role })),
        groupIds: this.groupIds(),
      });
      if (valid() && this.kind() === kind && this.resourceId() === id) {
        this.dialog().nativeElement.close();
        this.saved.emit();
      }
    } catch (error) {
      if (valid()) this.error.set(this.scope.message(error));
    } finally {
      if (valid()) this.saving.set(false);
    }
  }
}
