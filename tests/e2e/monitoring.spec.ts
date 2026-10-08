import { test, expect, type Page } from "@playwright/test";
import { createServer, type Server } from "node:http";
import { randomUUID } from "node:crypto";
import { ApiFixture, chooseSelect } from "./fixtures";
import type { MonitoringSnapshot } from "../../frontend/src/app/features/admin/monitoring/monitoring-store";

const metrics = (requests = 960, errors = 0, ms = 42) => ({
  requests,
  errors,
  serverErrors: errors,
  cancelled: 0,
  receivedBytes: 98000,
  sentBytes: 2940000,
  requestsPerSecond: requests / 300,
  errorPercent: requests ? (errors / requests) * 100 : 0,
  averageMs: ms,
  p95Ms: 75,
});
function snapshot(): MonitoringSnapshot {
  const names = [
    "陳怡君",
    "林志宏",
    "黃雅婷",
    "王建宇",
    "張筱涵",
    "李冠廷",
    "吳佩珊",
    "周柏翰",
    "劉思妤",
    "蔡承恩",
    "鄭宇軒",
    "許庭安",
  ];
  const now = new Date().toISOString();
  return {
    version: 1,
    enabled: true,
    instance: "NEXUS-APP-01 / 4280",
    startedAt: new Date(Date.now() - 86400000).toISOString(),
    at: now,
    windowMinutes: 5,
    refreshSeconds: 3,
    sessionTimeoutSeconds: 90,
    onlineUsers: 12,
    onlineSessions: 12,
    activeSessions: 8,
    inFlightRequests: 4,
    openStreams: 3,
    omittedSessions: 0,
    droppedSessions: 0,
    resources: {
      cpuPercent: 12.4,
      workingSetBytes: 384000000,
      managedHeapBytes: 120000000,
      threadCount: 28,
      uptimeSeconds: 86400,
    },
    traffic: metrics(960, 3),
    timeline: Array.from({ length: 30 }, (_, i) => ({
      at: new Date(Date.now() - (30 - i) * 10000).toISOString(),
      requestsPerSecond: 1.2 + (i % 7) * 0.4 + (i > 15 ? 1.1 : 0),
      errorPercent: i === 20 ? 2.1 : 0,
      averageMs: 30 + (i % 5) * 10,
      receivedBytes: 5000,
      sentBytes: 96000 + (i % 4) * 45000,
    })),
    sessions: names.map((name, i) => ({
      id: randomUUID(),
      userId: randomUUID(),
      account: `CORP\\user${i + 1}`,
      displayName: name,
      authentication: "ad",
      address: `10.20.3.${40 + i}`,
      browser: "Edge",
      device: "Windows",
      feature: [
        "chat",
        "knowledge",
        "artifacts",
        "projects",
        "dashboard",
        "repositories",
      ][i % 6],
      state: i > 7 ? (i > 9 ? "background" : "idle") : "active",
      connectedAt: new Date(Date.now() - 7200000).toISOString(),
      lastSeenAt: now,
      lastAction:
        i % 3 === 0 ? "執行對話" : i % 3 === 1 ? "更新知識庫" : "更新成果文件",
      lastActionAt: now,
      requests: 184 - i * 11,
      errors: i === 2 ? 1 : 0,
      receivedBytes: 16000,
      sentBytes: 128000,
      testingIdentity: false,
    })),
    activities: names.slice(0, 8).map((name, i) => ({
      sequence: 8 - i,
      at: new Date(Date.now() - i * 14000).toISOString(),
      userId: randomUUID(),
      sessionId: null,
      displayName: name,
      feature: i === 2 ? "knowledge" : "chat",
      action: i === 2 ? "更新知識庫" : "執行對話",
      method: "POST",
      route: "/api/v1/runs",
      statusCode: i === 2 ? 503 : 200,
      durationMs: 48 + i * 7,
      outcome: i === 2 ? "error" : "completed",
      traceId: "a".repeat(32),
    })),
    dependencies: [
      {
        id: "sql.nexus",
        name: "主資料庫",
        kind: "database",
        status: "healthy",
        inFlight: 2,
        lastSeenAt: now,
        metrics: metrics(2480, 0, 8),
      },
      {
        id: "google",
        name: "Google AI",
        kind: "http",
        status: "healthy",
        inFlight: 1,
        lastSeenAt: now,
        metrics: metrics(160, 0, 320),
      },
      {
        id: "ollama",
        name: "Ollama",
        kind: "http",
        status: "healthy",
        inFlight: 1,
        lastSeenAt: now,
        metrics: metrics(82, 0, 64),
      },
      {
        id: "gitea",
        name: "Gitea 程式庫",
        kind: "http",
        status: "degraded",
        inFlight: 0,
        lastSeenAt: now,
        metrics: metrics(34, 1, 128),
      },
      {
        id: "sql.gdweb",
        name: "公文資料庫",
        kind: "database",
        status: "unobserved",
        inFlight: 0,
        lastSeenAt: null,
        metrics: { ...metrics(0), averageMs: null, p95Ms: null },
      },
    ],
    endpoints: [
      "/api/v1/runs",
      "/api/v1/conversations/{id}",
      "/api/v1/knowledge/collections",
      "/api/v1/artifacts/{id}",
    ].map((route, i) => ({
      method: i === 0 ? "POST" : "GET",
      route,
      feature: i < 2 ? "chat" : i === 2 ? "knowledge" : "artifacts",
      metrics: metrics(290 - i * 50, 0, 35 + i * 6),
    })),
  };
}

