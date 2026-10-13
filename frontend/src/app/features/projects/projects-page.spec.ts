import { ApplicationRef, signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { RouterTestingHarness } from '@angular/router/testing';
import { afterEach, beforeAll, describe, expect, it, vi } from 'vitest';
import type { ProjectDto } from '../../core/api/schema';
import { AuthService } from '../../core/auth/auth-service';
import { WorkspaceSession } from '../../core/auth/workspace-session';
import { optionalId } from '../../core/layout/optional-id';
import { ConversationDraftTransfer } from '../../core/preferences/conversation-draft-transfer';
import { ToastService } from '../../shared/ui/toast-service';
import { ArtifactsApi } from '../artifacts/artifacts-api';
import { KnowledgeApi } from '../knowledge/knowledge-api';
import { WorkspaceApi } from '../workspace/workspace-api';
import { ProjectsApi } from './projects-api';
import { ProjectsPage } from './projects-page';

const project = (id: string) =>
  ({
    resource: { id, name: id, isOwner: true, canEdit: true, updatedAt: '2026-10-13T00:00:00Z' },
    description: '',
    instructions: '',
    version: 1,
    isArchived: false,
  }) as unknown as ProjectDto;

async function setup(url: string, rows: ProjectDto[]) {
  const generation = signal(1);
  // The server's list; writes below change it like the backend would.
  let server = [...rows];
  const api = {
    list: vi.fn(async () => [...server]),
    get: vi.fn(async (id: string) => server.find((x) => x.resource.id === id) ?? project(id)),
    save: vi.fn(async (id: string | null, name: string) => {
      const saved = project(id ?? name);
      server = [saved, ...server.filter((x) => x.resource.id !== saved.resource.id)];
      return saved;
    }),
    remove: vi.fn(async (id: string) => {
      server = server.filter((x) => x.resource.id !== id);
    }),
    files: vi.fn(async () => []),
    templates: vi.fn(async () => []),
  };
  TestBed.configureTestingModule({
    providers: [
      provideRouter([{ matcher: optionalId('projects'), component: ProjectsPage }]),
      { provide: ProjectsApi, useValue: api },
      { provide: WorkspaceApi, useValue: { attachmentPolicy: async () => ({}) } },
      { provide: KnowledgeApi, useValue: {} },
      { provide: ArtifactsApi, useValue: {} },
      { provide: ConversationDraftTransfer, useValue: {} },
      { provide: ToastService, useValue: { success: vi.fn(), show: vi.fn(), dismiss: vi.fn() } },
      {
        provide: WorkspaceSession,
        useValue: {
          auth: { generation },
          load: async () => true,
          has: (feature: string) => feature === 'projects',
        },
      },
      { provide: AuthService, useValue: { generation, guard: AuthService.prototype.guard } },
    ],
  });
  // The list and write logic is under test; the full template needs layout APIs jsdom lacks.
  TestBed.overrideComponent(ProjectsPage, {
    set: { template: '<dialog #dialog></dialog>', imports: [] },
  });
  const harness = await RouterTestingHarness.create();
  const page = await harness.navigateByUrl(url, ProjectsPage);
  // Deleting asks first; the confirmation itself is covered elsewhere.
  Object.assign(page, { confirm: () => ({ ask: async () => true }) });
  const settle = () => TestBed.inject(ApplicationRef).whenStable();
  await settle();
  const ids = () => page.projects().map((x) => x.resource.id);
  return { api, harness, page, settle, ids };
}

describe('ProjectsPage', () => {
  beforeAll(() => {
    HTMLDialogElement.prototype.showModal ??= function (this: HTMLDialogElement) {
      this.setAttribute('open', '');
    };
    HTMLDialogElement.prototype.close ??= function (this: HTMLDialogElement) {
      this.removeAttribute('open');
    };
  });
  afterEach(() => TestBed.resetTestingModule());

  it('lists a created project when returning to the list on the same page', async () => {
    const { harness, page, settle, ids } = await setup('/projects', [project('old')]);
    expect(ids()).toEqual(['old']);
    page.openProject();
    page.name.set('new');
    await page.save(new Event('submit'));
    await settle();
    expect(page.id()).toBe('new');
    expect(await harness.navigateByUrl('/projects', ProjectsPage)).toBe(page);
    await settle();
    expect(ids()).toEqual(['new', 'old']);
  });

  it('drops a deleted project from the list it returns to', async () => {
    const { api, page, settle, ids } = await setup('/projects/gone', [
      project('gone'),
      project('kept'),
    ]);
    expect(page.current()?.resource.id).toBe('gone');
    await page.removeProject();
    expect(api.remove).toHaveBeenCalledWith('gone');
    expect(page.id()).toBeNull();
    expect(ids()).toEqual(['kept']);
    await settle();
    expect(ids()).toEqual(['kept']);
  });
});
