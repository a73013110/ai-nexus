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
import type { DirectoryUserDto } from '@core/api/schema';
import { nameInitial } from '@shared/browser/format';
import { DateTimePicker } from '@shared/ui/date-time-picker';
import { Field } from '@shared/ui/field';
import { Icon } from '@shared/ui/icon';
import { InlineTitle } from '@shared/ui/inline-title';
import { KeyCombo } from '@shared/ui/key-combo';
import { PersonBrowser, type BrowsePerson } from '@shared/ui/person-browser';
import { SearchField } from '@shared/ui/search-field';
import { SettingGroup, SettingRow } from '@shared/ui/setting-row';
import { TimeRangePicker, hoursAgo, type TimeRange } from '@shared/ui/time-range-picker';
import { TreeSelect, type TreeSelectNode } from '@shared/ui/tree-select';
import type { DirectorySearch } from '@features/work/collaboration/directory-search';
import { PersonPicker } from '@features/work/collaboration/person-picker';
import { DesignExample, DesignSection } from './design-section';
import { DesignSamples } from './design-samples';

/** 範例用的虛構同事；工作臺不呼叫目錄 API。 */
const PEOPLE: DirectoryUserDto[] = [
  { id: 'p1', account: 'lin.jy', displayName: '林佳穎' },
  { id: 'p2', account: 'lin.jh', displayName: '林建宏' },
  { id: 'p3', account: 'chen.yt', displayName: '陳雅婷' },
  { id: 'p4', account: 'huang.lz', displayName: '黃林志' },
];

/** 從名單挑人的範例：依主要所屬分組，尚未分派的排最前面（虛構姓名）。 */
const ROSTER: BrowsePerson[] = [
  { id: 'r1', name: '張育誠', detail: 'chang.yc', group: '尚未分派部門' },
  { id: 'r2', name: '林佳穎', detail: 'lin.jy', group: '總公司 › 業務部' },
  { id: 'r3', name: '林建宏', detail: 'lin.jh', group: '總公司 › 業務部' },
  { id: 'r4', name: '陳雅婷', detail: 'chen.yt', group: '總公司 › 工程部' },
  { id: 'r5', name: '黃林志', detail: 'huang.lz', group: '總公司 › 工程部' },
  { id: 'r6', name: '吳書豪', detail: 'wu.sh', group: '總公司 › 工程部' },
];

/** 以本機名單代替 `directorySearch`，排除 `chosen` 已選的人。 */
function sampleDirectory(chosen: () => string[]): DirectorySearch {
  const text = signal('');
  const searchable = computed(() => text().trim().length >= 2);
  return {
    text,
    searchable,
    results: computed(() => {
      const query = text().trim().toLowerCase();
      if (!searchable()) return [];
      return PEOPLE.filter(
        (user) =>
          !chosen().includes(user.id) &&
          `${user.displayName} ${user.account}`.toLowerCase().includes(query),
      );
    }),
    loading: signal(false),
    error: signal(''),
    find: (value) => text.set(value),
    clear: () => text.set(''),
  };
}

/** 文字欄位、搜尋、選人、表單段落、日期與原地編輯。 */
/** 名稱前段相同、含停用（與停用上層之下）的單位：平面清單看不出差別，樹狀才分得清楚。 */
const UNITS: TreeSelectNode[] = [
  { value: 'hq', label: '星河科技股份有限公司（總公司）' },
  { value: 'sales', label: '業務／技服部', parent: 'hq' },
  { value: 'sales-n', label: '北區業務組', parent: 'sales' },
  { value: 'sales-s', label: '南區業務組', parent: 'sales' },
  { value: 'sw', label: '軟體工程部', parent: 'hq', inactive: true },
  { value: 'sw-qa', label: '測試組', parent: 'sw' },
  { value: 'eng', label: '工程部', parent: 'hq' },
  { value: 'eng-p', label: '平台組', parent: 'eng' },
  { value: 'eng-q', label: '品質組', parent: 'eng' },
  { value: 'gm', label: '總經理室', parent: 'hq' },
  { value: 'tc', label: '星河科技股份有限公司（台中分公司）' },
  { value: 'tc-rd', label: '研發處', parent: 'tc' },
  { value: 'lab', label: '星河測試據點', inactive: true },
];

