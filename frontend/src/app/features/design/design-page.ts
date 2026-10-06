import {
  ChangeDetectionStrategy,
  Component,
  computed,
  ElementRef,
  inject,
  signal,
  viewChild,
} from '@angular/core';
import { RouterLink } from '@angular/router';
import { WorkspaceSession } from '../../core/auth/workspace-session';
import type { Job } from '../../core/api/types';
import { ViewScope } from '../../shared/browser/view-scope';
import { downloadFile } from '../../shared/browser/download';
import { FeaturePage } from '../../shared/ui/feature-page';
import { Icon } from '../../shared/ui/icon';
import { Select, type SelectOption } from '../../shared/ui/select';
import { ActionMenu, type MenuAction } from '../../shared/ui/action-menu';
import { ConfirmDialog } from '../../shared/ui/confirm-dialog';
import { InlineTitle } from '../../shared/ui/inline-title';
import { JobProgress } from '../../shared/ui/job-progress';
import { MarkdownView } from '../../shared/ui/markdown-view';
import { Checkbox } from '../../shared/ui/checkbox';
import { SearchField } from '../../shared/ui/search-field';
import { GenerationIndicator } from '../../shared/ui/generation-indicator';
import { InferenceSignal } from '../../shared/ui/inference-signal';
import { generationStatus } from '../../core/api/generation-status';

const sampleTitle = '把想法，整理成可用的成果';
const jobStates: SelectOption[] = [
  { value: 'completed', label: '已完成' },
  { value: 'queued', label: '等待處理' },
  { value: 'failed', label: '需要重試' },
  { value: 'cancelled', label: '已取消' },
  { value: 'running', label: '處理中（示範播放）', disabled: true },
];

