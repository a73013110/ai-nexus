import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { provideRouter, Router } from '@angular/router';
import { afterEach, describe, expect, it, vi } from 'vitest';
import type { DocumentDto, JobDto, NotificationDto } from '@core/api/schema';
import { WorkspaceSession } from '@core/auth/workspace-session';
import { ToastService, type ToastOptions } from '@shared/ui/toast-service';
import { KnowledgeApi } from '@features/work/knowledge/knowledge-api';
import { NotificationStore } from '@features/platform/notifications/notification-store';
import { JobToasts } from './job-toasts';

const job = (patch: Partial<JobDto> = {}) =>
  ({
    id: 'job-1',
    kind: 'embedding-reindex',
    subjectId: 'profile-1',
    label: '重建 bge-m3 檢索索引',
    status: 'queued',
    stage: '等待處理',
    attempt: 0,
    completedUnits: 0,
    totalUnits: null,
    cancelRequested: false,
    errorCode: null,
    errorMessage: null,
    createdAt: '2026-10-11T08:00:00Z',
    updatedAt: '2026-10-11T08:00:00Z',
    issueCode: null,
    ...patch,
  }) as JobDto;
const event = (status: string, target: string, createdAt = '2026-10-11T08:01:00Z', kind = 'task') =>
  ({
    id: `n-${status}-${target}-${createdAt}`,
    version: 1,
    type: `task.${status}`,
    severity: status === 'failed' ? 'error' : 'success',
    title: '',
    body: '',
    target: { kind, id: target },
    createdAt,
    readAt: null,
    issueCode: null,
  }) as NotificationDto;
const document = (patch: Partial<DocumentDto> = {}) =>
  ({
    id: 'doc-1',
    collectionId: 'c-1',
    fileName: '差旅補充.pdf',
    contentType: 'application/pdf',
    status: 'queued',
    pageCount: 0,
    chunkCount: 0,
    warning: null,
    jobId: 'ingest-1',
    canEdit: true,
    hasOriginal: true,
    textVersion: 0,
    ...patch,
  }) as DocumentDto;

function setup() {
  const items = signal<NotificationDto[]>([]);
  const generation = signal(1);
  const shown: ToastOptions[] = [];
  const knowledge = { document: vi.fn(async () => document({ status: 'ready' })) };
  TestBed.configureTestingModule({
    providers: [
      provideRouter([]),
      { provide: NotificationStore, useValue: { items } },
      { provide: KnowledgeApi, useValue: knowledge },
      { provide: ToastService, useValue: { show: (item: ToastOptions) => shown.push(item) } },
      { provide: WorkspaceSession, useValue: { auth: { generation }, has: () => true } },
    ],
  });
  const toasts = TestBed.inject(JobToasts);
  const navigate = vi.spyOn(TestBed.inject(Router), 'navigateByUrl').mockResolvedValue(true);
  /** 通知收件匣的一次輪詢。 */
  const poll = async (...next: NotificationDto[]) => {
    items.set(next);
    TestBed.tick();
    await Promise.resolve();
    await Promise.resolve();
  };
  return { toasts, items, generation, shown, knowledge, navigate, poll };
}

