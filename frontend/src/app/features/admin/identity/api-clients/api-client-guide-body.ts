import { DOCUMENT } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, inject } from '@angular/core';
import { downloadFile } from '@shared/browser/download';
import { CodeBlock } from '@shared/markdown/code-block';
import { fillPlaceholders, selectedCodeSample, type CodeSample } from '@shared/ui/code-samples';
import type { BadgeTone } from '@shared/ui/count-badge';
import { CopyValue } from '@shared/ui/copy-value';
import { Icon } from '@shared/ui/icon';
import { StatusBadge } from '@shared/ui/status-badge';
import { API_CLIENT_SAMPLES, ApiClientSamples, SAMPLE_PREFERENCE } from './api-client-samples';

interface ResponseCase {
  readonly status: number;
  readonly code: string | null;
  readonly meaning: string;
  readonly action: string;
}

interface ResponseGroup {
  readonly title: string;
  readonly tone: BadgeTone;
  readonly cases: readonly ResponseCase[];
}

/**
 * 串接說明的文字只寫在這裡：抽屜與下載的 Markdown 都由它產生，兩邊不會說法不一。
 * 讀者是對方系統的開發者，不提這個平台的名稱，只稱「驗證服務」。
 * 狀態碼與 `VerifyCredentials` 一致，改端點時一起改（docs/features/api-clients.md）。
 */
const GUIDE = {
  title: 'AD 帳密驗證串接說明',
  intro:
    '你的系統以金鑰呼叫驗證服務，確認使用者輸入的 AD 帳號與密碼。\n服務只回答帳密屬於誰。能否登入、有哪些權限，由你的系統決定。',
  key: '請管理員在「外部系統」新增你的系統，金鑰只會顯示一次。\n金鑰存進設定檔或秘密庫，不要寫進程式碼或版控。',
  call: '以 POST 送出帳號與密碼，Authorization 標頭帶 Bearer 金鑰。\n一律走 HTTPS 並驗證伺服器憑證。密碼與金鑰不要寫進日誌。',
  fields: [
    { name: 'account', description: 'AD 帳號，例如 bob。也可寫成 網域\\帳號。' },
    { name: 'password', description: '使用者輸入的密碼，原樣送出。' },
  ],
  responses: '依狀態碼分成三類處理。\n帳密問題告訴使用者。系統問題記下查證代碼，不要當成密碼錯。',
  problem:
    '錯誤回應是 application/problem+json。\n以狀態碼判斷。code 與 issueCode 寫進日誌供管理員查詢。',
  samples:
    '範例已處理上面每一種回應，複製後改 package 或 namespace 即可使用。\n服務網址已帶入。金鑰從設定讀取，不寫在程式碼裡。',
} as const;

const RESPONSE_GROUPS: readonly ResponseGroup[] = [
  {
    title: '成功',
    tone: 'success',
    cases: [
      {
        status: 200,
        code: null,
        meaning: '帳密正確',
        action: '以回傳的 sid 對應自己的使用者：帳號改名也不變。\nemail 可能是 null。',
      },
    ],
  },
  {
    title: '告訴使用者',
    tone: 'warning',
    cases: [
      {
        status: 422,
        code: 'credentials_rejected',
        meaning: '帳號或密碼錯，或帳號在 AD 已停用',
        action: '一律顯示「帳號或密碼不正確」，不透露是哪一種。',
      },
      {
        status: 429,
        code: 'login_rate_limited',
        meaning: '同一帳號失敗太多次',
        action:
          '請使用者依 Retry-After 的秒數後再試。\n失敗次數與網站登入共用。每個系統每分鐘超過 300 次也會回 429。',
      },
    ],
  },
  {
    title: '系統問題，不是密碼錯',
    tone: 'danger',
    cases: [
      {
        status: 401,
        code: 'authentication_required',
        meaning: '金鑰缺少、錯誤、已重新產生，或系統已停用',
        action: '檢查設定的金鑰。不要讓使用者重試密碼。',
      },
      {
        status: 503,
        code: null,
        meaning: '驗證服務連不上 AD',
        action: '記下回應的 issueCode 交給管理員查日誌。\n提示使用者稍後再試。',
      },
    ],
  },
];

const VERIFIED_EXAMPLE = `{
  "sid": "S-1-5-21-1000-2000-3000-1101",
  "account": "bob",
  "displayName": "王小明",
  "email": "bob@example.com"
}`;

const PROBLEM_EXAMPLE = `{
  "status": 422,
  "code": "credentials_rejected",
  "issueCode": "NX-0123456789ABCDEF0123456789ABCDEF"
}`;

function requestOf(endpoint: URL) {
  return `POST ${endpoint.pathname} HTTP/1.1
Host: ${endpoint.host}
Authorization: Bearer <金鑰>
Content-Type: application/json

{ "account": "bob", "password": "…" }`;
}

const fence = (language: string, source: string) =>
  '```' + language + '\n' + source.trimEnd() + '\n```';
// Markdown 表格的儲存格不能有換行與直線。
const cell = (text: string) => text.replaceAll('|', '\\|').replaceAll('\n', '<br>');
// 畫面以 \n 分行；Markdown 的單一換行會併成同一行，行尾兩個空白才是換行。
const prose = (...lines: (string | undefined)[]) =>
  lines.filter(Boolean).join('\n').replaceAll('\n', '  \n');

