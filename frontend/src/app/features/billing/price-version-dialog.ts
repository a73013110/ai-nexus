import {
  ChangeDetectionStrategy,
  Component,
  ElementRef,
  computed,
  inject,
  input,
  output,
  signal,
  viewChild,
} from '@angular/core';
import { WorkspaceSession } from '../../core/auth/workspace-session';
import type { PriceDto, PriceRequest, PriceTargetDto } from '../../core/api/schema';
import {
  formatDate,
  formatModelDisplayName,
  parseDateTimeInput,
} from '../../shared/browser/format';
import { ViewScope } from '../../shared/browser/view-scope';
import { CompactDialog } from '../../shared/ui/compact-dialog';
import { ConfirmDialog } from '../../shared/ui/confirm-dialog';
import { DateTimePicker } from '../../shared/ui/date-time-picker';
import { DialogHeader } from '../../shared/ui/dialog-header';
import { DialogSaveBar } from '../../shared/ui/dialog-save-bar';
import { Field } from '../../shared/ui/field';
import { InfoPopover } from '../../shared/ui/info-popover';
import { Notice } from '../../shared/ui/notice';
import { Select } from '../../shared/ui/select';
import { ToastService } from '../../shared/ui/toast-service';
import { BillingApi, chargeKind, providerName } from './billing-api';

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
const samePrice = (a: PriceRequest, b: PriceRequest) =>
  (Object.keys(a) as (keyof PriceRequest)[]).every((key) => a[key] === b[key]);
type RateKey = 'inputPerMillion' | 'cachedInputPerMillion' | 'outputPerMillion' | 'perRequest';
/** Where a draft starts: nothing, a model's current price, a model without one, or an old version. */
export type PriceSeed =
  | { from: 'blank' }
  | { from: 'current' | 'history'; price: PriceDto }
  | { from: 'target'; provider: string; modelId: string };

/**
 * Adds a price version. Versions are never changed: starting from an existing one copies its fields
 * into a new draft. The footer reads 「說明｜還原｜儲存」 and both buttons stay disabled until the
 * draft differs from where it started; closing with changes asks first.
 */
