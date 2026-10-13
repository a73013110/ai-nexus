import { test, expect } from "@playwright/test";
import {
  ApiFixture,
  chooseSelect,
  expectViewportContained,
  toasts,
} from "./fixtures";

test("price tab creates immutable versions, reuses history and fits a phone", async ({
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
  const editor = page.getByRole("region", {
    name: "新增價格版本",
    exact: true,
  });
  const history = page.getByRole("region", { name: "價格歷史", exact: true });
  await expect(history).toContainText("尚未設定價格");
  await chooseSelect(page, "計費供應商", "ollama");
  await chooseSelect(page, "計費模型或工具", "本地助理");
  await expect(editor).not.toContainText("qwen3:8b");
  await chooseSelect(page, "費用類型", "內部成本估算");
  await editor.getByLabel("每次呼叫固定費用", { exact: true }).fill("0.3");
  const effective = editor.getByRole("textbox", {
    name: "生效時間",
    exact: true,
  });
  await expect(effective).toHaveValue("");
  await effective.fill("2026/02/30 14:30:45");
  await effective.press("Tab");
  const submit = editor.getByRole("button", {
    name: "新增價格版本",
    exact: true,
  });
  await expect(submit).toBeDisabled();
  await effective.fill("2026/10/09 14:30:45");
  await submit.click();
  await expect(
    toasts(page).filter({ hasText: "新價格版本已儲存" }),
  ).toBeVisible();
  expect(saved).toMatchObject({
    provider: "ollama",
    modelId: "qwen3:8b",
    currency: "USD",
    kind: "internal",
    perRequest: 0.3,
    effectiveAt: "2026-10-09T06:30:45.000Z",
  });
  const row = history.getByRole("button").filter({ hasText: "本地助理" });
  await expect(row).toBeVisible();
  await chooseSelect(page, "計費供應商", "google");
  await row.click();
  await expect(editor).toContainText("已帶入此版本");
  await expect(
    editor.getByLabel("每次呼叫固定費用", { exact: true }),
  ).toHaveValue("0.3");
  await page.reload();
  await expect(history.getByRole("button")).toHaveCount(1);
  await page.setViewportSize({ width: 375, height: 812 });
  await expectViewportContained(page);
});
