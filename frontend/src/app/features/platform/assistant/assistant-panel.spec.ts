import { Component, signal, viewChild } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import type { ModelsDto } from '@core/api/schema';
import { ClientIssues } from '@core/errors/client-issues';
import { defaultSettings, UserSettingsService } from '@core/preferences/user-settings';
import { AssistantConversation, type AssistantSource } from './assistant-conversation';
import { AssistantPanel } from './assistant-panel';

const catalog = {
  models: [{ id: 'local', reasoningEfforts: [], defaultReasoningEffort: 'auto' }],
  policy: { allowModelSelection: true, showModelNames: true, defaultModelId: 'local' },
  notice: null,
} as unknown as ModelsDto;

const reply = () =>
  new Response(
    [
      [
        'grounding',
        {
          scope: '10/01–10/31 · 1 人',
          facts: [
            { label: '查詢範圍', text: '10/01–10/31。' },
            { label: '王小明', text: '未補足 1 天。' },
          ],
          partial: false,
        },
      ],
      ['delta', { text: '王小明未補足 1 天 [2]。' }],
      ['done', { truncated: false }],
    ]
      .map(([event, data]) => `event: ${event}\ndata: ${JSON.stringify(data)}\n\n`)
      .join(''),
  );

@Component({
  imports: [AssistantPanel],
  template: `<nx-assistant-panel
    page="出勤紀錄"
    [conversation]="chat"
    [suggestions]="['誰還沒補足？']"
  />`,
})
class Host {
  readonly ask = vi.fn<AssistantSource['ask']>(async () => reply());
  readonly chat = new AssistantConversation({
    ask: this.ask,
    context: () => 'A',
    models: async () => catalog,
    preferredModelId: () => null,
  });
  readonly panel = viewChild.required(AssistantPanel);
}

describe('AssistantPanel', () => {
  beforeEach(() => {
    vi.stubGlobal('matchMedia', () => ({
      matches: false,
      addEventListener: () => undefined,
      removeEventListener: () => undefined,
    }));
    // jsdom 沒有捲動與 ResizeObserver；這裡只需要面板內容渲染出來。
    Element.prototype.scrollIntoView ??= () => undefined;
    vi.stubGlobal(
      'ResizeObserver',
      class {
        observe() {}
        unobserve() {}
        disconnect() {}
      },
    );
    TestBed.configureTestingModule({
      providers: [
        { provide: ClientIssues, useValue: {} },
        { provide: UserSettingsService, useValue: { value: signal(defaultSettings()) } },
      ],
    });
  });
  afterEach(() => vi.unstubAllGlobals());

  async function open() {
    const fixture = TestBed.createComponent(Host);
    fixture.detectChanges();
    await fixture.whenStable();
    // 島常駐，但第一次展開前不建立面板內容。
    const host = fixture.nativeElement as HTMLElement;
    expect(host.querySelector('.ui-floating[data-mode="island"]')).not.toBeNull();
    expect(host.querySelector('textarea')).toBeNull();
    // 平常只有圓鈕，指到才露出要問哪一頁。
    const island = host.querySelector<HTMLElement>('.ui-floating-island')!;
    expect(host.querySelector('.assistant-island-text')).toBeNull();
    island.dispatchEvent(new MouseEvent('pointerenter'));
    await fixture.whenStable();
    expect(host.querySelector('.assistant-island-text')?.textContent).toBe('問「出勤紀錄」');
    host.querySelector<HTMLButtonElement>('.ui-floating-island-open')!.click();
    await fixture.whenStable();
    return { fixture, host };
  }

  it('asks a suggestion and cites the fact the answer points to', async () => {
    const { fixture, host } = await open();
    host.querySelector<HTMLButtonElement>('.assistant-suggestions button')!.click();
    await vi.waitFor(() => expect(fixture.componentInstance.chat.busy()).toBe(false));
    await fixture.whenStable();

    expect(host.querySelector('.prompt-bubble')?.textContent).toBe('誰還沒補足？');
    expect(host.querySelector('.assistant-facts')).toBeNull();
    const cite = host.querySelector<HTMLButtonElement>('.markdown-cite')!;
    expect(cite.getAttribute('aria-label')).toBe('依據 2');
    cite.click();
    await fixture.whenStable();
    expect(host.querySelector('.assistant-facts li.is-cited')?.textContent).toContain('王小明');
    expect(host.querySelector('.assistant-facts-toggle')?.getAttribute('aria-expanded')).toBe(
      'true',
    );
  });

  it('sends on Enter but leaves Shift+Enter and IME composition alone', async () => {
    const { fixture, host } = await open();
    const textarea = host.querySelector('textarea')!;
    expect(textarea.labels?.[0]?.textContent).toBe('提問');
    textarea.value = '我有遲到嗎？';
    textarea.dispatchEvent(new Event('input'));
    textarea.dispatchEvent(new KeyboardEvent('keydown', { key: 'Enter', shiftKey: true }));
    textarea.dispatchEvent(new KeyboardEvent('keydown', { key: 'Enter', isComposing: true }));
    expect(fixture.componentInstance.ask).not.toHaveBeenCalled();

    textarea.dispatchEvent(new KeyboardEvent('keydown', { key: 'Enter', cancelable: true }));
    await vi.waitFor(() => expect(fixture.componentInstance.ask).toHaveBeenCalledOnce());
    expect(fixture.componentInstance.panel().draft()).toBe('');
  });

  it('shows the answer progress on the island while minimized and previews the reply', async () => {
    let finish!: () => void;
    const { fixture, host } = await open();
    fixture.componentInstance.ask.mockImplementation(
      () => new Promise<Response>((resolve) => (finish = () => resolve(reply()))),
    );
    host.querySelector<HTMLButtonElement>('.assistant-suggestions button')!.click();
    await vi.waitFor(() => expect(fixture.componentInstance.ask).toHaveBeenCalled());
    host.querySelector<HTMLButtonElement>('[aria-label="縮小成動態島"]')!.click();
    await fixture.whenStable();
    const island = host.querySelector('.ui-floating-island-open')!;
    expect(island.getAttribute('aria-label')).toBe('助理，正在回答…');
    expect(host.querySelector('.assistant-island-stop')).not.toBeNull();

    finish();
    await vi.waitFor(() => expect(fixture.componentInstance.chat.busy()).toBe(false));
    await fixture.whenStable();
    expect(host.querySelector('.assistant-island-copy')?.textContent).toBe(
      '已回答王小明未補足 1 天。',
    );
    island.dispatchEvent(new MouseEvent('click', { bubbles: true }));
    await fixture.whenStable();
    expect(host.querySelector('.assistant-island')?.getAttribute('data-state')).toBe('idle');
  });
});
