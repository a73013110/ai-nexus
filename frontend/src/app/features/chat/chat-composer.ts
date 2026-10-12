import {
  ChangeDetectionStrategy,
  Component,
  computed,
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
import { ActionMenu, type MenuAction } from '../../shared/ui/action-menu';
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
    ActionMenu,
    AttachmentList,
    Autosize,
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
  styleUrls: ['./chat-column.scss', './chat-composer.scss', './chat-start.scss'],
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
  private readonly fileInput = viewChild.required<ElementRef<HTMLInputElement>>('fileInput');
  private readonly library = viewChild.required(LibraryPicker);
  private readonly knowledge = viewChild(KnowledgePicker);
  private readonly toolsAnchor = viewChild.required('toolsMenu', { read: ElementRef });
  /** The narrow composer's tools menu: the same actions its wide row shows as separate controls. */
  readonly tools = computed<MenuAction[]>(() => {
    const store = this.store;
    const sources = store.knowledge.ids().length;
    return [
      { id: 'upload', label: '加入文件或圖片', icon: 'paperclip', disabled: !store.canUpload() },
      { id: 'library', label: '從檔案庫加入', icon: 'files', disabled: !store.canUpload() },
      ...(store.hasKnowledgeAccess()
        ? [
            {
              id: 'knowledge',
              label: sources ? `知識來源（${sources}）` : '知識來源',
              icon: 'library',
              disabled:
                store.busy() || store.loadingConversation() || !!store.selected()?.isArchived,
            },
          ]
        : []),
      ...(store.webSearchStatus().available
        ? [
            {
              id: 'web',
              label: '搜尋網路',
              icon: 'globe',
              checked: store.webSearchEnabled(),
              disabled: store.busy(),
            },
          ]
        : []),
    ];
  });

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
  tool(id: string) {
    if (id === 'upload') this.fileInput().nativeElement.click();
    else if (id === 'library') this.library().open();
    else if (id === 'knowledge') this.knowledge()?.open(this.toolsAnchor().nativeElement);
    else if (id === 'web') this.store.webSearchEnabled.update((on) => !on);
  }
  uploadChanged(event: Event) {
    const input = event.target as HTMLInputElement;
    if (input.files) void this.store.attachments.upload(input.files);
    input.value = '';
  }
}
