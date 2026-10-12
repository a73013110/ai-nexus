import { map } from 'rxjs';
import { apiResource } from '../../core/api/api-resource';
import { Notice } from '../../shared/ui/notice';
import { EmptyState } from '../../shared/ui/empty-state';
import { formatDate, formatModelDisplayName, relativeTime } from '../../shared/browser/format';
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
import { ConfirmDialog } from '../../shared/ui/confirm-dialog';
import { ActionMenu, type MenuAction } from '../../shared/ui/action-menu';
import { Panel } from '../../shared/ui/panel';
import { ResourceCard } from '../../shared/ui/resource-card';
import { ResourceGallery } from '../../shared/ui/resource-gallery';
import { StatusBadge } from '../../shared/ui/status-badge';
import type { BadgeTone } from '../../shared/ui/count-badge';
import { SharingApi } from './sharing-api';

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
    ResourceCard,
    ResourceGallery,
    StatusBadge,
  ],
  providers: [ViewScope],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './shared-page.html',
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
  readonly id = toSignal(this.route.paramMap.pipe(map((p) => p.get('id'))), {
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
  readonly listLoading = this.listRead.loading;
  readonly contentLoading = this.contentRead.loading;
  readonly term = signal('');
  readonly scopes = [
    { value: 'received', label: '分享給我' },
    { value: 'sent', label: '我分享的' },
  ];
  readonly visible = computed(() =>
    this.list().filter((x) => x.title.toLowerCase().includes(this.term().toLowerCase())),
  );
  readonly shareActions = computed<MenuAction[]>(() =>
    this.content()?.share.isOwner
      ? [{ id: 'revoke', label: '撤銷分享', icon: 'stop', danger: true, disabled: this.busy() }]
      : [],
  );
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
  readonly when = relativeTime;
  /** Shares that can no longer be read say so on their card. */
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
