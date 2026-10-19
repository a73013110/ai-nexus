import { expect, type Locator, type Page, type Route } from "@playwright/test";
import { randomUUID } from "node:crypto";
import { CATALOG_FEATURES } from "../src/app/core/api/feature-catalog";
export const fixtureIssueCode = "NX-" + "D".repeat(32);

/** 前端產生的功能目錄（後端 FeatureCatalog）：mock 的名稱、圖示與網址都從這裡取，不另寫一份。 */
const CATALOG = new Map<string, (typeof CATALOG_FEATURES)[number]>(
  CATALOG_FEATURES.map((feature) => [feature.id, feature]),
);
/** 目錄頁面或閱讀器的網址；e2e 的 goto 與網址斷言一律由此取得。 */
export function routeOf(id: string) {
  const route = CATALOG.get(id)?.route;
  if (!route) throw new Error(`目錄中沒有網址的功能：${id}`);
  return route;
}
/** 目錄中的預設名稱。 */
export function catalogName(id: string) {
  const name = CATALOG.get(id)?.name;
  if (!name) throw new Error(`目錄中沒有這個功能：${id}`);
  return name;
}
/** `/me`、`/admin/catalog` 形狀的功能：名稱與網址預設取目錄（動作與未知 ID 的網址為空字串，同後端）。 */
export function featureOf(id: string, name?: string) {
  return {
    id,
    name: name ?? catalogName(id),
    route: CATALOG.get(id)?.route ?? "",
  };
}

/** `/me`、`/admin/catalog` 的功能加上目錄圖示（同後端 `FeatureDto`）；測試給的欄位優先。 */
export function featureDto<T extends { id: string }>(feature: T) {
  return {
    icon: CATALOG.get(feature.id)?.icon ?? "document",
    ...featureOf(feature.id),
    ...feature,
  };
}

/** 種子的節點（`<分群>.<功能 ID 的點換成連字號>`），與 `GET /admin/navigation-items` 同形。 */
function seededPage(
  parentId: string | null,
  featureId: string,
  sortOrder: number,
) {
  return navigationItem({
    id: parentId
      ? `${parentId}.${featureId.replaceAll(".", "-")}`
      : featureId.replaceAll(".", "-"),
    parentId,
    kind: "page",
    featureId,
    sortOrder,
  });
}
function seededGroup(
  id: string,
  parentId: string | null,
  name: string,
  sortOrder: number,
) {
  return navigationItem({ id, parentId, kind: "group", name, sortOrder });
}
/** 一個選單節點（`NavigationItemDto`）；省略的欄位用伺服器的預設值。 */
export function navigationItem(
  item: Pick<NavigationItemDto, "id" | "kind" | "sortOrder"> &
    Partial<NavigationItemDto>,
): NavigationItemDto {
  return {
    parentId: null,
    featureId: null,
    name: null,
    url: null,
    openInNewTab: true,
    icon: null,
    audienceGroupIds: [],
    ...item,
  };
}
/** 資料庫種子（database/App/Seed/accesscontrol.sql）的預設選單樹；頁面的名稱與圖示來自目錄。 */
export const SEEDED_NAVIGATION: readonly NavigationItemDto[] = [
  seededPage(null, "dashboard", 10),
  seededGroup("work", null, "AI 工作台", 20),
  ...["chat", "projects", "files", "knowledge", "artifacts"].map((id, index) =>
    seededPage("work", id, (index + 1) * 10),
  ),
  seededGroup("collaboration", null, "協作", 30),
  ...["sharing", "jobs", "repositories"].map((id, index) =>
    seededPage("collaboration", id, (index + 1) * 10),
  ),
  seededGroup("system", null, "系統管理", 40),
  seededGroup("system.access", "system", "身分與權限", 10),
  ...[
    "identity.users",
    "access-control.roles",
    "access-control.groups",
    "access-control.features",
    "access-control.navigation",
  ].map((id, index) => seededPage("system.access", id, (index + 1) * 10)),
  ...[
    "operations.usage",
    "billing.prices",
    "knowledge.retrieval",
    "integrations",
    "monitoring",
    "diagnostics.logs",
    "audit",
    "quality",
    "quality.retrieval",
    "design",
  ].map((id, index) => seededPage("system", id, (index + 2) * 10)),
];

/**
 * 與後端 `NavigationTree` 相同的修剪：頁面要已授權（在 `features` 裡）且是目錄頁面，名稱用授權回報的名稱；
 * 外部連結要沒有可見群組或使用者屬於其中之一；沒有可見子節點的分群不輸出，只剩一個時以該子節點取代。
 * 同層依 `sortOrder`、再依 id 排列。
 */
export function navigationTree(
  items: readonly NavigationItemDto[],
  features: readonly { id: string; name: string; route: string }[],
  groups: readonly string[] = ["workspace"],
): NavigationNodeDto[] {
  const granted = new Map(features.map((feature) => [feature.id, feature]));
  const level = (parentId: string | null): NavigationNodeDto[] =>
    items
      .filter((item) => (item.parentId ?? null) === parentId)
      .sort(
        (a, b) =>
          a.sortOrder - b.sortOrder || (a.id < b.id ? -1 : a.id > b.id ? 1 : 0),
      )
      .flatMap((item): NavigationNodeDto[] => {
        const base = {
          id: item.id,
          kind: item.kind,
          featureId: null,
          route: null,
          url: null,
          openInNewTab: false,
          children: [],
        };
        if (item.kind === "page") {
          const feature = granted.get(item.featureId ?? "");
          const page = CATALOG.get(item.featureId ?? "");
          if (!feature || page?.kind !== "page") return [];
          return [
            {
              ...base,
              name: feature.name,
              icon: page.icon,
              featureId: page.id,
              route: page.route,
            },
          ];
        }
        if (item.kind === "link") {
          if (
            item.audienceGroupIds.length &&
            !item.audienceGroupIds.some((id) => groups.includes(id))
          )
            return [];
          return [
            {
              ...base,
              name: item.name ?? "",
              icon: item.icon,
              url: item.url,
              openInNewTab: item.openInNewTab,
            },
          ];
        }
        const children = level(item.id);
        if (children.length === 0) return [];
        if (children.length === 1) return children;
        return [{ ...base, name: item.name ?? "", icon: item.icon, children }];
      });
  return level(null);
}
export async function chooseSelect(
  page: import("@playwright/test").Page,
  label: string,
  text: string,
) {
  await page.getByRole("combobox", { name: label, exact: true }).click();
  const list = page.getByRole("listbox", { name: label, exact: true });
  await list.getByRole("option", { name: text, exact: true }).click();
  // The panel stays painted while it leaves; wait so the next lookup cannot match it.
  await expect(list).toBeHidden();
}
/** 多選 Select 的 trigger：名稱是欄位名稱加上已選內容（aria-labelledby）。 */
export function multiSelect(
  page: import("@playwright/test").Page,
  label: string,
) {
  const escaped = label.replace(/[.*+?^${}()|[\]\\]/g, "\\$&");
  return page.getByRole("combobox", { name: new RegExp(`^${escaped}( |$)`) });
}
/** Toggles options of a multiple Select, which stays open until Escape. */
export async function chooseOptions(
  page: import("@playwright/test").Page,
  label: string,
  texts: string[],
) {
  await multiSelect(page, label).click();
  const list = page.getByRole("listbox", { name: label, exact: true });
  for (const text of texts)
    await list.getByRole("option", { name: text, exact: true }).click();
  await page.keyboard.press("Escape");
  await expect(list).toBeHidden();
}
/** The shell's bottom-right toast stack; it stays visible above modal dialogs. */
export function toasts(page: Page) {
  return page.locator("nx-toaster .toast");
}
export async function openSettings(page: Page) {
  const account = page.getByRole("button", { name: "登入者選單", exact: true });
  const sidebar = page.locator(".workspace-sidebar");
  await sidebar.waitFor({ state: "attached" });
  if ((await sidebar.getAttribute("aria-hidden")) === "true")
    await page.locator(".workspace-menu-button").click();
  await account.click();
  await page.getByRole("menuitem", { name: "設定", exact: true }).click();
  const dialog = page.getByRole("dialog", { name: "個人設定", exact: true });
  await dialog.waitFor({ state: "visible" });
  return dialog;
}

