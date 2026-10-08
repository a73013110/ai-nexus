import { test, expect } from "@playwright/test";
import { randomUUID } from "node:crypto";
import {
  expectCompactWorkspace,
  ApiFixture,
  chooseSelect,
  settleEntrance,
} from "./fixtures";
import type { ReadonlyShare } from "../../frontend/src/app/core/api/types";

test("named read-only sharing is created beside a conversation and can be revoked from its reader", async ({
  page,
}) => {
  const core = new ApiFixture();
  core.extraFeatures.push({ id: "shared", name: "分享", route: "/shared" });
  const colleague = {
    id: randomUUID(),
    displayName: "林同事",
    account: "TEST\\colleague",
  };
  let share: ReadonlyShare | null = null;
  let included = true;
  await core.attach(page);
  await page.route("**/api/v1/directory?**", (route) =>
    route.fulfill({
      contentType: "application/json",
      body: JSON.stringify([colleague]),
    }),
  );
  await page.route("**/api/v1/shares**", async (route) => {
    const path = new URL(route.request().url()).pathname,
      method = route.request().method();
    const json = (body: unknown, status = 200) =>
      route.fulfill({
        status,
        contentType: "application/json",
        body: JSON.stringify(body),
      });
    if (method === "POST") {
      const body = route.request().postDataJSON();
      included = body.includeAttachments;
      share = {
        id: randomUUID(),
        kind: "conversation",
        title: core.conversations[0].title,
        owner: "測試使用者",
        isOwner: true,
        isRevoked: false,
        expiresAt: new Date(Date.now() + 86400000).toISOString(),
        createdAt: new Date().toISOString(),
        recipients: ["林同事"],
        includeAttachments: included,
      };
      expect(body.hours).toBe(24);
      expect(body.recipientIds).toEqual([colleague.id]);
      return json(share);
    }
    if (method === "DELETE") {
      share!.isRevoked = true;
      return route.fulfill({ status: 204 });
    }
    if (path.endsWith("/shares")) return json(share ? [share] : []);
    if (share?.isRevoked) return json({ title: "分享已撤銷。" }, 404);
    return json({
      share,
      snapshot: {
        content: "",
        artifactVersion: null,
        messages: core.messages.map((m) => ({
          role: m.role,
          content: m.content,
          status: m.status,
          createdAt: m.createdAt,
          attachments: [],
        })),
      },
    });
  });
  await page.goto("/chat");
  await page
    .getByRole("textbox", { name: "傳送訊息", exact: true })
    .fill("請摘要這份工作");
  await page.getByRole("button", { name: "送出訊息", exact: true }).click();
  await expect(page.getByRole("table")).toBeVisible();
  await page.getByRole("button", { name: "分享此對話", exact: true }).click();
  const dialog = page.getByRole("dialog");
  await dialog.getByRole("searchbox", { name: "搜尋分享收件者" }).fill("同事");
  await dialog.getByRole("button", { name: /林同事.*TEST/ }).click();
  await chooseSelect(page, "分享有效期限", "1 天");
  await dialog.getByRole("button", { name: "建立分享", exact: true }).click();
  await expect(dialog).toContainText("分享已建立");
  expect(included).toBe(false);
  await dialog.getByRole("link", { name: "檢視分享", exact: true }).click();
  await expect(page.locator(".shared-message")).toHaveCount(2);
  await expect(page.locator(".shared-user")).toContainText("請摘要這份工作");
  await settleEntrance(page);
  await expectCompactWorkspace(page);
  await page.screenshot({
    path: "artifacts/screenshots/sharing-desktop.png",
    fullPage: true,
  });
  await page.setViewportSize({ width: 375, height: 812 });
  expect(
    await page.evaluate(
      () => document.documentElement.scrollWidth <= innerWidth,
    ),
  ).toBe(true);
  await page.getByRole("button", { name: "撤銷分享", exact: true }).click();
  await page
    .getByRole("dialog")
    .getByRole("button", { name: "撤銷分享", exact: true })
    .click();
  await expect(page.locator(".share-content")).toHaveCount(0);
  expect(share!.isRevoked).toBe(true);
});
