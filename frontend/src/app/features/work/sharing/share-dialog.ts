import { Notice } from '@shared/ui/notice';
import { CompactDialog } from '@shared/ui/compact-dialog';
import { Field } from '@shared/ui/field';
import {
  ChangeDetectionStrategy,
  Component,
  ElementRef,
  inject,
  input,
  signal,
  viewChild,
  computed,
} from '@angular/core';
import { RouterLink } from '@angular/router';
import type { DirectoryUserDto, ShareDto } from '@core/api/schema';
import { ViewScope } from '@shared/browser/view-scope';
import { CopyFeedback } from '@shared/browser/copy-feedback';
import { Select } from '@shared/ui/select';
import { DialogHeader } from '@shared/ui/dialog-header';
import { Icon } from '@shared/ui/icon';
import { SharingApi } from './sharing-api';
import { directorySearch } from '@features/work/collaboration/directory-search';
import { Checkbox } from '@shared/ui/checkbox';
import { routeOf } from '@core/api/catalog';

type ShareKind = 'conversation' | 'artifact';

/** 與後端 `ShareLink.MaxRecipients` 一致；超過時伺服器以驗證錯誤拒絕。 */
const MAX_RECIPIENTS = 20;

/** 收件者開啟的分享頁完整網址；路徑由功能目錄推得，模組換區時跟著走，不寫字面值。 */
export function shareLink(id: string, base = document.baseURI) {
  return new URL(`${routeOf('sharing')}/${id}`, base).href;
}

