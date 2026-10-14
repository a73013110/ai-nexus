import { test, expect } from "@playwright/test";
import {
  ApiFixture,
  chooseSelect,
  expectViewportContained,
  toasts,
} from "./fixtures";

test("price tab lists current prices, adds immutable versions in a dialog and fits a phone", async ({
  page,
}) => {
  const fixture = new ApiFixture();
  fixture.adminAccess = true;
  await fixture.attach(page);
  let saved: Record<string, unknown> | undefined;
  await page.route("**/api/v1/admin/billing/prices", (route) => {
    if (route.request().method() === "POST") {
      saved = route.request().postDataJSON();
      return route.fulfill({ json: { ...saved, id: "price-1" } });
    }
    return route.fulfill({
      json: saved
        ? [{ ...saved, id: "price-1", modelDisplayName: "本地助理" }]
        : [],
    });
  });
  await page.route("**/api/v1/admin/billing/targets", (route) =>
    route.fulfill({
      json: [
        {
          provider: "google",
          modelId: "gemma-private-model-id",
          displayName: "雲端助理",
        },
        { provider: "ollama", modelId: "qwen3:8b", displayName: "本地助理" },
      ],
    }),
  );
  await page.goto("/admin/operations");
  await page
    .getByRole("group", { name: "平台營運分類", exact: true })
    .getByRole("button", { name: "模型與工具價格", exact: true })
    .click();
  await expect(page).toHaveURL(/\/admin\/operations\?tab=prices$/);
  // Every configured model and tool has a row; without a version its cost is unknown, not zero.
  const current = page.getByRole("region", { name: "目前價格", exact: true });
  await expect(current.getByRole("row")).toHaveCount(3);
  await expect(
    current.getByRole("row").filter({ hasText: "本地助理" }),
  ).toContainText("未定價");
  await page.getByRole("button", { name: "新增價格版本", exact: true }).click();
  const editor = page.getByRole("dialog", {
    name: "新增價格版本",
    exact: true,
  });
  const submit = editor.getByRole("button", {
    name: "新增價格版本",
    exact: true,
  });
  // A draft that has not changed can be neither saved nor reverted.
  await expect(submit).toBeDisabled();
  await expect(editor.getByRole("button", { name: "還原" })).toBeDisabled();
  await chooseSelect(page, "計費供應商", "地端 Ollama");
  await chooseSelect(page, "計費模型或工具", "本地助理");
  await expect(editor).not.toContainText("qwen3:8b");
  await chooseSelect(page, "費用類型", "內部成本估算");
  await editor.getByLabel("每次呼叫固定費用", { exact: true }).fill("0.3");
  await expect(editor.locator(".dialog-note")).toHaveText("有未儲存的變更");
  const effective = editor.getByRole("textbox", {
    name: "生效時間",
    exact: true,
  });
  await expect(effective).toHaveValue("");
  await effective.fill("2026/02/30 14:30:45");
  await effective.press("Tab");
  await expect(submit).toBeDisabled();
  await effective.fill("2026/10/09 14:30:45");
  await submit.click();
  await expect(
    toasts(page).filter({ hasText: "新價格版本已儲存" }),
  ).toBeVisible();
  await expect(editor).not.toBeVisible();
  expect(saved).toMatchObject({
    provider: "ollama",
    modelId: "qwen3:8b",
    currency: "USD",
    kind: "internal",
    perRequest: 0.3,
    effectiveAt: "2026-10-09T06:30:45.000Z",
  });
  await page.getByRole("button", { name: "價格歷史", exact: true }).click();
  const history = page.getByRole("region", { name: "價格歷史", exact: true });
  await expect(
    history.getByRole("row").filter({ hasText: "本地助理" }),
  ).toContainText("內部成本估算");
  await history
    .getByRole("button", { name: "帶入此版本：本地助理", exact: true })
    .click();
  await expect(editor).toContainText("已帶入此版本");
  await expect(
    editor.getByLabel("每次呼叫固定費用", { exact: true }),
  ).toHaveValue("0.3");
  // Closing an untouched copy needs no confirmation; a changed one asks first.
  await editor.getByLabel("每次呼叫固定費用", { exact: true }).fill("0.4");
  await editor
    .getByRole("button", { name: "關閉新增價格版本", exact: true })
    .click();
  await page
    .getByRole("dialog", { name: "放棄未儲存的變更？", exact: true })
    .getByRole("button", { name: "放棄變更", exact: true })
    .click();
  await expect(editor).not.toBeVisible();
  await page.reload();
  await page.getByRole("button", { name: "價格歷史", exact: true }).click();
  await expect(history.getByRole("row")).toHaveCount(2);
  await page.setViewportSize({ width: 375, height: 812 });
  await expectViewportContained(page);
});