class MonitorFixture {
  readonly api = new ApiFixture();
  readonly data = snapshot();
  readonly windows: string[] = [];
  readonly heartbeats: Record<string, unknown>[] = [];
  connections = 0;
  fail = false;
  revoked = false;
  private server?: Server;
  async attach(page: Page, granted = true) {
    this.api.adminAccess = true;
    this.api.extraFeatures = [
      ...(granted
        ? [{ id: "monitoring", name: "即時監控", route: "/admin/monitoring" }]
        : []),
      { id: "logs.query", name: "系統日誌", route: "/admin/logs" },
      { id: "dashboard", name: "總覽", route: "/dashboard" },
    ];
    await this.api.attach(page);
    await page.route("**/api/v1/presence", (route) => {
      this.heartbeats.push(route.request().postDataJSON());
      return route.fulfill({ json: { enabled: true, heartbeatSeconds: 25 } });
    });
    this.server = createServer((request, response) => {
      this.connections++;
      this.windows.push(
        new URL(request.url!, "http://localhost").searchParams.get("minutes")!,
      );
      response.writeHead(200, {
        "Content-Type": "text/event-stream",
        "Cache-Control": "no-store",
        "Access-Control-Allow-Origin": "http://localhost:5180",
        "Access-Control-Allow-Credentials": "true",
      });
      const write = () => {
        if (this.revoked) {
          response.end(
            "event: error\ndata: " +
              JSON.stringify({
                status: 403,
                code: "feature_forbidden",
                issueCode: "NX-" + "A".repeat(32),
              }) +
              "\n\n",
          );
          return;
        }
        this.data.at = new Date().toISOString();
        response.write(
          "event: snapshot\ndata: " + JSON.stringify(this.data) + "\n\n",
        );
      };
      write();
      const timer = setInterval(write, 250);
      request.on("close", () => clearInterval(timer));
    });
    await new Promise<void>((resolve) =>
      this.server!.listen(0, "127.0.0.1", resolve),
    );
    const port = (this.server.address() as { port: number }).port;
    await page.route("**/api/v1/admin/monitoring/**", (route) => {
      if (route.request().url().includes("/export"))
        return route.fulfill({
          contentType: "application/json",
          headers: {
            "Content-Disposition": 'attachment; filename="monitoring.json"',
          },
          body: JSON.stringify(this.data),
        });
      if (this.fail)
        return route.fulfill({
          status: 503,
          json: {
            code: "service_unavailable",
            title: "private-database-password",
            issueCode: "NX-" + "A".repeat(32),
          },
        });
      return route.continue({
        url:
          `http://127.0.0.1:${port}/events?` +
          new URL(route.request().url()).searchParams,
      });
    });
  }
  async close() {
    this.server?.closeAllConnections();
    await new Promise<void>((resolve) =>
      this.server ? this.server.close(() => resolve()) : resolve(),
    );
  }
}

