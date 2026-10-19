import {
  ChangeDetectionStrategy,
  Component,
  computed,
  inject,
  input,
  output,
  signal,
} from '@angular/core';
import { NgTemplateOutlet } from '@angular/common';
import { RouterLink } from '@angular/router';
import type { NavigationNodeDto } from '@core/api/schema';
import { Icon } from '@shared/ui/icon';
import { navigationIcon, navigationNodes, navigationRuns } from './navigation-tree';
import { PIN_LIMIT, NavigationPreferences, type NavigationDisplay } from './navigation-preferences';
import { WorkspaceCommands } from './workspace-commands';

/**
 * 「全部功能」：伺服器依此帳號修剪過的選單樹（頁面、外部連結與巢狀分群，依管理員排的順序），
 * 呈現為三欄圖示或每行一個。釘選的頁面仍留在原位置並顯示釘選標記，因此目錄不會重新洗牌；
 * 編輯時每個頁面都會變成釘選切換。目前頁面若已釘選，只在它的釘選列上標記一次。
 * 外部連結帶外部連結標記，一律 `rel="noopener noreferrer"`，依設定決定是否開新分頁。
 */
@Component({
  selector: 'nx-feature-launcher',
  imports: [RouterLink, NgTemplateOutlet, Icon],
  changeDetection: ChangeDetectionStrategy.OnPush,
  styleUrl: './feature-launcher.scss',
  template: `<div class="launcher-tools">
      <button
        type="button"
        class="quiet-button"
        [attr.aria-pressed]="editing()"
        (click)="editing.set(!editing())"
      >
        <nx-icon [name]="editing() ? 'check' : 'pin'" />{{ editing() ? '完成' : '自訂釘選' }}
      </button>
      <button
        type="button"
        class="icon-button display-toggle"
        [attr.data-display]="pins.display()"
        [attr.aria-label]="nextDisplay().label"
        [attr.title]="nextDisplay().label"
        (click)="pins.setDisplay(nextDisplay().value)"
      >
        <nx-icon name="apps" /><nx-icon name="list" />
      </button>
      <button
        type="button"
        class="icon-button"
        aria-label="搜尋全部功能"
        title="搜尋功能 Ctrl + K"
        (click)="commands.open()"
      >
        <nx-icon name="search" />
      </button>
    </div>
    @if (editing()) {
      <p class="launcher-note" role="status">
        已釘選 {{ pins.ids().length }}／{{ limit }} 項，選擇要放在上方的功能。
      </p>
    }
    <ng-template #entries let-nodes>
      <div class="workspace-icons" [class.is-list]="pins.display() === 'list'">
        @for (node of asNodes(nodes); track node.id) {
          @if (node.kind === 'link') {
            <!-- 外部連結不是功能，不能釘選；編輯釘選時停用，避免看起來像漏選。 -->
            @if (editing()) {
              <button type="button" disabled [attr.title]="node.name + '（外部連結不能釘選）'">
                <nx-icon [name]="icon(node)!" /><span>{{ node.name }}</span>
              </button>
            } @else {
              <a
                [href]="node.url"
                rel="noopener noreferrer"
                [attr.target]="node.openInNewTab ? '_blank' : null"
                [attr.title]="
                  node.name + (node.openInNewTab ? '（外部連結，開新分頁）' : '（外部連結）')
                "
                [attr.aria-label]="
                  node.name + (node.openInNewTab ? '（外部連結，開新分頁）' : '（外部連結）')
                "
                (click)="activated.emit()"
              >
                <nx-icon [name]="icon(node)!" /><span>{{ node.name }}</span>
                <nx-icon class="entry-mark" name="external" aria-hidden="true" />
              </a>
            }
          } @else if (editing()) {
            <button
              type="button"
              [attr.aria-pressed]="pins.has(node.featureId!)"
              [disabled]="!pins.has(node.featureId!) && pins.full()"
              [attr.aria-label]="'釘選' + node.name"
              (click)="pins.toggle(node.featureId!)"
            >
              <nx-icon [name]="icon(node)!" /><span>{{ node.name }}</span>
            </button>
          } @else {
            <a
              [routerLink]="node.route"
              [class.current]="current(node)"
              [attr.aria-current]="current(node) ? 'page' : null"
              [attr.title]="pins.has(node.featureId!) ? node.name + '（已釘選）' : node.name"
              [attr.aria-label]="node.name"
              (click)="activated.emit()"
            >
              <nx-icon [name]="icon(node)!" /><span>{{ node.name }}</span>
              @if (pins.has(node.featureId!)) {
                <nx-icon class="entry-mark pin-mark" name="pin" aria-hidden="true" />
              }
            </a>
          }
        }
      </div>
    </ng-template>
    <!-- 一層的子節點依原順序：相鄰的頁面與連結排成一塊，分群在其位置巢狀成自己的區段。 -->
    <ng-template #level let-nodes>
      @for (run of runs(nodes); track run.key) {
        @if (run.kind === 'entries') {
          <ng-container
            [ngTemplateOutlet]="entries"
            [ngTemplateOutletContext]="{ $implicit: run.nodes }"
          />
        } @else {
          <ng-container
            [ngTemplateOutlet]="section"
            [ngTemplateOutletContext]="{ $implicit: run.node, depth: levelOf(run.node) }"
          />
        }
      }
    </ng-template>
    <ng-template #section let-node let-depth="depth">
      <section
        class="workspace-group"
        [class.is-nested]="depth > 0"
        [attr.aria-label]="asNode(node).name"
      >
        @if (depth === 0) {
          <h3>{{ asNode(node).name }}</h3>
        } @else {
          <h4>{{ asNode(node).name }}</h4>
        }
        <ng-container
          [ngTemplateOutlet]="level"
          [ngTemplateOutletContext]="{ $implicit: asNode(node).children }"
        />
      </section>
    </ng-template>
    <div class="workspace-groups">
      @for (run of runs(pins.tree()); track run.key) {
        @if (run.kind === 'entries') {
          <div class="workspace-group">
            <ng-container
              [ngTemplateOutlet]="entries"
              [ngTemplateOutletContext]="{ $implicit: run.nodes }"
            />
          </div>
        } @else {
          <ng-container
            [ngTemplateOutlet]="section"
            [ngTemplateOutletContext]="{ $implicit: run.node, depth: 0 }"
          />
        }
      }
    </div>`,
})
export class FeatureLauncher {
  readonly pins = inject(NavigationPreferences);
  readonly commands = inject(WorkspaceCommands);
  readonly active = input<string | null>(null);
  readonly activated = output<void>();
  readonly editing = signal(false);
  readonly limit = PIN_LIMIT;
  /**
   * 一個按鈕在兩種顯示之間切換，讓工具列只佔一格。它顯示的是切換後的顯示方式，
   * 這是兩種狀態都是視圖的切換鈕的慣例。
   */
  readonly nextDisplay = computed<{ value: NavigationDisplay; label: string }>(() =>
    this.pins.display() === 'icons'
      ? { value: 'list', label: '切換為條列顯示' }
      : { value: 'icons', label: '切換為圖示顯示' },
  );
  readonly icon = navigationIcon;
  readonly runs = navigationRuns;
  /** 每個節點的分群深度（最上層為 0），讓子分群知道自己要內縮。 */
  private readonly depths = computed(() => {
    const depths = new Map<string, number>();
    const visit = (nodes: readonly NavigationNodeDto[], depth: number) => {
      for (const node of nodes) {
        depths.set(node.id, depth);
        visit(node.children, depth + 1);
      }
    };
    visit(this.pins.tree(), 0);
    return depths;
  });
  /**
   * 同一頁面可能放在多個分群；`aria-current` 只標第一次出現的那一個。
   * 目前頁面已釘選時只在釘選列上標記，這裡不再標。
   */
  private readonly firstPlacement = computed(() => {
    const first = new Map<string, string>();
    for (const node of navigationNodes(this.pins.tree()))
      if (node.featureId && !first.has(node.featureId)) first.set(node.featureId, node.id);
    return first;
  });
  levelOf(node: NavigationNodeDto) {
    return this.depths().get(node.id) ?? 0;
  }
  current(node: NavigationNodeDto) {
    const feature = node.featureId;
    return (
      !!feature &&
      this.active() === feature &&
      !this.pins.has(feature) &&
      this.firstPlacement().get(feature) === node.id
    );
  }
  /** 範本的脈絡沒有型別；這裡為遞迴的區段恢復型別。 */
  asNodes = (nodes: unknown) => nodes as NavigationNodeDto[];
  asNode = (node: unknown) => node as NavigationNodeDto;
}
