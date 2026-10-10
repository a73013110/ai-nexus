import { Notice } from '../../shared/ui/notice';
import { CompactDialog } from '../../shared/ui/compact-dialog';
import { Field } from '../../shared/ui/field';
import {
  ChangeDetectionStrategy,
  Component,
  ElementRef,
  inject,
  input,
  signal,
  viewChild,
} from '@angular/core';
import { RouterLink } from '@angular/router';
import type { DirectoryUserDto, ShareDto } from '../../core/api/schema';
import { ResourceApi } from '../../core/api/resource-api';
import { WorkspaceSession } from '../../core/auth/workspace-session';
import { ViewScope } from '../../shared/browser/view-scope';
import { CopyFeedback } from '../../shared/browser/copy-feedback';
import { Select } from '../../shared/ui/select';
import { Icon } from '../../shared/ui/icon';
import { SharingApi } from './sharing-api';
import { Checkbox } from '../../shared/ui/checkbox';
@Component({
  selector: 'nx-share-dialog',
  imports: [Notice, CompactDialog, Field, Select, Icon, RouterLink, Checkbox],
  providers: [ViewScope, CopyFeedback],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `<dialog
    nxCompactDialog
    #dialog
    class="platform-dialog"
    (cancel)="busy() && $event.preventDefault()"
  >
    <div class="dialog-scroll">
      <div class="dialog-heading">
        <div>
          <h2>建立唯讀分享</h2>
          <p>{{ name() }}</p>
        </div>
        <button
          class="icon-button"
          aria-label="關閉分享視窗"
          [disabled]="busy()"
          (click)="dialog.close()"
        >
          <nx-icon name="close" />
        </button>
      </div>
      @if (error()) {
        <nx-notice tone="danger" [message]="error()" />
      }
      @if (created(); as share) {
        <div class="share-created">
          <nx-icon name="check" />
          <h3>分享已建立</h3>
          <p>指定帳號登入後，即可從「分享」找到內容。<br />後續修改不會自動公開。</p>
          <button class="primary-button" (click)="copy.copy(link(share.id))">
            {{ copy.copied() ? '已複製連結' : '複製分享連結' }}</button
          ><a [routerLink]="['/shared', share.id]" (click)="dialog.close()" class="secondary-button"
            >檢視分享</a
          >
        </div>
      } @else {
        <nx-notice tone="warning">
          {{
            kind() === 'artifact' ? '分享選取的已儲存成果版本' : '分享目前可見的對話分支'
          }}。收件者只能閱讀，無法修改原始內容；需要更新時請建立新的分享。
        </nx-notice>
        <div class="platform-form">
          <label
            >收件者<input
              nxField
              type="search"
              aria-label="搜尋分享收件者"
              placeholder="輸入至少兩個字，搜尋公司帳號"
              maxlength="120"
              [disabled]="busy()"
              [value]="search()"
              (input)="find($any($event.target).value)"
          /></label>
          <div class="share-recipients">
            @for (user of recipients(); track user.id) {
              <button class="share-recipient" [disabled]="busy()" (click)="remove(user.id)">
                {{ user.displayName }}<nx-icon name="close" />
              </button>
            }
          </div>
          @if (search().trim().length >= 2) {
            <div class="directory-results">
              @for (user of results(); track user.id) {
                <button
                  class="directory-user"
                  [disabled]="busy() || recipients().length >= 20"
                  (click)="add(user)"
                >
                  <span
                    ><strong>{{ user.displayName }}</strong
                    ><small>{{ user.account }}</small></span
                  ><nx-icon name="plus" />
                </button>
              } @empty {
                <p class="form-note">尚無符合且未加入的帳號。</p>
              }
            </div>
          }
          <label
            >有效期限<nx-select
              label="分享有效期限"
              [options]="durations"
              [value]="hours()"
              [disabled]="busy()"
              (valueChange)="hours.set($event)"
          /></label>
          @if (kind() === 'conversation') {
            <nx-checkbox
              label="包含此分支的附件"
              description="收件者可下載；到期或撤銷後停止存取。"
              [checked]="includeAttachments()"
              [disabled]="busy()"
              (checkedChange)="includeAttachments.set($event)"
            />
          }
          <p class="form-note">最多 20 位收件者。建立後可在「我分享的」隨時撤銷。</p>
          <div class="dialog-actions">
            <button class="secondary-button" [disabled]="busy()" (click)="dialog.close()">
              取消</button
            ><button
              class="primary-button"
              [disabled]="busy() || !recipients().length"
              (click)="create()"
            >
              {{ busy() ? '正在建立…' : '建立分享' }}
            </button>
          </div>
        </div>
      }
    </div>
  </dialog>`,
})
export class ShareDialog {
  readonly kind = input.required<string>();
  readonly sourceId = input.required<string>();
  readonly name = input('');
  readonly version = input<number | null>(null);
  readonly dialog = viewChild.required<ElementRef<HTMLDialogElement>>('dialog');
  readonly recipients = signal<DirectoryUserDto[]>([]);
  readonly results = signal<DirectoryUserDto[]>([]);
  readonly search = signal('');
  readonly hours = signal('168');
  readonly includeAttachments = signal(false);
  readonly created = signal<ShareDto | null>(null);
  readonly busy = signal(false);
  readonly error = signal('');
  readonly copy = inject(CopyFeedback);
  private readonly api = inject(SharingApi);
  private readonly directory = inject(ResourceApi);
  private readonly session = inject(WorkspaceSession);
  private readonly scope = inject(ViewScope);
  private request = 0;
  private context?: { kind: string; id: string; version: number | null };
  readonly durations = [
    { value: '1', label: '1 小時' },
    { value: '24', label: '1 天' },
    { value: '168', label: '7 天' },
    { value: '720', label: '30 天' },
  ];
  open() {
    if (this.busy()) return;
    ++this.request;
    this.context = { kind: this.kind(), id: this.sourceId(), version: this.version() };
    this.created.set(null);
    this.error.set('');
    this.recipients.set([]);
    this.search.set('');
    this.results.set([]);
    this.includeAttachments.set(false);
    this.dialog().nativeElement.showModal();
  }
  link(id: string) {
    return `${location.origin}/shared/${id}`;
  }
  add(user: DirectoryUserDto) {
    if (!this.recipients().some((x) => x.id === user.id) && this.recipients().length < 20)
      this.recipients.update((all) => [...all, user]);
    this.results.update((all) => all.filter((x) => x.id !== user.id));
  }
  remove(id: string) {
    this.recipients.update((all) => all.filter((x) => x.id !== id));
  }
  find(value: string) {
    this.search.set(value);
    const sequence = ++this.request,
      valid = this.scope.guard();
    if (value.trim().length < 2) {
      this.results.set([]);
      return;
    }
    this.scope.later(
      () => {
        void this.directory
          .directory(value.trim())
          .then((all) => {
            if (valid() && sequence === this.request)
              this.results.set(
                all.filter(
                  (x) =>
                    x.id !== this.session.me()?.id && !this.recipients().some((u) => u.id === x.id),
                ),
              );
          })
          .catch((e) => {
            if (valid() && sequence === this.request) this.error.set(this.scope.message(e));
          });
      },
      250,
      'share-directory',
    );
  }
  async create() {
    if (this.busy() || !this.recipients().length || !this.context) return;
    const context = this.context,
      alive = this.scope.guard();
    const valid = () => alive() && context === this.context && context.id === this.sourceId();
    this.busy.set(true);
    this.error.set('');
    try {
      const value = await this.api.create({
        kind: context.kind,
        sourceId: context.id,
        recipientIds: this.recipients().map((x) => x.id),
        hours: Number(this.hours()),
        includeAttachments: this.includeAttachments(),
        artifactVersion: context.version,
      });
      if (valid()) this.created.set(value);
    } catch (e) {
      if (valid()) this.error.set(this.scope.message(e));
    } finally {
      if (alive()) this.busy.set(false);
    }
  }
}
