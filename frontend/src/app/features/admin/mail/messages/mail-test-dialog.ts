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
import { safeMessage } from '@core/errors/safe-errors';
import { Checkbox } from '@shared/ui/checkbox';
import { CompactDialog } from '@shared/ui/compact-dialog';
import { CopyValue } from '@shared/ui/copy-value';
import { DialogHeader } from '@shared/ui/dialog-header';
import { Field } from '@shared/ui/field';
import { Notice } from '@shared/ui/notice';
import { PendingButton } from '@shared/ui/pending-button';
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

const types: Record<string, string> = { text: '文字', number: '整數', flag: '是否', time: '時間' };

/**
 * 寄信測試：選一種已登記的系統信、調整參數後寄出，順便顯示開發者入列這封信的 C# 寫法。
 * 信照常排隊、節流與重試，送出後回傳訊息 ID，結果要在紀錄裡看；伺服器每人每分鐘限 3 封。
 */
@Component({
  selector: 'nx-mail-test-dialog',
  imports: [
    Checkbox,
    CompactDialog,
    CopyValue,
    DialogHeader,
    Field,
    Notice,
    PendingButton,
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
  readonly loading = signal(false);
  readonly busy = signal(false);
  readonly error = signal('');
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
    this.error.set('');
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
    if (!to || !sample || this.invalid() || this.busy()) return;
    this.busy.set(true);
    this.error.set('');
    try {
      const result = await this.api.test(to, sample.category, changedValues(sample, this.values()));
      this.dialog().nativeElement.close();
      this.sent.emit(result.messageId);
    } catch (error) {
      this.error.set(safeMessage(error));
    } finally {
      this.busy.set(false);
    }
  }

  private async load() {
    this.loading.set(true);
    try {
      const samples = await this.api.samples();
      this.samples.set(samples);
      // 保留上次選的範本與填寫中的值；範本已不存在時回到第一個（設定測試信）。
      if (!samples.some((x) => x.category === this.category()))
        this.choose(samples[0]?.category ?? '');
    } catch (error) {
      this.error.set(safeMessage(error));
    } finally {
      this.loading.set(false);
    }
  }
}