/**
 * A touch drag through CDP (Chromium/Edge), so the page sees real touch pointer events. `hold`
 * pauses before lifting, which turns a drag into a slow release with no fling speed; `held` runs
 * while the finger is still down.
 */
export async function touchSwipe(
  page: Page,
  from: { x: number; y: number },
  to: { x: number; y: number },
  {
    steps = 10,
    hold = 0,
    held,
  }: { steps?: number; hold?: number; held?: () => Promise<void> } = {},
) {
  const cdp = await page.context().newCDPSession(page);
  const touch = (x: number, y: number) => [{ x, y, id: 1 }];
  await cdp.send("Input.dispatchTouchEvent", {
    type: "touchStart",
    touchPoints: touch(from.x, from.y),
  });
  for (let step = 1; step <= steps; step++)
    await cdp.send("Input.dispatchTouchEvent", {
      type: "touchMove",
      touchPoints: touch(
        from.x + ((to.x - from.x) * step) / steps,
        from.y + ((to.y - from.y) * step) / steps,
      ),
    });
  // Chromium delivers moves with the next animation frame; let the last one land before `held`.
  if (held)
    await page.evaluate(
      () =>
        new Promise((resolve) =>
          requestAnimationFrame(() => requestAnimationFrame(resolve)),
        ),
    );
  await held?.();
  if (hold) await page.waitForTimeout(hold);
  await cdp.send("Input.dispatchTouchEvent", {
    type: "touchEnd",
    touchPoints: [],
  });
  await cdp.detach();
}

export async function expectViewportContained(page: Page) {
  await expect
    .poll(() =>
      page.evaluate(() => ({
        vertical: document.documentElement.scrollHeight <= innerHeight,
        horizontal: document.documentElement.scrollWidth <= innerWidth,
      })),
    )
    .toEqual({ vertical: true, horizontal: true });
}

// A visual contract checked on populated feature pages, in addition to their workflow tests.
export async function expectCompactWorkspace(page: Page) {
  const workspace = page.locator(".feature-content").first();
  await expectCompactSurfaces(workspace);
  // A phone header is one row, so its title steps down a size.
  const phone = (page.viewportSize()?.width ?? 1440) <= 640;
  await expect(workspace.locator(".feature-header h1")).toHaveCSS(
    "font-size",
    phone ? "20px" : "24px",
  );
}

export async function expectCompactSurfaces(workspace: Locator) {
  await expect(workspace).toHaveCSS("font-size", "13px");
  await expect(workspace.locator("[nxCard]:not(.ui-card)")).toHaveCount(0);
  const surfaces = await workspace.locator(".ui-card").evaluateAll((cards) =>
    cards
      .filter((card) => card.getClientRects().length)
      .map((card) => ({
        padding: getComputedStyle(card).paddingTop,
        gallery: card.classList.contains("file-card"),
      })),
  );
  for (const surface of surfaces)
    expect(surface.padding).toBe(surface.gallery ? "0px" : "12px");
}
import type {
  ConversationDto,
  CreateRunRequest,
  MessageDto,
  ModelPolicyDto,
  PreferencesDto,
  RunDto,
  RunEventDto,
  AttachmentDto,
  LibraryFileDto,
  PromptTemplateDto,
  NavigationItemDto,
  NavigationNodeDto,
} from "../src/app/core/api/schema";

export const richAnswer =
  '可以。我們先把內容整理成一份方便追蹤的工作筆記。\n\n## 本週工作重點\n\n先確認需要交付的成果，再把工作拆成明確步驟。\n\n| 項目 | 下一步 |\n| --- | --- |\n| 需求確認 | 彙整問題，確認優先順序 |\n| 第一版實作 | 完成文字聊天與資料保存 |\n\n```typescript\nconst nextStep = "開始實作";\nconsole.log(nextStep);\n```\n\n> 保留原始資訊，讓每一項決定都能回頭確認。';

// Capture final surfaces, while allowing intentional inference loops to keep running.
export async function settleEntrance(page: Page) {
  await page.evaluate(async () => {
    // Include CSS and after-render animations scheduled for the next painted frame.
    await new Promise<void>((resolve) =>
      requestAnimationFrame(() => requestAnimationFrame(() => resolve())),
    );
    const finished = Promise.all(
      document
        .getAnimations()
        .filter((animation) => {
          const target = (animation.effect as KeyframeEffect | null)?.target;
          return (
            target instanceof Element &&
            target.getClientRects().length > 0 &&
            animation.playState === "running" &&
            Number.isFinite(animation.effect?.getTiming().iterations ?? 1)
          );
        })
        .map((animation) => animation.finished.catch(() => undefined)),
    );
    // Chromium can suspend transitions in closed details; those should never block a capture.
    await Promise.race([
      finished,
      new Promise((resolve) => setTimeout(resolve, 1000)),
    ]);
  });
}

