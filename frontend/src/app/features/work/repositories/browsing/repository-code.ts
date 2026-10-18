import {
  ChangeDetectionStrategy,
  Component,
  computed,
  effect,
  inject,
  signal,
  untracked,
  viewChild,
} from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { apiResource } from '@core/api/api-resource';
import type { RepositoryEntryDto } from '@core/api/schema';
import { WorkspaceSession } from '@core/auth/workspace-session';
import { formatBytes } from '@shared/browser/format';
import { ViewScope } from '@shared/browser/view-scope';
import { Icon } from '@shared/ui/icon';
import { Notice } from '@shared/ui/notice';
import { ToastService } from '@shared/ui/toast-service';
import { RepositoriesApi } from '../repositories-api';
import { RepositoryFile } from './repository-file';
import { RepositoryImportDialog } from '../handoff/repository-import-dialog';
import { baseName } from './repository-language';
import { RepositoryStore } from '../repository-store';

/** 對話一次能接受的檔案數；伺服器的上限相同。 */
const chatLimit = 20;

/**
 * 釘選提交的資料夾與檔案。檔案可跨資料夾挑選，並以一個附件交給新對話；資料夾可以匯入知識庫集合。
 */
@Component({
  selector: 'nx-repository-code',
  imports: [RouterLink, Icon, Notice, RepositoryFile, RepositoryImportDialog],
  providers: [ViewScope],
  changeDetection: ChangeDetectionStrategy.OnPush,
  styleUrl: './repository-code.scss',
  template: `<div class="trail">
      <nav class="crumbs" aria-label="目前位置">
        <a [routerLink]="store.link()" queryParamsHandling="merge" [queryParams]="at('')">
          <nx-icon name="git" />{{ store.key()?.name }}</a
        >
        @for (crumb of crumbs(); track crumb.path; let last = $last) {
          <nx-icon name="chevron" class="sep" />
          @if (last) {
            <span aria-current="page">{{ crumb.name }}</span>
          } @else {
            <a
              [routerLink]="store.link()"
              queryParamsHandling="merge"
              [queryParams]="at(crumb.path)"
              >{{ crumb.name }}</a
            >
          }
        }
      </nav>
      @if (!filePath() && canImport()) {
        <button
          type="button"
          class="quiet-button repository-import"
          (click)="importer().open(folder(), store.commit())"
        >
          <nx-icon name="library" />匯入知識庫
        </button>
      }
    </div>
    @if (error()) {
      <nx-notice [message]="error()"
        ><button notice-actions type="button" (click)="retry()">重試</button></nx-notice
      >
    }
    @if (filePath()) {
      @if (file(); as file) {
        <nx-repository-file [file]="file">
          @if (canChat()) {
            <button
              file-actions
              type="button"
              class="quiet-button"
              [disabled]="busy()"
              (click)="chat([file.path])"
            >
              <nx-icon name="chat" />帶入對話
            </button>
          }
        </nx-repository-file>
      } @else if (!error()) {
        <div class="skeleton file-skeleton" role="status" aria-label="正在讀取檔案"></div>
      }
    } @else {
      <section class="entries" aria-label="檔案" [class.stale]="treeStale()">
        @if (tree(); as tree) {
          @for (entry of tree.entries; track entry.path; let i = $index) {
            <div
              class="row"
              [class.picked]="picked().has(entry.path)"
              [style.--i]="i < 16 ? i : 16"
            >
              @if (canChat()) {
                @if (entry.kind === 'file') {
                  <input
                    type="checkbox"
                    class="pick"
                    [attr.aria-label]="'選取 ' + entry.name"
                    [checked]="picked().has(entry.path)"
                    [disabled]="!picked().has(entry.path) && picked().size >= limit"
                    (change)="toggle(entry.path)"
                  />
                } @else {
                  <span class="pick" aria-hidden="true"></span>
                }
              }
              @if (entry.kind === 'dir' || entry.kind === 'file') {
                <a
                  class="entry"
                  [routerLink]="store.link()"
                  queryParamsHandling="merge"
                  [queryParams]="entry.kind === 'dir' ? at(entry.path) : open(entry.path)"
                >
                  <nx-icon
                    [name]="entry.kind === 'dir' ? 'folder' : 'document'"
                    [class.dir]="entry.kind === 'dir'"
                  />
                  <span class="entry-name">{{ entry.name }}</span>
                  @if (entry.kind === 'file') {
                    <span class="size">{{ bytes(entry.size) }}</span>
                  } @else {
                    <nx-icon name="chevron" class="go" />
                  }
                </a>
              } @else {
                <span class="entry inert" [title]="kindName(entry)">
                  <nx-icon name="external" /><span class="entry-name">{{ entry.name }}</span
                  ><span class="size">{{ kindName(entry) }}</span>
                </span>
              }
            </div>
          } @empty {
            <p class="blank">這個資料夾是空的。</p>
          }
        } @else if (!error()) {
          @for (row of [1, 2, 3, 4, 5]; track row) {
            <span class="skeleton row-skeleton"></span>
          }
        }
      </section>
      @if (tree()?.readme; as readme) {
        <nx-repository-file class="readme" [file]="readme" [compact]="true" />
      }
    }
    @if (picked().size) {
      <div class="selection" role="region" aria-label="已選取的檔案">
        <span class="count"
          ><strong>{{ picked().size }}</strong> / {{ limit }} 個檔案</span
        >
        <span class="names">{{ pickedNames() }}</span>
        <button type="button" class="quiet-button" [disabled]="busy()" (click)="picked.set(empty)">
          清除
        </button>
        <button
          type="button"
          class="primary-button"
          [disabled]="busy()"
          (click)="chat([...picked()])"
        >
          <nx-icon name="chat" />{{ busy() ? '正在整理…' : '帶入對話' }}
        </button>
      </div>
    }
    <nx-repository-import-dialog />`,
})
export class RepositoryCode {
  readonly store = inject(RepositoryStore);
  private readonly api = inject(RepositoriesApi);
  private readonly session = inject(WorkspaceSession);
  private readonly scope = inject(ViewScope);
  private readonly toast = inject(ToastService);
  private readonly query = toSignal(inject(ActivatedRoute).queryParamMap, { requireSync: true });
  readonly importer = viewChild.required(RepositoryImportDialog);
  readonly limit = chatLimit;
  readonly empty: ReadonlySet<string> = new Set();
  readonly bytes = formatBytes;
  readonly folder = computed(() => this.query().get('path') ?? '');
  readonly filePath = computed(() => this.query().get('file') ?? '');
  readonly canChat = computed(() => this.session.has('chat'));
  readonly canImport = computed(() => this.session.has('knowledge'));
  readonly crumbs = computed(() => {
    const parts = (this.filePath() || this.folder()).split('/').filter(Boolean);
    return parts.map((name, index) => ({ name, path: parts.slice(0, index + 1).join('/') }));
  });
  private readonly treeRead = apiResource({
    feature: 'repositories',
    params: () => {
      const key = this.store.key(),
        commit = this.store.commit();
      return key && commit && !this.filePath() ? { key, commit, path: this.folder() } : undefined;
    },
    loader: async ({ key, commit, path }) => ({
      id: `${commit}:${path}`,
      value: await this.api.tree(key, commit, path),
    }),
  });
  /** 上次讀取的資料夾；下一個載入時它會保留並變暗。 */
  readonly tree = computed(() => this.treeRead.value()?.value ?? null);
  readonly treeStale = computed(
    () => this.treeRead.value()?.id !== `${this.store.commit()}:${this.folder()}`,
  );
  private readonly fileRead = apiResource({
    feature: 'repositories',
    params: () => {
      const key = this.store.key(),
        commit = this.store.commit(),
        path = this.filePath();
      return key && commit && path ? { key, commit, path } : undefined;
    },
    loader: ({ key, commit, path }) => this.api.file(key, commit, path),
  });
  readonly file = computed(() => {
    const file = this.fileRead.value();
    return file && file.path === this.filePath() && file.commit === this.store.commit()
      ? file
      : null;
  });
  readonly picked = signal<ReadonlySet<string>>(this.empty);
  readonly pickedNames = computed(
    () =>
      [...this.picked()].map(baseName).slice(0, 3).join('、') + (this.picked().size > 3 ? '…' : ''),
  );
  readonly busy = signal(false);
  private readonly actionError = signal('');
  readonly error = computed(
    () =>
      this.actionError() || (this.filePath() ? this.fileRead.error() : this.treeRead.error()) || '',
  );

