import { ChangeDetectionStrategy, Component, computed, inject, input } from '@angular/core';
import { Router } from '@angular/router';
import { CopyFeedback } from '../../shared/browser/copy-feedback';
import { EmptyState } from '../../shared/ui/empty-state';
import { Icon } from '../../shared/ui/icon';
import { Notice } from '../../shared/ui/notice';
import { Select, type SelectOption } from '../../shared/ui/select';
import { ViewMotion } from '../../shared/ui/view-motion';
import { ViewSwitch, type ViewOption } from '../../shared/ui/view-switch';
import { RepositoryChange } from './repository-change';
import { RepositoryCode } from './repository-code';
import { RepositoryCommits } from './repository-commits';
import { RepositoryReview } from './repository-review';
import { RepositoryReviews } from './repository-reviews';
import { isSha, RepositoryStore, shortSha } from './repository-store';
import { RepositoryThread } from './repository-thread';
import { RepositoryThreads } from './repository-threads';

/** One repository: the ref it is pinned to, its sections, and the section in the URL. */
@Component({
  selector: 'nx-repository-detail',
  imports: [
    EmptyState,
    Icon,
    Notice,
    Select,
    ViewMotion,
    ViewSwitch,
    RepositoryChange,
    RepositoryCode,
    RepositoryCommits,
    RepositoryReview,
    RepositoryReviews,
    RepositoryThread,
    RepositoryThreads,
  ],
  providers: [CopyFeedback],
  changeDetection: ChangeDetectionStrategy.OnPush,
  styleUrl: './repository-detail.scss',
  template: `@if (store.detail(); as detail) {
      <div class="bar">
        <nav class="sections" aria-label="程式庫內容">
          <nx-view-switch
            label="程式庫內容"
            [options]="sections()"
            [value]="section()"
            (valueChange)="open($event)"
          />
        </nav>
        <div class="ref">
          @if (detail.repository.private) {
            <span class="flag"><nx-icon name="lock" />私人</span>
          }
          @if (detail.repository.archived) {
            <span class="flag">已封存</span>
          }
          @if (!detail.repository.empty) {
            <nx-select
              class="ref-select"
              label="分支或標籤"
              appearance="pill"
              [searchable]="refs().length > 8"
              [options]="refs()"
              [value]="store.refName()"
              (valueChange)="selectRef($event)"
            />
            @if (store.commit(); as commit) {
              <button
                type="button"
                class="sha"
                [title]="'固定版本 ' + commit + '（點一下複製）'"
                (click)="copy.copy(commit)"
              >
                <nx-icon [name]="copy.copied() ? 'check' : 'commit'" />{{ short(commit) }}
              </button>
            }
          }
        </div>
      </div>
      @if (store.refMissing()) {
        <nx-notice tone="warning" [message]="'找不到分支或標籤「' + store.ref() + '」。'"
          ><button notice-actions type="button" (click)="selectRef('')">
            改看預設分支
          </button></nx-notice
        >
      }
      @if (detail.repository.empty) {
        <nx-empty-state>
          <nx-icon name="git" />
          <h2>這個程式庫還沒有任何提交</h2>
          <p>推送第一個 commit 後，就能在這裡閱讀程式與紀錄。</p>
        </nx-empty-state>
      } @else {
        <div class="content" [nxViewMotion]="section() + '/' + (item() ? 'item' : 'list')">
          @switch (section()) {
            @case ('commits') {
              @if (view() === 'compare' && range(); as range) {
                <nx-repository-change [head]="range.head" [base]="range.base" />
              } @else if (item()) {
                <nx-repository-change [head]="item()" />
              } @else {
                <nx-repository-commits />
              }
            }
            @case ('pulls') {
              @if (number()) {
                <nx-repository-thread kind="pulls" [number]="number()" />
              } @else {
                <nx-repository-threads kind="pulls" />
              }
            }
            @case ('issues') {
              @if (number()) {
                <nx-repository-thread kind="issues" [number]="number()" />
              } @else {
                <nx-repository-threads kind="issues" />
              }
            }
            @case ('reviews') {
              @if (item()) {
                <nx-repository-review [id]="item()" />
              } @else {
                <nx-repository-reviews />
              }
            }
            @default {
              <nx-repository-code />
            }
          }
        </div>
      }
    } @else if (store.loading()) {
      <div class="loading" role="status" aria-label="正在讀取程式庫">
        <span class="skeleton bar-skeleton"></span><span class="skeleton body-skeleton"></span>
      </div>
    }`,
})
export class RepositoryDetail {
  readonly view = input('');
  readonly item = input('');
  readonly store = inject(RepositoryStore);
  readonly copy = inject(CopyFeedback);
  private readonly router = inject(Router);
  readonly short = shortSha;
  readonly section = computed(() => {
    const view = this.view();
    return view === 'compare'
      ? 'commits'
      : ['commits', 'pulls', 'issues', 'reviews'].includes(view)
        ? view
        : 'code';
  });
  readonly number = computed(() => (/^\d{1,9}$/.test(this.item()) ? Number(this.item()) : 0));
  /** `base...head` of a compare view. */
  readonly range = computed(() => {
    const [base, head] = this.item().split('...');
    return base && head && isSha(base) && isSha(head) ? { base, head } : null;
  });
  readonly sections = computed<ViewOption[]>(() => {
    const repository = this.store.detail()?.repository;
    const count = (label: string, value = 0) => (value ? `${label} ${value}` : label);
    return [
      { value: 'code', label: '程式碼', icon: 'file-code' },
      { value: 'commits', label: '提交紀錄', icon: 'commit' },
      { value: 'pulls', label: count('Pull Request', repository?.openPulls), icon: 'pull' },
      { value: 'issues', label: count('議題', repository?.openIssues), icon: 'issue' },
      { value: 'reviews', label: 'AI 檢閱', icon: 'review' },
    ];
  });
  readonly refs = computed<SelectOption[]>(() => {
    const detail = this.store.detail();
    if (!detail) return [];
    const options: SelectOption[] = [
      ...detail.branches.map((x) => ({
        value: x.name,
        label: x.name,
        description: x.name === detail.repository.defaultBranch ? '預設分支' : '分支',
      })),
      ...detail.tags.map((x) => ({ value: x.name, label: x.name, description: '標籤' })),
    ];
    const ref = this.store.refName();
    if (ref && !options.some((x) => x.value === ref))
      options.unshift({
        value: ref,
        label: isSha(ref) ? shortSha(ref) : ref,
        description: 'commit',
      });
    return options;
  });

  open(section: string) {
    const ref = this.store.ref();
    void this.router.navigate(this.store.link(...(section === 'code' ? [] : [section])), {
      queryParams: ref && ['code', 'commits'].includes(section) ? { ref } : {},
    });
  }
  selectRef(ref: string) {
    const detail = this.store.detail();
    const value = ref === detail?.repository.defaultBranch ? null : ref || null;
    // Folders and files may not exist on another ref, so a new ref starts at its root.
    void this.router.navigate(
      this.store.link(...(this.section() === 'commits' ? ['commits'] : [])),
      {
        queryParams: { ref: value },
      },
    );
  }
}