/** 下載用的完整說明：抽屜的每一段，加上讀者選的那種語言的全部範例檔。 */
function guideMarkdown(sample: CodeSample, baseUrl: string, endpoint: URL) {
  const values = { baseUrl };
  const responseRows = RESPONSE_GROUPS.flatMap((group) =>
    group.cases.map(
      (item) =>
        `| ${item.status} | ${item.code ? '`' + item.code + '`' : '—'} | ${group.title} | ${cell(item.meaning)} | ${cell(item.action)} |`,
    ),
  );
  return [
    `# ${GUIDE.title}`,
    prose(GUIDE.intro),
    '## 1. 取得金鑰',
    prose(GUIDE.key),
    '## 2. 呼叫驗證',
    prose(GUIDE.call),
    `服務網址：<${baseUrl}>`,
    fence('http', requestOf(endpoint)),
    '| 欄位 | 說明 |\n|---|---|\n' +
      GUIDE.fields.map((field) => `| \`${field.name}\` | ${cell(field.description)} |`).join('\n'),
    '## 3. 依回應處理',
    prose(GUIDE.responses),
    '| 狀態 | 代碼 | 分類 | 情況 | 怎麼做 |\n|---|---|---|---|---|\n' + responseRows.join('\n'),
    '成功時的回應：',
    fence('json', VERIFIED_EXAMPLE),
    prose(GUIDE.problem),
    fence('json', PROBLEM_EXAMPLE),
    `## 4. 範例程式（${sample.label}）`,
    prose(GUIDE.samples, sample.note),
    ...sample.files.flatMap((file) => [
      `### ${file.name}`,
      fence(file.language, fillPlaceholders(file.source, values)),
    ]),
  ]
    .join('\n\n')
    .concat('\n');
}

/**
 * 串接說明的內容，由 `ApiClientGuide` 在抽屜第一次打開時以 `@defer` 載入。
 * 下載的是整份說明（含目前選的語言的範例），交給對方開發者一個檔案就夠。
 */
@Component({
  selector: 'nx-api-client-guide-body',
  imports: [ApiClientSamples, CodeBlock, CopyValue, Icon, StatusBadge],
  changeDetection: ChangeDetectionStrategy.OnPush,
  styleUrl: './api-client-guide-body.scss',
  template: `<div class="ui-drawer-sections guide">
    <header class="guide-lead">
      <p class="guide-note">{{ guide.intro }}</p>
      <button
        type="button"
        class="secondary-button"
        [title]="downloadLabel()"
        [attr.aria-label]="downloadLabel()"
        (click)="download()"
      >
        <nx-icon name="download" />下載說明
      </button>
    </header>
    <section class="guide-step" aria-labelledby="guide-key">
      <h3 id="guide-key" class="guide-heading"><span aria-hidden="true">1</span>取得金鑰</h3>
      <p class="guide-note">{{ guide.key }}</p>
    </section>
    <section class="guide-step" aria-labelledby="guide-call">
      <h3 id="guide-call" class="guide-heading"><span aria-hidden="true">2</span>呼叫驗證</h3>
      <p class="guide-note">{{ guide.call }}</p>
      <nx-copy-value label="服務網址" [compact]="false" [value]="baseUrl" />
      <nx-code-block label="請求" language="http" [value]="request" />
      <dl class="guide-fields">
        @for (field of guide.fields; track field.name) {
          <div>
            <dt>
              <code>{{ field.name }}</code>
            </dt>
            <dd>{{ field.description }}</dd>
          </div>
        }
      </dl>
    </section>
    <section class="guide-step" aria-labelledby="guide-responses">
      <h3 id="guide-responses" class="guide-heading">
        <span aria-hidden="true">3</span>依回應處理
      </h3>
      <p class="guide-note">{{ guide.responses }}</p>
      @for (group of groups; track group.title) {
        <section
          class="guide-outcome"
          [attr.data-tone]="group.tone"
          [attr.aria-label]="group.title"
        >
          <h4>{{ group.title }}</h4>
          <ul>
            @for (item of group.cases; track item.status) {
              <li>
                <nx-status-badge [tone]="group.tone">{{ item.status }}</nx-status-badge>
                <div>
                  <p class="guide-case">
                    <strong>{{ item.meaning }}</strong>
                    @if (item.code; as code) {
                      <code>{{ code }}</code>
                    }
                  </p>
                  <p class="guide-note">{{ item.action }}</p>
                </div>
              </li>
            }
          </ul>
        </section>
      }
      <nx-code-block label="成功的回應" language="json" [value]="verifiedExample" />
      <p class="guide-note">{{ guide.problem }}</p>
      <nx-code-block label="錯誤的回應（以 422 為例）" language="json" [value]="problemExample" />
    </section>
    <section class="guide-step" aria-labelledby="guide-samples">
      <h3 id="guide-samples" class="guide-heading">
        <span aria-hidden="true">4</span>套用範例程式
      </h3>
      <p class="guide-note">{{ guide.samples }}</p>
      <nx-api-client-samples />
    </section>
  </div>`,
})
export class ApiClientGuideBody {
  readonly guide = GUIDE;
  readonly groups = RESPONSE_GROUPS;
  readonly verifiedExample = VERIFIED_EXAMPLE;
  readonly problemExample = PROBLEM_EXAMPLE;
  // 經 IIS 子路徑部署時 base href 不是 /，網址取自文件的 base 而不是 origin。
  readonly baseUrl = new URL('.', inject(DOCUMENT).baseURI).href;
  private readonly endpoint = new URL('api/v1/credential-verifications', this.baseUrl);
  readonly request = requestOf(this.endpoint);
  private readonly sample = computed(() =>
    selectedCodeSample(API_CLIENT_SAMPLES, SAMPLE_PREFERENCE),
  );
  readonly downloadLabel = computed(
    () => `下載串接說明（Markdown，含 ${this.sample()?.label ?? ''} 範例）`,
  );

  download() {
    const sample = this.sample();
    if (!sample) return;
    downloadFile(
      guideMarkdown(sample, this.baseUrl, this.endpoint),
      `${GUIDE.title}（${sample.label}）`,
      'md',
    );
  }
}