  constructor() {
    // 挑選只屬於同一個儲存庫的同一個提交。
    effect(() => {
      this.store.fullName();
      this.store.commit();
      untracked(() => this.picked.set(this.empty));
    });
  }

  at = (path: string) => ({ path: path || null, file: null });
  open = (path: string) => ({ file: path, path: null });
  kindName = (entry: RepositoryEntryDto) => (entry.kind === 'submodule' ? '子模組' : '連結');

  toggle(path: string) {
    this.picked.update((current) => {
      const next = new Set(current);
      if (!next.delete(path) && next.size < chatLimit) next.add(path);
      return next;
    });
  }
  retry() {
    this.actionError.set('');
    if (this.filePath()) this.fileRead.reload();
    else this.treeRead.reload();
  }
  async chat(paths: string[]) {
    const commit = this.store.commit();
    if (!paths.length || !commit || this.busy()) return;
    const valid = this.scope.guard();
    this.busy.set(true);
    this.actionError.set('');
    try {
      const result = await this.store.chat({ commit, paths, issue: null, pull: null }, valid);
      if (result?.skipped.length)
        this.toast.show({
          tone: 'warning',
          title: `${result.skipped.length} 個檔案沒有帶入`,
          description: '非文字或超過大小上限的檔案會略過：' + result.skipped.slice(0, 3).join('、'),
        });
    } catch (error2) {
      if (valid()) this.actionError.set(this.scope.message(error2));
    } finally {
      if (valid()) this.busy.set(false);
    }
  }
}
