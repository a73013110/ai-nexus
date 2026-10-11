import { map } from 'rxjs';
import { apiResource } from '../../core/api/api-resource';
import { Notice } from '../../shared/ui/notice';
import { EmptyState } from '../../shared/ui/empty-state';
import { Card } from '../../shared/ui/card';
import { SearchField } from '../../shared/ui/search-field';
import { Field } from '../../shared/ui/field';
import { ViewSwitch } from '../../shared/ui/view-switch';
import {
  ChangeDetectionStrategy,
  Component,
  DestroyRef,
  ElementRef,
  computed,
  inject,
  signal,
  viewChild,
  linkedSignal,
} from '@angular/core';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { takeUntilDestroyed, toSignal } from '@angular/core/rxjs-interop';
import type { ArtifactDto, ArtifactRevisionDto, ArtifactSummaryDto } from '../../core/api/schema';
import { WorkspaceSession } from '../../core/auth/workspace-session';
import { ViewScope } from '../../shared/browser/view-scope';
import { FeaturePage } from '../../core/layout/feature-page';
import { Icon } from '../../shared/ui/icon';
import { Select } from '../../shared/ui/select';
import { ResourceSharing } from '../sharing/resource-sharing';
import { ConfirmDialog } from '../../shared/ui/confirm-dialog';
import { MarkdownView } from '../../shared/markdown/markdown-view';
import { downloadBlob } from '../../shared/browser/download';
import { formatDate } from '../../shared/browser/format';
import { ArtifactsApi } from './artifacts-api';
import { TextTools } from './text-tools';
import { TEXT_ACTIONS, TEXT_ACTION_ICON_PROVIDER } from './text-actions';
import { ShareDialog } from '../sharing/share-dialog';