@Component({
  selector: 'nx-price-version-dialog',
  imports: [
    CompactDialog,
    ConfirmDialog,
    DateTimePicker,
    DialogHeader,
    DialogSaveBar,
    Field,
    InfoPopover,
    Notice,
    Select,
  ],
  providers: [ViewScope],
  changeDetection: ChangeDetectionStrategy.OnPush,
  styleUrl: './price-version-dialog.scss',
  templateUrl: './price-version-dialog.html',
})
export class PriceVersionDialog {
  readonly prices = input<PriceDto[]>([]);
  readonly targets = input<PriceTargetDto[]>([]);
  readonly saved = output<void>();
  private readonly api = inject(BillingApi);
  private readonly scope = inject(ViewScope);
  private readonly toast = inject(ToastService);
  private readonly session = inject(WorkspaceSession);
  private readonly dialog = viewChild.required<ElementRef<HTMLDialogElement>>('dialog');
  private readonly picker = viewChild(DateTimePicker);
  private readonly confirmation = viewChild.required(ConfirmDialog);
  /** The form is built on first open and kept, so the closing dialog does not empty. */
  readonly opened = signal(false);
  readonly seed = signal<PriceSeed>({ from: 'blank' });
  private readonly baseline = signal(emptyPrice());
  readonly draft = signal(emptyPrice());
  readonly effective = signal('');
  readonly saving = signal(false);
  readonly error = signal('');
  readonly locked = computed(() => !!this.session.auth.session()?.testing);
  readonly dirty = computed(() => !!this.effective() || !samePrice(this.draft(), this.baseline()));
  readonly detail = computed(() => {
    const seed = this.seed();
    if (seed.from === 'history')
      return `已帶入此版本（${formatDate(seed.price.effectiveAt)} 生效）。儲存會建立新版本，歷史價格保持原樣。`;
    if (seed.from === 'current') return '以目前價格為起點；儲存會建立新版本，歷史價格保持原樣。';
    return '價格只影響之後的呼叫；不同幣別分開統計。';
  });
  /** Replaces the save bar's own 尚未變更／有未儲存的變更 while writes are refused. */
  readonly note = computed(() => (this.locked() ? '測試身分期間不能修改管理設定' : ''));
  readonly rateError = computed(() =>
    this.draft().cachedInputPerMillion > this.draft().inputPerMillion
      ? '快取輸入不得高於一般輸入。'
      : '',
  );
  readonly canSave = computed(
    () =>
      !this.locked() &&
      !this.saving() &&
      this.dirty() &&
      !!this.draft().modelId.trim() &&
      !this.rateError() &&
      (this.picker()?.valid() ?? true),
  );
  readonly providers = ['google', 'ollama', 'searxng', 'brave'].map((value) => ({
    value,
    label: providerName(value),
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
  readonly rates: readonly { key: RateKey; label: string }[] = [
    { key: 'inputPerMillion', label: '輸入 / 百萬 tokens' },
    { key: 'cachedInputPerMillion', label: '快取輸入 / 百萬 tokens' },
    { key: 'outputPerMillion', label: '輸出 / 百萬 tokens' },
    { key: 'perRequest', label: '每次呼叫固定費用' },
  ];
  /** The provider's models and tools by display name: configured targets and priced ones. */
  readonly modelChoices = computed(() => {
    const provider = this.draft().provider,
      choices = new Map<string, string>();
    for (const price of this.prices().filter((x) => x.provider === provider))
      choices.set(price.modelId, formatModelDisplayName(price));
    for (const target of this.targets().filter((x) => x.provider === provider))
      choices.set(target.modelId, target.displayName);
    return [...choices].map(([value, label]) => ({ value, label }));
  });
  open(seed: PriceSeed = { from: 'blank' }) {
    let draft: PriceRequest;
    if (seed.from === 'current' || seed.from === 'history') {
      const { id, modelDisplayName, ...price } = seed.price;
      draft = { ...price, effectiveAt: '' };
    } else if (seed.from === 'target')
      draft = { ...emptyPrice(), provider: seed.provider, modelId: seed.modelId };
    else draft = emptyPrice();
    this.seed.set(seed);
    this.draft.set(draft);
    if (!draft.modelId) this.draft.update((x) => ({ ...x, modelId: this.firstChoice() }));
    this.baseline.set(this.draft());
    this.effective.set('');
    this.resetPicker();
    this.error.set('');
    this.opened.set(true);
    this.dialog().nativeElement.showModal();
  }
  field<K extends keyof PriceRequest>(key: K, value: PriceRequest[K]) {
    this.draft.update((x) => ({ ...x, [key]: value }));
  }
  setProvider(provider: string) {
    this.draft.update((x) => ({ ...x, provider, modelId: '' }));
    this.field('modelId', this.firstChoice());
  }
  setKind(kind: string) {
    this.field('kind', kind);
    if (kind === 'free')
      this.draft.update((x) => ({
        ...x,
        inputPerMillion: 0,
        cachedInputPerMillion: 0,
        outputPerMillion: 0,
        perRequest: 0,
      }));
  }
  rateField(key: RateKey, event: Event) {
    const value = (event.target as HTMLInputElement).valueAsNumber;
    this.field(key, Number.isFinite(value) ? value : 0);
  }
  revert() {
    this.draft.set(this.baseline());
    this.effective.set('');
    this.resetPicker();
    this.error.set('');
  }
  /** Escape, the close button and the sheet's swipe arrive here; unsaved changes are confirmed. */
  async cancel(event: Event) {
    event.preventDefault();
    if (this.saving()) return;
    if (
      this.dirty() &&
      !(await this.confirmation().ask({
        title: '放棄未儲存的變更？',
        message: '這個價格版本尚未儲存，關閉後會捨棄填寫的內容。',
        confirm: '放棄變更',
      }))
    )
      return;
    this.dialog().nativeElement.close();
  }
  async save(event: Event) {
    event.preventDefault();
    if (!this.canSave()) return;
    const valid = this.scope.guard();
    this.saving.set(true);
    this.error.set('');
    try {
      const effectiveAt = (
        this.effective() ? parseDateTimeInput(this.effective()) : new Date()
      ).toISOString();
      await this.api.addPrice({
        ...this.draft(),
        modelId: this.draft().modelId.trim(),
        note: this.draft().note.trim(),
        effectiveAt,
      });
      if (!valid()) return;
      this.dialog().nativeElement.close();
      this.toast.success('新價格版本已儲存', '之後的呼叫會套用。');
      this.saved.emit();
    } catch (e) {
      if (valid()) this.error.set(this.scope.message(e));
    } finally {
      if (valid()) this.saving.set(false);
    }
  }
  private firstChoice() {
    return this.modelChoices()[0]?.value ?? '';
  }
  /** Typed text the picker could not read stays in the field until it is cleared here. */
  private resetPicker() {
    const picker = this.picker();
    picker?.text.set(null);
    picker?.touched.set(false);
  }
}