/** Uses production components; all samples are local and perform no model or database calls. */
@Component({
  selector: 'nx-design-page',
  imports: [
    RouterLink,
    FeaturePage,
    Icon,
    Select,
    ActionMenu,
    ConfirmDialog,
    InlineTitle,
    JobProgress,
    MarkdownView,
    Checkbox,
    SearchField,
    GenerationIndicator,
    InferenceSignal,
  ],
  providers: [ViewScope],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './design-page.html',
})
export class DesignPage {
  readonly session = inject(WorkspaceSession);
  private readonly scope = inject(ViewScope);
  readonly loading = signal(true);
  readonly error = signal('');
  readonly theme = signal<'light' | 'dark'>('light');
  readonly title = signal(sampleTitle);
  readonly titleVersion = signal(0);
  readonly selection = signal('standard');
  readonly checked = signal(true);
  readonly search = signal('');
  readonly notice = signal('');
  readonly states = jobStates;
  readonly generation = computed(() => generationStatus(this.demo().status, false));
  readonly demo = signal<Job>({
    id: 'design-sample',
    subjectId: 'design-sample',
    kind: 'demo',
    label: '狀態元件示範',
    status: 'completed',
    stage: '處理完成',
    completedUnits: 3,
    totalUnits: 3,
    attempt: 1,
    cancelRequested: false,
    errorCode: null,
    errorMessage: null,
    createdAt: new Date().toISOString(),
    updatedAt: new Date().toISOString(),
  });
  private demoVersion = 0;
  readonly choices: SelectOption[] = [
    { value: 'standard', label: '標準閱讀', description: '清楚留白，適合一般文件' },
    { value: 'wide', label: '寬幅閱讀', description: '為表格與程式碼保留空間' },
    { value: 'unavailable', label: '未開放選項', description: '停用狀態示範', disabled: true },
  ];
  readonly actions: MenuAction[] = [
    { id: 'edit', label: '修改示範標題', icon: 'edit' },
    { id: 'export', label: '未開放操作', icon: 'download', disabled: true },
    { id: 'reset', label: '重設示範', icon: 'restore', danger: true },
  ];
  readonly swatches = [
    { token: '--canvas', label: '頁面', purpose: '工作區底色' },
    { token: '--surface', label: '閱讀面', purpose: '卡片與彈出視窗' },
    { token: '--accent', label: '主要操作', purpose: '重點與目前位置' },
    { token: '--signal', label: '處理訊號', purpose: '進度與完成' },
    { token: '--error', label: '需要注意', purpose: '錯誤文字' },
    { token: '--danger-surface', label: '危險操作', purpose: '搭配獨立前景色' },
  ];
  readonly markdown =
    '### 好的回答，也需要好的閱讀介面\n\n文字有清楚的層次，資料有可以核對的來源。**段落、表格與程式碼**沿用同一套閱讀元件。\n\n| 項目 | 用途 |\n| --- | --- |\n| 文件來源 | 核對原始資訊 |\n| 成果版本 | 保存每次異動 |\n\n```typescript\nconst workspace = { clarity: true };\n```\n\n> 先讓使用者看得清楚，再讓操作自然發生。';
  readonly confirmation = viewChild.required(ConfirmDialog);
  readonly editable = viewChild.required(InlineTitle);
  readonly preview = viewChild.required<ElementRef<HTMLElement>>('preview');
  readonly saveTitle = async (value: string) => {
    this.title.set(value);
    return true;
  };
  constructor() {
    void this.load();
  }
  async load() {
    const guard = this.scope.guard();
    this.loading.set(true);
    this.error.set('');
    try {
      await this.session.load();
    } catch (error) {
      if (guard()) this.error.set(this.scope.message(error));
    } finally {
      if (guard()) this.loading.set(false);
    }
  }
  action(id: string) {
    if (id === 'edit') this.editable().begin();
    if (id === 'reset') void this.reset();
  }
  async reset() {
    const guard = this.scope.guard();
    if (
      !(await this.confirmation().ask({
        title: '重設介面示範？',
        message: '這只會還原此頁的示範標題、選項及進度。',
        confirm: '重設示範',
        danger: true,
      })) ||
      !guard()
    )
      return;
    this.titleVersion.update((x) => x + 1);
    this.title.set(sampleTitle);
    this.selection.set('standard');
    this.setState('completed');
    this.notice.set('示範已重設。');
  }
  setState(status: string) {
    this.demoVersion++;
    const stage = jobStates.find((x) => x.value === status)?.label ?? '處理中';
    this.demo.update((x) => ({
      ...x,
      status,
      stage,
      completedUnits: status === 'completed' ? 3 : 0,
      errorMessage: status === 'failed' ? '示範：來源暫時無法使用，可重新處理。' : null,
    }));
  }
  play() {
    const version = ++this.demoVersion;
    this.demo.update((x) => ({
      ...x,
      status: 'running',
      stage: '讀取文件',
      completedUnits: 0,
      errorMessage: null,
    }));
    ['整理文字', '建立索引', '處理完成'].forEach((stage, i) =>
      this.scope.later(
        () => {
          if (version !== this.demoVersion) return;
          this.demo.update((x) => ({
            ...x,
            stage,
            completedUnits: i + 1,
            status: i === 2 ? 'completed' : 'running',
          }));
        },
        (i + 1) * 700,
        `design-stage-${i}`,
      ),
    );
  }
  exportTokens() {
    const style = getComputedStyle(this.preview().nativeElement);
    const names = [
      ...this.swatches.map((x) => x.token),
      '--ink',
      '--secondary',
      '--accent-text',
      '--danger-text',
      '--focus',
      '--text-body',
      '--text-ui',
      '--text-caption',
      '--button-target',
      '--motion',
    ];
    const tokens = Object.fromEntries(
      names.map((name) => [name, style.getPropertyValue(name).trim()]),
    );
    downloadFile(
      JSON.stringify({ theme: this.theme(), tokens }, null, 2),
      `ai-nexus-tokens-${this.theme()}`,
      'json',
    );
  }
}
