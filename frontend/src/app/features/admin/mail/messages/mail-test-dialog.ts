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
import type { MailSampleDto } from '@core/api/schema';
import { action } from '@shared/browser/action';
import { Checkbox } from '@shared/ui/checkbox';
import { CompactDialog } from '@shared/ui/compact-dialog';
import { CopyValue } from '@shared/ui/copy-value';
import { DialogHeader } from '@shared/ui/dialog-header';
import { Field } from '@shared/ui/field';
import { Notice } from '@shared/ui/notice';
import { Select, type SelectOption } from '@shared/ui/select';
import { StatusBadge } from '@shared/ui/status-badge';
import { MailApi } from '../mail-api';
import { mailPriority } from './mail-presentation';
import {
  type MailSampleValues,
  changedValues,
  sampleError,
  sampleExamples,
  sampleUsage,
} from './mail-sample';
import { DialogActions } from '@shared/ui/dialog-actions';

const types: Record<string, string> = { text: '文字', number: '整數', flag: '是否', time: '時間' };

/**
 * 寄信測試：選一種已登記的系統信、調整參數後寄出，順便顯示開發者入列這封信的 C# 寫法。
 * 信照常排隊、節流與重試，送出後回傳訊息 ID，結果要在紀錄裡看；伺服器每人每分鐘限 3 封。
 */
@Component({
  selector: 'nx-mail-test-dialog',
  imports: [
    DialogActions,
    Checkbox,
    CompactDialog,
    CopyValue,
    DialogHeader,
    Field,
    Notice,
    Select,
    StatusBadge,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  styleUrl: './mail-test-dialog.scss',
  templateUrl: './mail-test-dialog.html',
})
export class MailTestDialog {
  private readonly api = inject(MailApi);
  readonly from = input('');
  /** 入列成功，帶新訊息的 ID。 */
  readonly sent = output<string>();
  readonly to = signal('');
  readonly samples = signal<MailSampleDto[]>([]);
  readonly category = signal('');
  readonly values = signal<MailSampleValues>({});
  /** 讀範本（每次開啟都重讀）與寄出；兩者的失敗都顯示在對話框內。 */
  readonly reading = action();
  readonly sending = action();
  readonly busy = this.sending.pending;
  readonly error = computed(() => this.sending.error() || this.reading.error());
  readonly types = types;
  readonly options = computed<SelectOption[]>(() =>
    this.samples().map((x) => ({ value: x.category, label: x.name, description: x.category })),
  );
  readonly sample = computed(() => this.samples().find((x) => x.category === this.category()));
  readonly priority = computed(() => mailPriority(this.sample()?.priority ?? ''));
  readonly invalid = computed(() => {
    const sample = this.sample();
    return sample ? sampleError(sample, this.values()) : '';
  });
  readonly usage = computed(() => {
    const sample = this.sample();
    return sample ? sampleUsage(sample, this.values()) : '';
  });
  private readonly dialog = viewChild.required<ElementRef<HTMLDialogElement>>('dialog');
  private readonly field = viewChild.required<ElementRef<HTMLInputElement>>('field');

  open() {
    this.reading.reset();
    this.sending.error.set('');
    this.dialog().nativeElement.showModal();
    requestAnimationFrame(() => this.field().nativeElement.focus());
    // 每次開啟重讀：範例值含操作者與當下時間，也可能剛部署了新的範本。
    void this.load();
  }
  choose(category: string) {
    this.category.set(category);
    this.reset();
  }
  reset() {
    const sample = this.sample();
    this.values.set(sample ? sampleExamples(sample) : {});
  }
  set(name: string, value: string) {
    this.values.update((x) => ({ ...x, [name]: value }));
  }
  async send(event: Event) {
    event.preventDefault();
    const to = this.to().trim();
    const sample = this.sample();
    if (!to || !sample || this.invalid()) return;
    const outcome = await this.sending.run(() =>
      this.api.test(to, sample.category, changedValues(sample, this.values())),
    );
    if (!outcome.ok) return;
    this.dialog().nativeElement.close();
    this.sent.emit(outcome.value.messageId);
  }

  private async load() {
    const outcome = await this.reading.run(() => this.api.samples());
    if (!outcome.ok) return;
    const samples = outcome.value;
    this.samples.set(samples);
    // 保留上次選的範本與填寫中的值；範本已不存在時回到第一個（設定測試信）。
    if (!samples.some((x) => x.category === this.category()))
      this.choose(samples[0]?.category ?? '');
  }
}
