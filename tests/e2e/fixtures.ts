import { expect, type Page, type Route } from "@playwright/test";
import { randomUUID } from "node:crypto";
export async function chooseSelect(
  page: import("@playwright/test").Page,
  label: string,
  text: string,
) {
  await page.getByRole("combobox", { name: label, exact: true }).click();
  await page
    .getByRole("listbox", { name: label, exact: true })
    .getByRole("option", { name: text, exact: true })
    .click();
}
export async function openSettings(page: Page) {
  const account = page.getByRole("button", { name: "登入者選單", exact: true });
  await account.waitFor({ state: "attached" });
  if (!(await account.isVisible()))
    await page.getByRole("button", { name: "展開側欄" }).click();
  await account.click();
  await page.getByRole("menuitem", { name: "設定", exact: true }).click();
  const dialog = page.getByRole("dialog", { name: "個人設定", exact: true });
  await dialog.waitFor({ state: "visible" });
  return dialog;
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
import type {
  Conversation,
  CreateRun,
  Message,
  ModelPolicy,
  Preferences,
  Run,
  RunEvent,
  Attachment,
  LibraryFile,
  PromptTemplate,
} from "../../frontend/src/app/core/api/types";

export const richAnswer =
  '可以。我們先把內容整理成一份方便追蹤的工作筆記。\n\n## 本週工作重點\n\n先確認需要交付的成果，再把工作拆成明確步驟。\n\n| 項目 | 下一步 |\n| --- | --- |\n| 需求確認 | 彙整問題，確認優先順序 |\n| 第一版實作 | 完成文字聊天與資料保存 |\n\n```typescript\nconst nextStep = "開始實作";\nconsole.log(nextStep);\n```\n\n> 保留原始資訊，讓每一項決定都能回頭確認。';

// Capture final surfaces, while allowing intentional inference loops to keep running.
export async function settleEntrance(page: Page) {
  await page.evaluate(async () => {
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
  readonly conversations: Conversation[] = [];
  readonly messages: Message[] = [];
  readonly runs: Run[] = [];
  readonly events = new Map<string, RunEvent[]>();
  readonly submissions = new Map<string, Run>();
  readonly attachments: Attachment[] = [];
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
  readonly fileUsages = new Map<string, LibraryFile["usages"]>();
  libraryItems(): LibraryFile[] {
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
  readonly prompts: PromptTemplate[] = [];
  readonly messageConversation = new Map<string, string>();
  userId = randomUUID();
  settings = {
    readingFontSize: 17,
    readingLineHeight: 1.8,
    density: "comfortable",
    sidebarWidth: 264,
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
  preferences: Preferences = {
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
  loginMethod = "ad";
  chatAccess = true;
  adminAccess = false;
  extraFeatures: { id: string; name: string; route: string }[] = [];
  modelPolicy: ModelPolicy = {
    allowModelSelection: true,
    showModelNames: true,
    defaultModelId: "fixture:8b",
    maxInputCharacters: 12000,
  };
  lastRequest: CreateRun | null = null;

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
    if (
      path === "/auth/session" ||
      path === "/auth/windows" ||
      path === "/auth/login" ||
      path === "/auth/logout"
    ) {
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
      });
    }
    if (path === "/me") {
      if (this.unavailable)
        return json(
          {
            title: "伺服器尚未完成資料庫設定。",
            code: "storage_not_configured",
          },
          503,
        );
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
          features: [
            ...this.extraFeatures,
            ...(this.chatAccess
              ? [
                  { id: "chat", name: "對話", route: "/chat" },
                  { id: "files", name: "檔案庫", route: "/files" },
                ]
              : []),
            ...(this.adminAccess
              ? [{ id: "admin", name: "平台管理", route: "/admin" }]
              : []),
          ],
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
            { title: "偏好設定保存失敗。", code: "service_unavailable" },
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
          { title: "偏好設定保存失敗。", code: "service_unavailable" },
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
      const attachment: Attachment = {
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
        body.messages.map((x: Message) => [x.id, randomUUID()]),
      );
      const imported: Conversation = {
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
      const conversation: Conversation = {
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
      const request = route.request().postDataJSON() as CreateRun;
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
      const assistant: Message = {
        id: randomUUID(),
        parentId: user.id,
        role: "assistant",
        content: "",
        status: "queued",
        createdAt: now,
        runId: null,
        modelId: request.modelId ?? this.modelPolicy.defaultModelId!,
        attachments: [],
        errorCode: null,
      };
      const run: Run = {
        id: randomUUID(),
        conversationId: conversation.id,
        userMessageId: user.id,
        assistantMessageId: assistant.id,
        modelId: request.modelId ?? this.modelPolicy.defaultModelId!,
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
            { title: "沒有對話權限。", code: "feature_denied" },
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