// Browser fixtures only. These routes are never included in frontend bundles or server auth.
export class ApiFixture {
  readonly conversations: ConversationDto[] = [];
  readonly messages: MessageDto[] = [];
  readonly runs: RunDto[] = [];
  readonly events = new Map<string, RunEventDto[]>();
  readonly submissions = new Map<string, RunDto>();
  readonly attachments: AttachmentDto[] = [];
  attachmentLimitBytes = 5_000_000_000;
  storage() {
    const usedBytes = this.attachments.reduce(
      (sum, file) => sum + file.size,
      0,
    );
    return {
      usedBytes,
      limitBytes: this.attachmentLimitBytes,
      remainingBytes: Math.max(0, this.attachmentLimitBytes - usedBytes),
      personalLimitBytes: null,
      groupLimitBytes: null,
      defaultLimitBytes: 5_000_000_000,
      limitSource: "default",
    };
  }
  readonly retainedFiles = new Set<string>();
  readonly fileUsages = new Map<string, LibraryFileDto["usages"]>();
  libraryItems(): LibraryFileDto[] {
    return this.attachments
      .filter((file) => this.retainedFiles.has(file.id))
      .slice()
      .reverse()
      .map((file) => {
        const usages = [...(this.fileUsages.get(file.id) || [])];
        for (const message of this.messages.filter((value) =>
          value.attachments?.some((attachment) => attachment.id === file.id),
        )) {
          const conversation = this.conversations.find(
            (value) => value.id === this.messageConversation.get(message.id),
          );
          if (
            conversation &&
            !usages.some(
              (value) =>
                value.kind === "chat" && value.resourceId === conversation.id,
            )
          )
            usages.push({
              kind: "chat",
              resourceId: conversation.id,
              name: conversation.title,
              documentId: null,
              status: null,
            });
        }
        return {
          file,
          createdAt: "2026-10-05T02:00:00Z",
          usages,
          canDelete: !usages.length,
        };
      });
  }
  readonly attachmentData = new Map<string, Buffer>();
  readonly prompts: PromptTemplateDto[] = [];
  readonly messageConversation = new Map<string, string>();
  userId = randomUUID();
  settings = {
    readingFontSize: 15,
    readingLineHeight: 1.2,
    density: "comfortable",
    sidebarWidth: 240,
    readingWidth: "standard",
    enterToSend: true,
    autoFollow: true,
    saveLocalDrafts: true,
    notifyOnCompletion: false,
    defaultReasoningEffort: "auto",
  };
  supportsImages = true;
  eventsStatus = 200;
  eventReads = 0;
  preferences: PreferencesDto = {
    theme: "light",
    reducedMotion: false,
    defaultModelId: "fixture:8b",
  };
  posts = 0;
  generated = 0;
  stateReads = 0;
  answer = richAnswer;
  hold = false;
  partialAnswer = "這是已保存的部分回答。";
  disconnectOnce = false;
  losePostOnce = false;
  unavailable = false;
  failPreferencesOnce = false;
  ldap = false;
  authenticated = false;
  /** Extra sign-in identities after the personal one; the login page asks only when there are any. */
  identities: {
    id: string;
    kind: string;
    name: string;
    detail: string | null;
  }[] = [];
  identity = "personal";
  loginMethod = "ad";
  chatAccess = true;
  adminAccess = false;
  /** Platform operations (usage, prices, retrieval); administrators have it unless a test sets it on its own. */
  operationsAccess: boolean | null = null;
  auditAccess = false;
  /** Features beyond the switches above; the menu places them where the seeded tree has them. */
  extraFeatures: {
    id: string;
    /** 省略時用目錄預設名稱與網址（同後端）。 */
    name?: string;
    route?: string;
  }[] = [];
  /** 選單節點；`/me` 依授權修剪成樹（同後端），測試可以加外部連結或另排。 */
  navigationItems: NavigationItemDto[] = SEEDED_NAVIGATION.map((item) => ({
    ...item,
  }));
  /** 使用者所屬的功能群組；決定外部連結的可見範圍。 */
  groupIds = ["workspace"];
  modelPolicy: ModelPolicyDto = {
    allowModelSelection: true,
    showModelNames: true,
    defaultModelId: "fixture:8b",
    maxInputCharacters: 12000,
  };
  lastRequest: CreateRunRequest | null = null;

