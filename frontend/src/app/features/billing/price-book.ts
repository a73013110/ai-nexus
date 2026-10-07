import { IssueCode } from '../../shared/ui/issue-code';
import {
  ChangeDetectionStrategy,
  Component,
  computed,
  DestroyRef,
  ElementRef,
  inject,
  signal,
  viewChild,
} from '@angular/core';
import type { ModelPrice, PriceRequest, PriceTarget } from '../../core/api/types';
import { formatModelDisplayName } from '../../shared/browser/format';
import { ViewScope } from '../../shared/browser/view-scope';
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
@Component({
  selector: 'nx-price-book',
  imports: [IssueCode,Icon, Select],
  providers: [ViewScope],
  changeDetection: ChangeDetectionStrategy.OnPush,
  styleUrl: './price-book.scss',
  template: `<button class="secondary-button" type="button" (click)="open()">
      <nx-icon name="money" />模型與工具價格
    </button>
    <dialog
      #dialog
      class="platform-dialog price-dialog"
      aria-labelledby="price-title"
      (cancel)="close($event)"
    >
      <div class="dialog-scroll">
        <div class="dialog-heading">
          <div>
            <span class="panel-eyebrow">計費設定</span>
            <h2 id="price-title">模型與工具價格版本</h2>
          </div>
          <button class="icon-button" type="button" aria-label="關閉價格設定" (click)="close()">
            <nx-icon name="close" />
          </button>
        </div>
        <p class="form-note">
          價格只影響之後的呼叫。依你的方案填寫 API 價格，本機模型可使用內部成本；不同幣別分開統計。
        </p>
        @if (error()) {
          <p class="error-note" role="alert">{{ error() }}<nx-issue-code [message]="error()" /></p>
        }
        @if (notice()) {
          <p class="form-note" role="status">{{ notice() }}</p>
        }
        <form (submit)="save($event)" class="platform-form price-form">
          <div class="price-fields">
            <label
              >供應商<nx-select
                label="計費供應商"
                [options]="providers"
                [value]="draft().provider"
                (valueChange)="setProvider($event)"
            /></label>
            <label
              >模型或工具<nx-select
                label="計費模型或工具"
                [options]="modelChoices()"
                [value]="draft().modelId"
                [disabled]="loading() || !modelChoices().length"
                (valueChange)="field('modelId', $event)"
            /></label>
            <label
              >幣別<nx-select
                label="計費幣別"
                [options]="currencies"
                [value]="draft().currency"
                (valueChange)="field('currency', $event)"
            /></label>
            <label
              >費用類型<nx-select
                label="費用類型"
                [options]="kinds"
                [value]="draft().kind"
                (valueChange)="setKind($event)"
            /></label>
            @for (rate of rates; track rate.key) {
              <label
                >{{ rate.label
                }}<input
                  type="number"
                  min="0"
                  max="100000"
                  step="0.00000001"
                  required
                  [disabled]="draft().kind === 'free'"
                  [value]="draft()[rate.key]"
                  (input)="rateField(rate.key, $event)"
              /></label>
            }
            <label
              >單次計費時機<nx-select
                label="單次計費時機"
                [options]="triggers"
                [value]="draft().requestCharge"
                (valueChange)="field('requestCharge', $event)"
            /></label>
            <label
              >生效時間<input
                type="datetime-local"
                [value]="effective()"
                (input)="effective.set($any($event.target).value)"
              /><small>留空即儲存時生效。</small></label
            >
          </div>
          <label
            >價格依據或備註<textarea
              maxlength="500"
              rows="2"
              [value]="draft().note"
              (input)="field('note', $any($event.target).value)"
              placeholder="例如：內部每次處理成本，或合約／官方價格表日期"
            ></textarea>
          </label>
          <div class="dialog-actions">
            <button
              class="primary-button"
              type="submit"
              [disabled]="busy() || loading() || !draft().modelId.trim()"
            >
              {{ busy() ? '儲存中…' : '新增價格版本' }}
            </button>
          </div>
        </form>
        <section class="price-history" aria-label="價格歷史">
          <h3>價格歷史</h3>
          @if (loading()) {
            <p role="status">正在載入價格…</p>
          }
          @for (price of prices(); track price.id) {
            <button type="button" class="price-history-row" (click)="use(price)">
              <span
                ><strong>{{ modelName(price) }}</strong
                ><small
                  >{{ price.provider }} · {{ kind(price.kind) }} ·
                  {{ time(price.effectiveAt) }}</small
                ></span
              ><span
                >{{ price.currency
                }}<small
                  >輸入 {{ price.inputPerMillion }} / 輸出 {{ price.outputPerMillion }} / 每次
                  {{ price.perRequest }}</small
                ></span
              ><nx-icon name="copy" />
            </button>
          }
          @if (!loading() && !prices().length) {
            <p class="form-note">尚未設定價格。系統會明確標示未定價，不會把未知費用算成零。</p>
          }
        </section>
      </div>
    </dialog>`,
})
export class PriceBook {
  readonly dialog = viewChild.required<ElementRef<HTMLDialogElement>>('dialog');
  readonly draft = signal(emptyPrice());
  readonly effective = signal('');
  readonly prices = signal<ModelPrice[]>([]);
  readonly targets = signal<PriceTarget[]>([]);
  readonly modelName = formatModelDisplayName;
  readonly modelChoices = computed(() => {
    const choices = new Map<string, string>();
    for (const price of this.prices().filter((x) => x.provider === this.draft().provider))
      choices.set(price.modelId, this.modelName(price));
    for (const target of this.targets().filter((x) => x.provider === this.draft().provider))
      choices.set(target.modelId, target.displayName);
    return [...choices].map(([value, label]) => ({ value, label }));
  });
  readonly loading = signal(false);
  readonly busy = signal(false);
  readonly error = signal('');
  readonly notice = signal('');
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
  private requestSequence = 0;
  constructor() {
    inject(DestroyRef).onDestroy(() => this.dialog()?.nativeElement.close());
  }
  open() {
    this.draft.set(emptyPrice());
    this.effective.set('');
    this.error.set('');
    this.notice.set('');
    this.dialog().nativeElement.showModal();
    void this.load();
  }
  close(event?: Event) {
    if (this.busy()) {
      event?.preventDefault();
      return;
    }
    this.dialog().nativeElement.close();
  }
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
  use(value: ModelPrice) {
    const { id: _id, modelDisplayName: _label, ...price } = value;
    this.draft.set(price);
    this.effective.set('');
    this.notice.set('已帶入此版本。儲存會建立新版本，歷史價格保持原樣。');
  }
  time(value: string) {
    return new Date(value).toLocaleString('zh-TW');
  }
  async load() {
    const valid = this.scope.guard(),
      sequence = ++this.requestSequence;
    this.loading.set(true);
    try {
      const [rows, targets] = await Promise.all([this.api.prices(), this.api.targets()]);
      if (valid() && sequence === this.requestSequence) {
        this.prices.set(rows);
        this.targets.set(targets);
        if (!this.draft().modelId) this.field('modelId', this.modelChoices()[0]?.value ?? '');
      }
    } catch (e) {
      if (valid()) this.error.set(this.scope.message(e));
    } finally {
      if (valid() && sequence === this.requestSequence) this.loading.set(false);
    }
  }
  async save(event: Event) {
    event.preventDefault();
    if (this.busy()) return;
    const valid = this.scope.guard();
    this.busy.set(true);
    this.error.set('');
    try {
      const effectiveAt = (
        this.effective() ? new Date(this.effective()) : new Date()
      ).toISOString();
      await this.api.addPrice({
        ...this.draft(),
        modelId: this.draft().modelId.trim(),
        effectiveAt,
      });
      if (valid()) {
        this.notice.set('新價格版本已儲存，之後的呼叫會套用。');
        await this.load();
      }
    } catch (e) {
      if (valid()) this.error.set(this.scope.message(e));
    } finally {
      if (valid()) this.busy.set(false);
    }
  }
}
