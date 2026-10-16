import {
  ChangeDetectionStrategy,
  Component,
  computed,
  inject,
  signal,
  viewChild,
} from '@angular/core';
import { ViewScope } from '@shared/browser/view-scope';
import { ActionMenu, type MenuAction } from '@shared/ui/action-menu';
import { Checkbox } from '@shared/ui/checkbox';
import { ChoiceGroups, type ChoiceGroup } from '@shared/ui/choice-groups';
import { ConfirmDialog } from '@shared/ui/confirm-dialog';
import { Icon } from '@shared/ui/icon';
import { NameDialog } from '@shared/ui/name-dialog';
import { Select, type SelectOption } from '@shared/ui/select';
import { Tabs } from '@shared/ui/tabs';
import { ToastService } from '@shared/ui/toast-service';
import { ViewSwitch } from '@shared/ui/view-switch';
import { DesignDialogs } from './design-dialogs';
import { DesignExample, DesignSection } from './design-section';
import { DesignSamples } from './design-samples';
import { FloatingToolbar } from '@shared/ui/floating-toolbar';
import { IdleChrome } from '@shared/ui/idle-chrome';

/** Buttons, menus, confirmation and every selection control. */
@Component({
  selector: 'nx-design-controls',
  imports: [
    ActionMenu,
    Checkbox,
    ChoiceGroups,
    ConfirmDialog,
    DesignDialogs,
    DesignExample,
    DesignSection,
    FloatingToolbar,
    IdleChrome,
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
    .design-floating {
      position: relative;
      flex-basis: 100%;
      min-height: 10rem;
      padding: 4.5rem var(--p-space-4) var(--p-space-4);
      border: 1px solid var(--line);
      border-radius: var(--p-radius-md);
      background: var(--canvas);
      color: var(--secondary);
    }
    .design-floating nx-select {
      width: 7.5rem;
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
        ><button type="button" class="danger-button" (click)="reset()">
          <nx-icon name="reset" />重設示範</button
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
      description="浮層在 top layer，跨越捲動區；Tab 移動、方向鍵選取、Enter 套用、Esc 關閉。multiple 用勾選框切換、不關閉浮層，觸發鈕顯示「已選 N 項」。"
    >
      <nx-design-example label="Select">
        <nx-select
          label="示範閱讀模式"
          [value]="samples.selection()"
          [options]="choices"
          (valueChange)="samples.selection.set($event)"
        />
      </nx-design-example>
      <nx-design-example label="Select multiple">
        <nx-select
          label="示範等級"
          placeholder="全部等級"
          [multiple]="true"
          [options]="levels"
          [values]="levelValues()"
          (valuesChange)="levelValues.set($event)"
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
      id="design-floating-toolbar"
      title="浮動工具列"
      api="FloatingToolbar"
      description="檢視器的內容功能（模式、頁碼、縮放、搜尋）浮在內容上方、不佔版面；範圍（nxIdleChrome）開啟時短暫出現，指標移近或停在工具列、觸控點一下內容、Tab 時出現，閒置約 2 秒或 Esc 淡出；鍵盤焦點在內、下拉展開或 pinned 時保留。放大的檢視器標題列遵循同一規則。"
    >
      <div
        class="design-floating"
        role="region"
        aria-label="浮動工具列示範內容"
        tabindex="0"
        nxIdleChrome
      >
        <nx-floating-toolbar label="示範檢視工具">
          <nx-view-switch
            label="示範檢視方式"
            appearance="segment"
            [options]="views"
            [value]="view()"
            (valueChange)="view.set($event)"
          />
          <nx-select
            appearance="pill"
            label="示範縮放"
            [options]="zooms"
            [value]="zoom()"
            (valueChange)="zoom.set($event)"
          />
        </nx-floating-toolbar>
        在內容上移動指標不會喚回工具列；移到上緣附近、觸控點一下或用 Tab 進入才會出現。
      </div>
    </nx-design-section>
    <nx-design-section
      id="design-checkbox"
      title="核取方塊"
      api="Checkbox"
      description="整列可點選，保留原生鍵盤操作；勾選只由方框表示，列不填色，清單只有 hover 一種底色。"
    >
      <nx-checkbox
        label="開啟此項授權"
        description="整個項目都可點選。"
        [checked]="checked()"
        (checkedChange)="checked.set($event)"
      />
      <nx-checkbox label="目前無法調整" description="停用狀態" [disabled]="true" />
    </nx-design-section>
    <nx-design-section
      id="design-choice-groups"
      title="分組多選"
      api="ChoiceGroups"
      description="依分類分組的多選清單：每類一張有框卡片，標題旁是已選數與全選／全不選；寬處兩欄、窄處一欄，不再包一層總 fieldset。"
    >
      <nx-choice-groups
        label="示範授權"
        [groups]="choiceGroups"
        [selected]="chosen()"
        (selectedChange)="chosen.set($event)"
      />
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
  readonly levels: SelectOption[] = ['Information', 'Warning', 'Error'].map((value) => ({
    value,
    label: value,
  }));
  readonly levelValues = signal(['Warning', 'Error']);
  readonly chosen = signal(['chat', 'files']);
  readonly choiceGroups: ChoiceGroup[] = [
    {
      id: 'work',
      name: '工作',
      options: [
        { id: 'chat', label: '對話', description: 'chat' },
        { id: 'files', label: '檔案庫', description: 'files' },
        { id: 'knowledge', label: '知識庫', description: 'knowledge' },
      ],
    },
    {
      id: 'system',
      name: '系統',
      options: [
        { id: 'admin', label: '存取管理', description: 'admin' },
        { id: 'audit', label: '活動稽核', description: 'audit' },
      ],
    },
  ];
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
  readonly view = signal('preview');
  readonly zoom = signal('1');
  readonly views = [
    { value: 'preview', label: '預覽' },
    { value: 'source', label: '原始碼' },
  ];
  readonly zooms = ['0.75', '1', '1.5'].map((value) => ({
    value,
    label: `${Number(value) * 100}%`,
  }));
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
    { id: 'reset', label: '重設示範', icon: 'reset', danger: true },
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