describe('JobToasts', () => {
  afterEach(() => TestBed.resetTestingModule());

  it('announces a job started here once, keyed by the job id, and ignores others', async () => {
    const { toasts, shown, navigate, poll } = setup();
    toasts.track(job());
    await poll(event('completed', 'someone-else'));
    expect(shown).toEqual([]);
    await poll(event('completed', 'job-1'));
    await poll(event('completed', 'job-1'), event('completed', 'someone-else'));
    toasts.observe(job({ status: 'completed', updatedAt: '2026-10-11T08:02:00Z' }));
    expect(shown).toHaveLength(1);
    expect(shown[0]).toMatchObject({
      id: 'job-1',
      tone: 'success',
      title: '背景任務已完成',
      description: '檢索索引重建：重建 bge-m3 檢索索引',
    });
    shown[0].action!.run();
    expect(navigate).toHaveBeenCalledWith('/tasks?job=job-1');
  });

  it('settles from a page poll before the inbox and links failures to the task', async () => {
    const { toasts, shown, navigate, poll } = setup();
    toasts.track(job());
    toasts.observe(job({ status: 'running' }));
    expect(shown).toEqual([]);
    toasts.observe(job({ status: 'failed', updatedAt: '2026-10-11T08:01:00Z' }));
    await poll(event('failed', 'job-1'));
    expect(shown).toHaveLength(1);
    expect(shown[0]).toMatchObject({ id: 'job-1', tone: 'danger', title: '背景任務需要重試' });
    shown[0].action!.run();
    expect(navigate).toHaveBeenCalledWith('/tasks?job=job-1');
  });

  it('ignores the earlier attempt after a retry and stays quiet when stopped', async () => {
    const { toasts, shown, poll } = setup();
    toasts.track(job({ updatedAt: '2026-10-11T09:00:00Z' }));
    await poll(event('failed', 'job-1', '2026-10-11T08:30:00Z'));
    expect(shown).toEqual([]);
    await poll(event('cancelled', 'job-1', '2026-10-11T09:01:00Z'));
    await poll(event('completed', 'job-1', '2026-10-11T09:02:00Z'));
    expect(shown).toEqual([]);
  });

  it('follows a document from recognition to embedding before announcing it', async () => {
    const { toasts, shown, knowledge, navigate, poll } = setup();
    toasts.trackDocument(document());
    knowledge.document.mockResolvedValueOnce(document({ jobId: 'embed-1', status: 'running' }));
    await poll(event('completed', 'ingest-1'));
    expect(knowledge.document).toHaveBeenCalledWith('doc-1');
    expect(shown).toEqual([]);
    await poll(event('completed', 'ingest-1'), event('completed', 'embed-1'));
    expect(knowledge.document).toHaveBeenCalledTimes(2);
    expect(shown).toHaveLength(1);
    expect(shown[0]).toMatchObject({
      id: 'ingest-1',
      tone: 'success',
      description: '文件辨識與索引：差旅補充.pdf',
    });
    shown[0].action!.run();
    expect(navigate).toHaveBeenCalledWith('/reader/doc-1');
  });

  it('reads the handoff and the result from a page that already polls the document', async () => {
    const { toasts, shown, knowledge, poll } = setup();
    toasts.trackDocument(document());
    toasts.observeDocument(document({ jobId: 'embed-1', status: 'running' }));
    toasts.observeDocument(document({ jobId: 'embed-1', status: 'failed' }));
    await poll(event('failed', 'embed-1'));
    expect(knowledge.document).not.toHaveBeenCalled();
    expect(shown).toHaveLength(1);
    expect(shown[0]).toMatchObject({ id: 'ingest-1', tone: 'danger' });
  });

  it('does not watch a document that is already searchable', async () => {
    const { toasts, shown, poll } = setup();
    toasts.trackDocument(document({ status: 'ready' }));
    await poll(event('completed', 'ingest-1'));
    expect(shown).toEqual([]);
  });

  it('matches a code review by its review and forgets everything on an account switch', async () => {
    const { toasts, shown, generation, poll } = setup();
    toasts.track(job({ id: 'job-r', kind: 'repository-review', subjectId: 'review-1' }));
    toasts.track(job());
    generation.set(2);
    TestBed.tick();
    await poll(event('completed', 'job-1'));
    expect(shown).toEqual([]);
    toasts.track(job({ id: 'job-r', kind: 'repository-review', subjectId: 'review-1' }));
    await poll(event('completed', 'review-1', undefined, 'repository-review'));
    expect(shown).toHaveLength(1);
    expect(shown[0].id).toBe('job-r');
  });
});
