import { Card } from '../../shared/ui/card';
import { EmptyState } from '../../shared/ui/empty-state';
import { ViewSwitch } from '../../shared/ui/view-switch';
import { formatDate } from '../../shared/browser/format';
import { ClientValidationError } from '../../core/api/safe-errors';
import { IssueCode } from '../../shared/ui/issue-code';
import { ChangeDetectionStrategy, Component, inject, signal, viewChild } from '@angular/core';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import type { ReadonlyShare, SharedContent } from '../../core/api/types';
import { WorkspaceSession } from '../../core/auth/workspace-session';
import { ViewScope } from '../../shared/browser/view-scope';
import { FeaturePage } from '../../shared/ui/feature-page';
import { MarkdownView } from '../../shared/ui/markdown-view';
import { combineLatest } from 'rxjs';
import { MessageContent } from '../workspace/message-content';
import { ReaderOverlay } from '../../shared/browser/reader-overlay';
import { Icon } from '../../shared/ui/icon';
import { formatModelDisplayName } from '../../shared/browser/format';
import { ConfirmDialog } from '../../shared/ui/confirm-dialog';
import { SharingApi } from './sharing-api';
@Component({
  selector: 'nx-shared-page',
  imports: [
    Card,
    EmptyState,
    ViewSwitch,
    IssueCode,
    FeaturePage,
    MarkdownView,
    Icon,
    RouterLink,
    ConfirmDialog,
    MessageContent,
  ],
  providers: [ViewScope],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `<nx-feature-page
      [title]="session.featureName('shared')"
      description="收到的內容集中閱讀，自己建立的分享可隨時撤銷。"
      ><button
        page-actions
        class="secondary-button"
        [disabled]="loading()"
        (click)="load(route.snapshot.paramMap.get('id'))"
      >
        <nx-icon name="repeat" />重新整理
      </button>
      <nx-view-switch
        label="分享分類"
        [options]="[
          { value: 'received', label: '分享給我' },
          { value: 'sent', label: '我分享的' },
        ]"
        [value]="sent() ? 'sent' : 'received'"
        (valueChange)="switchTab($event === 'sent')"
      />
      @if (error()) {
        <p role="alert" class="error-banner">{{ error() }}<nx-issue-code [message]="error()" /></p>
      }
      @if (loading()) {
        <p role="status">正在載入分享…</p>
      }
      <div class="share-workspace">
        <nav class="share-list" aria-label="分享清單">
          @for (item of list(); track item.id) {
            <a
              [routerLink]="['/shared', item.id]"
              [queryParams]="{ sent: sent() }"
              [attr.aria-current]="content()?.share?.id === item.id ? 'page' : null"
              ><nx-icon [name]="item.kind === 'artifact' ? 'document' : 'lines'" /><span
                ><strong>{{ item.title }}</strong
                ><small>{{
                  item.isRevoked
                    ? '已撤銷'
                    : expired(item.expiresAt)
                      ? '已到期'
                      : item.isOwner
                        ? '分享給 ' + item.recipients.join('、')
                        : item.owner + ' 分享給你'
                }}</small></span
              ></a
            >
          } @empty {
            <p class="form-note">目前沒有分享。可以在對話或成果文件建立具名分享。</p>
          }
        </nav>
        @if (content(); as view) {
          <article nxCard class="share-content">
            <div class="share-heading">
              <div>
                <span class="panel-eyebrow"
                  >唯讀分享{{
                    view.snapshot.artifactVersion ? ' · 版本 ' + view.snapshot.artifactVersion : ''
                  }}</span
                >
                <h2>{{ view.share.title }}</h2>
                <p>{{ view.share.owner }} · 到期 {{ date(view.share.expiresAt) }}</p>
              </div>
              @if (view.share.isOwner) {
                <button class="danger-button" [disabled]="busy()" (click)="revoke(view.share)">
                  撤銷分享
                </button>
              }
            </div>
            @if (view.share.kind === 'artifact') {
              <nx-markdown-view [content]="view.snapshot.content" />
            } @else {
              @for (message of view.snapshot.messages; track $index) {
                <section class="shared-message" [class.shared-user]="message.role === 'user'">
                  <span class="message-author"
                    >{{ message.role === 'user' ? '提問' : 'AI 回答'
                    }}{{
                      message.status !== 'completed'
                        ? ' · ' + (message.status === 'cancelled' ? '已停止' : '未完成')
                        : ''
                    }}</span
                  >
                  <div class="shared-message-meta">
                    @if (message.modelId) {
                      <span class="message-model">{{ modelName(message) }}</span>
                    }
                    <time class="form-note" [attr.datetime]="message.createdAt">{{
                      date(message.createdAt)
                    }}</time>
                  </div>
                  <nx-message-content [message]="message" [shareId]="view.share.id" />
                </section>
              }
            }
          </article>
        } @else {
          <nx-empty-state>
            <span class="empty-symbol"><nx-icon name="copy" /></span>
            <h2>分享當下的成果</h2>
            <p>只有指定帳號能閱讀。新內容不會自動公開，<br />到期與撤銷會立即停止存取。</p>
          </nx-empty-state>
        }
      </div> </nx-feature-page
    ><nx-confirm-dialog />`,
})
export class SharedPage {
  readonly modelName = formatModelDisplayName;
  readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly reader = inject(ReaderOverlay);
  private readonly api = inject(SharingApi);
  readonly session = inject(WorkspaceSession);
  private readonly scope = inject(ViewScope);
  readonly confirm = viewChild.required(ConfirmDialog);
  readonly sent = signal(false);
  readonly list = signal<ReadonlyShare[]>([]);
  readonly content = signal<SharedContent | null>(null);
  readonly loading = signal(true);
  readonly busy = signal(false);
  readonly error = signal('');
  private revision = 0;
  constructor() {
    combineLatest([this.route.paramMap, this.route.queryParamMap])
      .pipe(takeUntilDestroyed())
      .subscribe(([p]) => void this.load(p.get('id')));
  }
  readonly date = formatDate;
  expired(value: string) {
    return new Date(value).getTime() <= Date.now();
  }
  async switchTab(sent: boolean) {
    await this.router.navigate(['/shared'], { queryParams: { sent } });
  }
  async load(id: string | null) {
    const revision = ++this.revision,
      guard = this.scope.guard(),
      valid = () => guard() && revision === this.revision;
    this.loading.set(true);
    this.error.set('');
    if (this.content()?.share.id !== id) this.clearContent();
    try {
      await this.session.load();
      if (!valid() || !this.session.me()) return;
      if (!this.session.has('shared')) throw new ClientValidationError('featureAccess');
      const filter = this.route.snapshot.queryParamMap.get('sent');
      if (filter !== null) this.sent.set(filter === 'true');
      const [detail, listing] = await Promise.allSettled([
        id ? this.api.get(id) : Promise.resolve(null),
        this.api.list(this.sent()),
      ]);
      if (!valid()) return;
      if (listing.status === 'fulfilled') this.list.set(listing.value);
      else this.error.set(this.scope.message(listing.reason));
      if (detail.status === 'rejected') {
        this.clearContent();
        this.error.set(this.scope.message(detail.reason));
        return;
      }
      const value = detail.value;
      if (filter === null && value && value.share.isOwner !== this.sent()) {
        this.sent.set(value.share.isOwner);
        const rows = await this.api.list(this.sent());
        if (!valid()) return;
        this.list.set(rows);
      }
      this.content.set(value);
      if (value) this.watchAccess(value.share.id, revision);
    } catch (e) {
      if (valid()) {
        this.clearContent();
        this.error.set(this.scope.message(e));
      }
    } finally {
      if (valid()) this.loading.set(false);
    }
  }
  private clearContent() {
    if (this.reader.target()?.shareId === this.content()?.share.id) this.reader.close();
    this.content.set(null);
  }
  private watchAccess(id: string, revision: number) {
    const alive = this.scope.guard();
    const valid = () => alive() && revision === this.revision && this.content()?.share.id === id;
    const remaining = new Date(this.content()!.share.expiresAt).getTime() - Date.now();
    this.scope.later(
      () => {
        if (!valid()) return;
        if (remaining <= 30000 && this.expired(this.content()!.share.expiresAt)) {
          this.clearContent();
          this.error.set('分享已到期，內容已收起。');
          return;
        }
        void this.api
          .get(id)
          .then((value) => {
            if (valid()) {
              this.content.set(value);
              this.watchAccess(id, revision);
            }
          })
          .catch((e) => {
            if (valid()) {
              this.clearContent();
              this.error.set(this.scope.message(e));
            }
          });
      },
      Math.max(50, Math.min(30000, remaining)),
      'share-access',
    );
  }
  async revoke(share: ReadonlyShare) {
    if (
      this.busy() ||
      !(await this.confirm().ask({
        title: '撤銷分享',
        message: '指定收件者將立即無法再閱讀或下載附件。原始對話與成果保留。',
        confirm: '撤銷分享',
        danger: true,
      }))
    )
      return;
    const valid = this.scope.guard();
    this.busy.set(true);
    try {
      await this.api.revoke(share.id);
      if (valid()) {
        this.clearContent();
        await this.router.navigate(['/shared'], { queryParams: { sent: this.sent() } });
      }
    } catch (e) {
      if (valid()) this.error.set(this.scope.message(e));
    } finally {
      if (valid()) this.busy.set(false);
    }
  }
}
