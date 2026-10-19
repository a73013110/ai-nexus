import { effect, inject, Injectable, untracked } from '@angular/core';
import { Router } from '@angular/router';
import { routeOf } from '@core/api/catalog';
import type { DocumentDto, JobDto, NotificationDto } from '@core/api/schema';
import { WorkspaceSession } from '@core/auth/workspace-session';
import { ToastService } from '@shared/ui/toast-service';
import { KnowledgeApi } from '@features/work/knowledge/knowledge-api';
import { NotificationStore } from '@features/platform/notifications/notification-store';
import { jobKindName } from './job-kind';

interface Watched {
  /** 正在等待的工作；文件會接續到它的向量化工作。 */
  id: string;
  /** 使用者開始的工作，因此交接或重試時只更新同一則提示。 */
  toast: string;
  kind: string;
  subjectId: string;
  label: string;
  /** 工作（重新）排入佇列的伺服器時間；較早的終結事件屬於前一次嘗試。 */
  since: number;
  document: boolean;
}

const ACTIVE = ['queued', 'running', 'processing', 'indexing', 'building'];

/**
 * 以提示通知此分頁開始的背景工作如何結束。本身不發出請求：殼層的通知輪詢會回報每個結束的工作，
 * 已經在輪詢工作的頁面（工作、知識庫、閱讀器）會更早結束它。在別處開始的工作（其他分頁、
 * 重新整理前、伺服器），
 * 只會出現在收件匣與工作頁，因此同一件事不會被通知兩次。
 */
@Injectable({ providedIn: 'root' })
export class JobToasts {
  private readonly session = inject(WorkspaceSession);
  private readonly toasts = inject(ToastService);
  private readonly router = inject(Router);
  private readonly knowledge = inject(KnowledgeApi);
  private readonly watched = new Map<string, Watched>();

  constructor() {
    const notifications = inject(NotificationStore);
    effect(() => {
      const items = notifications.items();
      untracked(() => items.forEach((item) => this.notified(item)));
    });
    let generation = this.session.auth.generation();
    effect(() => {
      if (this.session.auth.generation() === generation) return;
      generation = this.session.auth.generation();
      this.watched.clear();
    });
  }

  /** 使用者剛在此開始或重試的工作。 */
  track(job: JobDto | null | undefined) {
    if (!job || !ACTIVE.includes(job.status)) return;
    this.watch({
      id: job.id,
      toast: this.watched.get(job.id)?.toast ?? job.id,
      kind: job.kind,
      subjectId: job.subjectId,
      label: job.label,
      since: Date.parse(job.updatedAt) || 0,
      document: job.kind.startsWith('document-'),
    });
  }
  /** 使用者剛新增、編輯或重建索引的知識庫文件。 */
  trackDocument(document: DocumentDto) {
    if (!document.jobId || !ACTIVE.includes(document.status)) return;
    this.watch({
      id: document.jobId,
      toast: document.jobId,
      kind: 'document-ingest',
      subjectId: document.id,
      label: document.fileName,
      since: 0,
      document: true,
    });
  }
  /** 會輪詢工作的頁面提供的較新讀取；可在 effect 中呼叫。 */
  observe(job: JobDto) {
    const watched = this.watched.get(job.id);
    if (watched && Date.parse(job.updatedAt) >= watched.since)
      untracked(() => void this.settle(watched, job.status));
  }
  /** 會輪詢文件的頁面提供的較新讀取；可在 effect 中呼叫。 */
  observeDocument(document: DocumentDto) {
    const watched = [...this.watched.values()].find(
      (item) => item.document && item.subjectId === document.id,
    );
    if (watched) untracked(() => this.documentState(watched, document, false));
  }

  private watch(item: Watched) {
    this.forget(item);
    this.watched.set(item.id, item);
  }
  private forget(item: Watched) {
    for (const [id, old] of this.watched) if (old.toast === item.toast) this.watched.delete(id);
  }
  private notified(item: NotificationDto) {
    if (!item.type.startsWith('task.')) return;
    const watched =
      item.target.kind === 'task'
        ? this.watched.get(item.target.id)
        : [...this.watched.values()].find(
            (item2) => item2.kind === item.target.kind && item2.subjectId === item.target.id,
          );
    if (watched && Date.parse(item.createdAt) >= watched.since)
      void this.settle(watched, item.type.slice('task.'.length));
  }
  private async settle(watched: Watched, status: string) {
    if (!['completed', 'failed', 'cancelled'].includes(status)) return;
    if (this.watched.get(watched.id) !== watched) return;
    this.watched.delete(watched.id);
    // 停止是使用者自己的動作，不需要確認。
    if (status === 'cancelled') return;
    if (status === 'failed' || !watched.document) return this.announce(watched, status);
    // 辨識文件時會把它交給另一個向量化工作；只有文件本身知道
    // 是否已可搜尋。
    const generation = this.session.auth.generation();
    try {
      const document = await this.knowledge.document(watched.subjectId);
      if (generation === this.session.auth.generation())
        this.documentState(watched, document, true);
    } catch {
      /* 文件已被刪除或不再可讀；工作頁仍有該工作。 */
    }
  }
  /** `ended`：被監看的工作已結束，只有交接給另一個工作時才繼續等待。 */
  private documentState(watched: Watched, document: DocumentDto, ended: boolean) {
    const moved = !!document.jobId && document.jobId !== watched.id,
      current = moved ? { ...watched, id: document.jobId! } : watched;
    if (document.status === 'ready' || document.status === 'failed') {
      this.forget(watched);
      this.announce(current, document.status === 'ready' ? 'completed' : 'failed');
    } else if (ACTIVE.includes(document.status) && (moved || !ended)) this.watch(current);
    else this.forget(watched);
  }
  private announce(watched: Watched, status: string) {
    const completed = status === 'completed',
      task = this.session.has('jobs')
        ? `${routeOf('jobs')}?job=${encodeURIComponent(watched.id)}`
        : null,
      subject = encodeURIComponent(watched.subjectId),
      page =
        watched.document && this.session.has('knowledge')
          ? `${routeOf('knowledge.reader')}/${subject}`
          : watched.kind === 'repository-review' && this.session.has('repositories')
            ? `${routeOf('repositories')}?review=${subject}`
            : null,
      destination = (completed ? page : task) ?? task ?? page;
    this.toasts.show({
      id: watched.toast,
      tone: completed ? 'success' : 'danger',
      title: completed ? '背景任務已完成' : '背景任務需要重試',
      description: `${jobKindName(watched.kind)}：${watched.label}`,
      action: destination
        ? { label: '查看', run: () => void this.router.navigateByUrl(destination) }
        : undefined,
    });
  }
}