@Component({
  selector: 'nx-design-forms',
  imports: [
    DateTimePicker,
    DesignExample,
    DesignSection,
    Field,
    Icon,
    InlineTitle,
    PersonBrowser,
    PersonPicker,
    KeyCombo,
    SearchField,
    SettingGroup,
    SettingRow,
    TimeRangePicker,
    TreeSelect,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  styles: `
    :host {
      display: grid;
      gap: var(--p-space-6);
    }
    nx-design-example {
      flex: 1 1 14rem;
    }
    nx-design-example > * {
      width: 100%;
    }
    .design-picker {
      display: grid;
      gap: var(--p-space-3);
    }
    .design-form {
      width: min(100%, 36rem);
    }
    .design-settings {
      width: min(100%, 40rem);
    }
    .design-bar-frame {
      width: min(100%, 28rem);
      border: 1px solid var(--line);
      border-radius: var(--dialog-radius);
    }
    .design-bar-frame p {
      margin: 0;
      padding: var(--p-space-3) var(--p-space-4);
      color: var(--secondary);
      font-size: var(--text-caption);
    }
  `,
  template: `<nx-design-section
      id="design-fields"
      title="欄位與搜尋"
      api="nxField · SearchField"
      description="驗證說明靠近欄位；手機輸入字級至少 16px，避免 iOS 自動放大。打字即搜尋時傳 busy（防抖等待加讀取中）：放大鏡換成轉圈，欄位仍可輸入。"
    >
      <nx-design-example label="nxField">
        <label class="ui-field"
          >共用表單欄位<input nxField aria-label="示範資料篩選" placeholder="依事件名稱查詢"
        /></label>
      </nx-design-example>
      <nx-design-example label="SearchField">
        <nx-search-field
          label="示範搜尋"
          placeholder="搜尋文件與對話"
          [value]="search()"
          (valueChange)="search.set($event)"
        />
      </nx-design-example>
      <nx-design-example label="SearchField busy">
        <nx-search-field label="示範搜尋中" value="星河" [busy]="true" />
      </nx-design-example>
    </nx-design-section>
    <nx-design-section
      id="design-search-bare"
      title="列內搜尋"
      api='SearchField appearance="bare"'
      description="放進已有外框的列（對話框標題列、面板頂端）時去掉欄位外框，由列的細線與游標標示輸入位置。"
    >
      <div class="design-bar-frame">
        <div class="dialog-bar">
          <nx-search-field
            appearance="bare"
            label="示範列內搜尋"
            placeholder="搜尋檔案庫中的檔案"
            [value]="bareSearch()"
            (valueChange)="bareSearch.set($event)"
          />
        </div>
        <p>{{ bareSearch() ? '搜尋：' + bareSearch() : '結果列在標題列下方。' }}</p>
      </div>
    </nx-design-section>
    <nx-design-section
      id="design-person-picker"
      title="人員選擇器"
      api="PersonPicker · directorySearch · .form-chips"
      description="輸入兩個字起搜尋，結果浮在欄位下方（↑↓ 移動、Enter 選取、Esc 收起）。查詢狀態由呼叫端的 directorySearch 管理，選取後要不要清空由呼叫端決定。可選多人時已選的排成膠囊；nameHint 讓沒有帳號的人只記姓名。試試輸入「林佳」或「lin」。"
    >
      <nx-design-example label="加入多人">
        <div class="design-picker">
          <nx-person-picker label="示範加入成員" [search]="team" (picked)="addMember($event)" />
          @if (members().length) {
            <div class="form-chips">
              @for (user of members(); track user.id) {
                <button
                  type="button"
                  class="form-chip"
                  [attr.aria-label]="'移除示範成員 ' + user.displayName"
                  (click)="removeMember(user.id)"
                >
                  <span class="avatar" aria-hidden="true">{{ initial(user.displayName) }}</span
                  >{{ user.displayName }}<nx-icon name="close" />
                </button>
              }
            </div>
          }
        </div>
      </nx-design-example>
      <nx-design-example label="停用">
        <nx-person-picker label="示範停用的選人" [search]="idle" [disabled]="true" />
      </nx-design-example>
    </nx-design-section>
    <nx-design-section
      id="design-person-browser"
      title="從名單挑人"
      api="PersonBrowser · ChoiceGroups"
      description="名單由呼叫端載入，依分組（例如主要所屬部門）列出全部的人，可用文字或分組篩選、勾選多人；篩選掉的人仍保持勾選。看到才認得出來時用它，已知道要找誰時用人員選擇器。"
    >
      <nx-design-example label="勾選多人">
        <nx-person-browser
          label="示範可加入的人員"
          [people]="roster"
          [selected]="picked()"
          (selectedChange)="picked.set($event)"
        />
        <p class="form-note" role="status">已選 {{ picked().length }} 人</p>
      </nx-design-example>
      <nx-design-example label="載入中">
        <nx-person-browser
          label="示範載入中的名單"
          [people]="[]"
          [selected]="[]"
          [loading]="true"
        />
      </nx-design-example>
      <nx-design-example label="沒有可選的人">
        <nx-person-browser
          label="示範空名單"
          emptyText="所有在職的使用者都已是這個單位的成員。"
          [people]="[]"
          [selected]="[]"
        />
      </nx-design-example>
    </nx-design-section>
    <nx-design-section
      id="design-tree-select"
      title="階層選擇器"
      api="TreeSelect · unitNodes()"
      description="從階層挑一個（單位用 unitNodes() 轉換）。觸發鈕是「上層路徑 › 名稱」，放不下先截路徑；面板可展開收合，搜尋時改成平面清單並標出所屬路徑；allLabel 釘在最上方。可選的狀態由 status 指定、使用者不能改：any 都可選（停用的預設收起，底部可勾選列出）、active 只列啟用的、inactive 只能選停用的（啟用的上層只當路徑）。"
    >
      <nx-design-example label="status any（預設）">
        <nx-tree-select
          label="示範單位"
          allLabel="所有單位"
          noun="單位"
          [nodes]="units"
          [value]="unitAny()"
          (valueChange)="unitAny.set($event)"
        />
      </nx-design-example>
      <nx-design-example label="status active">
        <nx-tree-select
          label="示範啟用的單位"
          noun="單位"
          placeholder="選擇單位"
          status="active"
          [nodes]="units"
          [value]="unitActive()"
          (valueChange)="unitActive.set($event)"
        />
      </nx-design-example>
      <nx-design-example label="status inactive">
        <nx-tree-select
          label="示範停用的單位"
          noun="單位"
          placeholder="選擇停用的單位"
          status="inactive"
          [nodes]="units"
          [value]="unitInactive()"
          (valueChange)="unitInactive.set($event)"
        />
      </nx-design-example>
      <nx-design-example label="載入中">
        <nx-tree-select
          label="示範載入中的單位"
          allLabel="所有單位"
          [nodes]="[]"
          [loading]="true"
        />
      </nx-design-example>
    </nx-design-section>
    <nx-design-section
      id="design-form-sections"
      title="表單段落"
      api=".form-sections · .form-choice · .form-row"
      description="兩組以上欄位的表單（設定頁、md 表單對話框）依填的人怎麼想分段：段落標題加最多一句說明，細線分隔，間距只靠共用 gap。選定單一對象後換成一列加「更換」；成對欄位並排；選填寫在標籤裡。範本是卡號的指派持有人對話框。"
    >
      <form class="design-form" novalidate (submit)="$event.preventDefault()">
        <div class="form-sections">
          <section class="form-section" role="group" aria-labelledby="design-form-holder">
            <header class="form-section-head">
              <h3 id="design-form-holder">持有人</h3>
            </header>
            <div class="form-section-body">
              @if (holder(); as h) {
                <div class="form-choice">
                  <span class="avatar" [class.is-name]="!h.account" aria-hidden="true">{{
                    initial(h.displayName)
                  }}</span>
                  <span class="form-choice-text"
                    ><strong>{{ h.displayName }}</strong
                    ><small>{{ h.account || '尚無帳號，只記姓名' }}</small></span
                  >
                  <button type="button" class="secondary-button" (click)="holder.set(null)">
                    更換
                  </button>
                </div>
              } @else {
                <nx-person-picker
                  label="示範尋找持有人"
                  nameHint="尚無帳號時只記姓名，之後可再綁定帳號"
                  [search]="holders"
                  (picked)="chooseHolder($event)"
                  (named)="chooseName($event)"
                />
              }
            </div>
          </section>
          <section class="form-section" role="group" aria-labelledby="design-form-period">
            <header class="form-section-head">
              <h3 id="design-form-period">期間</h3>
              <p class="form-note">台北時間；開始時間之後的紀錄才算這位持有人。</p>
            </header>
            <div class="form-section-body">
              <div class="form-row">
                <nx-date-time-picker
                  label="開始時間"
                  [value]="periodStart()"
                  (valueChange)="periodStart.set($event)"
                />
                <nx-date-time-picker
                  label="結束時間（留空＝持有中）"
                  [required]="false"
                  [min]="periodStart()"
                  [value]="periodEnd()"
                  (valueChange)="periodEnd.set($event)"
                />
              </div>
              <label>備註（選填）<input nxField autocomplete="off" /></label>
            </div>
          </section>
        </div>
      </form>
    </nx-design-section>
    <nx-design-section
      id="design-dates"
      title="日期與時間"
      api="DateTimePicker · TimeRangePicker"
      description="以台北 wall-clock 輸入與顯示、送出時轉 UTC；民國曆只影響顯示。查詢的時間範圍用 TimeRangePicker：預設區間與自訂起訖在同一個浮層，時區只寫一次。"
    >
      <nx-design-example label="西元">
        <nx-date-time-picker
          label="西元日期時間"
          [value]="calendarDate()"
          (valueChange)="calendarDate.set($event)"
        />
      </nx-design-example>
      <nx-design-example label="民國">
        <nx-date-time-picker
          label="民國日期時間"
          calendarSystem="roc"
          [value]="calendarDate()"
          (valueChange)="calendarDate.set($event)"
        />
      </nx-design-example>
      <nx-design-example label="選填、只有日期">
        <nx-date-time-picker
          label="選填日期"
          [withTime]="false"
          [required]="false"
          [value]="optionalDate()"
          (valueChange)="optionalDate.set($event)"
        />
      </nx-design-example>
      <nx-design-example label="時間範圍">
        <nx-time-range-picker
          label="示範時間範圍"
          [presets]="ranges"
          [value]="range()"
          (valueChange)="range.set($event)"
        />
      </nx-design-example>
      <nx-design-example label="時間範圍、只有日期、可不限">
        <nx-time-range-picker
          label="示範日期範圍"
          [presets]="dayRanges"
          [withTime]="false"
          [required]="false"
          [value]="dayRange()"
          (valueChange)="dayRange.set($event)"
        />
      </nx-design-example>
    </nx-design-section>
    <nx-design-section
      id="design-inline"
      title="直接編輯"
      api="InlineTitle"
      description="雙擊標題或按 F2／Enter 編輯；Enter 儲存、Esc 取消。"
    >
      <nx-inline-title
        appearance="block"
        [value]="samples.title()"
        [scope]="'design-' + samples.titleVersion()"
        [save]="saveTitle"
      />
    </nx-design-section>
    <nx-design-section
      id="design-settings"
      title="偏好設定列"
      api="SettingGroup · SettingRow · KeyCombo"
      description="個人偏好一列一項：名稱與一句說明在左、控制項在右。給 options 是下拉選單、給 checked 是開關，其他控制項直接投影；窄卡片時控制項靠上對齊。按鍵用 KeyCombo，依平台顯示 ⌘ 或 Ctrl。"
    >
      <nx-setting-group
        class="design-settings"
        heading="示範偏好"
        description="卡片標題下最多一句說明。"
      >
        <nx-setting-row
          label="主題"
          description="下拉選單的名稱取自列名稱。"
          [options]="themes"
          [value]="theme()"
          (valueChange)="theme.set($event)"
        />
        <nx-setting-row
          label="減少動態效果"
          description="開關的名稱與說明由列提供。"
          [checked]="reduced()"
          (checkedChange)="reduced.set($event)"
        />
        <nx-setting-row
          label="停用的開關"
          description="需要先取得權限。"
          [checked]="false"
          [disabled]="true"
        >
          <span setting-detail>說明下方可投影狀態原因。</span>
        </nx-setting-row>
        <nx-setting-row label="快捷指令" description="投影其他控制項。">
          <nx-key-combo keys="mod+k" />
        </nx-setting-row>
      </nx-setting-group>
    </nx-design-section>`,
})
export class DesignForms {
  readonly samples = inject(DesignSamples);
  protected readonly initial = nameInitial;
  readonly members = signal<DirectoryUserDto[]>([PEOPLE[2]]);
  readonly team = sampleDirectory(() => this.members().map((user) => user.id));
  readonly idle = sampleDirectory(() => []);
  readonly roster = ROSTER;
  readonly picked = signal<string[]>(['r4']);
  readonly units = UNITS;
  readonly unitAny = signal('eng');
  readonly unitActive = signal('');
  readonly unitInactive = signal('');
  readonly holder = signal<DirectoryUserDto | null>(null);
  readonly holders = sampleDirectory(() => []);
  readonly periodStart = signal('2026-10-08T09:00:00');
  readonly periodEnd = signal('');
  private readonly title = viewChild.required(InlineTitle);
  readonly search = signal('');
  readonly bareSearch = signal('');
  readonly calendarDate = signal('2026-10-08T14:30:45');
  readonly optionalDate = signal('');
  readonly ranges = [
    { value: '1h', label: '最近 1 小時', duration: hoursAgo(1) },
    { value: '24h', label: '最近 24 小時', duration: hoursAgo(24) },
  ];
  readonly range = signal<TimeRange>({ preset: '24h', from: '', to: '' });
  readonly dayRanges = [
    { value: 'all', label: '不限時間' },
    { value: '7d', label: '最近 7 天', duration: hoursAgo(24 * 7) },
  ];
  readonly dayRange = signal<TimeRange>({ preset: 'all', from: '', to: '' });
  readonly themes = [
    { value: 'system', label: '跟隨系統' },
    { value: 'dark', label: '深色' },
  ];
  readonly theme = signal('system');
  readonly reduced = signal(false);
  addMember(user: DirectoryUserDto) {
    this.members.update((all) => [...all, user]);
    this.team.clear();
  }
  removeMember(id: string) {
    this.members.update((all) => all.filter((user) => user.id !== id));
  }
  chooseHolder(user: DirectoryUserDto) {
    this.holder.set(user);
    this.holders.clear();
  }
  chooseName(name: string) {
    this.chooseHolder({ id: '', account: '', displayName: name });
  }
  readonly saveTitle = async (value: string) => {
    this.samples.title.set(value);
    return true;
  };
  constructor() {
    effect(() => {
      if (this.samples.editRequest()) untracked(() => this.title().begin());
    });
  }
}
