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
import type { PriceDto, PriceRequest, PriceTargetDto } from '@core/api/schema';
import { formatDate, formatModelDisplayName, parseDateTimeInput } from '@shared/browser/format';
import { uniqueId } from '@shared/browser/unique-id';
import { ViewScope } from '@shared/browser/view-scope';
import { CompactDialog } from '@shared/ui/compact-dialog';
import { ConfirmDialog } from '@shared/ui/confirm-dialog';
import { DateTimePicker } from '@shared/ui/date-time-picker';
import { DialogHeader } from '@shared/ui/dialog-header';
import { DialogSaveBar } from '@shared/ui/dialog-save-bar';
import { DraftEditor } from '@shared/ui/draft-editor';
import { Field } from '@shared/ui/field';
import { InfoPopover } from '@shared/ui/info-popover';
import { Notice } from '@shared/ui/notice';
import { Select } from '@shared/ui/select';
import { ToastService } from '@shared/ui/toast-service';
import { BillingApi, PROVIDERS, chargeKind, providerName } from '../billing-api';
import { priceVersionTour } from '../prices-tour';

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
 * 新增一個價格版本。版本不會被修改：從既有版本開始時複製其欄位成新草稿。
 * 底部為「說明｜還原｜儲存」，草稿與起點相同時兩個按鈕都停用；不同時，關閉前會詢問。
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
  private readonly dialog = viewChild.required<ElementRef<HTMLDialogElement>>('dialog');
  private readonly picker = viewChild(DateTimePicker);
  private readonly confirmation = viewChild.required(ConfirmDialog);
  /** 區塊標題與費率錯誤的 DOM ID 前綴（`aria-labelledby`、`aria-describedby`）。 */
  readonly id = uniqueId('nx-price-version');
  /** 表單在首次開啟時建立並保留，讓關閉中的對話框不會被清空。 */
  readonly editor = new DraftEditor<PriceRequest, { seed: PriceSeed }>(samePrice);
  /** 生效時間另存：留白是「立即」，不屬於從起點複製的欄位，填了就算有變更。 */
  readonly effective = signal('');
  readonly saving = signal(false);
  readonly error = signal('');
  readonly dirty = computed(() => !!this.effective() || this.editor.dirty());
  readonly detail = computed(() => {
    const seed = this.editor.current()?.seed;
    if (seed?.from === 'history')
      return `已帶入此版本（${formatDate(seed.price.effectiveAt)} 生效）。儲存會建立新版本，歷史價格保持原樣。`;
    if (seed?.from === 'current') return '以目前價格為起點；儲存會建立新版本，歷史價格保持原樣。';
    return '價格只影響之後的呼叫；不同幣別分開統計。';
  });
  readonly tour = computed(() => {
    const from = this.editor.current()?.seed.from;
    return priceVersionTour({ copied: from === 'current' || from === 'history' });
  });
  readonly rateError = computed(() => {
    const draft = this.editor.current()?.draft;
    return draft && draft.cachedInputPerMillion > draft.inputPerMillion
      ? '快取輸入不得高於一般輸入。'
      : '';
  });
  readonly canSave = computed(
    () =>
      !this.saving() &&
      this.dirty() &&
      !!this.editor.current()?.draft.modelId.trim() &&
      !this.rateError() &&
      (this.picker()?.valid() ?? true),
  );
  readonly providers = PROVIDERS.map((value) => ({
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
  readonly modelChoices = computed(() =>
    this.choicesFor(this.editor.current()?.draft.provider ?? ''),
  );
  open(seed: PriceSeed = { from: 'blank' }) {
    let draft: PriceRequest;
    if (seed.from === 'current' || seed.from === 'history') {
      const { id, modelDisplayName, ...price } = seed.price;
      draft = { ...price, effectiveAt: '' };
    } else if (seed.from === 'target')
      draft = { ...emptyPrice(), provider: seed.provider, modelId: seed.modelId };
    else draft = emptyPrice();
    if (!draft.modelId) draft = { ...draft, modelId: this.firstChoice(draft.provider) };
    this.editor.open({ seed }, draft);
    this.effective.set('');
    this.resetPicker();
    this.error.set('');
    this.dialog().nativeElement.showModal();
  }
  field<K extends keyof PriceRequest>(key: K, value: PriceRequest[K]) {
    this.editor.update(key, value);
  }
  setProvider(provider: string) {
    this.editor.patch({ provider, modelId: this.firstChoice(provider) });
  }
  setKind(kind: string) {
    this.editor.patch(
      kind === 'free'
        ? { kind, inputPerMillion: 0, cachedInputPerMillion: 0, outputPerMillion: 0, perRequest: 0 }
        : { kind },
    );
  }
  rateField(key: RateKey, event: Event) {
    const value = (event.target as HTMLInputElement).valueAsNumber;
    this.field(key, Number.isFinite(value) ? value : 0);
  }
  revert() {
    this.editor.revert();
    this.effective.set('');
    this.resetPicker();
    this.error.set('');
  }
  /** Escape、關閉鈕與抽屜的滑動都會到這裡；未儲存的變更會先確認。 */
  async cancel(event: Event) {
    event.preventDefault();
    if (this.saving()) return;
    if (
      !this.dirty() ||
      (await this.confirmation().discard('這個價格版本尚未儲存，關閉後會捨棄填寫的內容。'))
    )
      this.dialog().nativeElement.close();
  }
  async save(event: Event) {
    event.preventDefault();
    const draft = this.editor.current()?.draft;
    if (!draft || !this.canSave()) return;
    const valid = this.scope.guard();
    this.saving.set(true);
    this.error.set('');
    try {
      const effectiveAt = (
        this.effective() ? parseDateTimeInput(this.effective()) : new Date()
      ).toISOString();
      await this.api.addPrice({
        ...draft,
        modelId: draft.modelId.trim(),
        note: draft.note.trim(),
        effectiveAt,
      });
      if (!valid()) return;
      this.editor.markSaved();
      this.effective.set('');
      this.dialog().nativeElement.close();
      this.toast.success('新價格版本已儲存', '之後的呼叫會套用。');
      this.saved.emit();
    } catch (error) {
      if (valid()) this.error.set(this.scope.message(error));
    } finally {
      if (valid()) this.saving.set(false);
    }
  }
  /** 供應商的模型與工具，依顯示名稱排列：已設定的目標與已定價的。 */
  private choicesFor(provider: string) {
    const choices = new Map<string, string>();
    for (const price of this.prices())
      if (price.provider === provider) choices.set(price.modelId, formatModelDisplayName(price));
    // 已設定目標的名稱優先於價格紀錄上的名稱。
    for (const target of this.targets())
      if (target.provider === provider) choices.set(target.modelId, target.displayName);
    return [...choices].map(([value, label]) => ({ value, label }));
  }
  private firstChoice(provider: string) {
    return this.choicesFor(provider)[0]?.value ?? '';
  }
  /** 選擇器無法讀取的輸入文字，會留在欄位中，直到在此清除。 */
  private resetPicker() {
    const picker = this.picker();
    picker?.text.set(null);
    picker?.touched.set(false);
  }
}
