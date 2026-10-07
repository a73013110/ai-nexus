import { ClientValidationError } from '../../core/api/safe-errors';
import { IssueCode } from '../../shared/ui/issue-code';
import {
  ChangeDetectionStrategy,
  Component,
  DestroyRef,
  ElementRef,
  computed,
  inject,
  signal,
  viewChild,
} from '@angular/core';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import type { ArtifactDocument, ArtifactSummary, ArtifactRevision } from '../../core/api/types';
import { WorkspaceSession } from '../../core/auth/workspace-session';
import { ViewScope } from '../../shared/browser/view-scope';
import { FeaturePage } from '../../shared/ui/feature-page';
import { Icon } from '../../shared/ui/icon';
import { Select } from '../../shared/ui/select';
import { ResourceSharing } from '../../shared/ui/resource-sharing';
import { ConfirmDialog } from '../../shared/ui/confirm-dialog';
import { MarkdownView } from '../../shared/ui/markdown-view';
import { downloadBlob } from '../../shared/browser/download';
import { ArtifactsApi } from './artifacts-api';
import { TextTools } from './text-tools';
import { ShareDialog } from '../sharing/share-dialog';

@Component({
  selector: 'nx-artifacts-page',
  imports: [IssueCode,
    FeaturePage,
    Icon,
    Select,
    ResourceSharing,
    ConfirmDialog,
    TextTools,
    RouterLink,
    MarkdownView, ShareDialog,
  ],
  providers: [ViewScope],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './artifacts-page.html',
})
export class ArtifactsPage {
  private readonly api = inject(ArtifactsApi);
  private readonly scope = inject(ViewScope);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  readonly session = inject(WorkspaceSession);
  readonly list = signal<ArtifactSummary[]>([]);
  readonly document = signal<ArtifactDocument | null>(null);
  readonly revisions = signal<ArtifactRevision[]>([]);
  readonly loading = signal(true);
  readonly busy = signal(false);
  readonly error = signal('');
  readonly notice = signal('');
  readonly filter = signal('');
  readonly mode = signal('read');
  readonly title = signal('');
  readonly content = signal('');
  readonly restoring = signal(false);
  readonly selection = signal<{ start: number; end: number; text: string } | null>(null);
  readonly listOpen = signal(false);
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
  readonly choices = computed(() =>
    this.revisions().map((x) => ({
      value: String(x.version),
      label: `版本 ${x.version}`,
      description: `${x.author} · ${new Intl.DateTimeFormat('zh-TW', { dateStyle: 'short', timeStyle: 'short' }).format(new Date(x.createdAt))}`,
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
    this.route.paramMap
      .pipe(takeUntilDestroyed())
      .subscribe((params) => void this.load(params.get('id')));
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
  async load(id: string | null) {
    const version = ++this.version,
      guard = this.scope.guard(),
      valid = () => guard() && version === this.version;
    this.loading.set(true);
    this.error.set('');
    this.notice.set('');
    this.selection.set(null);
    try {
      await this.session.load();
      if (!valid() || !this.session.me()) return;
      if (!this.session.has('artifacts')) throw new ClientValidationError('featureAccess');
      const list = await this.api.list();
      if (!valid()) return;
      this.list.set(list);
      if (id) {
        const value = await this.api.get(id);
        if (!valid()) return;
        this.adopt(value);
        const revisions = await this.api.versions(id);
        if (valid()) this.revisions.set(revisions);
      } else {
        this.document.set(null);
        this.revisions.set([]);
      }
    } catch (error) {
      if (valid()) {
        this.document.set(null);
        this.error.set(this.scope.message(error));
      }
    } finally {
      if (valid()) this.loading.set(false);
    }
  }
  private adopt(value: ArtifactDocument) {
    this.document.set(value);
    this.title.set(value.resource.name);
    this.content.set(value.content);
    this.restoring.set(false);
    this.selection.set(null);
    this.mode.set('read');
    this.listOpen.set(false);
  }
  async save() {
    const doc = this.document(),
      valid = this.operationGuard();
    if (!doc || !this.editable() || this.busy() || !this.dirty()) return;
    this.busy.set(true);
    this.error.set('');
    try {
      const value = await this.api.save(
        doc.resource.id,
        this.title(),
        this.content(),
        doc.currentVersion,
      );
      if (!valid()) return;
      const priorMode = this.mode();
      this.adopt(value);
      this.mode.set(priorMode);
      this.notice.set(`版本 ${value.version} 已儲存。`);
      const [revisions, list] = await Promise.all([
        this.api.versions(doc.resource.id),
        this.api.list(),
      ]);
      if (valid()) {
        this.revisions.set(revisions);
        this.list.set(list);
      }
    } catch (error) {
      if (valid()) this.error.set(this.scope.message(error));
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
    this.error.set('');
    try {
      const value = await this.api.get(id, Number(version));
      if (valid() && this.document()?.resource.id === id) this.adopt(value);
    } catch (error) {
      if (valid()) this.error.set(this.scope.message(error));
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
    if (doc) this.adopt(doc);
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
      this.error.set('選取內容已經改變，請重新選取後再套用。');
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
    this.error.set('');
    try {
      const response = await this.api.export(doc.resource.id, format, doc.version),
        blob = await response.blob();
      if (valid()) {
        downloadBlob(blob, doc.resource.name + '-v' + doc.version, format);
        this.notice.set(`已匯出已儲存的版本 ${doc.version}。`);
      }
    } catch (error) {
      if (valid()) this.error.set(this.scope.message(error));
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
    this.error.set('');
    try {
      await this.api.remove(doc.resource.id);
      if (valid()) {
        this.document.set(null);
        this.busy.set(false);
        await this.router.navigate(['/artifacts']);
      }
    } catch (error) {
      if (valid()) this.error.set(this.scope.message(error));
    } finally {
      if (valid()) this.busy.set(false);
    }
  }
}
