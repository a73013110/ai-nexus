import { map } from 'rxjs';
import { apiResource } from '../../core/api/api-resource';
import { Notice } from '../../shared/ui/notice';
import { Card } from '../../shared/ui/card';
import { EmptyState } from '../../shared/ui/empty-state';
import { ViewSwitch } from '../../shared/ui/view-switch';
import { formatDate } from '../../shared/browser/format';
import {
  ChangeDetectionStrategy,
  Component,
  inject,
  signal,
  viewChild,
  linkedSignal,
  untracked,
  computed,
  effect,
} from '@angular/core';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { toSignal } from '@angular/core/rxjs-interop';
import type { ShareDto, SharedContentDto } from '../../core/api/schema';
import { WorkspaceSession } from '../../core/auth/workspace-session';
import { ViewScope } from '../../shared/browser/view-scope';
import { FeaturePage } from '../../core/layout/feature-page';
import { MarkdownView } from '../../shared/markdown/markdown-view';
import { MessageContent } from '../workspace/message-content';
import { ReaderOverlay } from '../../shared/browser/reader-overlay';
import { Icon } from '../../shared/ui/icon';
import { formatModelDisplayName } from '../../shared/browser/format';
import { ConfirmDialog } from '../../shared/ui/confirm-dialog';
import { SharingApi } from './sharing-api';
@Component({
  selector: 'nx-shared-page',
  styleUrl: './shared-page.scss',
  imports: [
    Notice,
    Card,
    EmptyState,
    ViewSwitch,
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
      ><button page-actions class="secondary-button" [disabled]="loading()" (click)="load()">
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
        <nx-notice tone="danger" [message]="error()" />
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
  private readonly id = toSignal(this.route.paramMap.pipe(map((p) => p.get('id'))), {
    initialValue: null,
  });
  private readonly sentParam = toSignal(this.route.queryParamMap.pipe(map((p) => p.get('sent'))), {
    initialValue: null,
  });
  /** Access is checked again before the share expires and at least every 30 seconds. */
  private readonly contentRead = apiResource({
    feature: 'shared',
    params: () => this.id() || undefined,
    loader: (id) => this.api.get(id),
    poll: (value) =>
      value
        ? Math.max(50, Math.min(30000, new Date(value.share.expiresAt).getTime() - Date.now()))
        : null,
  });
  /** A failed check hides the content at once: the share may have been revoked or expired. */
  readonly content = computed<SharedContentDto | null>(() => {
    const value = this.contentRead.value();
    return value && value.share.id === this.id() ? value : null;
  });
  /** The tab in the link, else the side of the open share, else the last one shown. */
  readonly sent = linkedSignal<{ param: string | null; owner: boolean | undefined }, boolean>({
    source: () => ({ param: this.sentParam(), owner: this.content()?.share.isOwner }),
    computation: (source, previous) =>
      source.param !== null ? source.param === 'true' : (source.owner ?? previous?.value ?? false),
  });
  private readonly listRead = apiResource({
    feature: 'shared',
    params: () => this.sent(),
    loader: (sent) => this.api.list(sent),
  });
  readonly list = computed<ShareDto[]>(() => this.listRead.value() ?? []);
  readonly loading = computed(() => this.listRead.loading() || this.contentRead.loading());
  readonly busy = signal(false);
  readonly actionError = signal('');
  readonly error = computed(
    () => this.actionError() || this.contentRead.error() || this.listRead.error(),
  );
  constructor() {
    // The reader overlay never keeps showing a share this page no longer shows.
    let shown: string | null = null;
    effect(() => {
      const id = this.content()?.share.id ?? null;
      if (shown && shown !== id && untracked(this.reader.target)?.shareId === shown)
        untracked(() => this.reader.close());
      shown = id;
    });
  }
  load() {
    this.actionError.set('');
    this.contentRead.reload();
    this.listRead.reload();
  }
  readonly date = formatDate;
  expired(value: string) {
    return new Date(value).getTime() <= Date.now();
  }
  async switchTab(sent: boolean) {
    await this.router.navigate(['/shared'], { queryParams: { sent } });
  }
  async revoke(share: ShareDto) {
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
        this.contentRead.value.set(undefined);
        this.listRead.reload();
        await this.router.navigate(['/shared'], { queryParams: { sent: this.sent() } });
      }
    } catch (e) {
      if (valid()) this.actionError.set(this.scope.message(e));
    } finally {
      if (valid()) this.busy.set(false);
    }
  }
}
