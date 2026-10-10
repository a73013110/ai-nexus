import { Notice } from '../../shared/ui/notice';
import { CompactDialog } from '../../shared/ui/compact-dialog';
import { DialogMotion, ViewMotion } from '../../shared/ui/view-motion';
import { MarkdownEditor } from '../../shared/markdown/markdown-editor';
import { Field } from '../../shared/ui/field';
import {
  ChangeDetectionStrategy,
  afterRenderEffect,
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
import { ViewSwitch } from '../../shared/ui/view-switch';
import { InferenceSignal } from '../../shared/ui/inference-signal';
import { ArtifactsApi } from './artifacts-api';
import { TEXT_ACTIONS, TEXT_ACTION_ICON_PROVIDER } from './text-actions';

@Component({
  selector: 'nx-text-tools',
  imports: [
    Notice,
    CompactDialog,
    DialogMotion,
    ViewMotion,
    MarkdownEditor,
    Field,
    Icon,
    ViewSwitch,
    InferenceSignal,
  ],
  providers: [ViewScope, CopyFeedback, TEXT_ACTION_ICON_PROVIDER],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `<dialog
    nxCompactDialog
    #surface="nxCompactDialog"
    #dialog
    class="platform-dialog text-tools-dialog"
    [class.is-expanded]="expanded()"
    [nxDialogView]="expanded()"
    (cancel)="cancel($event)"
  >
    <div class="text-tools-layout">
      <div class="dialog-heading">
        <h2>{{ mode() === 'save' ? '儲存成果文件' : labels[mode()] }}</h2>
        <div class="page-actions">
          <button
            class="icon-button"
            [attr.aria-label]="expanded() ? '縮小段落工具' : '展開段落工具'"
            [attr.aria-pressed]="expanded()"
            (click)="expanded.set(!expanded())"
          >
            <nx-icon [name]="expanded() ? 'minimize' : 'maximize'" />
          </button>
          <button class="icon-button" aria-label="關閉段落工具" (click)="close()">
            <nx-icon name="close" />
          </button>
        </div>
      </div>
      @if (error()) {
        <nx-notice tone="danger" [message]="error()" />
      }
      @if (surface.opened()) {
        @if (mode() === 'save') {
          <form class="text-tools-content" [nxViewMotion]="mode()" (submit)="save($event)">
            <label class="text-tools-title"
              >成果名稱<input
                nxField
                autofocus
                aria-label="成果名稱"
                maxlength="120"
                required
                [value]="title()"
                (input)="title.set($any($event.target).value)" /></label
            ><nx-markdown-editor label="成果內容" [(content)]="result" [disabled]="saving()" />
            <div class="dialog-actions">
              <button
                type="button"
                class="secondary-button"
                (click)="close()"
                [disabled]="saving()"
              >
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
          <div class="text-tools-content" [nxViewMotion]="mode()">
            <details class="text-tool-source">
              <summary>
                選取原文 <span>{{ source().length.toLocaleString() }} 字元</span>
              </summary>
              <p>{{ source() }}</p>
            </details>
            @if (mode() === 'translate') {
              <nx-view-switch
                label="翻譯語言"
                appearance="segment"
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
              <nx-markdown-editor label="段落處理結果" [(content)]="result" [disabled]="busy()" />
            } @else if (!busy()) {
              <p class="form-note text-tools-placeholder">選擇語言或開始處理，結果會顯示在這裡。</p>
            }
            @if (truncated()) {
              <nx-notice tone="warning">
                模型輸出已達上限，結果可能不完整；請核對或縮小選取範圍後重試。
              </nx-notice>
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
          </div>
        }
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
  private readonly surface = viewChild.required<CompactDialog>('surface');
  readonly mode = signal('save');
  readonly source = signal('');
  readonly result = signal('');
  readonly expanded = signal(false);
  readonly title = signal('工作成果');
  readonly language = signal('English');
  readonly truncated = signal(false);
  readonly busy = signal(false);
  readonly saving = signal(false);
  readonly error = signal('');
  readonly labels: Record<string, string> = Object.fromEntries(
    TEXT_ACTIONS.map((action) => [action.value, `${action.label}段落`]),
  );
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
    afterRenderEffect(() => {
      if (this.surface().opened() && this.mode() === 'save')
        this.dialog().nativeElement.querySelector<HTMLElement>('[autofocus]')?.focus();
    });
    inject(DestroyRef).onDestroy(() => this.controller?.abort());
  }
  open(text: string, action: string, message: string | null = null) {
    this.controller?.abort();
    this.busy.set(false);
    this.error.set('');
    this.source.set(text);
    this.result.set(action === 'save' ? text : '');
    this.mode.set(action);
    this.expanded.set(false);
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