test("live map, session filters, inspectors, range changes and snapshot export", async ({
  page,
}) => {
  const fixture = new MonitorFixture();
  await fixture.attach(page);
  try {
    await page.goto("/admin/monitoring");
    await expect(page.getByText("即時連線", { exact: true })).toBeVisible();
    await expect(page.getByRole("heading", { name: "連線拓樸" })).toBeVisible();
    await expect(page.getByRole("table").getByRole("row")).toHaveCount(11);
    await page.getByRole("button", { name: "檢視 陳怡君 的連線" }).click();
    const drawer = page.getByRole("dialog", {
      name: "工作階段詳情",
      exact: true,
    });
    await expect(drawer).toBeVisible();
    await expect(drawer.getByText("10.20.3.40")).toBeVisible();
    await page
      .getByRole("button", { name: "關閉工作階段詳情", exact: true })
      .click();
    await page.getByRole("button", { name: "檢視 主資料庫，運作正常" }).click();
    await expect(
      page.getByRole("dialog", { name: "周邊服務詳情", exact: true }),
    ).toContainText("2,480".replace(",", ""));
    await page
      .getByRole("button", { name: "關閉周邊服務詳情", exact: true })
      .click();
    const services = page
      .locator("details")
      .filter({ hasText: "檢視所有周邊服務" });
    await services.locator("summary").click();
    await expect(services.getByRole("row")).toHaveCount(6);
    await services
      .getByRole("button", { name: "公文資料庫", exact: false })
      .click();
    await expect(
      page.getByRole("dialog", { name: "周邊服務詳情", exact: true }),
    ).toContainText("尚無觀測");
    await page
      .getByRole("button", { name: "關閉周邊服務詳情", exact: true })
      .click();
    await services.locator("summary").click();
    await page
      .getByRole("searchbox", { name: "搜尋在線使用者、IP 或裝置" })
      .fill("10.20.3.42");
    await expect(page.getByRole("table").getByRole("row")).toHaveCount(2);
    await expect(page.getByRole("table")).toContainText("黃雅婷");
    await page.getByRole("searchbox").clear();
    await chooseSelect(page, "在線狀態", "背景分頁");
    await expect(page.getByRole("table").getByRole("row")).toHaveCount(3);
    await chooseSelect(page, "在線狀態", "所有在線狀態");
    await page.getByRole("button", { name: "下一頁連線" }).click();
    await expect(page.getByRole("table").getByRole("row")).toHaveCount(3);
    await chooseSelect(page, "監控時間範圍", "最近 15 分鐘");
    await expect.poll(() => fixture.windows.at(-1)).toBe("15");
    await chooseSelect(page, "趨勢指標", "平均回應時間");
    await expect(page.locator("nx-trend-chart figcaption")).toHaveText(
      "平均回應時間",
    );
    const download = page.waitForEvent("download");
    await page.getByRole("button", { name: "匯出快照" }).click();
    await expect(await download).toBeTruthy();
    await expect.poll(() => fixture.heartbeats.length).toBeGreaterThan(0);
    expect(fixture.heartbeats[0]["feature"]).toBe("monitoring");
    expect(fixture.heartbeats[0]).not.toHaveProperty("url");
  } finally {
    await page.goto("about:blank");
    await fixture.close();
  }
});

test("pause freezes updates and resume recovers without duplicate subscriptions", async ({
  page,
}) => {
  const fixture = new MonitorFixture();
  await fixture.attach(page);
  try {
    await page.goto("/admin/monitoring");
    await expect(page.getByText("即時連線", { exact: true })).toBeVisible();
    await page.getByRole("button", { name: "暫停更新" }).click();
    await expect(page.getByText("已暫停", { exact: true })).toBeVisible();
    fixture.data.onlineUsers = 17;
    await expect(page.locator(".monitor-stat").first()).toContainText("12");
    await page.getByRole("button", { name: "繼續更新" }).click();
    await expect(page.locator(".monitor-stat").first()).toContainText("17");
    await expect(page.getByText("即時連線", { exact: true })).toBeVisible();
    expect(fixture.connections).toBe(2);
  } finally {
    await page.goto("about:blank");
    await fixture.close();
  }
});

