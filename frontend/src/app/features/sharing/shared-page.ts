import { ChangeDetectionStrategy, Component, inject, signal, viewChild } from '@angular/core';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import type { ReadonlyShare, SharedContent } from '../../core/api/types';
import { WorkspaceSession } from '../../core/auth/workspace-session';
import { ViewScope } from '../../shared/browser/view-scope';
import { FeaturePage } from '../../shared/ui/feature-page';
import { MarkdownView } from '../../shared/ui/markdown-view';
import { Icon } from '../../shared/ui/icon';
import { ConfirmDialog } from '../../shared/ui/confirm-dialog';
import { SharingApi } from './sharing-api';
@Component({
  selector: 'nx-shared-page',
  imports: [FeaturePage, MarkdownView, Icon, RouterLink, ConfirmDialog],
  providers: [ViewScope],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `<nx-feature-page
      title="分享"
      description="收到的內容集中閱讀，自己建立的分享可隨時撤銷。"
      ><button
        page-actions
        class="secondary-button"
        [disabled]="loading()"
        (click)="load(route.snapshot.paramMap.get('id'))"
      >
        <nx-icon name="repeat" />重新整理
      </button>
      <div class="page-tabs">
        <button [attr.aria-pressed]="!sent()" (click)="switchTab(false)">分享給我</button
        ><button [attr.aria-pressed]="sent()" (click)="switchTab(true)">我分享的</button>
      </div>
      @if (error()) {
        <p role="alert" class="error-banner">{{ error() }}</p>
      }
      @if (loading()) {
        <p role="status">正在載入分享…</p>
      } @else {
        <div class="share-workspace">
          <nav class="share-list" aria-label="分享清單">
            @for (item of list(); track item.id) {
              <a
                [routerLink]="['/shared', item.id]"
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
            <article class="share-content">
              <div class="share-heading">
                <div>
                  <span class="panel-eyebrow"
                    >唯讀分享{{
                      view.snapshot.artifactVersion
                        ? ' · 版本 ' + view.snapshot.artifactVersion
                        : ''
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
                    ><nx-markdown-view [content]="message.content" />
                    @if (message.attachments.length) {
                      <div class="shared-files">
                        @for (file of message.attachments; track file.id) {
                          <a
                            [href]="'/api/v1/shares/' + view.share.id + '/files/' + file.id"
                            download
                            ><nx-icon name="paperclip" />{{ file.fileName
                            }}<nx-icon name="download"
                          /></a>
                        }
                      </div>
                    }
                  </section>
                }
              }
            </article>
          } @else {
            <section class="artifact-empty">
              <span class="empty-symbol"><nx-icon name="copy" /></span>
              <h2>分享當下的成果</h2>
              <p>只有指定帳號能閱讀。新內容不會自動公開，<br />到期與撤銷會立即停止存取。</p>
            </section>
          }
        </div>
      }</nx-feature-page
    ><nx-confirm-dialog />`,
})
export class SharedPage {
  readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly api = inject(SharingApi);
  private readonly session = inject(WorkspaceSession);
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
    this.route.paramMap.pipe(takeUntilDestroyed()).subscribe((p) => void this.load(p.get('id')));
  }
  date(value: string) {
    return new Intl.DateTimeFormat('zh-TW', { dateStyle: 'medium', timeStyle: 'short' }).format(
      new Date(value),
    );
  }
  expired(value: string) {
    return new Date(value).getTime() <= Date.now();
  }
  async switchTab(sent: boolean) {
    this.sent.set(sent);
    await this.router.navigate(['/shared']);
    await this.load(null);
  }
  async load(id: string | null) {
    const revision = ++this.revision,
      guard = this.scope.guard(),
      valid = () => guard() && revision === this.revision;
    this.loading.set(true);
    this.error.set('');
    this.content.set(null);
    try {
      await this.session.load();
      if (!valid() || !this.session.me()) return;
      if (!this.session.has('shared')) throw new Error('目前沒有分享功能權限。');
      const list = await this.api.list(this.sent());
      if (!valid()) return;
      this.list.set(list);
      if (id) {
        const value = await this.api.get(id);
        if (valid()) {
          this.content.set(value);
          this.watchAccess(id, revision);
        }
      }
    } catch (e) {
      if (valid()) this.error.set(this.scope.message(e));
    } finally {
      if (valid()) this.loading.set(false);
    }
  }
  private watchAccess(id: string, revision: number) {
    const alive = this.scope.guard();
    const valid = () => alive() && revision === this.revision && this.content()?.share.id === id;
    const remaining = new Date(this.content()!.share.expiresAt).getTime() - Date.now();
    this.scope.later(
      () => {
        if (!valid()) return;
        if (remaining <= 30000 && this.expired(this.content()!.share.expiresAt)) {
          this.content.set(null);
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
              this.content.set(null);
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
        this.content.set(null);
        await this.router.navigate(['/shared']);
        await this.load(null);
      }
    } catch (e) {
      if (valid()) this.error.set(this.scope.message(e));
    } finally {
      if (valid()) this.busy.set(false);
    }
  }
}
