import { TestBed } from '@angular/core/testing';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { ApiClientGuideBody } from './api-client-guide-body';

describe('ApiClientGuideBody', () => {
  const { createObjectURL, revokeObjectURL } = URL;
  afterEach(() => {
    TestBed.resetTestingModule();
    vi.restoreAllMocks();
    Object.assign(URL, { createObjectURL, revokeObjectURL });
    localStorage.removeItem('ai-nexus.code-samples.api-clients');
  });

  async function render() {
    const fixture = TestBed.createComponent(ApiClientGuideBody);
    await fixture.whenStable();
    return { fixture, element: fixture.nativeElement as HTMLElement };
  }

  /** 攔下下載：記錄檔名與內容，不真的觸發瀏覽器下載。 */
  function captureDownloads() {
    const files: { name: string; content: Promise<string> }[] = [];
    let blob: Blob | undefined;
    Object.assign(URL, {
      createObjectURL: (value: Blob) => ((blob = value), 'blob:guide'),
      revokeObjectURL: () => undefined,
    });
    vi.spyOn(HTMLAnchorElement.prototype, 'click').mockImplementation(function (
      this: HTMLAnchorElement,
    ) {
      files.push({ name: this.download, content: blob!.text() });
    });
    return files;
  }

  it('groups responses by what the caller should do', async () => {
    const { element } = await render();
    const groups = [...element.querySelectorAll('.guide-outcome')].map((group) => ({
      title: group.querySelector('h4')?.textContent,
      statuses: [...group.querySelectorAll('nx-status-badge')].map((badge) =>
        badge.textContent?.trim(),
      ),
    }));
    expect(groups).toEqual([
      { title: '成功', statuses: ['200'] },
      { title: '告訴使用者', statuses: ['422', '429'] },
      { title: '系統問題，不是密碼錯', statuses: ['401', '503'] },
    ]);
  });

  it('downloads the whole guide with every file of the chosen language', async () => {
    const { fixture, element } = await render();
    const files = captureDownloads();
    element.querySelectorAll<HTMLButtonElement>('[role=tab]')[1].click();
    await fixture.whenStable();
    element.querySelector<HTMLButtonElement>('.guide-lead button')!.click();

    expect(files.map((file) => file.name)).toEqual(['AD 帳密驗證串接說明（C#）.md']);
    const content = await files[0].content;
    for (const heading of [
      '## 1. 取得金鑰',
      '## 2. 呼叫驗證',
      '## 3. 依回應處理',
      '## 4. 範例程式（C#）',
      '### CredentialVerificationClient.cs',
      '### Program.cs',
    ])
      expect(content).toContain(heading);
    expect(content).toContain('| 503 | — | 系統問題，不是密碼錯 |');
    expect(content).not.toContain('.java');
    // 給對方開發者的文件不帶平台名稱；網址已帶入，沒有剩下的佔位。
    expect(content).not.toMatch(/ai[\s-]?nexus/i);
    expect(content).not.toContain('{{baseUrl}}');
  });
});
