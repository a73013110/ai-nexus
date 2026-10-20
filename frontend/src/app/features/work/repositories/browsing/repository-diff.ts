import { ChangeDetectionStrategy, Component, computed, input, linkedSignal } from '@angular/core';
import { formatNumber } from '@shared/browser/format';
import { Icon } from '@shared/ui/icon';
import { type DiffFile, parseDiff } from './repository-diff-parser';

/** 先開啟的行；較後的與非常大的檔案要點擊才會展開，讓長的 diff 維持快速。 */
const openBudget = 1600;
const largeFile = 600;
const statusMarks = { added: 'A', deleted: 'D', renamed: 'R', modified: 'M' } as const;
const statusNames = { added: '新增', deleted: '刪除', renamed: '更名', modified: '修改' } as const;

/** 統一格式的 diff，以可摺疊的檔案呈現，附上舊與新的行號。 */
@Component({
  selector: 'nx-repository-diff',
  imports: [Icon],
  changeDetection: ChangeDetectionStrategy.OnPush,
  styleUrl: './repository-diff.scss',
  template: `<header class="summary">
      <span
        ><strong>{{ files().length }}</strong> 個檔案 ·
        <span class="add">+{{ number(totals().additions) }}</span>
        <span class="del">−{{ number(totals().deletions) }}</span></span
      >
      @if (files().length > 1) {
        <button type="button" class="quiet-button" (click)="all(!allOpen())">
          {{ allOpen() ? '全部收合' : '全部展開' }}
        </button>
      }
    </header>
    @for (file of files(); track $index; let i = $index) {
      <section class="file" [class.open]="opened().has(i)" [style.--i]="i < 10 ? i : 10">
        <button
          type="button"
          class="file-head"
          [attr.aria-expanded]="opened().has(i)"
          (click)="toggle(i)"
        >
          <nx-icon name="chevron" class="caret" />
          <span class="mark" [attr.data-status]="file.status" [title]="statusName(file)">{{
            statusMark(file)
          }}</span>
          <span class="path">
            @if (file.status === 'renamed' && file.oldPath !== file.path) {
              <span class="old">{{ file.oldPath }}</span> →
            }
            {{ file.path }}
          </span>
          @if (!file.binary) {
            <span class="stat"
              ><span class="add">+{{ file.additions }}</span>
              <span class="del">−{{ file.deletions }}</span></span
            >
            <span class="blocks" aria-hidden="true">
              @for (block of blocks(file); track $index) {
                <i [attr.data-kind]="block"></i>
              }
            </span>
          }
        </button>
        @if (opened().has(i)) {
          @if (file.binary) {
            <p class="note">二進位檔案，無法顯示差異。</p>
          } @else if (!file.hunks.length) {
            <p class="note">
              {{
                file.status === 'renamed' ? '只有更名，內容相同。' : '沒有文字變更（例如權限）。'
              }}
            </p>
          } @else {
            <div class="lines" role="table" [attr.aria-label]="file.path + ' 的變更'">
              @for (hunk of file.hunks; track $index) {
                <div class="hunk" role="row">
                  <span role="cell">⋯</span><span role="cell"></span
                  ><span role="cell">{{ hunk.header }}</span>
                </div>
                @for (line of hunk.lines; track $index) {
                  <div class="line" role="row" [attr.data-kind]="line.kind">
                    <span class="n" role="cell">{{ line.old }}</span
                    ><span class="n" role="cell">{{ line.new }}</span
                    ><span class="t" role="cell">{{ line.text }}</span>
                  </div>
                }
              }
            </div>
          }
        }
      </section>
    } @empty {
      <p class="empty">兩個版本之間沒有差異。</p>
    }`,
})
export class RepositoryDiff {
  readonly diff = input.required<string>();
  readonly files = computed(() => parseDiff(this.diff()));
  readonly totals = computed(() =>
    this.files().reduce(
      (sum, item) => ({
        additions: sum.additions + item.additions,
        deletions: sum.deletions + item.deletions,
      }),
      { additions: 0, deletions: 0 },
    ),
  );
  readonly opened = linkedSignal<DiffFile[], ReadonlySet<number>>({
    source: this.files,
    computation: (files) => {
      const open = new Set<number>();
      let budget = openBudget;
      files.forEach((file, index) => {
        const size = file.additions + file.deletions;
        if (size <= largeFile && size <= budget) {
          open.add(index);
          budget -= size;
        }
      });
      return open;
    },
  });
  readonly allOpen = computed(() => this.opened().size === this.files().length);
  readonly number = formatNumber;
  statusMark = (file: DiffFile) => statusMarks[file.status];
  statusName = (file: DiffFile) => statusNames[file.status];

  toggle(index: number) {
    this.opened.update((current) => {
      const next = new Set(current);
      if (!next.delete(index)) next.add(index);
      return next;
    });
  }
  all(open: boolean) {
    this.opened.set(open ? new Set(this.files().map((_, index) => index)) : new Set());
  }
  /** 五個方塊分為新增與刪除兩部分，與代管網站的繪製方式相同。 */
  blocks(file: DiffFile) {
    const total = file.additions + file.deletions;
    if (!total) return Array<string>(5).fill('none');
    const filled = Math.min(5, Math.max(1, Math.ceil(Math.log2(total + 1))));
    const add = Math.round((file.additions / total) * filled);
    return [
      ...Array<string>(add).fill('add'),
      ...Array<string>(filled - add).fill('del'),
      ...Array<string>(5 - filled).fill('none'),
    ];
  }
}
