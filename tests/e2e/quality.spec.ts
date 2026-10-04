import { test, expect } from "@playwright/test";
import { randomUUID } from "node:crypto";
import { ApiFixture, chooseSelect, settleEntrance } from "./fixtures";
import type {
  EvaluationSet,
  EvaluationDetail,
} from "../../frontend/src/app/core/api/types";

test("fixed evaluation cases compare instructions, show diagnostic results and retain a manual review", async ({
  page,
}) => {
  const core = new ApiFixture();
  core.extraFeatures.push({
    id: "quality",
    name: "品質評測",
    route: "/quality",
  });
  core.modelPolicy.allowModelSelection = false;
  core.modelPolicy.showModelNames = false;
  core.modelPolicy.defaultModelId = "model-1";
  await core.attach(page);
  const set: EvaluationSet = {
    resource: {
      id: randomUUID(),
      name: "公文摘要檢核",
      kind: "evaluation",
      canEdit: true,
      isOwner: true,
      updatedAt: new Date().toISOString(),
    },
    description: "比較摘要的可用性",
    version: 1,
    cases: [
      {
        question: "整理通知的辦理期限",
        reference: "整理重點後確認日期與承辦人",
        requiredTerms: ["期限"],
        forbiddenTerms: ["捏造"],
      },
    ],
  };
  let detail: EvaluationDetail | null = null;
  await page.route("**/api/v1/quality/**", async (route) => {
    const path = new URL(route.request().url()).pathname,
      method = route.request().method();
    const json = (data: unknown) =>
      route.fulfill({
        contentType: "application/json",
        body: JSON.stringify(data),
      });
    if (path.endsWith("/feedback")) return json([]);
    if (path.endsWith("/sets")) return json([set]);
    if (path.endsWith("/runs") && method === "POST") {
      const variants = route.request().postDataJSON().variants;
      detail = {
        run: {
          id: randomUUID(),
          setId: set.resource.id,
          title: set.resource.name,
          setVersion: 1,
          canControl: true,
          createdAt: new Date().toISOString(),
          job: {
            id: randomUUID(),
            kind: "evaluation",
            subjectId: randomUUID(),
            label: "品質評測",
            status: "completed",
            stage: "處理完成",
            attempt: 1,
            completedUnits: 2,
            totalUnits: 2,
            cancelRequested: false,
            errorCode: null,
            errorMessage: null,
            createdAt: new Date().toISOString(),
            updatedAt: new Date().toISOString(),
          },
        },
        cases: set.cases,
        variants,
        canReview: true,
        results: variants.map((_: unknown, i: number) => ({
          caseIndex: 0,
          variantIndex: i,
          output: `**方案 ${i + 1}**：辦理期限須由承辦人確認。`,
          truncated: false,
          requiredMatches: 1,
          requiredTotal: 1,
          forbiddenMatches: 0,
          elapsedMs: 1200,
          inputTokens: 100,
          outputTokens: 30,
          reviewScore: null,
          reviewNote: "",
        })),
      };
      return json(detail.run);
    }
    if (path.endsWith("/runs")) return json(detail ? [detail.run] : []);
    if (path.endsWith("/review")) {
      const data = route.request().postDataJSON();
      detail!.results[0].reviewScore = data.score;
      detail!.results[0].reviewNote = data.note;
      return route.fulfill({ status: 204 });
    }
    if (path.includes("/quality/runs/")) return json(detail);
    return json(set);
  });
  await page.goto(`/quality/${set.resource.id}`);
  await expect(
    page.getByRole("heading", { name: set.resource.name }),
  ).toBeVisible();
  await page.getByRole("button", { name: "開始比較", exact: true }).click();
  const dialog = page.getByRole("dialog");
  await expect(dialog).toBeVisible();
  await expect(dialog.getByRole("combobox")).toHaveCount(0);
  await dialog.getByRole("button", { name: "加入比較方案" }).click();
  await dialog
    .getByLabel("比較指令", { exact: true })
    .nth(1)
    .fill("先列出待確認日期。");
  await dialog.getByRole("button", { name: "開始評測", exact: true }).click();
  await expect(page.locator(".evaluation-answer")).toHaveCount(2);
  await expect(page.getByText("必要詞 1 / 1").first()).toBeVisible();
  await page.getByRole("button", { name: "評分", exact: true }).first().click();
  await chooseSelect(page, "人工品質評分", "4 分");
  await dialog.getByLabel("判讀說明").fill("需要確認期限，但格式清楚。");
  await dialog.getByRole("button", { name: "儲存", exact: true }).click();
  await expect(page.getByText("人工評分：4 / 5")).toBeVisible();
  await expect(page.getByText("需要確認期限，但格式清楚。")).toBeVisible();
  await settleEntrance(page);
  await page.screenshot({ path: "artifacts/screenshots/quality-desktop.png" });
  await page.setViewportSize({ width: 390, height: 844 });
  await expect(page.locator(".evaluation-answer").first()).toBeVisible();
  expect(
    await page.evaluate(
      () => document.documentElement.scrollWidth <= innerWidth,
    ),
  ).toBeTruthy();
  await page.screenshot({ path: "artifacts/screenshots/quality-mobile.png" });
});

test("answer feedback is one click, can be supplemented and toggled off", async ({
  page,
}) => {
  const core = new ApiFixture();
  await core.attach(page);
  let rating = 0,
    note = "";
  await page.route("**/api/v1/messages/*/feedback", (route) => {
    if (route.request().method() === "GET")
      return route.fulfill({
        contentType: "application/json",
        body: JSON.stringify({ rating, note, reason: "" }),
      });
    const value = route.request().postDataJSON();
    rating = value.rating;
    note = value.note;
    return route.fulfill({
      contentType: "application/json",
      body: JSON.stringify(value.rating ? { rating, note } : null),
    });
  });
  await page.goto("/chat");
  await page
    .getByRole("textbox", { name: "傳送訊息", exact: true })
    .fill("測試回答品質");
  await page.getByRole("button", { name: "送出訊息" }).click();
  const negative = page.getByRole("button", { name: "回答待改善" });
  await expect(negative).toBeVisible();
  await negative.click();
  await expect(negative).toHaveAttribute("aria-pressed", "true");
  expect(rating).toBe(-1);
  await page.getByRole("button", { name: "補充回饋" }).click();
  await page
    .getByRole("textbox", { name: "回饋補充說明" })
    .fill("希望先說結論。");
  await page.getByRole("button", { name: "儲存補充" }).click();
  await expect(page.getByRole("textbox", { name: "回饋補充說明" })).toHaveCount(
    0,
  );
  expect(note).toBe("希望先說結論。");
  await negative.click();
  await expect(negative).toHaveAttribute("aria-pressed", "false");
  expect(rating).toBe(0);
});
