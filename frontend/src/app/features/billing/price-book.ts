import { apiResource } from '../../core/api/api-resource';
import { Notice } from '../../shared/ui/notice';
import { ToastService } from '../../shared/ui/toast-service';
import { Card } from '../../shared/ui/card';
import { Field } from '../../shared/ui/field';
import { DateTimePicker } from '../../shared/ui/date-time-picker';
import {
  ChangeDetectionStrategy,
  Component,
  computed,
  ElementRef,
  inject,
  signal,
  viewChild,
  linkedSignal,
} from '@angular/core';
import type { PriceDto, PriceRequest, PriceTargetDto } from '../../core/api/schema';
import {
  formatDate,
  formatModelDisplayName,
  parseDateTimeInput,
} from '../../shared/browser/format';
import { ViewScope } from '../../shared/browser/view-scope';
import { ThemeService } from '../../core/preferences/theme-service';
import { Icon } from '../../shared/ui/icon';
import { Select } from '../../shared/ui/select';
import { BillingApi, chargeKind } from './billing-api';

const emptyPrice = (): PriceRequest => ({
  provider: 'google',
  modelId: '',
  currency: 'USD',
  kind: 'api',
  inputPerMillion: 0,
  cachedInputPerMillion: 0,
  outputPerMillion: 0,
  perRequest: 0,
  requestCharge: 'completed',
  effectiveAt: '',
  note: '',
});
interface PriceCatalog {
  prices: PriceDto[];
  targets: PriceTargetDto[];
}

/** Model and tool price versions: the history and a form that adds the next version. */
@Component({
  selector: 'nx-price-book',
  imports: [Notice, Card, Field, DateTimePicker, Icon, Select],
  providers: [ViewScope],
  changeDetection: ChangeDetectionStrategy.OnPush,
  styleUrl: './price-book.scss',
  templateUrl: './price-book.html',
})
export class PriceBook {
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
  /** The form; a blank model picks the provider's first known model once the catalog loads. */
  readonly draft = linkedSignal<PriceCatalog | undefined, PriceRequest>({
    source: this.catalogRead.value,
    computation: (catalog, previous) => {
      const draft = previous?.value ?? emptyPrice();
      if (draft.modelId || !catalog) return draft;
      const first =
        catalog.prices.find((x) => x.provider === draft.provider)?.modelId ??
        catalog.targets.find((x) => x.provider === draft.provider)?.modelId ??
        '';
      return { ...draft, modelId: first };
    },
  });
  readonly effective = signal('');
  private readonly effectivePicker = viewChild.required(DateTimePicker);
  readonly modelName = formatModelDisplayName;
  readonly modelChoices = computed(() => {
    const choices = new Map<string, string>();
    for (const price of this.prices().filter((x) => x.provider === this.draft().provider))
      choices.set(price.modelId, this.modelName(price));
    for (const target of this.targets().filter((x) => x.provider === this.draft().provider))
      choices.set(target.modelId, target.displayName);
    return [...choices].map(([value, label]) => ({ value, label }));
  });
  readonly loading = this.catalogRead.refreshing;
  readonly busy = signal(false);
  readonly saveError = signal('');
  readonly notice = signal('');
  private readonly toast = inject(ToastService);
  readonly providers = ['google', 'ollama', 'searxng', 'brave'].map((value) => ({
    value,
    label: value,
  }));
  readonly currencies = ['USD', 'TWD', 'EUR', 'JPY'].map((value) => ({ value, label: value }));
  readonly kinds = ['api', 'internal', 'free'].map((value) => ({
    value,
    label: chargeKind(value),
  }));
  readonly triggers = [
    { value: 'completed', label: '成功完成時' },
    { value: 'attempted', label: '已開始呼叫時（包含失敗）' },
  ];
  readonly rates = [
    { key: 'inputPerMillion', label: '輸入 / 百萬 tokens' },
    { key: 'cachedInputPerMillion', label: '快取輸入 / 百萬 tokens' },
    { key: 'outputPerMillion', label: '輸出 / 百萬 tokens' },
    { key: 'perRequest', label: '每次呼叫固定費用' },
  ] as const;
  readonly kind = chargeKind;
  private readonly api = inject(BillingApi);
  private readonly scope = inject(ViewScope);
  private readonly themes = inject(ThemeService);
  private readonly editor = viewChild.required<ElementRef<HTMLElement>>('editor');
  field(key: keyof PriceRequest, value: string) {
    this.draft.update((x) => ({ ...x, [key]: value }));
  }
  setProvider(provider: string) {
    this.draft.update((x) => ({ ...x, provider, modelId: '' }));
    this.field('modelId', this.modelChoices()[0]?.value ?? '');
  }
  rateField(key: string, event: Event) {
    const value = (event.target as HTMLInputElement).valueAsNumber;
    this.draft.update((x) => ({ ...x, [key]: Number.isFinite(value) ? value : 0 }));
  }
  setKind(value: string) {
    this.field('kind', value);
    if (value === 'free')
      this.draft.update((x) => ({
        ...x,
        inputPerMillion: 0,
        cachedInputPerMillion: 0,
        outputPerMillion: 0,
        perRequest: 0,
      }));
  }
  use(value: PriceDto) {
    const { id, modelDisplayName, ...price } = value;
    this.draft.set(price);
    this.effective.set('');
    this.notice.set('已帶入此版本。儲存會建立新版本，歷史價格保持原樣。');
    this.editor().nativeElement.scrollIntoView({
      block: 'start',
      behavior: this.themes.scrollBehavior(),
    });
  }
  readonly time = formatDate;
  async save(event: Event) {
    event.preventDefault();
    if (this.busy() || !this.effectivePicker().valid()) return;
    const valid = this.scope.guard();
    this.busy.set(true);
    this.saveError.set('');
    try {
      const effectiveAt = (
        this.effective() ? parseDateTimeInput(this.effective()) : new Date()
      ).toISOString();
      await this.api.addPrice({
        ...this.draft(),
        modelId: this.draft().modelId.trim(),
        effectiveAt,
      });
      if (valid()) {
        this.notice.set('');
        this.toast.success('新價格版本已儲存', '之後的呼叫會套用。');
        this.catalogRead.reload();
      }
    } catch (e) {
      if (valid()) this.saveError.set(this.scope.message(e));
    } finally {
      if (valid()) this.busy.set(false);
    }
  }
}
