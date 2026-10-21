import { ApplicationRef, signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { afterEach, describe, expect, it, vi } from 'vitest';
import type { PromptTemplateDto } from '@core/api/schema';
import { AuthService } from '@core/auth/auth-service';
import { WorkspaceSession } from '@core/auth/workspace-session';
import { ClientIssues } from '@core/errors/client-issues';
import { ProductTour, tourSteps } from '@shared/ui/product-tour';
import { PromptLibraryDialog } from './prompt-library-dialog';
import { PromptsApi } from './prompts-api';
import { promptLibraryTour } from './prompt-library-tour';

const titles = (definition: Parameters<typeof tourSteps>[0]) =>
  tourSteps(definition).map((step) => step.title);

describe('promptLibraryTour', () => {
  it('covers applying, creating, editing and deleting personal templates', () => {
    expect(titles(promptLibraryTour({ editing: false, testing: false }))).toEqual([
      '你的常用範本',
      '套用到輸入框',
      '新增範本',
      '編輯範本',
      '刪除範本',
      '撰寫範本',
    ]);
  });

  it('explains overwriting while an existing template is in the editor', () => {
    const steps = tourSteps(promptLibraryTour({ editing: true, testing: false }));
    expect(steps.at(-1)!.title).toBe('正在編輯');
    expect(steps.at(-1)!.description).toContain('覆寫');
  });

  it('says the templates belong to the tested user while testing an identity', () => {
    const [list] = tourSteps(promptLibraryTour({ editing: false, testing: true }));
    expect(list.description).toContain('該使用者');
    expect(
      tourSteps(promptLibraryTour({ editing: false, testing: false }))[0].description,
    ).toContain('只有你自己');
  });

  it('scopes every target to the dialog', () => {
    for (const editing of [false, true])
      for (const step of tourSteps(promptLibraryTour({ editing, testing: false })))
        expect(step.target).toMatch(/^nx-prompt-library /);
  });
});

describe('prompt library tour targets', () => {
  afterEach(() => {
    TestBed.resetTestingModule();
    vi.unstubAllGlobals();
  });

  it('finds every target in both the create and edit states', async () => {
    vi.stubGlobal('matchMedia', () => ({
      matches: false,
      addEventListener: () => undefined,
      removeEventListener: () => undefined,
    }));
    // jsdom 沒有 showModal；這裡只需要對話框內容渲染出來。
    HTMLDialogElement.prototype.showModal ??= function (this: HTMLDialogElement) {
      this.open = true;
    };
    const generation = signal(1);
    const prompt: PromptTemplateDto = {
      id: 't1',
      title: '會議摘要',
      content: '整理會議重點與待辦。',
      updatedAt: '2026-01-01T00:00:00Z',
    };
    TestBed.configureTestingModule({
      providers: [
        { provide: PromptsApi, useValue: { prompts: vi.fn(async () => [prompt]) } },
        {
          provide: WorkspaceSession,
          useValue: { auth: { generation }, testing: signal(false), load: vi.fn(async () => true) },
        },
        { provide: AuthService, useValue: { generation, guard: AuthService.prototype.guard } },
        { provide: ProductTour, useValue: { loading: signal(false), active: signal(false) } },
        { provide: ClientIssues, useValue: {} },
      ],
    });
    const fixture = TestBed.createComponent(PromptLibraryDialog);
    const settle = async () => {
      await TestBed.inject(ApplicationRef).whenStable();
      fixture.detectChanges();
    };
    const dialog = fixture.componentInstance;
    const host = fixture.nativeElement as HTMLElement;
    const expectTargets = () => {
      for (const step of tourSteps(dialog.tour())) {
        const selector = step.target.replace(/^nx-prompt-library /, '');
        expect(host.querySelector(selector), step.target).not.toBeNull();
      }
    };
    await settle();
    dialog.open();
    await settle();
    expect(host.querySelector('nx-product-tour-button')).not.toBeNull();
    expect(titles(dialog.tour()).at(-1)).toBe('撰寫範本');
    expectTargets();
    dialog.compose(dialog.prompts()[0]);
    await settle();
    expect(titles(dialog.tour()).at(-1)).toBe('正在編輯');
    expectTargets();
  });
});
