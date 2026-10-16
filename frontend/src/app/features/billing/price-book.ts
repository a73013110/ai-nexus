import {
  ChangeDetectionStrategy,
  Component,
  computed,
  inject,
  signal,
  viewChild,
} from '@angular/core';
import { apiResource } from '@core/api/api-resource';
import { WorkspaceSession } from '@core/auth/workspace-session';
import type { PriceDto, PriceTargetDto } from '@core/api/schema';
import { formatDate, formatDay, formatModelDisplayName } from '@shared/browser/format';
import type { BadgeTone } from '@shared/ui/count-badge';
import {
  DataTable,
  DataTableColumn,
  DataTableRow,
  TableState,
  type TableColumn,
} from '@shared/ui/data-table';
import { Icon } from '@shared/ui/icon';
import { Notice } from '@shared/ui/notice';
import { SearchField } from '@shared/ui/search-field';
import { StatusBadge } from '@shared/ui/status-badge';
import { ViewSwitch } from '@shared/ui/view-switch';
import { BillingApi, chargeKind, providerName } from './billing-api';
import { PriceVersionDialog } from './price-version-dialog';

/** One priceable model or tool and the version that applies to it now. */
export interface CurrentPrice {
  key: string;
  provider: string;
  modelId: string;
  name: string;
  current: PriceDto | null;
  /** The earliest version scheduled after now. */
  next: PriceDto | null;
}
export interface PriceVersion {
  price: PriceDto;
  name: string;
  state: { label: string; tone: BadgeTone };
}
const providerOrder = ['google', 'ollama', 'searxng', 'brave'];
const keyOf = (x: { provider: string; modelId: string }) => `${x.provider}\n${x.modelId}`;
/** 伺服器只回傳最新的這麼多個版本；達到時提示歷史不完整。 */
const HISTORY_LIMIT = 500;
const rateFormat = new Intl.NumberFormat('zh-TW', { maximumFractionDigits: 8 });

/**
 * For each configured or priced model and tool, the version in effect now (its latest version not
 * after `now`) and the next scheduled one; and the history, newest first, each version with its
 * state. The server returns the latest 500 versions.
 */
export function priceCatalog(prices: PriceDto[], targets: PriceTargetDto[], now = Date.now()) {
  const names = new Map(targets.map((x) => [keyOf(x), x.displayName]));
  const rows = new Map<string, CurrentPrice>();
  const row = (x: { provider: string; modelId: string }, name: string) => {
    const key = keyOf(x);
    if (!rows.has(key))
      rows.set(key, {
        key,
        provider: x.provider,
        modelId: x.modelId,
        name,
        current: null,
        next: null,
      });
    return rows.get(key)!;
  };
  for (const target of targets) row(target, target.displayName);
  const sorted = [...prices].sort((a, b) => Date.parse(b.effectiveAt) - Date.parse(a.effectiveAt));
  for (const price of sorted) {
    const entry = row(price, names.get(keyOf(price)) ?? formatModelDisplayName(price));
    if (Date.parse(price.effectiveAt) > now) entry.next = price;
    else entry.current ??= price;
  }
  const current = [...rows.values()].sort(
    (a, b) => rank(a.provider) - rank(b.provider) || a.name.localeCompare(b.name, 'zh-Hant'),
  );
  const history: PriceVersion[] = sorted.map((price) => {
    const entry = rows.get(keyOf(price))!;
    return {
      price,
      name: entry.name,
      state:
        Date.parse(price.effectiveAt) > now
          ? { label: '已排定', tone: 'info' }
          : entry.current === price
            ? { label: '適用中', tone: 'success' }
            : { label: '已取代', tone: 'neutral' },
    };
  });
  return { current, history };
}
function rank(provider: string) {
  const index = providerOrder.indexOf(provider);
  return index < 0 ? providerOrder.length : index;
}

/**
 * Model and tool prices: what applies now and the version history, one view at a time. Versions are
 * never changed, so every edit opens the dialog that adds the next one.
 */
@Component({
  selector: 'nx-price-book',
  imports: [
    DataTable,
    DataTableColumn,
    DataTableRow,
    Icon,
    Notice,
    PriceVersionDialog,
    SearchField,
    StatusBadge,
    TableState,
    ViewSwitch,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  styleUrls: ['../administration/operations-tabs.scss', './price-book.scss'],
  templateUrl: './price-book.html',
})
export class PriceBook {
  readonly session = inject(WorkspaceSession);
  private readonly api = inject(BillingApi);
  /** Read each time the tab is shown: the price history and the models that can be priced. */
  readonly catalogRead = apiResource({
    feature: 'operations',
    loader: async () => {
      const [prices, targets] = await Promise.all([this.api.prices(), this.api.targets()]);
      return { prices, targets };
    },
  });
  readonly prices = computed<PriceDto[]>(() => this.catalogRead.value()?.prices ?? []);
  readonly targets = computed<PriceTargetDto[]>(() => this.catalogRead.value()?.targets ?? []);
  readonly catalog = computed(() => priceCatalog(this.prices(), this.targets()));
  readonly view = signal<'current' | 'history'>('current');
  readonly views = [
    { value: 'current', label: '目前價格' },
    { value: 'history', label: '價格歷史' },
  ];
  readonly search = signal('');
  readonly current = computed(() =>
    this.catalog().current.filter((x) => this.matches(x.name, x.provider)),
  );
  readonly history = computed(() =>
    this.catalog().history.filter((x) => this.matches(x.name, x.price.provider)),
  );
  readonly unpriced = computed(() => this.catalog().current.filter((x) => !x.current).length);
  readonly historyColumns: TableColumn[] = [
    { id: 'effective', label: '生效時間', hideable: false },
    { id: 'model', label: '模型或工具', hideable: false },
    { id: 'state', label: '狀態' },
    { id: 'input', label: '輸入' },
    { id: 'cached', label: '快取輸入' },
    { id: 'output', label: '輸出' },
    { id: 'request', label: '每次固定' },
    { id: 'note', label: '備註' },
  ];
  readonly loading = this.catalogRead.loading;
  readonly historyLimit = HISTORY_LIMIT;
  private readonly editor = viewChild.required(PriceVersionDialog);
  readonly kind = chargeKind;
  readonly provider = providerName;
  readonly date = formatDate;
  readonly day = formatDay;
  rate(value: number) {
    return rateFormat.format(value);
  }
  trigger(price: PriceDto) {
    return price.requestCharge === 'attempted' ? '開始呼叫即計' : '成功完成才計';
  }
  add() {
    this.editor().open();
  }
  adjust(row: CurrentPrice) {
    this.editor().open(
      row.current
        ? { from: 'current', price: row.current }
        : { from: 'target', provider: row.provider, modelId: row.modelId },
    );
  }
  reuse(version: PriceVersion) {
    this.editor().open({ from: 'history', price: version.price });
  }
  private matches(name: string, provider: string) {
    const query = this.search().trim().toLocaleLowerCase();
    return (
      !query ||
      name.toLocaleLowerCase().includes(query) ||
      providerName(provider).toLocaleLowerCase().includes(query)
    );
  }
}
