import { DOCUMENT } from '@angular/common';
import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { CodeSamples, type CodeSample } from '@shared/ui/code-samples';
import javaClient from './samples/CredentialVerificationClient.java';
import javaLogin from './samples/LoginExample.java';
import csharpClient from './samples/CredentialVerificationClient.cs';
import csharpProgram from './samples/Program.cs';

/**
 * 原始檔就是範例本身：C# 類別編進後端整合測試並實際呼叫（ApiClientTests），Java 以 JDK 8 編譯檢查；
 * 改 API 時跟著改這些檔，畫面不另存一份。用法檔裡的 `{{baseUrl}}` 換成這台服務的網址。
 */
export const API_CLIENT_SAMPLES: readonly CodeSample[] = [
  {
    id: 'java',
    label: 'Java',
    note: '需要 Java 8 以上與 Gson 2.8.6 以上。',
    files: [
      { name: 'CredentialVerificationClient.java', language: 'java', source: javaClient },
      { name: 'LoginExample.java', language: 'java', source: javaLogin },
    ],
  },
  {
    id: 'csharp',
    label: 'C#',
    note: '需要 .NET 8 以上，不需要其他套件。',
    files: [
      { name: 'CredentialVerificationClient.cs', language: 'csharp', source: csharpClient },
      { name: 'Program.cs', language: 'csharp', source: csharpProgram },
    ],
  },
];

/** 說明抽屜、金鑰對話框與下載共用同一個語言選擇。 */
export const SAMPLE_PREFERENCE = 'api-clients';

/**
 * 外部系統串接帳密驗證的 Java、C# 範例；說明抽屜與金鑰對話框共用。
 * 呼叫端以 `@defer` 載入，範例原文不進外部系統頁的初始程式。金鑰不帶進範例：程式碼從設定讀取，金鑰只在金鑰對話框顯示。
 */
@Component({
  selector: 'nx-api-client-samples',
  imports: [CodeSamples],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `<nx-code-samples
    label="範例語言"
    [preferenceKey]="preference"
    [samples]="samples"
    [values]="values"
  />`,
})
export class ApiClientSamples {
  readonly samples = API_CLIENT_SAMPLES;
  readonly preference = SAMPLE_PREFERENCE;
  // 經 IIS 子路徑部署時 base href 不是 /，網址取自文件的 base 而不是 origin。
  readonly values = { baseUrl: new URL('.', inject(DOCUMENT).baseURI).href };
}