@Component({
  selector: 'nx-share-dialog',
  imports: [Notice, CompactDialog, DialogHeader, Field, Select, Icon, RouterLink, Checkbox],
  providers: [ViewScope, CopyFeedback],
  changeDetection: ChangeDetectionStrategy.OnPush,
  styleUrl: './share-dialog.scss',
  template: `<dialog
    nxCompactDialog
    size="md"
    #dialog
    class="platform-dialog"
    (cancel)="busy() ? $event.preventDefault() : null"
  >
    <nx-dialog-header heading="建立唯讀分享" [detail]="name()" closeLabel="關閉分享視窗" />
    <div class="dialog-body">
      @if (error()) {
        <nx-notice tone="danger" [message]="error()" />
      }
      @if (created(); as share) {
        <div class="share-created">
          <nx-icon name="check" />
          <h3>分享已建立</h3>
          <p>指定帳號登入後，即可從「分享」找到內容。<br />後續修改不會自動公開。</p>
          <button class="primary-button" (click)="copy.copy(link(share.id))">
            {{ copy.copied() ? '已複製連結' : '複製分享連結' }}</button
          ><a
            [routerLink]="[routeOf('sharing'), share.id]"
            (click)="dialog.close()"
            class="secondary-button"
            >檢視分享</a
          >
        </div>
      } @else {
        <nx-notice tone="warning">
          {{
            kind() === 'artifact' ? '分享選取的已儲存成果版本' : '分享目前可見的對話分支'
          }}。收件者只能閱讀，無法修改原始內容；需要更新時請建立新的分享。
        </nx-notice>
        <label
          >收件者<input
            nxField
            type="search"
            aria-label="搜尋分享收件者"
            placeholder="輸入至少兩個字，搜尋公司帳號"
            maxlength="120"
            [disabled]="busy()"
            [value]="people.text()"
            (input)="people.find($any($event.target).value)"
        /></label>
        <div class="share-recipients">
          @for (user of recipients(); track user.id) {
            <button class="share-recipient" [disabled]="busy()" (click)="remove(user.id)">
              {{ user.displayName }}<nx-icon name="close" />
            </button>
          }
        </div>
        @if (people.searchable()) {
          <div class="directory-results">
            @for (user of people.results(); track user.id) {
              <button class="directory-user" [disabled]="busy() || full()" (click)="add(user)">
                <span
                  ><strong>{{ user.displayName }}</strong
                  ><small>{{ user.account }}</small></span
                ><nx-icon name="plus" />
              </button>
            } @empty {
              <p class="form-note">尚無符合且未加入的帳號。</p>
            }
          </div>
        }
        <label
          >有效期限<nx-select
            label="分享有效期限"
            [options]="durations"
            [value]="hours()"
            [disabled]="busy()"
            (valueChange)="hours.set($event)"
        /></label>
        @if (kind() === 'conversation') {
          <nx-checkbox
            label="包含此分支的附件"
            description="收件者可下載；到期或撤銷後停止存取。"
            [checked]="includeAttachments()"
            [disabled]="busy()"
            (checkedChange)="includeAttachments.set($event)"
          />
        }
      }
    </div>
    @if (!created()) {
      <footer class="dialog-foot">
        <p class="dialog-note">
          最多 {{ maxRecipients }} 位收件者。建立後可在「我分享的」隨時撤銷。
        </p>
        <button class="secondary-button" [disabled]="busy()" (click)="dialog.close()">取消</button>
        <button
          class="primary-button"
          [disabled]="busy() || !recipients().length"
          (click)="create()"
        >
          {{ busy() ? '正在建立…' : '建立分享' }}
        </button>
      </footer>
    }
  </dialog>`,
})
export class ShareDialog {
  /** 範本裡的連結由功能目錄取得網址。 */
  readonly routeOf = routeOf;
  readonly kind = input.required<ShareKind>();
  readonly sourceId = input.required<string>();
  readonly name = input('');
  readonly version = input<number | null>(null);
  private readonly dialog = viewChild.required<ElementRef<HTMLDialogElement>>('dialog');
  readonly maxRecipients = MAX_RECIPIENTS;
  readonly recipients = signal<DirectoryUserDto[]>([]);
  readonly full = computed(() => this.recipients().length >= MAX_RECIPIENTS);
  readonly people = directorySearch(() => this.recipients().map((item) => item.id));
  readonly hours = signal('168');
  readonly includeAttachments = signal(false);
  readonly created = signal<ShareDto | null>(null);
  readonly busy = signal(false);
  private readonly createError = signal('');
  readonly error = computed(() => this.createError() || this.people.error());
  readonly copy = inject(CopyFeedback);
  private readonly api = inject(SharingApi);
  private readonly scope = inject(ViewScope);
  /** 開啟當下的來源；建立期間宿主可能換成別的對話或成果，送出與結果都以此為準。 */
  private context?: { kind: ShareKind; id: string; version: number | null };
  readonly durations = [
    { value: '1', label: '1 小時' },
    { value: '24', label: '1 天' },
    { value: '168', label: '7 天' },
    { value: '720', label: '30 天' },
  ];
  open() {
    if (this.busy()) return;
    this.context = { kind: this.kind(), id: this.sourceId(), version: this.version() };
    this.created.set(null);
    this.createError.set('');
    this.recipients.set([]);
    this.people.clear();
    this.includeAttachments.set(false);
    this.dialog().nativeElement.showModal();
  }
  link(id: string) {
    return shareLink(id);
  }
  add(user: DirectoryUserDto) {
    if (!this.full() && !this.recipients().some((item) => item.id === user.id))
      this.recipients.update((all) => [...all, user]);
  }
  remove(id: string) {
    this.recipients.update((all) => all.filter((item) => item.id !== id));
  }
  async create() {
    if (this.busy() || !this.recipients().length || !this.context) return;
    const context = this.context,
      alive = this.scope.guard();
    // busy 期間 open() 不會換掉 context，只需確認宿主仍指向同一個來源。
    const valid = () => alive() && context.id === this.sourceId();
    this.busy.set(true);
    this.createError.set('');
    try {
      const value = await this.api.create({
        kind: context.kind,
        sourceId: context.id,
        recipientIds: this.recipients().map((item) => item.id),
        hours: Number(this.hours()),
        includeAttachments: this.includeAttachments(),
        artifactVersion: context.version,
      });
      if (valid()) this.created.set(value);
    } catch (failure) {
      if (valid()) this.createError.set(this.scope.message(failure));
    } finally {
      if (alive()) this.busy.set(false);
    }
  }
}
