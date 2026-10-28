import {
  ChangeDetectionStrategy,
  Component,
  computed,
  effect,
  ElementRef,
  inject,
  InjectionToken,
  input,
  ViewEncapsulation,
} from '@angular/core';
import {
  type IconNode,
  ThumbsUp,
  Files,
  Bell,
  LoaderCircle,
  ThumbsDown,
  Share2,
  Plus,
  Star,
  Archive,
  ArchiveRestore,
  Upload,
  Import,
  Paperclip,
  Library,
  PanelsTopLeft,
  SquareTerminal,
  LayoutDashboard,
  GitBranch,
  Globe,
  CircleDollarSign,
  Database,
  SlidersHorizontal,
  Tag,
  Search,
  PanelLeft,
  ArrowUp,
  ArrowDown,
  ChevronRight,
  Folder,
  Box,
  ChevronLeft,
  Ellipsis,
  Copy,
  Pencil,
  RefreshCw,
  Square,
  X,
  Check,
  Minus,
  FileText,
  MessageSquare,
  MessageSquareText,
  ClipboardList,
  ScrollText,
  ChevronUp,
  ChevronDown,
  CircleHelp,
  Lightbulb,
  Info,
  Trash2,
  LockKeyhole,
  Mail,
  Download,
  FolderKanban,
  ListOrdered,
  ListTodo,
  Workflow,
  ChartNoAxesCombined,
  Maximize2,
  Minimize2,
  User,
  LogOut,
  Settings2,
  ShieldCheck,
  History,
  ArrowLeft,
  Eye,
  Image as ImageIcon,
  ExternalLink,
  Filter,
  Play,
  Calendar,
  MapPin,
  ArrowRight,
  Activity,
  LayoutGrid,
  List,
  Pin,
  ChevronsLeft,
  ChevronsDownUp,
  ChevronsUpDown,
  Clock,
  RotateCcw,
  Network,
} from 'lucide';

// 語意名稱讓業務範本不依賴圖示供應商。
// 匯入精選的 SVG 資料，由下面的 `drawIcon` 自己畫，不帶入 lucide 的 Angular 套件（初始套件省 12 kB）。
const icons: Record<string, IconNode> = {
  activity: Activity,
  apps: LayoutGrid,
  list: List,
  pin: Pin,
  image: ImageIcon,
  files: Files,
  bell: Bell,
  loading: LoaderCircle,
  external: ExternalLink,
  'thumb-up': ThumbsUp,
  'thumb-down': ThumbsDown,
  share: Share2,
  plus: Plus,
  star: Star,
  archive: Archive,
  restore: ArchiveRestore,
  // upload 是把檔案放進平台（檔案庫、知識庫、專案文件）；import 是把外部資料轉成平台紀錄
  // （AD 使用者、打卡、備份、題庫），download 留給匯出與下載。
  upload: Upload,
  import: Import,
  paperclip: Paperclip,
  library: Library,
  command: SquareTerminal,
  workspace: PanelsTopLeft,
  dashboard: LayoutDashboard,
  git: GitBranch,
  globe: Globe,
  money: CircleDollarSign,
  database: Database,
  sliders: SlidersHorizontal,
  tag: Tag,
  search: Search,
  sidebar: PanelLeft,
  arrow: ArrowUp,
  down: ArrowDown,
  chevron: ChevronRight,
  left: ChevronLeft,
  more: Ellipsis,
  folder: Folder,
  container: Box,
  copy: Copy,
  edit: Pencil,
  repeat: RefreshCw,
  refresh: RefreshCw,
  reset: RotateCcw,
  stop: Square,
  close: X,
  check: Check,
  minus: Minus,
  document: FileText,
  lines: MessageSquare,
  chat: MessageSquareText,
  audit: ClipboardList,
  logs: ScrollText,
  'chevron-up': ChevronUp,
  'chevron-down': ChevronDown,
  tour: CircleHelp,
  idea: Lightbulb,
  info: Info,
  trash: Trash2,
  lock: LockKeyhole,
  mail: Mail,
  download: Download,
  projects: FolderKanban,
  tasks: ListTodo,
  order: ListOrdered,
  integrations: Workflow,
  chart: ChartNoAxesCombined,
  maximize: Maximize2,
  minimize: Minimize2,
  user: User,
  organization: Network,
  logout: LogOut,
  settings: Settings2,
  shield: ShieldCheck,
  history: History,
  back: ArrowLeft,
  forward: ArrowRight,
  eye: Eye,
  filter: Filter,
  play: Play,
  calendar: Calendar,
  location: MapPin,
  columns: SlidersHorizontal,
  first: ChevronsLeft,
  'expand-all': ChevronsUpDown,
  'collapse-all': ChevronsDownUp,
  clock: Clock,
};
/** `nx-icon` 不需功能套件即可繪製的名稱；API 的選單圖示不認得時會退回備用。 */
export const ICON_NAMES: ReadonlySet<string> = new Set(Object.keys(icons));
/** 選用的功能圖示留在它們的延遲載入功能套件中。 */
export const EXTRA_ICONS = new InjectionToken<Readonly<Record<string, IconNode>>>('Feature icons');
/**
 * 呼叫端以 `--icon-size`（`--icon-*` token）決定大小，而非 width 與 height。
 * `loading` 一律自己旋轉，呼叫端只換名稱、不另加動畫（減少動態時由全域規則停下）。
 */
