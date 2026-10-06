import { test, expect, type Locator, type Page } from "@playwright/test";
import { createServer, type ServerResponse } from "node:http";
import type { RunEvent } from "../../frontend/src/app/core/api/types";
import {
  ApiFixture,
  expectViewportContained,
  settleEntrance,
} from "./fixtures";

async function activeLoops(locator: Locator) {
  return locator.evaluate(
    (element) =>
      element
        .getAnimations({ subtree: true })
        .filter(
          (animation) =>
            animation.playState === "running" &&
            animation.effect?.getTiming().iterations === Infinity,
        ).length,
  );
}

// An open HTTP stream exercises normal generation independently of the replay fixture's disconnects.
async function liveEvents(page: Page) {
  let connected!: (response: ServerResponse) => void;
  const connection = new Promise<ServerResponse>((resolve) => {
    connected = resolve;
  });
  const server = createServer((request, response) => {
    response.setHeader("Access-Control-Allow-Origin", "http://localhost:5180");
    response.setHeader("Access-Control-Allow-Credentials", "true");
    if (request.method === "OPTIONS") {
      response.setHeader(
        "Access-Control-Allow-Headers",
        request.headers["access-control-request-headers"] ?? "",
      );
      response.writeHead(204).end();
      return;
    }
    response.writeHead(200, {
      "Content-Type": "text/event-stream",
      "Cache-Control": "no-cache",
    });
    response.write(": connected\n\n");
    connected(response);
  });
  await new Promise<void>((resolve) => {
    server.listen(0, "127.0.0.1", resolve);
  });
  const address = server.address() as { port: number };
  await page.route("**/runs/*/events?*", (route) => {
    const url = new URL(route.request().url());
    return route.continue({
      url: `http://127.0.0.1:${address.port}${url.pathname}${url.search}`,
    });
  });
  return {
    async send(event: RunEvent) {
      (await connection).write(
        `id: ${event.sequence}\nevent: run\ndata: ${JSON.stringify(event)}\n\n`,
      );
    },
    async close() {
      server.closeAllConnections();
      await new Promise<void>((resolve) => {
        server.close(() => resolve());
      });
    },
  };
}

test("generation follows queue, preparation and content with distinct unobtrusive signals", async ({
  page,
}) => {
  const fixture = new ApiFixture();
  fixture.hold = true;
  fixture.partialAnswer = "";
  fixture.preferences.theme = "system";
  fixture.settings.defaultReasoningEffort = "high";
  await fixture.attach(page);
  const events = await liveEvents(page);
  try {
    await page.emulateMedia({
      colorScheme: "light",
      reducedMotion: "no-preference",
    });
    await page.goto("/chat");
    await page
      .getByRole("textbox", { name: "傳送訊息", exact: true })
      .fill("準備回答");
    await page.getByRole("button", { name: "送出訊息", exact: true }).click();
    const indicator = page.locator("nx-generation-indicator");
    const header = page.locator(".topbar .inference-status");
    await expect(indicator).toContainText("等待模型回應");
    await expect(indicator.locator(".generation-indicator")).toHaveClass(
      /is-waiting/,
    );
    await expect(header).toContainText("等待模型回應");
    const run = fixture.runs[0];
    run.status = "running";
    run.startedAt = new Date().toISOString();
    await events.send({
      version: 1,
      sequence: ++run.lastSequence,
      runId: run.id,
      type: "status",
      status: "running",
      delta: null,
      errorCode: null,
    });
    await expect(indicator).toContainText("正在準備回答");
    await expect(indicator).not.toContainText("正在推理");
    await expect(header).toContainText("正在準備回答");
    await expect(indicator.locator(".generation-indicator")).not.toHaveClass(
      /is-waiting/,
    );
    expect(await activeLoops(indicator)).toBe(4);
    expect(await activeLoops(header)).toBe(2);
    expect(await activeLoops(page.locator(".composer"))).toBe(0);
    await expect(page.locator(".topbar [role='status']")).toHaveCount(1);
    await expect(indicator.locator("[role='status']")).toHaveCount(0);
    await expect(indicator.locator(".generation-orbit")).toHaveAttribute(
      "aria-hidden",
      "true",
    );
    for (const sample of [
      { theme: "light" as const, width: 1280, height: 768 },
      { theme: "dark" as const, width: 1280, height: 768 },
      { theme: "light" as const, width: 375, height: 812 },
      { theme: "dark" as const, width: 375, height: 812 },
    ]) {
      await page.setViewportSize({
        width: sample.width,
        height: sample.height,
      });
      const backdrop = page.locator(".workspace-backdrop");
      if (sample.width < 860 && (await backdrop.isVisible()))
        await page
          .getByRole("button", { name: "收合側欄", exact: true })
          .first()
          .click();
      await page.emulateMedia({ colorScheme: sample.theme });
      await expect(page.locator("html")).toHaveAttribute(
        "data-theme",
        sample.theme,
      );
      await expect(indicator).toBeInViewport();
      await expectViewportContained(page);
      await settleEntrance(page);
      await page.screenshot({
        path: `artifacts/screenshots/generation-${sample.theme}-${sample.width}.png`,
      });
    }
    run.content = "回答已開始。";
    fixture.messages.find(
      (message) => message.id === run.assistantMessageId,
    )!.content = run.content;
    await events.send({
      version: 1,
      sequence: ++run.lastSequence,
      runId: run.id,
      type: "delta",
      status: "running",
      delta: run.content,
      errorCode: null,
    });
    await expect(indicator).toHaveCount(0);
    await expect(page.locator("nx-streaming-answer")).toContainText(
      "回答已開始。",
    );
    await expect(header).toContainText("正在生成回答");
    await page.getByRole("button", { name: "停止生成", exact: true }).click();
    await expect(header).toHaveCount(0);
    await expect(page.locator(".stream-cursor")).toHaveCount(0);
    await expect(page.locator(".message:not(.user)")).toContainText(
      "回答已開始。",
    );
  } finally {
    await events.close();
  }
});

for (const preference of ["system", "personal"] as const) {
  test(`generation preserves readable status with ${preference} reduced motion`, async ({
    page,
  }) => {
    const fixture = new ApiFixture();
    fixture.hold = true;
    fixture.partialAnswer = "";
    fixture.preferences.reducedMotion = preference === "personal";
    await fixture.attach(page);
    await page.emulateMedia({
      reducedMotion: preference === "system" ? "reduce" : "no-preference",
    });
    await page.goto("/chat");
    await page
      .getByRole("textbox", { name: "傳送訊息", exact: true })
      .fill("減少動態效果");
    await page.getByRole("button", { name: "送出訊息", exact: true }).click();
    const indicator = page.locator("nx-generation-indicator");
    await expect(indicator).toContainText("正在準備回答");
    expect(await activeLoops(indicator)).toBe(0);
    expect(await activeLoops(page.locator(".topbar .inference-status"))).toBe(
      0,
    );
    await expect(indicator.locator(".generation-orbit i").first()).toHaveCSS(
      "opacity",
      "1",
    );
    await page.getByRole("button", { name: "停止生成", exact: true }).click();
    await expect(indicator).toHaveCount(0);
  });
}
