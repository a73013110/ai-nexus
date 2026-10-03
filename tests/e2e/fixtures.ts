import type { Page, Route } from "@playwright/test";
import { randomUUID } from "node:crypto";
import type {
  Conversation,
  CreateRun,
  Message,
  ModelPolicy,
  Preferences,
  Run,
  RunEvent,
} from "../../frontend/src/app/core/api/types";

export const richAnswer =
  '可以。我們先把內容整理成一份方便追蹤的工作筆記。\n\n## 本週工作重點\n\n先確認需要交付的成果，再把工作拆成明確步驟。\n\n| 項目 | 下一步 |\n| --- | --- |\n| 需求確認 | 彙整問題，確認優先順序 |\n| 第一版實作 | 完成文字聊天與資料保存 |\n\n```typescript\nconst nextStep = "開始實作";\nconsole.log(nextStep);\n```\n\n> 保留原始資訊，讓每一項決定都能回頭確認。';

// Browser fixtures only. These routes are never included in frontend bundles or server auth.
export class ApiFixture {
  readonly conversations: Conversation[] = [];
  readonly messages: Message[] = [];
  readonly runs: Run[] = [];
  readonly events = new Map<string, RunEvent[]>();
  readonly submissions = new Map<string, Run>();
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
  disconnectOnce = false;
  losePostOnce = false;
  unavailable = false;
  failPreferencesOnce = false;
  ldap = false;
  authenticated = false;
  chatAccess = true;
  modelPolicy: ModelPolicy = {
    allowModelSelection: true,
    showModelNames: true,
    defaultModelId: "fixture:8b",
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
      }
      if (path === "/auth/logout") this.authenticated = false;
      return json({
        mode: this.ldap ? "Ldap" : "Windows",
        authenticated: this.ldap ? this.authenticated : true,
        configured: true,
        account: "TEST\\fixture",
        displayName: "測試使用者",
        csrfToken: "browser-test-csrf",
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
        id: randomUUID(),
        account: "TEST\\fixture",
        displayName: "測試使用者",
        preferences: this.preferences,
        csrfToken: "browser-test-csrf",
        access: {
          roles: [{ id: "member", name: "一般使用者" }],
          groups: [{ id: "workspace", name: "基本工作台" }],
          features: this.chatAccess
            ? [{ id: "chat", name: "AI 對話", route: "/chat" }]
            : [],
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
            reasoningEfforts: ["minimal", "high"],
            defaultReasoningEffort: "minimal",
          },
        ],
        providerAvailable: true,
        notice: null,
        policy: this.modelPolicy,
      });
    if (path === "/context") {
      const request = route.request().postDataJSON();
      const input = 220 + new TextEncoder().encode(request.prompt ?? "").length;
      return json({
        estimatedInputTokens: input,
        contextTokens: 8192,
        reservedOutputTokens: 2048,
        droppedMessages: 0,
        budgetExceeded: input + 2048 > 8192,
        isEstimate: true,
      });
    }
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
    if (path === "/conversations" && method === "GET") {
      const search = url.searchParams.get("search") ?? "";
      return json(
        this.conversations
          .filter((x) => x.title.includes(search))
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
        messages: this.messages.filter((x) =>
          this.runs.some(
            (run) =>
              run.conversationId === conversation.id &&
              (run.userMessageId === x.id || run.assistantMessageId === x.id),
          ),
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
        };
        this.messages.push(user);
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
        if (run.status === "queued") {
          run.status = "running";
          run.startedAt = new Date().toISOString();
          run.content = assistant.content = this.hold
            ? "這是已保存的部分回答。"
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
