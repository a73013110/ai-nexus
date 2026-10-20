import {
  ChangeDetectionStrategy,
  Component,
  computed,
  effect,
  inject,
  input,
  output,
  signal,
  untracked,
} from '@angular/core';
import { NgTemplateOutlet } from '@angular/common';
import { RouterLink } from '@angular/router';
import type { NavigationNodeDto } from '@core/api/schema';
import { keyText } from '@shared/browser/hotkeys';
import { uniqueId } from '@shared/browser/unique-id';
import { Icon } from '@shared/ui/icon';
import { navigationIcon, navigationNodes, navigationRuns } from './navigation-tree';
import { PIN_LIMIT, NavigationPreferences, type NavigationDisplay } from './navigation-preferences';
import { SHORTCUTS } from './keyboard-shortcuts';
import { WorkspaceCommands } from './workspace-commands';

/**
 * 「全部功能」：伺服器依此帳號修剪過的選單樹（頁面、外部連結與巢狀分群，依管理員排的順序），
 * 呈現為三欄圖示或每行一個。釘選的頁面仍留在原位置並顯示釘選標記，因此目錄不會重新洗牌；
 * 編輯時每個頁面都會變成釘選切換。目前頁面若已釘選，只在它的釘選列上標記一次。
 * 外部連結帶外部連結標記，一律 `rel="noopener noreferrer"`，依設定決定是否開新分頁。
 * 每一格就是選單的一個節點（一個頁面＝一個網址）；分群標題是展開／收合的按鈕，收合狀態依帳號記住，
 * 目前頁面所在的分群在換頁時自動展開。
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
        [title]="'搜尋功能 ' + paletteKeys"
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
      @let group = asNode(node);
      @let open = !pins.isCollapsed(group.id);
      <section
        class="workspace-group"
        [class.is-nested]="depth > 0"
        [class.is-collapsed]="!open"
        [attr.aria-label]="group.name"
      >
        <ng-template #toggle>
          <button
            type="button"
            class="workspace-group-toggle"
            [attr.aria-expanded]="open"
            [attr.aria-controls]="bodyId(group)"
            (click)="pins.setCollapsed(group.id, open)"
          >
            <span>{{ group.name }}</span
            ><nx-icon name="chevron" aria-hidden="true" />
          </button>
        </ng-template>
        @if (depth === 0) {
          <h3><ng-container [ngTemplateOutlet]="toggle" /></h3>
        } @else {
          <h4><ng-container [ngTemplateOutlet]="toggle" /></h4>
        }
        <!-- 收合時 inert：連結離開 Tab 順序與無障礙樹，高度收合的過渡仍看得到。 -->
        <div class="workspace-group-body" [id]="bodyId(group)" [inert]="!open">
          <div class="workspace-group-content">
            <ng-container
              [ngTemplateOutlet]="level"
              [ngTemplateOutletContext]="{ $implicit: group.children }"
            />
          </div>
        </div>
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
  readonly paletteKeys = keyText(SHORTCUTS.commandPalette.keys);
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
  private readonly id = uniqueId('nx-launcher');
  /** 每個節點的分群深度（最上層為 0）與上層分群，給子分群樣式與「展開目前頁面所在的分群」用。 */
  private readonly structure = computed(() => {
    const depths = new Map<string, number>();
    const parents = new Map<string, string>();
    const visit = (nodes: readonly NavigationNodeDto[], depth: number, parent?: string) => {
      for (const node of nodes) {
        depths.set(node.id, depth);
        if (parent) parents.set(node.id, parent);
        visit(node.children, depth + 1, node.id);
      }
    };
    visit(this.pins.tree(), 0);
    return { depths, parents };
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
  /** 目前頁面（第一次出現的位置）的所有上層分群。 */
  private readonly currentGroups = computed(() => {
    const { parents } = this.structure();
    const groups: string[] = [];
    const current = this.firstPlacement().get(this.active() ?? '');
    for (let id = current && parents.get(current); id; id = parents.get(id)) groups.push(id);
    return groups;
  });

  constructor() {
    // 換頁時展開目前頁面所在的分群，側欄才看得到自己在哪；之後使用者仍可再收合它。
    effect(() => {
      const groups = this.currentGroups();
      untracked(() => this.pins.expand(groups));
    });
  }

  levelOf(node: NavigationNodeDto) {
    return this.structure().depths.get(node.id) ?? 0;
  }
  bodyId(group: NavigationNodeDto) {
    return `${this.id}-${group.id}`;
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