  async attach(page: Page) {
    await page.addInitScript(() => {
      Object.defineProperty(navigator, "clipboard", {
        configurable: true,
        value: {
          writeText: async (value: string) => {
            (window as unknown as { __copied: string }).__copied = value;
          },
        },
      });
    });
    await page.route("**/api/v1/**", (route) => this.handle(route));
  }
  private async handle(route: Route) {
    const url = new URL(route.request().url());
    const path = url.pathname.replace("/api/v1", "");
    const method = route.request().method();
    const json = (body: unknown, status = 200) =>
      route.fulfill({
        status,
        contentType: "application/json",
        body: JSON.stringify(body),
      });
    if (path === "/presence")
      return json({ enabled: true, heartbeatSeconds: 25 });
    if (path.startsWith("/presence/") && method === "DELETE")
      return route.fulfill({ status: 204 });
    if (
      path === "/auth/session" ||
      path === "/auth/windows" ||
      path === "/auth/login" ||
      path === "/auth/logout" ||
      path === "/auth/identity"
    ) {
      if (path === "/auth/identity")
        this.identity = route.request().postDataJSON().identity;
      if (path === "/auth/login") {
        if (route.request().postDataJSON().password !== "fixture-password")
          return json(
            { title: "AD 帳號或密碼不正確。", code: "ad_invalid_credentials" },
            401,
          );
        this.authenticated = true;
        this.loginMethod = route.request().postDataJSON().method || "ad";
      }
      if (path === "/auth/logout") this.authenticated = false;
      if (path === "/auth/windows") {
        this.authenticated = true;
        this.loginMethod = "windows";
      }
      return json({
        mode: this.ldap ? "Ldap" : "Windows",
        authenticated: this.ldap ? this.authenticated : true,
        configured: true,
        account: "TEST\\fixture",
        displayName: "測試使用者",
        csrfToken: "browser-test-csrf",
        methods: this.ldap ? ["ad", "local"] : ["windows", "local"],
        method:
          this.ldap || (this.authenticated && this.loginMethod === "local")
            ? this.loginMethod
            : "windows",
        userId: this.userId,
        testing: null,
        identities: [
          {
            id: "personal",
            kind: "personal",
            name: "測試使用者",
            detail: "TEST\\fixture",
          },
          ...this.identities,
        ],
        identity: this.identity,
      });
    }
    if (path === "/me") {
      if (this.unavailable)
        return json(
          {
            title: "Password=fixture-private; internal database endpoint",
            code: "storage_not_configured",
            issueCode: fixtureIssueCode,
          },
          503,
        );
      const features = [
        ...this.extraFeatures,
        ...(this.chatAccess ? [featureOf("chat"), featureOf("files")] : []),
        ...(this.adminAccess
          ? [
              featureOf("identity.users"),
              featureOf("access-control.roles"),
              featureOf("access-control.groups"),
              featureOf("access-control.features"),
              featureOf("access-control.navigation"),
              featureOf("design"),
            ]
          : []),
        ...((this.operationsAccess ?? this.adminAccess)
          ? [
              featureOf("operations.usage"),
              featureOf("billing.prices"),
              featureOf("knowledge.retrieval"),
            ]
          : []),
        ...(this.auditAccess ? [featureOf("audit")] : []),
      ].map(featureDto);
      return json({
        id: this.userId,
        account: "TEST\\fixture",
        displayName: "測試使用者",
        preferences: this.preferences,
        csrfToken: "browser-test-csrf",
        access: {
          roles: [
            { id: "member", name: "一般使用者" },
            ...(this.adminAccess
              ? [{ id: "administrator", name: "平台管理員" }]
              : []),
          ],
          groups: [{ id: "workspace", name: "基本工作區" }],
          features,
          navigation: navigationTree(
            this.navigationItems,
            features,
            this.groupIds,
          ),
        },
        activeRunId:
          this.runs.find(
            (run) => run.status === "queued" || run.status === "running",
          )?.id ?? null,
      });
    }
    if (path === "/models")
      return json({
        models: [
          {
            id: this.modelPolicy.showModelNames ? "fixture:8b" : "model-1",
            displayName: this.modelPolicy.showModelNames
              ? "本機測試模型"
              : "AI 助理 1",
            contextTokens: 8192,
            maxOutputTokens: 2048,
            supportsStreaming: true,
            supportsUsage: true,
            supportsImages: this.supportsImages,
            reasoningEfforts: ["minimal", "high"],
            defaultReasoningEffort: "minimal",
          },
        ],
        providerAvailable: true,
        notice: null,
        policy: this.modelPolicy,
      });
    if (path === "/tools/web-search")
      return json({ available: false, notice: "測試環境尚未啟用搜尋。" });
    if (/^\/conversations\/[^/]+\/spend$/.test(path))
      return json({
        requests: this.generated,
        pendingCalls: 0,
        legacyCalls: this.generated,
        totals: [],
        models: [],
      });
    if (path === "/context") {
      const request = route.request().postDataJSON();
      const input =
        220 +
        new TextEncoder().encode(request.prompt ?? "").length +
        (request.attachmentIds ?? []).reduce(
          (sum: number, id: string) =>
            sum +
            (this.attachments.find((x) => x.id === id)?.isImage ? 4096 : 100),
          0,
        );
      return json({
        estimatedInputTokens: input,
        contextTokens: 8192,
        reservedOutputTokens: 2048,
        droppedMessages: 0,
        budgetExceeded: input + 2048 > 8192,
        isEstimate: true,
      });
    }
    if (path === "/settings/model-policy")
      return json({
        allowedModelIds: null,
        models: [
          {
            modelId: "fixture:8b",
            dailyTokenLimit: null,
            usedTokens: this.generated * 129,
            reservedTokens: 0,
            remainingTokens: null,
            source: "unlimited",
          },
        ],
        storedAttachmentLimitBytes: null,
        resetsAt: "2026-10-05T00:00:00Z",
      });
    if (path === "/settings") {
      if (route.request().method() === "PUT") {
        if (this.failPreferencesOnce) {
          this.failPreferencesOnce = false;
          return json(
            {
              title: "Password=fixture-private",
              code: "service_unavailable",
              issueCode: fixtureIssueCode,
            },
            503,
          );
        }
        const { appearance, ...settings } = route.request().postDataJSON();
        this.preferences = appearance;
        this.settings = settings;
      }
      return json({ ...this.settings, appearance: this.preferences });
    }
    if (path === "/notifications")
      return json({ items: [], unread: 0, hasMore: false });
    if (path === "/settings/usage")
      return json({
        days: 30,
        requests: this.generated,
        completed: this.generated,
        failed: 0,
        cancelled: 0,
        inputTokens: 123,
        outputTokens: 12,
        requestsWithUsage: this.generated,
        daily: [],
        storage: this.storage(),
        totalDurationMilliseconds: this.generated * 1500,
        timedRequests: this.generated,
      });
    if (path === "/preferences") {
      if (this.failPreferencesOnce) {
        this.failPreferencesOnce = false;
        return json(
          {
            title: "Password=fixture-private",
            code: "service_unavailable",
            issueCode: fixtureIssueCode,
          },
          503,
        );
      }
      this.preferences = route.request().postDataJSON();
      return json(this.preferences);
    }
    if (path === "/attachments/storage") return json(this.storage());
    if (path === "/attachments/policy")
      return json({
        maxFileBytes: 4194304,
        maxFilesPerMessage: 4,
        maxMessageBytes: 8388608,
        extensions: [
          ".png",
          ".jpg",
          ".jpeg",
          ".webp",
          ".pdf",
          ".docx",
          ".txt",
          ".md",
          ".csv",
          ".json",
        ],
      });
    if (path === "/attachments" && method === "POST") {
      const multipart = await new Response(route.request().postDataBuffer(), {
        headers: { "content-type": route.request().headers()["content-type"] },
      }).formData();
      const file = multipart.get("file") as File;
      const data = Buffer.from(await file.arrayBuffer());
      const isImage = /\.(png|jpe?g|webp)$/i.test(file.name);
      const attachment: AttachmentDto = {
        id: randomUUID(),
        fileName: file.name,
        contentType: isImage
          ? file.type || "image/png"
          : /\.pdf$/i.test(file.name)
            ? "application/pdf"
            : "text/plain",
        size: data.length,
        isImage,
        analysisMode: isImage ? "vision" : "extracted-text",
      };
      this.attachments.push(attachment);
      this.attachmentData.set(attachment.id, data);
      return json(attachment);
    }
    const libraryFile = /^\/files\/([^/]+)(\/retain)?$/.exec(path);
    if (libraryFile) {
      const file = this.attachments.find(
        (value) => value.id === libraryFile[1],
      );
      if (!file) return json({ title: "找不到檔案。" }, 404);
      if (libraryFile[2]) this.retainedFiles.add(file.id);
      else {
        if (
          this.libraryItems().find((value) => value.file.id === file.id)
            ?.canDelete === false
        )
          return json({ title: "檔案仍被引用。" }, 409);
        this.retainedFiles.delete(file.id);
        this.attachments.splice(this.attachments.indexOf(file), 1);
      }
      return route.fulfill({ status: 204 });
    }
    if (path === "/files") {
      const query = new URL(route.request().url()).searchParams;
      let items = this.libraryItems();
      const search = (query.get("search") || "").toLowerCase(),
        type = query.get("type"),
        source = query.get("source");
      items = items.filter(
        (value) =>
          value.file.fileName.toLowerCase().includes(search) &&
          (type !== "images" || value.file.isImage) &&
          (type !== "documents" || !value.file.isImage) &&
          (!source ||
            source === "all" ||
            (source === "library" && !value.usages.length) ||
            value.usages.some((usage) => usage.kind === source)),
      );
      const offset = Number(query.get("offset")) || 0,
        limit = Number(query.get("limit")) || 40;
      return json({
        items: items.slice(offset, offset + limit),
        total: items.length,
        storage: this.storage(),
        offset,
        limit,
      });
    }
    const attachmentRoute = /^\/attachments\/([^/]+)(\/content)?$/.exec(path);
    if (attachmentRoute) {
      const file = this.attachments.find(
        (file) => file.id === attachmentRoute[1],
      );
      if (!file) return json({ title: "找不到附件。" }, 404);
      if (method === "DELETE") {
        if (this.retainedFiles.has(file.id))
          return route.fulfill({ status: 204 });
        this.attachments.splice(this.attachments.indexOf(file), 1);
        return route.fulfill({ status: 204 });
      }
      if (attachmentRoute[2])
        return route.fulfill({
          contentType: file.contentType,
          body: this.attachmentData.get(file.id),
        });
      return json(file);
    }
    if (path === "/prompt-templates" && method === "GET")
      return json(this.prompts);
    if (path === "/prompt-templates" && method === "POST") {
      const body = route.request().postDataJSON();
      const prompt = {
        id: randomUUID(),
        ...body,
        updatedAt: new Date().toISOString(),
      };
      this.prompts.unshift(prompt);
      return json(prompt);
    }
    const promptRoute = /^\/prompt-templates\/([^/]+)$/.exec(path);
    if (promptRoute) {
      const prompt = this.prompts.find((x) => x.id === promptRoute[1]);
      if (!prompt) return json({ title: "找不到範本。" }, 404);
      if (method === "DELETE") {
        this.prompts.splice(this.prompts.indexOf(prompt), 1);
        return route.fulfill({ status: 204 });
      }
      Object.assign(prompt, route.request().postDataJSON());
      return json(prompt);
    }
    if (path === "/conversations/labels")
      return json([
        ...new Set(this.conversations.flatMap((x) => x.labels ?? [])),
      ]);
    if (path === "/conversations/import") {
      const body = route.request().postDataJSON();
      const now = new Date().toISOString();
      const ids = new Map<string, string>(
        body.messages.map((x: MessageDto) => [x.id, randomUUID()]),
      );
      const imported: ConversationDto = {
        id: randomUUID(),
        title: body.title,
        activeLeafId: ids.get(body.activeLeafId) ?? null,
        createdAt: now,
        updatedAt: now,
        isFavorite: false,
        isArchived: false,
        labels: body.labels,
        systemInstruction: body.systemInstruction,
      };
      this.conversations.unshift(imported);
      for (const message of body.messages) {
        const copy = {
          ...message,
          id: ids.get(message.id)!,
          parentId: ids.get(message.parentId) ?? null,
          modelId: null,
          runId: null,
          attachments: [],
          errorCode: null,
        };
        this.messages.push(copy);
        this.messageConversation.set(copy.id, imported.id);
      }
      return json(imported);
    }
    const organizationRoute =
      /^\/conversations\/([^/]+)\/(settings|duplicate|export)$/.exec(path);
    if (organizationRoute) {
      const conversation = this.conversations.find(
        (x) => x.id === organizationRoute[1],
      );
      if (!conversation) return json({ title: "找不到對話。" }, 404);
      if (organizationRoute[2] === "settings") {
        Object.assign(conversation, route.request().postDataJSON());
        return json(conversation);
      }
      const messages = this.messages.filter(
        (x) => this.messageConversation.get(x.id) === conversation.id,
      );
      if (organizationRoute[2] === "export")
        return json({
          version: 1,
          title: conversation.title,
          systemInstruction: conversation.systemInstruction,
          labels: conversation.labels,
          activeLeafId: conversation.activeLeafId,
          messages: messages.map((x) => ({
            id: x.id,
            parentId: x.parentId,
            role: x.role,
            content: x.content,
            status: x.status,
            createdAt: x.createdAt,
            attachmentNames: (x.attachments ?? []).map((f) => f.fileName),
          })),
        });
      const ids = new Map(messages.map((x) => [x.id, randomUUID()]));
      const clone = {
        ...conversation,
        id: randomUUID(),
        title: conversation.title + " · 副本",
        activeLeafId: ids.get(conversation.activeLeafId!) ?? null,
        isFavorite: false,
        isArchived: false,
      };
      this.conversations.unshift(clone);
      for (const message of messages) {
        const copy = {
          ...message,
          id: ids.get(message.id)!,
          parentId: ids.get(message.parentId!) ?? null,
          runId: null,
        };
        this.messages.push(copy);
        this.messageConversation.set(copy.id, clone.id);
      }
      return json(clone);
    }
    if (path === "/conversations" && method === "GET") {
      const search = url.searchParams.get("search") ?? "";
      return json(
        this.conversations
          .filter(
            (x) =>
              x.title.includes(search) ||
              this.messages.some(
                (message) =>
                  this.messageConversation.get(message.id) === x.id &&
                  message.content.includes(search),
              ),
          )
          .filter(
            (x) =>
              url.searchParams.get("view") === "all" ||
              !!x.isArchived === (url.searchParams.get("view") === "archived"),
          )
          .filter(
            (x) => url.searchParams.get("view") !== "favorites" || x.isFavorite,
          )
          .filter(
            (x) =>
              !url.searchParams.get("label") ||
              x.labels?.includes(url.searchParams.get("label")!),
          )
          .slice(Number(url.searchParams.get("offset") ?? 0), 100),
      );
    }
    if (path === "/conversations" && method === "POST") {
      const now = new Date().toISOString();
      const conversation: ConversationDto = {
        id: randomUUID(),
        title: "新對話",
        activeLeafId: null,
        createdAt: now,
        updatedAt: now,
        isFavorite: false,
        isArchived: false,
        systemInstruction: "",
        labels: [],
      };
      this.conversations.unshift(conversation);
      return json(conversation, 201);
    }
    const conversationRoute = /^\/conversations\/([^/]+)(\/branch)?$/.exec(
      path,
    );
    if (conversationRoute) {
      const conversation = this.conversations.find(
        (x) => x.id === conversationRoute[1],
      );
      if (!conversation) return json({ title: "找不到對話。" }, 404);
      if (conversationRoute[2]) {
        conversation.activeLeafId = route.request().postDataJSON().leafId;
        return route.fulfill({ status: 204 });
      }
      if (method === "PATCH") {
        conversation.title = route.request().postDataJSON().title;
        return json(conversation);
      }
      if (method === "DELETE") {
        this.conversations.splice(this.conversations.indexOf(conversation), 1);
        return route.fulfill({ status: 204 });
      }
      return json({
        conversation,
        messages: this.messages.filter(
          (x) => this.messageConversation.get(x.id) === conversation.id,
        ),
        activeRun:
          this.runs.find(
            (run) =>
              run.conversationId === conversation.id &&
              (run.status === "queued" || run.status === "running"),
          ) ?? null,
      });
    }
    if (path === "/runs" && method === "POST") {
      this.posts++;
      const key = route.request().headers()["idempotency-key"];
      const existing = this.submissions.get(key);
      if (existing) return json(existing, 202);
      const request = route.request().postDataJSON() as CreateRunRequest;
      this.lastRequest = request;
      const conversation = this.conversations.find(
        (x) => x.id === request.conversationId,
      )!;
      const now = new Date().toISOString();
      let user = this.messages.find(
        (x) => x.id === request.regenerateUserMessageId,
      );
      if (!user) {
        user = {
          id: randomUUID(),
          parentId: request.parentMessageId,
          role: "user",
          content: request.prompt!,
          status: "completed",
          createdAt: now,
          runId: null,
          modelId: null,
          attachments: (request.attachmentIds ?? [])
            .map((id) => this.attachments.find((file) => file.id === id)!)
            .filter(Boolean),
          errorCode: null,
        };
        this.messages.push(user);
        for (const id of request.attachmentIds ?? [])
          this.retainedFiles.add(id);
      }
      const assistant: MessageDto = {
        id: randomUUID(),
        parentId: user.id,
        role: "assistant",
        content: "",
        status: "queued",
        createdAt: now,
        runId: null,
        modelId: request.modelId ?? this.modelPolicy.defaultModelId!,
        modelDisplayName: this.modelPolicy.showModelNames
          ? "本機測試模型"
          : "AI 助理 1",
        attachments: [],
        errorCode: null,
      };
      const run: RunDto = {
        id: randomUUID(),
        conversationId: conversation.id,
        userMessageId: user.id,
        assistantMessageId: assistant.id,
        modelId: request.modelId ?? this.modelPolicy.defaultModelId!,
        modelDisplayName: assistant.modelDisplayName,
        status: "queued",
        content: "",
        lastSequence: 1,
        errorCode: null,
        createdAt: now,
        startedAt: null,
        finishedAt: null,
        inputTokens: null,
        outputTokens: null,
      };
      assistant.runId = run.id;
      this.messages.push(assistant);
      this.messageConversation.set(user.id, conversation.id);
      this.messageConversation.set(assistant.id, conversation.id);
      this.runs.push(run);
      this.submissions.set(key, run);
      this.generated++;
      conversation.activeLeafId = assistant.id;
      if (conversation.title === "新對話")
        conversation.title = user.content.slice(0, 36);
      this.events.set(run.id, [
        {
          version: 1,
          sequence: 1,
          runId: run.id,
          type: "status",
          status: "queued",
          delta: null,
          errorCode: null,
        },
      ]);
      if (this.losePostOnce) {
        this.losePostOnce = false;
        return route.abort("failed");
      }
      return json(run, 202);
    }
    const runRoute = /^\/runs\/([^/]+)(\/(cancel|events))?$/.exec(path);
    if (runRoute) {
      const run = this.runs.find((x) => x.id === runRoute[1]);
      if (!run) return json({ title: "找不到生成。" }, 404);
      const assistant = this.messages.find(
        (x) => x.id === run.assistantMessageId,
      )!;
      if (runRoute[3] === "cancel") {
        run.status = assistant.status = "cancelled";
        run.finishedAt = new Date().toISOString();
        run.timing = assistant.timing = {
          totalMilliseconds: 1500,
          queueMilliseconds: 200,
          generationMilliseconds: 1300,
          inputTokens: null,
          outputTokens: null,
        };
        run.lastSequence++;
        this.events.get(run.id)!.push({
          version: 1,
          sequence: run.lastSequence,
          runId: run.id,
          type: "status",
          status: "cancelled",
          delta: null,
          errorCode: null,
        });
        return json(run);
      }
      if (runRoute[3] === "events") {
        this.eventReads++;
        if (this.eventsStatus !== 200)
          return json(
            {
              title: "Password=fixture-private",
              code: "access_denied",
              issueCode: fixtureIssueCode,
            },
            this.eventsStatus,
          );
        if (run.status === "queued") {
          run.status = "running";
          run.startedAt = new Date().toISOString();
          run.content = assistant.content = this.hold
            ? this.partialAnswer
            : this.answer;
          assistant.status = this.hold ? "running" : "completed";
          const events = this.events.get(run.id)!;
          events.push({
            version: 1,
            sequence: 2,
            runId: run.id,
            type: "status",
            status: "running",
            delta: null,
            errorCode: null,
          });
          events.push({
            version: 1,
            sequence: 3,
            runId: run.id,
            type: "delta",
            status: "running",
            delta: run.content,
            errorCode: null,
          });
          run.lastSequence = 3;
          if (!this.hold) {
            run.status = "completed";
            run.lastSequence = 4;
            run.finishedAt = new Date().toISOString();
            run.inputTokens = 123;
            run.outputTokens = 12;
            run.timing = assistant.timing = {
              totalMilliseconds: 1500,
              queueMilliseconds: 200,
              generationMilliseconds: 1300,
              inputTokens: 123,
              outputTokens: 12,
            };
            events.push({
              version: 1,
              sequence: 4,
              runId: run.id,
              type: "status",
              status: "completed",
              delta: null,
              errorCode: null,
            });
          }
        }
        if (this.disconnectOnce) {
          this.disconnectOnce = false;
          return route.abort("failed");
        }
        await new Promise((resolve) => setTimeout(resolve, 200));
        const after = Number(url.searchParams.get("after") ?? 0);
        const body = this.events
          .get(run.id)!
          .filter((x) => x.sequence > after)
          .map(
            (x) =>
              `id: ${x.sequence}\nevent: run\ndata: ${JSON.stringify(x)}\n\n`,
          )
          .join("");
        return route.fulfill({
          status: 200,
          contentType: "text/event-stream",
          body,
        });
      }
      this.stateReads++;
      return json(run);
    }
    return json({ title: "Unimplemented browser fixture route" }, 404);
  }
}