@Component({
  selector: 'nx-artifacts-page',
  imports: [
    Notice,
    ViewSwitch,
    EmptyState,
    Card,
    SearchField,
    Field,
    FeaturePage,
    Icon,
    Select,
    ResourceSharing,
    ConfirmDialog,
    TextTools,
    RouterLink,
    MarkdownView,
    ShareDialog,
  ],
  providers: [ViewScope, TEXT_ACTION_ICON_PROVIDER],
  changeDetection: ChangeDetectionStrategy.OnPush,
  styleUrl: './artifacts-page.scss',
  templateUrl: './artifacts-page.html',
})
export class ArtifactsPage {
  readonly textActions = TEXT_ACTIONS;
  private readonly api = inject(ArtifactsApi);
  private readonly scope = inject(ViewScope);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  readonly session = inject(WorkspaceSession);
  private readonly id = toSignal(this.route.paramMap.pipe(map((p) => p.get('id'))), {
    initialValue: null,
  });
  private readonly listRead = apiResource({
    feature: 'artifacts',
    loader: () => this.api.list(),
  });
  private readonly documentRead = apiResource({
    feature: 'artifacts',
    params: () => this.id() || undefined,
    loader: (id) => this.api.get(id),
  });
  private readonly revisionsRead = apiResource({
    feature: 'artifacts',
    params: () => this.id() || undefined,
    loader: async (id) => ({ id, rows: await this.api.versions(id) }),
  });
  readonly list = computed<ArtifactSummaryDto[]>(() => this.listRead.value() ?? []);
  /** The document on screen: the one in the URL, or a version or save that replaced it. */
  readonly document = linkedSignal<ArtifactDto | null>(() => {
    const value = this.documentRead.value();
    return value && value.resource.id === this.id() ? value : null;
  });
  readonly revisions = computed<ArtifactRevisionDto[]>(() => {
    const value = this.revisionsRead.value();
    return value && value.id === this.id() ? value.rows : [];
  });
  readonly loading = computed(() => this.listRead.loading() || this.documentRead.loading());
  readonly busy = signal(false);
  readonly actionError = signal('');
  readonly error = computed(
    () =>
      this.actionError() ||
      this.listRead.error() ||
      this.documentRead.error() ||
      this.revisionsRead.error(),
  );
  readonly notice = signal('');
  readonly filter = signal('');
  // Editing state starts over whenever another document or version is loaded or saved.
  readonly mode = linkedSignal(() => {
    this.document();
    return 'read';
  });
  readonly title = linkedSignal(() => this.document()?.resource.name ?? '');
  readonly content = linkedSignal(() => this.document()?.content ?? '');
  readonly restoring = linkedSignal(() => {
    this.document();
    return false;
  });
  readonly selection = linkedSignal<{ start: number; end: number; text: string } | null>(() => {
    this.document();
    return null;
  });
  readonly listOpen = linkedSignal(() => {
    this.document();
    return false;
  });
  readonly confirm = viewChild.required(ConfirmDialog);
  readonly readonlyShare = viewChild(ShareDialog);
  readonly tools = viewChild.required(TextTools);
  readonly sharing = viewChild(ResourceSharing);
  readonly editor = viewChild<ElementRef<HTMLTextAreaElement>>('editor');
  readonly visible = computed(() =>
    this.list().filter((x) => x.resource.name.toLowerCase().includes(this.filter().toLowerCase())),
  );
  readonly dirty = computed(
    () =>
      !!this.document() &&
      (this.restoring() ||
        this.title() !== this.document()!.resource.name ||
        this.content() !== this.document()!.content),
  );
  readonly editable = computed(
    () =>
      this.document()?.resource.canEdit &&
      (this.document()?.version === this.document()?.currentVersion || this.restoring()),
  );
  readonly modeOptions = computed(() => [
    { value: 'read', label: '閱讀' },
    ...(this.editable()
      ? [
          { value: 'edit', label: '編輯' },
          { value: 'split', label: '並排預覽' },
        ]
      : []),
  ]);
  readonly choices = computed(() =>
    this.revisions().map((x) => ({
      value: String(x.version),
      label: `版本 ${x.version}`,
      description: `${x.author} · ${formatDate(x.createdAt)}`,
    })),
  );
  private version = 0;
  private replaceTarget: { start: number; end: number; text: string } | null = null;
  private operationGuard() {
    const generation = this.version,
      valid = this.scope.guard();
    return () => valid() && generation === this.version;
  }
  constructor() {
    this.route.paramMap.pipe(takeUntilDestroyed()).subscribe(() => {
      ++this.version;
      this.actionError.set('');
      this.notice.set('');
    });
    const unload = (event: BeforeUnloadEvent) => {
      if (this.dirty()) event.preventDefault();
    };
    const key = (event: KeyboardEvent) => {
      if (
        (event.ctrlKey || event.metaKey) &&
        event.key.toLowerCase() === 's' &&
        !event.isComposing
      ) {
        event.preventDefault();
        if (this.dirty() && !this.busy()) void this.save();
      }
    };
    window.addEventListener('beforeunload', unload);
    document.addEventListener('keydown', key);
    inject(DestroyRef).onDestroy(() => {
      window.removeEventListener('beforeunload', unload);
      document.removeEventListener('keydown', key);
    });
  }
  async save() {
    const doc = this.document(),
      valid = this.operationGuard();
    if (!doc || !this.editable() || this.busy() || !this.dirty()) return;
    this.busy.set(true);
    this.actionError.set('');
    try {
      const value = await this.api.save(
        doc.resource.id,
        this.title(),
        this.content(),
        doc.currentVersion,
      );
      if (!valid()) return;
      const priorMode = this.mode();
      this.document.set(value);
      this.mode.set(priorMode);
      this.notice.set(`版本 ${value.version} 已儲存。`);
      this.revisionsRead.reload();
      this.listRead.reload();
    } catch (error) {
      if (valid()) this.actionError.set(this.scope.message(error));
    } finally {
      if (valid()) this.busy.set(false);
    }
  }
  async chooseVersion(version: string) {
    if (this.busy() || !(await this.canLeave())) return;
    const id = this.document()?.resource.id,
      valid = this.operationGuard();
    if (!id) return;
    this.busy.set(true);
    this.actionError.set('');
    try {
      const value = await this.api.get(id, Number(version));
      if (valid() && this.document()?.resource.id === id) this.document.set(value);
    } catch (error) {
      if (valid()) this.actionError.set(this.scope.message(error));
    } finally {
      if (valid()) this.busy.set(false);
    }
  }
  restore() {
    this.restoring.set(true);
    this.mode.set('edit');
    this.notice.set('舊版本內容已帶入編輯；儲存後會建立新版本。');
  }
  async discard() {
    if (!(await this.canLeave())) return;
    const doc = this.document();
    if (doc) this.document.set({ ...doc });
  }
  canLeave(): boolean | Promise<boolean> {
    return (
      !this.busy() &&
      (!this.dirty() ||
        this.confirm().ask({
          title: '尚有未儲存的編輯',
          message: '目前編輯尚未保存。離開後會捨棄本次修改，既有版本仍保留。',
          confirm: '捨棄本次修改',
        }))
    );
  }
  pick() {
    const editor = this.editor()?.nativeElement;
    if (!editor || editor.selectionEnd <= editor.selectionStart) {
      this.selection.set(null);
      return;
    }
    this.selection.set({
      start: editor.selectionStart,
      end: editor.selectionEnd,
      text: this.content().slice(editor.selectionStart, editor.selectionEnd),
    });
  }
  transform(action: string) {
    const selected = this.selection();
    if (!selected || selected.text.length > 8000) return;
    this.replaceTarget = selected;
    this.tools().open(selected.text, action);
  }
  replace(text: string) {
    const target = this.replaceTarget;
    if (!target || this.content().slice(target.start, target.end) !== target.text) {
      this.actionError.set('選取內容已經改變，請重新選取後再套用。');
      return;
    }
    this.content.set(
      this.content().slice(0, target.start) + text + this.content().slice(target.end),
    );
    this.selection.set(null);
  }
  async export(format: string) {
    const doc = this.document(),
      valid = this.operationGuard();
    if (!doc || this.busy()) return;
    this.busy.set(true);
    this.actionError.set('');
    try {
      const response = await this.api.export(doc.resource.id, format, doc.version),
        blob = await response.blob();
      if (valid()) {
        downloadBlob(blob, doc.resource.name + '-v' + doc.version, format);
        this.notice.set(`已匯出已儲存的版本 ${doc.version}。`);
      }
    } catch (error) {
      if (valid()) this.actionError.set(this.scope.message(error));
    } finally {
      if (valid()) this.busy.set(false);
    }
  }
  async remove() {
    const doc = this.document();
    if (
      !doc ||
      this.busy() ||
      !(await this.confirm().ask({
        title: '移除成果文件',
        message: '將永久移除此成果的內容及所有版本，已授權的成員也無法再閱讀。',
        confirm: '移除文件',
        danger: true,
      }))
    )
      return;
    const valid = this.operationGuard();
    this.busy.set(true);
    this.actionError.set('');
    try {
      await this.api.remove(doc.resource.id);
      if (valid()) {
        this.document.set(null);
        this.busy.set(false);
        await this.router.navigate(['/artifacts']);
      }
    } catch (error) {
      if (valid()) this.actionError.set(this.scope.message(error));
    } finally {
      if (valid()) this.busy.set(false);
    }
  }
}
