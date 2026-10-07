import { IssueCode } from '../../shared/ui/issue-code';
import {
  ChangeDetectionStrategy,
  Component,
  DestroyRef,
  ElementRef,
  inject,
  input,
  output,
  signal,
  viewChild,
} from '@angular/core';
import { Router } from '@angular/router';
import { WorkspaceSession } from '../../core/auth/workspace-session';
import { ViewScope } from '../../shared/browser/view-scope';
import { CopyFeedback } from '../../shared/browser/copy-feedback';
import { Icon } from '../../shared/ui/icon';
import { Select } from '../../shared/ui/select';
import { InferenceSignal } from '../../shared/ui/inference-signal';
import { ArtifactsApi } from './artifacts-api';

@Component({
  selector: 'nx-text-tools',
  imports: [IssueCode,Icon, Select, InferenceSignal],
  providers: [ViewScope, CopyFeedback],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `<dialog #dialog class="platform-dialog text-tools-dialog" (cancel)="cancel($event)">
    <div class="dialog-scroll">
      <div class="dialog-heading">
        <h2>{{ mode() === 'save' ? '儲存成果文件' : labels[mode()] }}</h2>
        <button class="icon-button" aria-label="關閉段落工具" (click)="close()">
          <nx-icon name="close" />
        </button>
      </div>
      @if (error()) {
        <p class="error-banner" role="alert">{{ error() }}<nx-issue-code [message]="error()" /></p>
      }
      @if (mode() === 'save') {
        <form class="platform-form" (submit)="save($event)">
          <label
            >成果名稱<input
              autofocus
              aria-label="成果名稱"
              maxlength="120"
              required
              [value]="title()"
              (input)="title.set($any($event.target).value)" /></label
          ><label
            >內容<textarea
              aria-label="成果內容"
              rows="10"
              maxlength="64000"
              required
              [value]="result()"
              (input)="result.set($any($event.target).value)"
            ></textarea>
          </label>
          <div class="dialog-actions">
            <button type="button" class="secondary-button" (click)="close()" [disabled]="saving()">
              取消</button
            ><button
              class="primary-button"
              [disabled]="saving() || !title().trim() || !result().trim()"
            >
              {{ saving() ? '正在儲存…' : '儲存並開啟' }}
            </button>
          </div>
        </form>
      } @else {
        <p class="text-tool-source">{{ source() }}</p>
        @if (mode() === 'translate') {
          <nx-select
            label="翻譯語言"
            [value]="language()"
            [options]="languages"
            [disabled]="busy()"
            (valueChange)="language.set($event)"
          />
        }
        @if (busy()) {
          <p class="upload-status" role="status">
            <nx-inference-signal [active]="true" />模型正在處理選取段落…<button
              class="quiet-button"
              (click)="stop()"
            >
              停止
            </button>
          </p>
        }
        @if (result()) {
          <label class="text-tool-result"
            >處理結果<textarea
              aria-label="段落處理結果"
              rows="10"
              maxlength="64000"
              [value]="result()"
              (input)="result.set($any($event.target).value)"
            ></textarea>
          </label>
        }
        @if (truncated()) {
          <p class="source-warning">
            模型輸出已達上限，結果可能不完整；請核對或縮小選取範圍後重試。
          </p>
        }
        <div class="dialog-actions">
          <button class="secondary-button" [disabled]="busy() || saving()" (click)="generate()">
            {{ result() ? '重新處理' : '開始處理' }}
          </button>
          @if (result()) {
            <button class="secondary-button" (click)="copy.copy(result())">
              {{ copy.copied() ? '已複製' : '複製結果' }}
            </button>
            @if (canReplace()) {
              <button class="primary-button" (click)="replace.emit(result()); close()">
                套用到選取段落
              </button>
            }
            @if (session.has('artifacts')) {
              <button class="secondary-button" (click)="mode.set('save')">另存成果</button>
            }
          }
        </div>
      }
    </div>
  </dialog>`,
})
export class TextTools {
  private readonly api = inject(ArtifactsApi);
  private readonly router = inject(Router);
  private readonly scope = inject(ViewScope);
  readonly session = inject(WorkspaceSession);
  readonly copy = inject(CopyFeedback);
  readonly modelId = input<string | null>(null);
  readonly projectId = input<string | null>(null);
  readonly canReplace = input(false);
  readonly replace = output<string>();
  readonly dialog = viewChild.required<ElementRef<HTMLDialogElement>>('dialog');
  readonly mode = signal('save');
  readonly source = signal('');
  readonly result = signal('');
  readonly title = signal('工作成果');
  readonly language = signal('English');
  readonly truncated = signal(false);
  readonly busy = signal(false);
  readonly saving = signal(false);
  readonly error = signal('');
  readonly labels: Record<string, string> = {
    rewrite: '改寫段落',
    summarize: '摘要段落',
    translate: '翻譯段落',
  };
  readonly languages = ['English', '繁體中文', '日本語', '简体中文'].map((value) => ({
    value,
    label:
      value === 'English'
        ? '英文'
        : value === '日本語'
          ? '日文'
          : value === '简体中文'
            ? '簡體中文'
            : value,
  }));
  private controller?: AbortController;
  private sourceMessage: string | null = null;
  constructor() {
    inject(DestroyRef).onDestroy(() => this.controller?.abort());
  }
  open(text: string, action: string, message: string | null = null) {
    this.controller?.abort();
    this.busy.set(false);
    this.error.set('');
    this.source.set(text);
    this.result.set(action === 'save' ? text : '');
    this.mode.set(action);
    this.sourceMessage = message;
    this.truncated.set(false);
    this.title.set(
      text
        .split('\n')
        .find((x) => x.trim())
        ?.replace(/^#+\s*/, '')
        .slice(0, 80) || '工作成果',
    );
    this.dialog().nativeElement.showModal();
    if (action !== 'save' && action !== 'translate') void this.generate();
  }
  async generate() {
    if (this.busy()) return;
    const valid = this.scope.guard(),
      controller = (this.controller = new AbortController());
    this.busy.set(true);
    this.error.set('');
    try {
      const value = await this.api.transform(
        this.source(),
        this.mode(),
        this.modelId(),
        this.language(),
        controller.signal,
      );
      if (valid() && this.controller === controller) {
        this.result.set(value.text);
        this.truncated.set(value.truncated);
      }
    } catch (error) {
      if (valid() && !controller.signal.aborted) this.error.set(this.scope.message(error));
    } finally {
      if (valid() && this.controller === controller) this.busy.set(false);
    }
  }
  async save(event: Event) {
    event.preventDefault();
    if (this.saving()) return;
    const valid = this.scope.guard();
    this.saving.set(true);
    this.error.set('');
    try {
      const artifact = await this.api.create(
        this.title(),
        this.result(),
        this.sourceMessage,
        this.projectId(),
      );
      if (valid()) {
        this.dialog().nativeElement.close();
        await this.router.navigate(['/artifacts', artifact.resource.id]);
      }
    } catch (error) {
      if (valid()) this.error.set(this.scope.message(error));
    } finally {
      if (valid()) this.saving.set(false);
    }
  }
  stop() {
    this.controller?.abort();
    this.busy.set(false);
  }
  close() {
    if (this.saving()) return;
    this.stop();
    this.dialog().nativeElement.close();
  }
  cancel(event: Event) {
    if (this.saving()) event.preventDefault();
    else this.stop();
  }
}