@Component({
  selector: 'nx-icon',
  changeDetection: ChangeDetectionStrategy.OnPush,
  // SVG 是 `drawIcon` 直接建立的，沒有封裝屬性，所以樣式不封裝、以標籤名稱指定。
  encapsulation: ViewEncapsulation.None,
  host: { '[class.is-spinning]': "name() === 'loading'" },
  template: '',
  styles:
    'nx-icon{display:inline-flex;align-items:center;justify-content:center;width:var(--icon-size,var(--icon-xl,1.25rem));height:var(--icon-size,var(--icon-xl,1.25rem));flex:none;line-height:0;vertical-align:middle}nx-icon>svg{display:block;width:100%;height:100%;fill:var(--icon-fill,none)}nx-icon.is-spinning>svg{animation:orbit-turn var(--motion-spin) linear infinite}',
})
export class Icon {
  private readonly extra = inject(EXTRA_ICONS, { optional: true });
  readonly name = input.required<string>();
  readonly icon = computed(() => this.extra?.[this.name()] ?? icons[this.name()] ?? Info);
  constructor() {
    const host = inject<ElementRef<HTMLElement>>(ElementRef).nativeElement;
    effect(() => host.replaceChildren(drawIcon(this.icon())));
  }
}

const SVG = 'http://www.w3.org/2000/svg';
/** 每個圖示的 SVG 只建一次，之後每次使用都複製一份：表格裡上百個圖示不必各自逐一建立節點。 */
const drawn = new WeakMap<IconNode, SVGSVGElement>();
function drawIcon(node: IconNode): Node {
  let svg = drawn.get(node);
  if (!svg) {
    svg = document.createElementNS(SVG, 'svg');
    for (const [name, value] of Object.entries(SVG_ATTRIBUTES)) svg.setAttribute(name, value);
    for (const [tag, attributes] of node) {
      const part = document.createElementNS(SVG, tag);
      for (const [name, value] of Object.entries(attributes))
        if (value != null) part.setAttribute(name, String(value));
      svg.append(part);
    }
    drawn.set(node, svg);
  }
  return svg.cloneNode(true);
}
/** lucide 的線條圖示：24 格的 viewBox、只描邊不填色（填色由 `--icon-fill` 決定）；大小由宿主的 CSS 決定。 */
const SVG_ATTRIBUTES = {
  viewBox: '0 0 24 24',
  fill: 'none',
  stroke: 'currentColor',
  'stroke-width': '1.65',
  'stroke-linecap': 'round',
  'stroke-linejoin': 'round',
  'aria-hidden': 'true',
};
