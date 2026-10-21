import { test, expect } from "@playwright/test";
import { randomUUID } from "node:crypto";
import type { SharedContentDto, ShareDto } from "../src/app/core/api/schema";
import {
  expectCompactWorkspace,
  ApiFixture,
  chooseSelect,
  settleEntrance,
  expectViewportContained,
  walkTour,
} from "./fixtures";
import { twoPagePdf } from "./knowledge-fixture";

test("named read-only sharing is created beside a conversation and can be revoked from its reader", async ({
  page,
}) => {
  const core = new ApiFixture();
  core.extraFeatures.push({
    id: "sharing",
    name: "分享",
    route: "/work/sharing",
  });
  const colleague = {
    id: randomUUID(),
    displayName: "林同事",
    account: "TEST\\colleague",
  };
  let share: ShareDto | null = null;
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
  await page.goto("/work/chat");
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
  await page.setViewportSize({ width: 375, height: 812 });
  expect(
    await page.evaluate(
      () => document.documentElement.scrollWidth <= innerWidth,
    ),
  ).toBe(true);
  await page.getByRole("button", { name: "分享操作", exact: true }).click();
  await page.getByRole("menuitem", { name: "撤銷分享" }).click();
  await page
    .getByRole("dialog")
    .getByRole("button", { name: "撤銷分享", exact: true })
    .click();
  await expect(page.locator(".share-content")).toHaveCount(0);
  await expect(page).toHaveURL(/\/work\/sharing\?sent=true$/);
  expect(share!.isRevoked).toBe(true);
});

test("sent shares preserve their list and reuse the authorized PDF reader, citations and timing", async ({
  page,
}) => {
  const core = new ApiFixture();
  core.extraFeatures.push({
    id: "sharing",
    name: "分享",
    route: "/work/sharing",
  });
  await core.attach(page);
  const id = randomUUID(),
    fileId = randomUUID();
  const file = {
    id: fileId,
    fileName: "分享報告.pdf",
    contentType: "application/pdf",
    size: twoPagePdf().length,
    isImage: false,
    analysisMode: "shared-file",
  };
  let previewText = true;
  const share = {
    id,
    kind: "conversation",
    title: "分享報告",
    owner: "測試使用者",
    isOwner: true,
    isRevoked: false,
    expiresAt: new Date(Date.now() + 86400000).toISOString(),
    createdAt: new Date().toISOString(),
    recipients: ["林同事"],
    includeAttachments: true,
  };
  await page.route("**/api/v1/shares**", (route) => {
    const url = new URL(route.request().url());
    if (url.pathname.endsWith("/shares"))
      return route.fulfill({
        json: url.searchParams.get("sent") === "true" ? [share] : [],
      });
    if (url.pathname.endsWith("/preview"))
      return route.fulfill({
        json: {
          file,
          pages: previewText
            ? [
                {
                  pageNumber: 1,
                  text: "第一頁摘要",
                  extraction: "native",
                  needsReview: false,
                },
                {
                  pageNumber: 2,
                  text: "第二頁摘要",
                  extraction: "native",
                  needsReview: false,
                },
              ]
            : [],
        },
      });
    if (url.pathname.includes("/files/"))
      return route.fulfill({
        contentType: "application/pdf",
        body: twoPagePdf(),
      });
    return route.fulfill({
      json: {
        share,
        snapshot: {
          content: "",
          artifactVersion: null,
          messages: [
            {
              role: "user",
              content: "檢視附件",
              status: "completed",
              createdAt: share.createdAt,
              attachments: [file],
            },
            {
              role: "assistant",
              content: "## 回答\n\n依據來源整理。",
              status: "completed",
              createdAt: share.createdAt,
              attachments: [],
              modelId: "fixture:8b",
              modelDisplayName: "本機測試模型",
              sources: [
                {
                  number: 1,
                  documentId: randomUUID(),
                  title: "受控來源",
                  pageNumber: 1,
                  excerpt: "核准的摘要文字",
                },
              ],
              webSources: null,
              timing: {
                totalMilliseconds: 1500,
                queueMilliseconds: 200,
                generationMilliseconds: 1300,
                inputTokens: 100,
                outputTokens: 30,
              },
            },
          ],
        },
      },
    });
  });
  await page.goto("/work/sharing");
  await page.getByRole("button", { name: "我分享的", exact: true }).click();
  const cards = page.locator("nx-resource-gallery").getByRole("link");
  await cards.click();
  await expect(page).toHaveURL(new RegExp(`/work/sharing/${id}\\?sent=true$`));
  await page.getByRole("link", { name: "全部分享", exact: true }).click();
  await expect(page).toHaveURL(/\/work\/sharing\?sent=true$/);
  await expect(cards).toHaveCount(1);
  await page.reload();
  await expect(
    page.getByRole("button", { name: "我分享的", exact: true }),
  ).toHaveAttribute("aria-pressed", "true");
  await expect(cards).toHaveCount(1);
  await cards.click();
  await expect(page).toHaveURL(new RegExp(`/work/sharing/${id}\\?sent=true$`));
  await page.locator("nx-run-timing summary").click();
  await expect(page.locator("nx-run-timing")).toContainText("100");
  await page.locator(".shared-citation summary").click();
  await expect(page.locator(".shared-citation")).toContainText(
    "核准的摘要文字",
  );
  await page.getByRole("link", { name: "閱讀附件：分享報告.pdf" }).click();
  const reader = page.getByRole("dialog", { name: "檔案預覽" });
  await expect(reader.locator("canvas")).toHaveAttribute("width", /[1-9]\d+/);
  await reader.getByRole("button", { name: "下一頁", exact: true }).click();
  await expect(
    reader.getByRole("combobox", { name: "文件頁碼", exact: true }),
  ).toContainText("第 2 頁");
  await reader.getByRole("button", { name: "擷取文字", exact: true }).click();
  await expect(reader).toContainText("第二頁摘要");
  await page.keyboard.press("Escape");
  previewText = false;
  await page.getByRole("link", { name: "閱讀附件：分享報告.pdf" }).click();
  await expect(reader.locator("canvas")).toHaveAttribute("width", /[1-9]\d+/);
  const original = reader.getByRole("button", {
    name: "原始頁面",
    exact: true,
  });
  await expect(
    reader.getByRole("button", { name: "擷取文字", exact: true }),
  ).toBeDisabled();
  await original.focus();
  await original.press("End");
  await expect(original).toBeFocused();
  await expect(original).toHaveAttribute("aria-pressed", "true");
  await expect(reader.locator("canvas")).toBeVisible();
  await page.keyboard.press("Escape");
  await settleEntrance(page);
  await page.setViewportSize({ width: 375, height: 812 });
  await expectViewportContained(page);
});

