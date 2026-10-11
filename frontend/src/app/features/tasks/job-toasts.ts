import { effect, inject, Injectable, untracked } from '@angular/core';
import { Router } from '@angular/router';
import type { DocumentDto, JobDto, NotificationDto } from '../../core/api/schema';
import { WorkspaceSession } from '../../core/auth/workspace-session';
import { ToastService } from '../../shared/ui/toast-service';
import { KnowledgeApi } from '../knowledge/knowledge-api';
import { NotificationStore } from '../notifications/notification-store';
import { jobKindName } from './job-kind';

interface Watched {
  /** The job being waited on; a document moves on to its embedding job. */
  id: string;
  /** The job the person started, so a handoff or retry updates one toast. */
  toast: string;
  kind: string;
  subjectId: string;
  label: string;
  /** Server time the job was (re)queued; older terminal events belong to an earlier attempt. */
  since: number;
  document: boolean;
}

const ACTIVE = ['queued', 'running', 'processing', 'indexing', 'building'];

/**
 * Announces, as a toast, how the background jobs this tab started ended. It adds no requests:
 * the shell's notification poll reports every finished job, and pages that already poll a job
 * (tasks, knowledge, reader) settle it sooner. Jobs started elsewhere — another tab, before a
 * reload, by the server — are only in the inbox and the tasks page, so nothing is announced twice.
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

  /** A job the person just started or retried here. */
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
  /** A knowledge document the person just added, edited or reindexed. */
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
  /** A fresher read of a job by a page that polls it anyway; safe to call from an effect. */
  observe(job: JobDto) {
    const watched = this.watched.get(job.id);
    if (watched && Date.parse(job.updatedAt) >= watched.since)
      untracked(() => void this.settle(watched, job.status));
  }
  /** A fresher read of a document by a page that polls it anyway; safe to call from an effect. */
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
            (x) => x.kind === item.target.kind && x.subjectId === item.target.id,
          );
    if (watched && Date.parse(item.createdAt) >= watched.since)
      void this.settle(watched, item.type.slice('task.'.length));
  }
  private async settle(watched: Watched, status: string) {
    if (!['completed', 'failed', 'cancelled'].includes(status)) return;
    if (this.watched.get(watched.id) !== watched) return;
    this.watched.delete(watched.id);
    // Stopping is the person's own act; it needs no confirmation.
    if (status === 'cancelled') return;
    if (status === 'failed' || !watched.document) return this.announce(watched, status);
    // Recognising a document hands it to a separate embedding job; only the document says
    // whether it can be searched yet.
    const generation = this.session.auth.generation();
    try {
      const document = await this.knowledge.document(watched.subjectId);
      if (generation === this.session.auth.generation())
        this.documentState(watched, document, true);
    } catch {
      /* The document was removed or is no longer readable; the tasks page still has the job. */
    }
  }
  /** `ended`: the watched job has finished, so only a handoff to another job keeps waiting. */
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
      task = this.session.has('tasks') ? `/tasks?job=${encodeURIComponent(watched.id)}` : null,
      subject = encodeURIComponent(watched.subjectId),
      page =
        watched.document && this.session.has('knowledge')
          ? `/reader/${subject}`
          : watched.kind === 'repository-review' && this.session.has('repositories')
            ? `/repositories?review=${subject}`
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
