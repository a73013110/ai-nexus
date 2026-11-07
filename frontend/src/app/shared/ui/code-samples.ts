import {
  ChangeDetectionStrategy,
  Component,
  computed,
  input,
  signal,
  type WritableSignal,
} from '@angular/core';
import { NgTemplateOutlet } from '@angular/common';
import { CodeBlock } from '../markdown/code-block';
import { Tabs, type TabItem } from './tabs';

/** 範例裡的一個檔案；`name` 是標題，也是讀者存檔時的檔名。 */
export interface CodeSampleFile {
  readonly name: string;
  /** highlight.js 的語言名稱或別名。 */
  readonly language: string;
  readonly source: string;
}

/** 一種語言（或平台）的範例，可以有多個檔案（類別＋用法）。 */
export interface CodeSample {
  readonly id: string;
  readonly label: string;
  /** 分頁裡、檔案上方的一句說明，例如執行環境與相依套件。 */
  readonly note?: string;
  readonly files: readonly CodeSampleFile[];
}

/**
 * 把 `{{name}}` 換成 `values` 的值；沒有給值的保留原樣，讀者看得出要自己填。
 * 換行統一成 LF：Windows 簽出（core.autocrlf）的原始檔帶 CR，在 pre 裡會多出空白。
 */
export function fillPlaceholders(source: string, values: Readonly<Record<string, string>>) {
  return source
    .replace(/\r\n?/g, '\n')
    .replace(/\{\{(\w+)\}\}/g, (placeholder, name: string) =>
      Object.hasOwn(values, name) ? values[name] : placeholder,
    );
}

const STORAGE_PREFIX = 'ai-nexus.code-samples.';
// 同一個 preferenceKey 的元件共用一個 signal：在抽屜選了 Java，同頁的對話框打開也是 Java。
const preferences = new Map<string, WritableSignal<string>>();

function preference(key: string) {
  let chosen = preferences.get(key);
  if (!chosen) {
    let stored = '';
    try {
      stored = localStorage.getItem(STORAGE_PREFIX + key) ?? '';
    } catch {
      /* 選用偏好。 */
    }
    chosen = signal(stored);
    preferences.set(key, chosen);
  }
  return chosen;
}

/**
 * `preferenceKey` 目前選的範例（沒選過或選項已不存在時是第一個）；在 computed 裡呼叫會跟著切換。
 * 頁面要依讀者選的語言產生內容（例如下載整份說明）時用它，不另存一份選擇。
 */
export function selectedCodeSample(samples: readonly CodeSample[], key: string) {
  return pick(samples, preference(key)());
}

function pick(samples: readonly CodeSample[], id: string): CodeSample | undefined {
  return samples.find((sample) => sample.id === id) ?? samples[0];
}

/**
 * 多語言的程式碼範例：語言分頁，每個檔案一個可複製、上色的 `CodeBlock`。
 * 範例內容由呼叫端提供（原始檔以文字匯入），`values` 換掉其中的 `{{name}}`；給 `preferenceKey` 時記住上次選的語言。
 */
@Component({
  selector: 'nx-code-samples',
  imports: [CodeBlock, NgTemplateOutlet, Tabs],
  changeDetection: ChangeDetectionStrategy.OnPush,
  styles: `
    :host {
      display: grid;
      gap: var(--p-space-3);
      min-width: 0;
    }
    nx-tabs {
      padding-inline: 0;
    }
    [role='tabpanel'] {
      display: grid;
      gap: var(--p-space-3);
      min-width: 0;
    }
    .code-samples-note {
      margin: 0;
      color: var(--secondary);
      font-size: var(--p-font-xs);
    }
  `,
  template: `<ng-template #fileList>
      @if (active()?.note; as note) {
        <p class="code-samples-note">{{ note }}</p>
      }
      @for (file of files(); track file.name) {
        <nx-code-block [label]="file.name" [value]="file.source" [language]="file.language" />
      }
    </ng-template>
    @if (active(); as sample) {
      @if (tabs().length > 1) {
        <nx-tabs
          #tabList
          [label]="label()"
          [items]="tabs()"
          [value]="sample.id"
          (valueChange)="choose($event)"
        />
        <div
          role="tabpanel"
          [id]="tabList.panelId(sample.id)"
          [attr.aria-labelledby]="tabList.tabId(sample.id)"
        >
          <ng-container [ngTemplateOutlet]="fileList" />
        </div>
      } @else {
        <ng-container [ngTemplateOutlet]="fileList" />
      }
    }`,
})
export class CodeSamples {
  /** 語言分頁的報讀名稱。 */
  readonly label = input.required<string>();
  readonly samples = input.required<readonly CodeSample[]>();
  readonly values = input<Readonly<Record<string, string>>>({});
  /** 記住選擇的鍵；同一個鍵的範例一起切換。 */
  readonly preferenceKey = input<string | null>(null);

  private readonly local = signal('');
  private readonly chosen = computed(() => {
    const key = this.preferenceKey();
    return key ? preference(key) : this.local;
  });
  readonly tabs = computed<TabItem[]>(() =>
    this.samples().map((sample) => ({ value: sample.id, label: sample.label })),
  );
  readonly active = computed(() => pick(this.samples(), this.chosen()()));
  readonly files = computed(() => {
    const values = this.values();
    return (this.active()?.files ?? []).map((file) => ({
      ...file,
      source: fillPlaceholders(file.source, values),
    }));
  });

  choose(id: string) {
    this.chosen().set(id);
    const key = this.preferenceKey();
    if (!key) return;
    try {
      localStorage.setItem(STORAGE_PREFIX + key, id);
    } catch {
      /* 選用偏好。 */
    }
  }
}
