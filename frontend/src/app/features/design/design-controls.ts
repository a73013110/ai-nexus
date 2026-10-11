import {
  ChangeDetectionStrategy,
  Component,
  computed,
  inject,
  signal,
  viewChild,
} from '@angular/core';
import { ViewScope } from '../../shared/browser/view-scope';
import { ActionMenu, type MenuAction } from '../../shared/ui/action-menu';
import { Checkbox } from '../../shared/ui/checkbox';
import { ConfirmDialog } from '../../shared/ui/confirm-dialog';
import { Icon } from '../../shared/ui/icon';
import { NameDialog } from '../../shared/ui/name-dialog';
import { Select, type SelectOption } from '../../shared/ui/select';
import { Tabs } from '../../shared/ui/tabs';
import { ToastService } from '../../shared/ui/toast-service';
import { ViewSwitch } from '../../shared/ui/view-switch';
import { DesignDialogs } from './design-dialogs';
import { DesignExample, DesignSection } from './design-section';
import { DesignSamples } from './design-samples';

/** Buttons, menus, confirmation and every selection control. */
@Component({
  selector: 'nx-design-controls',
  imports: [
    ActionMenu,
    Checkbox,
    ConfirmDialog,
    DesignDialogs,
    DesignExample,
    DesignSection,
    Icon,
    NameDialog,
    Select,
    Tabs,
    ViewSwitch,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  styles: `
    :host {
      display: grid;
      gap: var(--p-space-6);
    }
    .design-buttons {
      display: flex;
      flex-wrap: wrap;
      align-items: center;
      gap: var(--p-space-2);
    }

    nx-view-switch {
      margin-bottom: 0;
    }
    .design-wide {
      width: min(100%, 18rem);
    }
    .design-tabs {
      flex-basis: 100%;
      min-width: 0;
    }
    .design-tabs [role='tabpanel'] {
      padding: var(--p-space-3);
      color: var(--secondary);
      font-size: var(--text-caption);
    }
    nx-select {
      width: min(100%, 16rem);
    }
  `,
  template: `<nx-design-section
      id="design-buttons"
      title="按鈕"
      api=".primary-button · .secondary-button · .quiet-button · .icon-button"
      description="一個區域只有一個主要操作；危險操作一律經過確認。按下時微縮，完成回饋以右下角通知呈現。"
    >
      <div class="design-buttons">
        <button type="button" class="primary-button" (click)="toast.success('主要操作示範')">
          主要操作</button
        ><button type="button" class="secondary-button" (click)="toast.success('次要操作示範')">
          次要操作</button
        ><button type="button" class="quiet-button" (click)="toast.success('安靜操作示範')">
          <nx-icon name="edit" />安靜操作</button
        ><button
          type="button"
          class="icon-button"
          aria-label="圖示操作示範"
          title="圖示操作"
          (click)="toast.success('圖示操作示範')"
        >
          <nx-icon name="settings" /></button
        ><button type="button" class="danger-button" (click)="reset()">重設示範</button
        ><button type="button" class="secondary-button" disabled>停用操作</button>
      </div>
    </nx-design-section>
    <nx-design-section
      id="design-dialogs"
      title="對話框"
      api=".platform-dialog · ConfirmDialog · NameDialog"
      description="同一套外框：細線、分層陰影、12px 圓角；開啟時由下方彈入，關閉時沿原路退場。初始焦點在取消，Esc 視為取消並還原焦點。"
    >
      <div class="design-buttons">
        <button type="button" class="secondary-button" (click)="confirmPlain()">一般確認</button
        ><button type="button" class="secondary-button" (click)="rename()">
          <nx-icon name="edit" />命名對話框
        </button>
      </div>
    </nx-design-section>
    <nx-design-dialogs />
    <nx-design-section
      id="design-menus"
      title="選單與下拉"
      api="ActionMenu · Select"
      description="浮層在 top layer，跨越捲動區；Tab 移動、方向鍵選取、Enter 套用、Esc 關閉。"
    >
      <nx-design-example label="Select">
        <nx-select
          label="示範閱讀模式"
          [value]="samples.selection()"
          [options]="choices"
          (valueChange)="samples.selection.set($event)"
        />
      </nx-design-example>
      <nx-design-example label="ActionMenu">
        <nx-action-menu label="示範更多操作" [items]="actions" (action)="action($event)" />
      </nx-design-example>
    </nx-design-section>
    <nx-design-section
      id="design-menu-check"
      title="核取選單項目"
      api="MenuPanel · MenuAction.checked"
      description="設定 checked 的項目成為 menuitemcheckbox（aria-checked），勾選時右側打勾；選取後選單關閉，狀態由頁面切換。未設定 checked 的項目仍是一般操作。"
    >
      <nx-action-menu label="示範檢視選項" [items]="toggles()" (action)="toggle($event)" />
    </nx-design-section>
    <nx-design-section
      id="design-switches"
      title="切換與頁籤"
      api="ViewSwitch · Tabs"
      description="同組只有一個選取 chip，以彈簧滑到新選項；連結 tabpanel 的內容才用 Tabs。"
    >
      <nx-design-example label="segment · fill（側欄篩選）">
        <nx-view-switch
          class="design-wide"
          label="示範篩選"
          appearance="segment"
          [fill]="true"
          [options]="filters"
          [value]="filter()"
          (valueChange)="filter.set($event)"
        />
      </nx-design-example>
      <nx-design-example label="segment">
        <nx-view-switch
          label="示範時間範圍"
          appearance="segment"
          [options]="ranges"
          [value]="range()"
          (valueChange)="range.set($event)"
        />
      </nx-design-example>
      <nx-design-example label="icon">
        <nx-view-switch
          label="示範顯示方式"
          [iconOnly]="true"
          [options]="displays"
          [value]="display()"
          (valueChange)="display.set($event)"
        />
      </nx-design-example>
      <nx-design-example label="line（頁面檢視）">
        <nx-view-switch
          label="示範頁面檢視"
          [options]="ranges"
          [value]="range()"
          (valueChange)="range.set($event)"
        />
      </nx-design-example>
      <div class="design-tabs">
        <nx-tabs
          #tabs
          label="示範頁籤"
          [items]="tabItems"
          [value]="tab()"
          (valueChange)="tab.set($event)"
        />
        <div
          role="tabpanel"
          tabindex="0"
          [id]="tabs.panelId(tab())"
          [attr.aria-labelledby]="tabs.tabId(tab())"
        >
          {{ tab() === 'overview' ? '概覽內容：Tabs 連結 tabpanel。' : '屬性內容：方向鍵切換。' }}
        </div>
      </div>
    </nx-design-section>
    <nx-design-section
      id="design-checkbox"
      title="核取方塊"
      api="Checkbox"
      description="整列可點選，保留原生鍵盤操作。"
    >
      <nx-checkbox
        label="開啟此項授權"
        description="整個項目都可點選。"
        [checked]="checked()"
        (checkedChange)="checked.set($event)"
      />
      <nx-checkbox label="目前無法調整" description="停用狀態" [disabled]="true" />
    </nx-design-section>
    <nx-confirm-dialog />
    <nx-name-dialog />`,
})
export class DesignControls {
  readonly samples = inject(DesignSamples);
  private readonly scope = inject(ViewScope);
  readonly toast = inject(ToastService);
  private readonly confirmation = viewChild.required(ConfirmDialog);
  private readonly naming = viewChild.required(NameDialog);
  readonly checked = signal(true);
  readonly options = signal({ web: true, time: false });
  readonly toggles = computed<MenuAction[]>(() => [
    { id: 'web', label: '網路搜尋', icon: 'globe', checked: this.options().web },
    { id: 'time', label: '顯示時間', icon: 'history', checked: this.options().time },
    { id: 'locked', label: '由管理員鎖定', icon: 'lock', checked: true, disabled: true },
  ]);
  readonly filter = signal('active');
  readonly range = signal('week');
  readonly display = signal('icons');
  readonly tab = signal('overview');
  readonly filters = [
    { value: 'active', label: '近期' },
    { value: 'favorites', label: '收藏' },
    { value: 'archived', label: '封存' },
  ];
  readonly ranges = [
    { value: 'day', label: '今天' },
    { value: 'week', label: '過去 7 天' },
    { value: 'month', label: '過去 30 天' },
    { value: 'off', label: '停用', disabled: true },
  ];
  readonly displays = [
    { value: 'icons', label: '圖示顯示', icon: 'apps' },
    { value: 'list', label: '條列顯示', icon: 'list' },
  ];
  readonly tabItems = [
    { value: 'overview', label: '概覽' },
    { value: 'properties', label: '屬性' },
  ];
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
  toggle(id: string) {
    if (id === 'web' || id === 'time')
      this.options.update((value) => ({ ...value, [id]: !value[id] }));
  }
  action(id: string) {
    if (id === 'edit') this.samples.editRequest.update((x) => x + 1);
    if (id === 'reset') void this.reset();
  }
  async reset() {
    const guard = this.scope.guard();
    if (
      (await this.confirmation().ask({
        title: '重設介面示範？',
        message: '這只會還原此頁的示範標題、選項及進度。',
        confirm: '重設示範',
        danger: true,
      })) &&
      guard()
    )
      this.samples.restore();
  }
  async confirmPlain() {
    const guard = this.scope.guard();
    const yes = await this.confirmation().ask({
      title: '套用示範版面？',
      message: '這只會在本頁顯示通知，不會修改任何資料。',
      confirm: '套用',
    });
    if (yes && guard()) this.toast.success('已套用示範版面');
  }
  rename() {
    this.naming().open({
      title: '重新命名示範',
      value: this.samples.title(),
      description: '儲存後會更新「直接編輯」範例的標題。',
      save: async (name) => {
        await new Promise((resolve) => setTimeout(resolve, 400));
        this.samples.title.set(name);
        this.toast.success('名稱已更新', name);
      },
    });
  }
}
