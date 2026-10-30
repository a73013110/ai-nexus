import { map } from 'rxjs';
import { apiResource } from '@core/api/api-resource';
import { Notice } from '@shared/ui/notice';
import { EmptyState } from '@shared/ui/empty-state';
import { formatDate, formatModelDisplayName, relativeTime } from '@shared/browser/format';
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
import type { ShareDto, SharedContentDto } from '@core/api/schema';
import { WorkspaceSession } from '@core/auth/workspace-session';
import { ViewScope } from '@shared/browser/view-scope';
import { FeaturePage } from '@core/layout/feature-page';
import { MarkdownView } from '@shared/markdown/markdown-view';
import { MessageContent } from '@features/work/conversations/message-content';
import { ReaderOverlay } from '@shared/browser/reader-overlay';
import { Icon } from '@shared/ui/icon';
import { ConfirmDialog } from '@shared/ui/confirm-dialog';
import { ActionMenu, type MenuAction } from '@shared/ui/action-menu';
import { Panel } from '@shared/ui/panel';
import { PendingButton } from '@shared/ui/pending-button';
import { ResourceCard } from '@shared/ui/resource-card';
import { ResourceGallery } from '@shared/ui/resource-gallery';
import { StatusBadge } from '@shared/ui/status-badge';
import type { BadgeTone } from '@shared/ui/count-badge';
import { SharingApi } from './sharing-api';
import { shareTour, sharesTour } from './shared-tour';
import { routeOf } from '@core/api/catalog';

@Component({
  selector: 'nx-shared-page',
  styleUrl: './shared-page.scss',
  imports: [
    Notice,
    EmptyState,
    FeaturePage,
    MarkdownView,
    Icon,
    RouterLink,
    ConfirmDialog,
    MessageContent,
    ActionMenu,
    Panel,
    PendingButton,
    ResourceCard,
    ResourceGallery,
    StatusBadge,
  ],
  providers: [ViewScope],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './shared-page.html',
})
export class SharedPage {
  /** 範本裡的連結由功能目錄取得網址。 */
  readonly routeOf = routeOf;
  readonly modelName = formatModelDisplayName;
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly reader = inject(ReaderOverlay);
  private readonly api = inject(SharingApi);
  readonly session = inject(WorkspaceSession);
  private readonly scope = inject(ViewScope);
  private readonly confirm = viewChild.required(ConfirmDialog);
  readonly id = toSignal(this.route.paramMap.pipe(map((part) => part.get('id'))), {
    initialValue: null,
  });
  private readonly sentParam = toSignal(
    this.route.queryParamMap.pipe(map((part) => part.get('sent'))),
    { initialValue: null },
  );
  /** 分享過期前會再檢查一次，且至少每 30 秒檢查一次。 */
  private readonly contentRead = apiResource({
    feature: 'sharing',
    params: () => this.id() || undefined,
    loader: (id) => this.api.get(id),
    poll: (value) =>
      value
        ? Math.max(50, Math.min(30000, new Date(value.share.expiresAt).getTime() - Date.now()))
        : null,
  });
  /** 檢查失敗時立即隱藏內容：分享可能已被撤銷或過期。 */
  readonly content = computed<SharedContentDto | null>(() => {
    const value = this.contentRead.value();
    return value && value.share.id === this.id() ? value : null;
  });
  /** 連結中的分頁；沒有時用已開啟分享的那一側，再沒有則用上一次顯示的。 */
  readonly sent = linkedSignal<{ param: string | null; owner: boolean | undefined }, boolean>({
    source: () => ({ param: this.sentParam(), owner: this.content()?.share.isOwner }),
    computation: (source, previous) =>
      source.param !== null ? source.param === 'true' : (source.owner ?? previous?.value ?? false),
  });
  private readonly listRead = apiResource({
    feature: 'sharing',
    params: () => this.sent(),
    loader: (sent) => this.api.list(sent),
  });
  private readonly list = computed<ShareDto[]>(() => this.listRead.value() ?? []);
  readonly listLoading = this.listRead.loading;
  readonly listRefreshing = this.listRead.refreshing;
  /** 使用者看得到的重讀（重試）；分享到期前的輪詢不算。 */
  readonly refreshing = computed(() => this.contentRead.refreshing() || this.listRead.refreshing());
  readonly contentLoading = this.contentRead.loading;
  readonly term = signal('');
  readonly scopes = [
    { value: 'received', label: '分享給我' },
    { value: 'sent', label: '我分享的' },
  ];
  readonly visible = computed(() =>
    this.list().filter((item) => item.title.toLowerCase().includes(this.term().toLowerCase())),
  );
  readonly shareActions = computed<MenuAction[]>(() =>
    this.content()?.share.isOwner
      ? [{ id: 'revoke', label: '撤銷分享', icon: 'stop', danger: true, disabled: this.busy() }]
      : [],
  );
  /** 集合的導覽，或已開啟分享的導覽（依其內容與目前檢視者決定）。 */
  readonly tour = computed(() => {
    const share = this.content()?.share;
    if (this.id())
      return share
        ? shareTour({
            artifact: share.kind === 'artifact',
            attachments: share.includeAttachments,
            manage: this.shareActions().length > 0,
          })
        : null;
    return sharesTour({
      chat: this.session.has('chat'),
      artifacts: this.session.has('artifacts'),
      cards: !this.listLoading() && this.visible().length > 0,
    });
  });
  /** 撤銷進行中：分享選單鈕轉圈。 */
  readonly busy = signal(false);
  private readonly actionError = signal('');
  readonly error = computed(
    () => this.actionError() || this.contentRead.error() || this.listRead.error(),
  );
  constructor() {
    // 閱讀器的覆蓋層絕不會繼續顯示本頁已不再顯示的分享。
    let shown: string | null = null;
    effect(() => {
      const id = this.content()?.share.id ?? null;
      if (shown && shown !== id && untracked(this.reader.target)?.scope?.id === shown)
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
  readonly when = relativeTime;
  /** 無法再讀取的分享會在卡片上註明。 */
  state(share: ShareDto): { label: string; tone: BadgeTone } | null {
    if (share.isRevoked) return { label: '已撤銷', tone: 'neutral' };
    if (new Date(share.expiresAt).getTime() <= Date.now())
      return { label: '已到期', tone: 'warning' };
    return null;
  }
  shareAction(action: string) {
    const share = this.content()?.share;
    if (action === 'revoke' && share) void this.revoke(share);
  }
  async switchTab(sent: boolean) {
    await this.router.navigate([routeOf('sharing')], { queryParams: { sent } });
  }
  private async revoke(share: ShareDto) {
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
        await this.router.navigate([routeOf('sharing')], { queryParams: { sent: this.sent() } });
      }
    } catch (failure) {
      if (valid()) this.actionError.set(this.scope.message(failure));
    } finally {
      if (valid()) this.busy.set(false);
    }
  }
}
