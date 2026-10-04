import { test, expect } from "@playwright/test";
import { randomUUID } from "node:crypto";
import { ApiFixture, settleEntrance } from "./fixtures";
import type {
  SourceDetail,
  Conversation,
} from "../../frontend/src/app/core/api/types";

test("controlled source search opens authorized status and history, then hands a draft to chat without sending", async ({
  page,
}) => {
  const core = new ApiFixture();
  core.extraFeatures.push({
    id: "integrations",
    name: "系統整合",
    route: "/integrations",
  });
  await core.attach(page);
  const detail: SourceDetail = {
    sourceId: "gdweb",
    record: {
      id: "DOC-001",
      kind: "document",
      title: "例行作業通知",
      status: "核准",
      revision: "v3",
      modifiedAt: new Date().toISOString(),
    },
    body: "## 作業通知\n\n請在本週五前整理待確認事項。",
    history: [
      {
        at: new Date().toISOString(),
        kind: "approved",
        actor: "承辦人",
        description: "核對並完成本次簽核",
        revision: "v3",
      },
    ],
    historyLimited: false,
  };
  await page.route("**/api/v1/integrations**", (route) => {
    const url = new URL(route.request().url());
    const path = url.pathname;
    const json = (body: unknown) =>
      route.fulfill({
        contentType: "application/json",
        body: JSON.stringify(body),
      });
    if (path.endsWith("/integrations"))
      return json([
        {
          id: "gdweb",
          name: "公文系統",
          description: "授權公文與簽核歷程",
          status: "configured",
          notice: "已設定，查詢時確認連線與資料授權。",
          canQuery: true,
          kinds: ["document"],
        },
        {
          id: "meiho",
          name: "校務系統",
          description: "核准的政策與單位資料",
          status: "disabled",
          notice: "來源尚未啟用。",
          canQuery: false,
          kinds: ["reference", "organization"],
        },
      ]);
    if (path.endsWith("/records")) {
      expect(url.searchParams.get("query")).toBe("通知");
      return json([detail.record]);
    }
    if (path.endsWith("/record")) return json(detail);
    if (path.endsWith("/chat")) {
      const request = route.request().postDataJSON();
      expect(request).toEqual({
        recordId: detail.record.id,
        expectedRevision: "v3",
      });
      const conversation: Conversation = {
        id: randomUUID(),
        title: detail.record.title,
        activeLeafId: null,
        createdAt: new Date().toISOString(),
        updatedAt: new Date().toISOString(),
        isFavorite: false,
        isArchived: false,
        systemInstruction: "",
        labels: [],
        projectId: null,
      };
      core.conversations.push(conversation);
      return json({
        conversation,
        prompt: "請分析下方來源資料：\n\n" + detail.body,
      });
    }
    return route.fulfill({ status: 404 });
  });
  await page.goto("/integrations");
  await page.getByPlaceholder("搜尋公文系統的標題或識別碼…").fill("通知");
  await page.getByRole("button", { name: "搜尋", exact: true }).click();
  await expect(
    page.getByRole("heading", { name: "例行作業通知", exact: true }),
  ).toBeVisible();
  await expect(page.getByText("核對並完成本次簽核")).toBeVisible();
  await settleEntrance(page);
  await page.screenshot({
    path: "artifacts/screenshots/integrations-desktop.png",
  });
  await page.setViewportSize({ width: 390, height: 844 });
  expect(
    await page.evaluate(
      () => document.documentElement.scrollWidth <= innerWidth,
    ),
  ).toBeTruthy();
  await page.screenshot({
    path: "artifacts/screenshots/integrations-mobile.png",
  });
  await page.getByRole("button", { name: "帶入對話", exact: true }).click();
  await expect(
    page.getByRole("textbox", { name: "傳送訊息", exact: true }),
  ).toHaveValue("請分析下方來源資料：\n\n" + detail.body);
  expect(core.posts).toBe(0);
  expect(
    await page.evaluate(() => JSON.stringify(history.state)),
  ).not.toContain("作業通知");
});

test("unconfigured sources disclose setup status and do not offer queries", async ({
  page,
}) => {
  const core = new ApiFixture();
  core.extraFeatures.push({
    id: "integrations",
    name: "系統整合",
    route: "/integrations",
  });
  await core.attach(page);
  await page.route("**/api/v1/integrations", (route) =>
    route.fulfill({
      contentType: "application/json",
      body: JSON.stringify([
        {
          id: "gdweb",
          name: "公文系統",
          description: "唯讀文件",
          status: "acl-unconfirmed",
          notice: "等待確認來源端授權。",
          canQuery: false,
          kinds: ["document"],
        },
      ]),
    }),
  );
  await page.goto("/integrations");
  await expect(
    page.getByRole("heading", { name: "公文系統尚未可用" }),
  ).toBeVisible();
  await expect(
    page.getByRole("button", { name: "搜尋", exact: true }),
  ).toHaveCount(0);
});