for (const width of [1440, 375]) {
  test(`sharing tours walk the list and what the viewer can do with a share at ${width}px`, async ({
    page,
  }) => {
    const errors: string[] = [];
    page.on("pageerror", (error) => errors.push(error.message));
    const core = new ApiFixture();
    core.extraFeatures.push({
      id: "sharing",
      name: "分享",
      route: "/work/sharing",
    });
    // A recipient reading an artifact version has no share menu, so the tour ends on its facts.
    const recipient = width === 375;
    const createdAt = new Date().toISOString();
    const content: SharedContentDto = {
      share: {
        id: randomUUID(),
        kind: recipient ? "artifact" : "conversation",
        title: "季度工作摘要",
        owner: recipient ? "林同事" : "測試使用者",
        isOwner: !recipient,
        isRevoked: false,
        expiresAt: new Date(Date.now() + 86400000).toISOString(),
        createdAt,
        recipients: recipient ? ["測試使用者"] : ["林同事"],
        includeAttachments: !recipient,
      },
      snapshot: recipient
        ? {
            content: "# 季度工作摘要\n\n第一段內容。",
            artifactVersion: 2,
            messages: [],
          }
        : {
            content: "",
            artifactVersion: null,
            messages: (["user", "assistant"] as const).map((role) => ({
              role,
              content:
                role === "user" ? "請摘要這季的工作" : "本季完成三項工作。",
              status: "completed",
              createdAt,
              attachments: [],
              modelId: null,
              errorCode: null,
              sources: null,
              webSources: null,
              timing: null,
              modelDisplayName: null,
              issueCode: null,
            })),
          },
    };
    await core.attach(page);
    await page.route("**/api/v1/shares**", (route) => {
      const path = new URL(route.request().url()).pathname;
      return route.fulfill({
        json: path === "/api/v1/shares" ? [content.share] : content,
      });
    });
    const height = width === 375 ? 812 : 1000;
    await page.setViewportSize({ width, height });
    const launcher = page
      .locator(".feature-header")
      .getByRole("button", { name: "操作導覽", exact: true });
    const walk = () => walkTour(page, launcher);
    await page.goto("/work/sharing");
    await expect(page.locator(".resource-card")).toBeVisible();
    const list = await walk();
    expect(list.titles).toEqual([
      "收到的分享集中閱讀",
      "分享給我與我分享的",
      "用標題尋找分享",
      "打開分享",
    ]);
    expect(list.last).toContain("重播導覽");
    await page.locator(".resource-card").click();
    await expect(page.locator(".share-content")).toContainText(
      recipient ? "第一段內容" : "本季完成三項工作",
    );
    const detail = await walk();
    expect(detail.titles).toEqual(
      recipient
        ? ["分享者與期限", "分享當下的成果版本", "分享資訊"]
        : ["分享者與期限", "分享當下的對話", "分享資訊", "撤銷分享"],
    );
    expect(detail.last).toContain("重播導覽");
    expect(errors).toEqual([]);
  });
}
