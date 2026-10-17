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
import { WorkspaceSession } from '@core/auth/workspace-session';
import type { PriceDto, PriceRequest, PriceTargetDto } from '@core/api/schema';
import { formatDate, formatModelDisplayName, parseDateTimeInput } from '@shared/browser/format';
import { ViewScope } from '@shared/browser/view-scope';
import { CompactDialog } from '@shared/ui/compact-dialog';
import { ConfirmDialog } from '@shared/ui/confirm-dialog';
import { DateTimePicker } from '@shared/ui/date-time-picker';
import { DialogHeader } from '@shared/ui/dialog-header';
import { DialogSaveBar } from '@shared/ui/dialog-save-bar';
import { Field } from '@shared/ui/field';
import { InfoPopover } from '@shared/ui/info-popover';
import { Notice } from '@shared/ui/notice';
import { Select } from '@shared/ui/select';
import { ToastService } from '@shared/ui/toast-service';
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
const samePrice = (first: PriceRequest, second: PriceRequest) =>
  (Object.keys(first) as (keyof PriceRequest)[]).every((key) => first[key] === second[key]);
type RateKey = 'inputPerMillion' | 'cachedInputPerMillion' | 'outputPerMillion' | 'perRequest';
/** 草稿的起點：空白、模型目前的價格、尚未定價的模型，或舊版本。 */
export type PriceSeed =
  | { from: 'blank' }
  | { from: 'current' | 'history'; price: PriceDto }
  | { from: 'target'; provider: string; modelId: string };

/**
 * 新增一個價格版本。版本不會被修改：從既有版本開始，會複製其欄位，
 * into a new draft. The footer reads 「說明｜還原｜儲存」 and both buttons stay disabled until the
 * 草稿與起點不同時，關閉前會詢問。
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
  /** 表單在首次開啟時建立並保留，讓關閉中的對話框不會被清空。 */
  readonly opened = signal(false);
  readonly seed = signal<PriceSeed>({ from: 'blank' });
  private readonly baseline = signal(emptyPrice());
  readonly draft = signal(emptyPrice());
  readonly effective = signal('');
  readonly saving = signal(false);
  readonly error = signal('');
  readonly locked = this.session.testing;
  readonly dirty = computed(() => !!this.effective() || !samePrice(this.draft(), this.baseline()));
  readonly detail = computed(() => {
    const seed = this.seed();
    if (seed.from === 'history')
      return `已帶入此版本（${formatDate(seed.price.effectiveAt)} 生效）。儲存會建立新版本，歷史價格保持原樣。`;
    if (seed.from === 'current') return '以目前價格為起點；儲存會建立新版本，歷史價格保持原樣。';
    return '價格只影響之後的呼叫；不同幣別分開統計。';
  });
  /** Replaces the save bar's own 尚未變更／有未儲存的變更 while writes are refused. */
  readonly note = this.session.testingNote;
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
  /** 供應商的模型與工具，依顯示名稱排列：已設定的目標與已定價的。 */
  readonly modelChoices = computed(() => {
    const provider = this.draft().provider,
      choices = new Map<string, string>();
    for (const price of this.prices().filter((item) => item.provider === provider))
      choices.set(price.modelId, formatModelDisplayName(price));
    for (const target of this.targets().filter((item) => item.provider === provider))
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
    if (!draft.modelId) this.draft.update((item2) => ({ ...item2, modelId: this.firstChoice() }));
    this.baseline.set(this.draft());
    this.effective.set('');
    this.resetPicker();
    this.error.set('');
    this.opened.set(true);
    this.dialog().nativeElement.showModal();
  }
  field<K extends keyof PriceRequest>(key: K, value: PriceRequest[K]) {
    this.draft.update((item3) => ({ ...item3, [key]: value }));
  }
  setProvider(provider: string) {
    this.draft.update((item4) => ({ ...item4, provider, modelId: '' }));
    this.field('modelId', this.firstChoice());
  }
  setKind(kind: string) {
    this.field('kind', kind);
    if (kind === 'free')
      this.draft.update((item5) => ({
        ...item5,
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
  /** Escape、關閉鈕與抽屜的滑動都會到這裡；未儲存的變更會先確認。 */
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
    } catch (error2) {
      if (valid()) this.error.set(this.scope.message(error2));
    } finally {
      if (valid()) this.saving.set(false);
    }
  }
  private firstChoice() {
    return this.modelChoices()[0]?.value ?? '';
  }
  /** 選擇器無法讀取的輸入文字，會留在欄位中，直到在此清除。 */
  private resetPicker() {
    const picker = this.picker();
    picker?.text.set(null);
    picker?.touched.set(false);
  }
}