/** Usage, spend and price data shared by the dashboard, navigation and price specs. */
type Tone = "neutral" | "info" | "success" | "warning" | "danger";
/** The last `days` local days, oldest first, as `YYYY-MM-DD`. */
function fixtureDays(days: number) {
  return Array.from({ length: days }, (_, i) =>
    new Date(Date.now() - (days - 1 - i) * 86400000).toLocaleDateString(
      "sv-SE",
    ),
  );
}
/** Deterministic daily values: a weekly rhythm with a slow rise. */
function fixtureSeries(scale: number, seed = 1) {
  return fixtureDays(30).map((date, i) => ({
    date,
    value: Math.max(
      0,
      Math.round(scale * (0.55 + 0.45 * Math.sin((i + seed) * 0.9) + i / 60)),
    ),
  }));
}
function metric(
  key: string,
  label: string,
  value: number | null,
  unit: string,
  extra: { format?: string; tone?: Tone; period?: boolean; hint?: string } = {},
) {
  return {
    key,
    label,
    value,
    unit,
    format: extra.format ?? "count",
    tone: extra.tone ?? "neutral",
    period: extra.period ?? false,
    hint: extra.hint ?? null,
  };
}
interface FixtureItem {
  id: string;
  title: string;
  detail: string | null;
  at: string | null;
  link: string | null;
  tone: Tone;
}
function section(
  id: string,
  feature: string,
  title: string,
  route: string | null,
  parts: {
    state?: string;
    metrics: ReturnType<typeof metric>[];
    series?: {
      key: string;
      label: string;
      unit: string;
      points: { date: string; value: number }[];
    }[];
    shares?: { key: string; label: string; value: number; tone: Tone }[];
    items?: FixtureItem[];
  },
) {
  return {
    id,
    feature,
    title,
    route,
    state: parts.state ?? "idle",
    metrics: parts.metrics,
    series: parts.series ?? [],
    shares: parts.shares ?? [],
    items: parts.items ?? [],
    facts: [],
  };
}
const ago = (minutes: number) =>
  new Date(Date.now() - minutes * 60000).toISOString();

