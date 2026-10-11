import {
  ChangeDetectionStrategy,
  Component,
  effect,
  ElementRef,
  inject,
  output,
  signal,
  viewChild,
} from '@angular/core';
import {
  FormField,
  form,
  maxLength,
  readonly as readonlyField,
  required,
} from '@angular/forms/signals';
import { ViewMorph } from '../../core/layout/view-morph';
import { ThemeService } from '../../core/preferences/theme-service';
import { Autosize } from '../../shared/browser/autosize';
import { isSubmitKey } from '../../shared/browser/submit-key';
import { Card } from '../../shared/ui/card';
import { Icon } from '../../shared/ui/icon';
import { InferenceSignal } from '../../shared/ui/inference-signal';
import { NexusIsland } from '../../shared/ui/island';
import { IslandService } from '../../shared/ui/island-service';
import { Notice } from '../../shared/ui/notice';
import { AttachmentList } from '../attachments/attachment-list';
import { LibraryPicker } from '../files/library-picker';
import { KnowledgePicker } from '../knowledge/knowledge-picker';
import { chatIslandItems } from './chat-island';
import { ChatStore } from './chat-store';
import { ComposerControls } from './composer-controls';
import { FocusComposer } from './focus-composer';

const SUGGESTIONS = [
  {
    icon: 'lines',
    title: '整理思緒',
    detail: '讓重點與下一步更清楚',
    text: '幫我整理以下筆記，歸納重點與待辦事項：\n',
  },
  {
    icon: 'document',
    title: '寫得更精準',
    detail: '打磨文字，保留你的觀點',
    text: '幫我修改以下文字，讓語氣清楚、自然且專業：\n',
  },
  {
    icon: 'idea',
    title: '拆解問題',
    detail: '從複雜問題找到可行方向',
    text: '和我一起分析以下問題，列出可行方向與需要確認的資訊：\n',
  },
];

/**
 * The prompt box with its attachments, model controls and the island of composer notices. It also keeps the Context
 * estimate current while the person types.
 */
@Component({
  selector: 'div[nxChatComposer]',
  imports: [
    AttachmentList,
    Autosize,
    Card,
    ComposerControls,
    FocusComposer,
    FormField,
    Icon,
    InferenceSignal,
    KnowledgePicker,
    LibraryPicker,
    NexusIsland,
    Notice,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  styleUrls: ['./chat-column.scss', './chat-composer.scss'],
  templateUrl: './chat-composer.html',
})
export class ChatComposer {
  readonly store = inject(ChatStore);
  private readonly themes = inject(ThemeService);
  private readonly morph = inject(ViewMorph);
  /** A prompt was handed to the store, so the thread can follow its answer. */
  readonly sent = output<void>();
  readonly suggestions = SUGGESTIONS;
  readonly composing = signal(false);
  readonly promptForm = form(this.store.draft, (schema) => {
    readonlyField(schema.text, {
      when: () =>
        this.store.loading() ||
        this.store.loadingConversation() ||
        !!this.store.selected()?.isArchived ||
        this.store.submitting() ||
        this.store.pendingSubmission(),
    });
    required(schema.text, { message: '請輸入訊息。' });
    maxLength(schema.text, () => this.store.policy().maxInputCharacters, {
      message: '訊息超過系統允許的字數，請縮短內容。',
    });
  });
  readonly focusComposer = viewChild.required(FocusComposer);
  private readonly textarea = viewChild.required<ElementRef<HTMLTextAreaElement>>('composer');

  constructor() {
    inject(IslandService).register(chatIslandItems());
    effect((onCleanup) => {
      const ready = this.store.ready(),
        modelId = this.store.modelId(),
        conversation = this.store.selected(),
        edit = this.store.editing(),
        prompt = this.store.draft().text,
        attachmentIds = this.store.attachments.files().map((file) => file.id),
        webSearch = this.store.webSearchEnabled();
      this.store.messages();
      this.store.knowledge.revision();
      this.store.contextUsage.set(null);
      if (!ready || !modelId) return;
      const controller = new AbortController();
      const timer = setTimeout(
        () =>
          void this.store.previewContext(
            {
              conversationId: conversation?.id ?? null,
              parentMessageId: edit ? edit.parentId : (conversation?.activeLeafId ?? null),
              prompt,
              modelId,
              attachmentIds,
              webSearch,
            },
            controller.signal,
          ),
        350,
      );
      onCleanup(() => {
        clearTimeout(timer);
        controller.abort();
      });
    });
  }

  focus() {
    requestAnimationFrame(() => this.textarea().nativeElement.focus());
  }
  use(text: string) {
    this.store.draft.set({ text });
    this.textarea().nativeElement.focus();
  }
  keydown(event: KeyboardEvent) {
    if (!isSubmitKey(event, this.composing(), this.store.personal.value().enterToSend)) return;
    event.preventDefault();
    this.submit();
  }
  submit(event?: Event) {
    event?.preventDefault();
    if (!this.store.canSend() || this.promptForm.text().invalid()) return;
    const send = () => {
      this.sent.emit();
      void this.store.send();
    };
    // The first prompt moves the composer from the centred start screen to the bottom.
    if (this.store.starting()) void this.morph.run(send);
    else send();
  }
  selectModel(id: string) {
    this.store.chooseModel(id);
    void this.store.savePreferences({
      ...this.themes.preferences(),
      defaultModelId: this.store.modelId() || null,
    });
  }
  uploadChanged(event: Event) {
    const input = event.target as HTMLInputElement;
    if (input.files) void this.store.attachments.upload(input.files);
    input.value = '';
  }
}
