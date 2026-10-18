import { ChangeDetectionStrategy, Component, computed, inject, input } from '@angular/core';
import { Router } from '@angular/router';
import { CopyFeedback } from '@shared/browser/copy-feedback';
import { EmptyState } from '@shared/ui/empty-state';
import { Icon } from '@shared/ui/icon';
import { Notice } from '@shared/ui/notice';
import { Select, type SelectOption } from '@shared/ui/select';
import { ViewMotion } from '@shared/ui/view-motion';
import { ViewSwitch, type ViewOption } from '@shared/ui/view-switch';
import { RepositoryChange } from './browsing/repository-change';
import { RepositoryCode } from './browsing/repository-code';
import { RepositoryCommits } from './browsing/repository-commits';
import { RepositoryReview } from './reviews/repository-review';
import { RepositoryReviews } from './reviews/repository-reviews';
import { isSha, RepositoryStore, shortSha } from './repository-store';
import { RepositoryThread } from './browsing/repository-thread';
import { RepositoryThreads } from './browsing/repository-threads';

export type RepositorySection = 'code' | 'commits' | 'pulls' | 'issues' | 'reviews';

/** 路由的 `view` 所開啟的區段；比較範圍屬於歷史。 */
export function repositorySection(view: string): RepositorySection {
  if (view === 'compare') return 'commits';
  return view === 'commits' || view === 'pulls' || view === 'issues' || view === 'reviews'
    ? view
    : 'code';
}

/** 單一儲存庫：它釘選的 ref、其區段，以及網址中的區段。 */
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
  readonly section = computed(() => repositorySection(this.view()));
  readonly number = computed(() => (/^\d{1,9}$/.test(this.item()) ? Number(this.item()) : 0));
  /** 比較檢視的 `base...head`。 */
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
      ...detail.branches.map((item2) => ({
        value: item2.name,
        label: item2.name,
        description: item2.name === detail.repository.defaultBranch ? '預設分支' : '分支',
      })),
      ...detail.tags.map((item) => ({ value: item.name, label: item.name, description: '標籤' })),
    ];
    const ref = this.store.refName();
    if (ref && !options.some((item) => item.value === ref))
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
    // 資料夾與檔案在另一個 ref 上可能不存在，所以新的 ref 會從根目錄開始。
    void this.router.navigate(
      this.store.link(...(this.section() === 'commits' ? ['commits'] : [])),
      {
        queryParams: { ref: value },
      },
    );
  }
}