/** A month of activity across every feature; `platform` adds the administrator sections. */
export function dashboardFixture(fixture: ApiFixture, scope = "personal") {
  const day = new Date().toLocaleDateString("sv-SE");
  const days = fixtureDays(30);
  const calls = fixtureSeries(14, 2);
  const conversation = randomUUID();
  const sections = [
    section("chat", "chat", "對話", routeOf("chat"), {
      state: "active",
      metrics: [
        metric("conversations", "對話", 12, "個", {
          hint: "3 個收藏 · 1 個封存",
        }),
        metric("generating", "AI 回覆生成", 1, "件進行中", { tone: "info" }),
        metric("questions", "期間提問", 186, "則", { period: true }),
        metric("replies", "完成回覆", 181, "則", {
          period: true,
          tone: "success",
        }),
        metric("failed", "回覆失敗", 2, "則", { period: true, tone: "danger" }),
        metric("latency", "平均回覆時間", 4820, "", {
          period: true,
          format: "duration",
        }),
      ],
      series: [
        {
          key: "questions",
          label: "每日提問",
          unit: "則",
          points: fixtureSeries(6, 1),
        },
      ],
      shares: [
        {
          key: "fixture:8b",
          label: "本機測試模型",
          value: 120,
          tone: "neutral",
        },
        { key: "second-model", label: "雲端助理", value: 63, tone: "neutral" },
      ],
      items: [
        {
          id: conversation,
          title: "整理期末評量規準",
          detail: "收藏",
          at: ago(12),
          link: routeOf("chat") + "/" + conversation,
          tone: "neutral",
        },
        {
          id: randomUUID(),
          title: "校務會議紀錄摘要",
          detail: null,
          at: ago(240),
          link: routeOf("chat") + "/" + randomUUID(),
          tone: "neutral",
        },
      ],
    }),
    section("web-search", "chat", "網路搜尋", routeOf("chat"), {
      metrics: [
        metric("searches", "期間搜尋", 23, "次", { period: true }),
        metric("failed", "搜尋失敗", 0, "次", { period: true }),
      ],
      series: [
        {
          key: "searches",
          label: "每日網路搜尋",
          unit: "次",
          points: fixtureSeries(1, 4),
        },
      ],
    }),
    section("knowledge", "knowledge", "知識庫", routeOf("knowledge"), {
      state: "attention",
      metrics: [
        metric("collections", "知識庫", 2, "個"),
        metric("documents", "知識庫文件", 8, "份", {
          hint: "7 份可查詢 · 312 頁",
        }),
        metric("chunks", "索引分段", 214, "段"),
        metric("processing", "處理中文件", 0, "份"),
        metric("failed", "處理失敗", 1, "份", { tone: "danger" }),
        metric("added", "期間新增文件", 5, "份", { period: true }),
      ],
      series: [
        {
          key: "added",
          label: "每日新增知識庫文件",
          unit: "份",
          points: fixtureSeries(0.4, 3),
        },
      ],
      shares: [
        { key: "ready", label: "可查詢", value: 7, tone: "success" },
        { key: "failed", label: "處理失敗", value: 1, tone: "danger" },
      ],
    }),
    section("files", "files", "檔案庫", routeOf("files"), {
      metrics: [
        metric("files", "已保存原檔", 5, "個"),
        metric("size", "原檔大小", 48_200_000, "", { format: "bytes" }),
        metric("uploads", "期間保存", 4, "個", { period: true }),
        metric("quota", "容量使用", 23.4, "", { format: "percent" }),
      ],
      series: [
        {
          key: "uploads",
          label: "每日保存原檔",
          unit: "個",
          points: fixtureSeries(0.3, 5),
        },
      ],
      shares: [
        { key: "PDF", label: "PDF", value: 3, tone: "neutral" },
        { key: "圖片", label: "圖片", value: 2, tone: "neutral" },
      ],
    }),
    section("tasks", "jobs", "背景任務", routeOf("jobs"), {
      state: "active",
      metrics: [
        metric("active", "處理中任務", 1, "件", {
          tone: "info",
          hint: "1 件執行中 · 0 件等待中",
        }),
        metric("failed", "失敗任務", 0, "件"),
        metric("created", "期間建立", 17, "件", { period: true }),
        metric("completed", "期間完成", 16, "件", {
          period: true,
          tone: "success",
        }),
      ],
      series: [
        {
          key: "created",
          label: "每日建立任務",
          unit: "件",
          points: fixtureSeries(0.8, 6),
        },
      ],
      shares: [
        {
          key: "document-ingest",
          label: "文字擷取",
          value: 9,
          tone: "neutral",
        },
        {
          key: "document-embedding",
          label: "向量索引",
          value: 8,
          tone: "neutral",
        },
      ],
      items: [
        {
          id: randomUUID(),
          title: "課程綱要.pdf",
          detail: "建立索引",
          at: ago(3),
          link: routeOf("jobs") + "?job=" + randomUUID(),
          tone: "info",
        },
      ],
    }),
    section("projects", "projects", "專案", routeOf("projects"), {
      metrics: [
        metric("projects", "專案", 3, "個"),
        metric("templates", "提問範本", 6, "個"),
        metric("created", "期間建立", 1, "個", { period: true }),
        metric("shared", "共用給我", 2, "個"),
      ],
      shares: [{ key: "active", label: "進行中", value: 3, tone: "info" }],
    }),
    section("artifacts", "artifacts", "成果文件", routeOf("artifacts"), {
      metrics: [
        metric("artifacts", "成果文件", 9, "份", { hint: "4 份由對話建立" }),
        metric("versions", "期間保存版本", 27, "版", { period: true }),
      ],
      series: [
        {
          key: "versions",
          label: "每日保存版本",
          unit: "版",
          points: fixtureSeries(1.2, 7),
        },
      ],
    }),
    section("shared", "sharing", "分享", routeOf("sharing"), {
      metrics: [
        metric("sent", "有效分享", 4, "個", { hint: "未撤銷且未到期" }),
        metric("expiring", "7 天內到期", 1, "個", { tone: "warning" }),
        metric("created", "期間建立", 3, "個", { period: true }),
        metric("received", "收到的分享", 2, "個"),
      ],
    }),
    section("quality", "quality", "品質評測", routeOf("quality"), {
      metrics: [
        metric("helpful", "回覆有幫助", 86, "", {
          period: true,
          format: "percent",
          hint: "43 則有幫助 · 7 則沒幫助",
        }),
        metric("ratings", "期間評分", 50, "則", { period: true }),
        metric("evaluations", "期間評測", 3, "次", { period: true }),
        metric("running", "評測進行中", 0, "次"),
      ],
      shares: [
        { key: "up", label: "有幫助", value: 43, tone: "success" },
        { key: "down", label: "沒幫助", value: 7, tone: "danger" },
      ],
    }),
    section("repositories", "repositories", "程式庫", routeOf("repositories"), {
      metrics: [
        metric("connections", "已連線帳號", 1, "個"),
        metric("running", "Review 進行中", 0, "次"),
        metric("reviews", "期間 Review", 5, "次", { period: true }),
        metric("failed", "Review 失敗", 0, "次", { period: true }),
      ],
    }),
    section("notifications", "dashboard", "通知", "", {
      metrics: [
        metric("unread", "未讀通知", 2, "則", { tone: "info" }),
        metric("errors", "未讀錯誤", 0, "則"),
        metric("received", "期間收到", 9, "則", { period: true }),
      ],
      items: [
        {
          id: randomUUID(),
          title: "收到分享",
          detail: "王老師分享了「期中考題」",
          at: ago(30),
          link: routeOf("sharing") + "/" + randomUUID(),
          tone: "info",
        },
      ],
    }),
  ];
  // Like the backend: only sections of granted features, titled with their (custom) names.
  const granted = new Map<string, string>([
    ["dashboard", "總覽"],
    ...fixture.extraFeatures.map((x): [string, string] => [
      x.id,
      x.name ?? catalogName(x.id),
    ]),
    ...(fixture.chatAccess
      ? ([
          ["chat", "對話"],
          ["files", "檔案庫"],
        ] as [string, string][])
      : []),
    ...(fixture.adminAccess
      ? ([
          ["identity.users", "使用者"],
          ["monitoring", "即時監控"],
        ] as [string, string][])
      : []),
  ]);
  const visible = sections
    .filter((x) => granted.has(x.feature))
    .map((x) =>
      x.id === x.feature ? { ...x, title: granted.get(x.feature)! } : x,
    );
  const window = {
    from: new Date(Date.now() - 29 * 86400000).toISOString(),
    until: new Date(Date.now() + 86400000).toISOString(),
  };
  return {
    scope,
    ownerId: scope === "personal" ? fixture.userId : null,
    generatedAt: new Date().toISOString(),
    period: { ...window, offsetMinutes: 480 },
    tokens: {
      ...window,
      timezoneOffsetMinutes: 480,
      daily: [
        ...days.slice(0, -1).map((date, i) => ({
          date,
          modelId: "second-model",
          modelDisplayName: "雲端助理",
          requests: 3,
          requestsWithUsage: 3,
          inputTokens: 3000 + i * 40,
          outputTokens: 900,
        })),
        {
          date: day,
          modelId: "fixture:8b",
          modelDisplayName: "本機測試模型",
          requests: 2,
          requestsWithUsage: 1,
          inputTokens: 10000,
          outputTokens: 1300,
        },
        {
          date: day,
          modelId: "second-model",
          modelDisplayName: "雲端助理",
          requests: 1,
          requestsWithUsage: 1,
          inputTokens: 200,
          outputTokens: 100,
        },
      ],
    },
    spend: {
      from: window.from,
      until: window.until,
      offsetMinutes: 480,
      requests: calls.reduce((sum, x) => sum + x.value, 0),
      pendingCalls: 1,
      legacyCalls: 2,
      inputTokens: 10200,
      outputTokens: 1400,
      totals: [
        {
          currency: "USD",
          kind: "api",
          amount: 0.1208,
          knownCalls: 38,
          unknownCalls: 1,
        },
        {
          currency: "TWD",
          kind: "internal",
          amount: 15,
          knownCalls: 1,
          unknownCalls: 0,
        },
      ],
      daily: [
        ...calls.map((x) => ({
          label: x.date,
          currency: "USD",
          kind: "api",
          amount: x.date === day ? 0.1208 : x.value * 0.002,
          requests: x.value,
          unknownCalls: 0,
          inputTokens: 1000,
          outputTokens: 300,
        })),
        {
          label: day,
          currency: "TWD",
          kind: "internal",
          amount: 15,
          requests: 1,
          unknownCalls: 0,
          inputTokens: 0,
          outputTokens: 0,
        },
      ],
      models: [
        {
          label: "AI 助理",
          currency: "USD",
          kind: "api",
          amount: 0.1208,
          requests: 40,
          unknownCalls: 1,
          inputTokens: 10000,
          outputTokens: 1300,
        },
        {
          label: "網路搜尋",
          currency: "USD",
          kind: "api",
          amount: 0.031,
          requests: 23,
          unknownCalls: 0,
          inputTokens: 0,
          outputTokens: 0,
        },
        {
          label: "內部模型",
          currency: "TWD",
          kind: "internal",
          amount: 15,
          requests: 1,
          unknownCalls: 0,
          inputTokens: 0,
          outputTokens: 0,
        },
      ],
      users: [
        {
          ownerId: fixture.userId,
          displayName: "測試使用者",
          account: "TEST\\fixture",
          currency: "USD",
          kind: "api",
          amount: 0.1208,
          requests: 40,
          unknownCalls: 1,
        },
        {
          ownerId: randomUUID(),
          displayName: "王小明",
          account: "TEST\\ming",
          currency: "USD",
          kind: "api",
          amount: 0.052,
          requests: 18,
          unknownCalls: 0,
        },
      ],
    },
    sections: visible,
  };
}
