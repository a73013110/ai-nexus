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
import { ViewMorph } from '@core/layout/view-morph';
import { ThemeService } from '@core/preferences/theme-service';
import { Autosize } from '@shared/browser/autosize';
import { isSubmitKey } from '@shared/browser/submit-key';
import { ActionMenu, type MenuAction } from '@shared/ui/action-menu';
import { Icon } from '@shared/ui/icon';
import { InferenceSignal } from '@shared/ui/inference-signal';
import { Island } from '@shared/ui/island';
import { IslandService } from '@shared/ui/island-service';
import { Notice } from '@shared/ui/notice';
import { AttachmentList } from '@features/work/files/attachment-list';
import { LibraryPicker } from '@features/work/files/file-library/library-picker';
import { KnowledgePicker } from '@features/work/knowledge/collections/knowledge-picker';
import { chatIslandItems } from './chat-island';
import { ChatStore } from '../chat-store';
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
 * 提示框，含附件、模型控制項，以及輸入框通知的島。它也會在使用者輸入時，
 * 讓 Context 的估算保持最新。
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
    Island,
    Notice,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  styleUrls: ['../chat-column.scss', './chat-composer.scss', './chat-start.scss'],
  templateUrl: './chat-composer.html',
})
export class ChatComposer {
  readonly store = inject(ChatStore);
  private readonly themes = inject(ThemeService);
  private readonly morph = inject(ViewMorph);
  /** 提示已交給 store，讓 thread 能跟隨其回答。 */
  readonly sent = output<void>();
  readonly suggestions = SUGGESTIONS;
  readonly composing = signal(false);
  readonly promptForm = form(this.store.draft, (schema) => {
    readonlyField(schema.text, {
      when: () =>
        this.store.conversationLocked() ||
        this.store.submitting() ||
        this.store.pendingSubmission(),
    });
    required(schema.text, { message: '請輸入訊息。' });
    maxLength(schema.text, () => this.store.policy().maxInputCharacters, {
      message: '訊息超過系統允許的字數，請縮短內容。',
    });
  });
  /** 送出需要 store 就緒，且提示不能是空白、長度需在上限之內。 */
  readonly canSubmit = computed(
    () =>
      this.store.canSend() && !!this.store.draft().text.trim() && !this.promptForm.text().invalid(),
  );
  readonly focusComposer = viewChild.required(FocusComposer);
  private readonly textarea = viewChild.required<ElementRef<HTMLTextAreaElement>>('composer');
  private readonly fileInput = viewChild.required<ElementRef<HTMLInputElement>>('fileInput');
  private readonly library = viewChild.required(LibraryPicker);
  private readonly knowledge = viewChild(KnowledgePicker);
  private readonly toolsAnchor = viewChild.required('toolsMenu', { read: ElementRef });
  private contextScope = '';
  /** 窄版輸入框的工具選單：寬版那一列以獨立控制項顯示的同樣動作。 */
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
              disabled: !store.canPickKnowledge(),
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
      // 上一次的估算會保留到下一次抵達，讓輸入時的用量環、預算通知與送出閘門維持穩定；
      // 只有切換到其他模型或對話時才停止套用。
      const scope = ready && modelId ? `${modelId}|${conversation?.id ?? ''}` : '';
      if (scope !== this.contextScope) this.store.contextUsage.set(null);
      this.contextScope = scope;
      this.store.contextPending.set(!!scope);
      if (!scope) return;
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
    if (!this.canSubmit()) return;
    const send = () => {
      this.sent.emit();
      void this.store.send();
    };
    // 第一則提問會讓輸入框從置中的起始畫面移到底部。
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
