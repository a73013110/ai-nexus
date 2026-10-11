import { test, expect } from "@playwright/test";
import {
  ApiFixture,
  chooseSelect,
  expectViewportContained,
  dashboardFixture,
} from "./fixtures";

test("price dialog creates immutable versions and stays within the viewport", async ({
  page,
}) => {
  const fixture = new ApiFixture();
  fixture.adminAccess = true;
  fixture.extraFeatures = [
    { id: "dashboard", name: "總覽", route: "/dashboard" },
  ];
  await fixture.attach(page);
  let saved: Record<string, unknown> | undefined;
  await page.route("**/api/v1/dashboard?**", (route) =>
    route.fulfill({ json: dashboardFixture(fixture) }),
  );
  await page.route("**/api/v1/admin/billing/prices", (route) => {
    if (route.request().method() === "POST") {
      saved = route.request().postDataJSON();
      return route.fulfill({ json: { ...saved, id: crypto.randomUUID() } });
    }
    return route.fulfill({ json: [] });
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
  await page.goto("/dashboard");
  await page
    .getByRole("button", { name: "模型與工具價格", exact: true })
    .click();
  const dialog = page.getByRole("dialog", { name: "模型與工具價格版本" });
  await expect(dialog).toBeVisible();
  await chooseSelect(page, "計費供應商", "ollama");
  await chooseSelect(page, "計費模型或工具", "本地助理");
  await expect(dialog).not.toContainText("qwen3:8b");
  await chooseSelect(page, "費用類型", "內部成本估算");
  await dialog.getByLabel("每次呼叫固定費用", { exact: true }).fill("0.3");
  const effective = dialog.getByRole("textbox", {
    name: "生效時間",
    exact: true,
  });
  await expect(effective).toHaveValue("");
  await effective.fill("2026/02/30 14:30:45");
  await effective.press("Tab");
  await expect(
    dialog.getByRole("button", { name: "新增價格版本", exact: true }),
  ).toBeDisabled();
  await effective.fill("2026/10/09 14:30:45");
  await dialog
    .getByRole("button", { name: "新增價格版本", exact: true })
    .click();
  await expect(
    dialog.getByRole("status").filter({ hasText: "新價格版本已儲存" }),
  ).toBeVisible();
  expect(saved).toMatchObject({
    provider: "ollama",
    modelId: "qwen3:8b",
    currency: "USD",
    kind: "internal",
    perRequest: 0.3,
    effectiveAt: "2026-10-09T06:30:45.000Z",
  });
  await page.setViewportSize({ width: 375, height: 812 });
  const bounds = await dialog.boundingBox();
  expect(bounds!.x).toBeGreaterThanOrEqual(0);
  expect(bounds!.width).toBeLessThanOrEqual(375);
  expect(bounds!.y + bounds!.height).toBeLessThanOrEqual(812);
  const body = dialog.locator(".dialog-scroll");
  expect(await body.evaluate((el) => el.scrollHeight > el.clientHeight)).toBe(
    true,
  );
  expect(
    await dialog.evaluate((el) => el.scrollHeight <= el.clientHeight),
  ).toBe(true);
  const bodyBounds = await body.boundingBox();
  expect(
    bounds!.x + bounds!.width - (bodyBounds!.x + bodyBounds!.width),
  ).toBeGreaterThanOrEqual(8);
  await body.hover();
  await page.mouse.wheel(0, 500);
  await expect
    .poll(() => body.evaluate((el) => el.scrollTop))
    .toBeGreaterThan(0);
  await expectViewportContained(page);
  await page.evaluate(() => (document.documentElement.dataset.theme = "dark"));
  await body.evaluate((el) => el.scrollTo({ top: 0, behavior: "instant" }));
  await page.keyboard.press("Escape");
  await expect(dialog).not.toBeVisible();
  await expect(
    page.getByRole("button", { name: "模型與工具價格", exact: true }),
  ).toBeFocused();
});