test("permission gate prevents telemetry subscription", async ({ page }) => {
  const fixture = new MonitorFixture();
  await fixture.attach(page, false);
  try {
    await page.goto("/admin/monitoring");
    await expect(
      page.getByRole("heading", { name: "需要即時監控權限" }),
    ).toBeVisible();
    expect(fixture.connections).toBe(0);
  } finally {
    await page.goto("about:blank");
    await fixture.close();
  }
});

test("revoked stream permissions clear the snapshot and stop subscriptions", async ({
  page,
}) => {
  const fixture = new MonitorFixture();
  await fixture.attach(page);
  try {
    await page.goto("/admin/monitoring");
    await expect(page.getByText("即時連線", { exact: true })).toBeVisible();
    fixture.revoked = true;
    await expect(
      page.getByRole("heading", { name: "監控權限已失效" }),
    ).toBeVisible();
    await expect(page.locator(".monitor-dashboard")).toHaveCount(0);
    expect(fixture.connections).toBe(1);
  } finally {
    await page.goto("about:blank");
    await fixture.close();
  }
});

test("reconnect failures show safe errors and recover", async ({ page }) => {
  const fixture = new MonitorFixture();
  fixture.fail = true;
  await fixture.attach(page);
  try {
    await page.goto("/admin/monitoring");
    await expect(page.locator(".monitor-loading")).toContainText("查證代碼");
    await expect(page.locator("body")).not.toContainText(
      "private-database-password",
    );
    fixture.fail = false;
    await page.getByRole("button", { name: "重新連線", exact: true }).click();
    await expect(page.getByText("即時連線", { exact: true })).toBeVisible();
  } finally {
    await page.goto("about:blank");
    await fixture.close();
  }
});

for (const theme of ["light", "dark"] as const) {
  test(`visual QA ${theme}, narrow layout, keyboard inspector and reduced motion`, async ({
    page,
  }, testInfo) => {
    const fixture = new MonitorFixture();
    fixture.api.preferences.theme = theme;
    await fixture.attach(page);
    try {
      await page.goto("/admin/monitoring");
      await expect(page.getByText("即時連線", { exact: true })).toBeVisible();
      await page.screenshot({
        path: `artifacts/monitoring-${theme}-desktop.png`,
        fullPage: true,
      });
      await page.locator(".monitor-chart-grid").scrollIntoViewIfNeeded();
      await page.screenshot({ path: `artifacts/monitoring-${theme}-data.png` });
      await page.locator(".monitor-bottom-grid").scrollIntoViewIfNeeded();
      await page.screenshot({
        path: `artifacts/monitoring-${theme}-events.png`,
      });
      await page.locator(".topology-panel").scrollIntoViewIfNeeded();
      const node = page.getByRole("button", { name: "檢視 陳怡君 的連線" });
      await node.focus();
      await page.keyboard.press("Enter");
      await expect(
        page.getByRole("dialog", { name: "工作階段詳情", exact: true }),
      ).toBeVisible();
      await page.keyboard.press("Escape");
      await expect(node).toBeFocused();
      await page.emulateMedia({ reducedMotion: "reduce" });
      await expect(page.locator(".core-orbit")).toHaveCSS(
        "animation-name",
        "none",
      );
      await page.setViewportSize({ width: 375, height: 812 });
      await expect
        .poll(() =>
          page.evaluate(
            () => document.documentElement.scrollWidth <= innerWidth,
          ),
        )
        .toBe(true);
      await page.screenshot({
        path: `artifacts/monitoring-${theme}-mobile.png`,
        fullPage: true,
      });
      await testInfo.attach(`${theme} dashboard`, {
        path: `artifacts/monitoring-${theme}-desktop.png`,
        contentType: "image/png",
      });
    } finally {
      await page.goto("about:blank");
      await fixture.close();
    }
  });
}
