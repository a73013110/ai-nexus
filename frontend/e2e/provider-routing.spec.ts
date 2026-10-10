import { test, expect } from "@playwright/test";
import { ApiFixture } from "./fixtures";

test("same-named models remain distinct routing references while the picker only shows DisplayName", async ({
  page,
}) => {
  const fixture = new ApiFixture();
  fixture.preferences.defaultModelId = fixture.modelPolicy.defaultModelId =
    "google/shared-model";
  await fixture.attach(page);
  await page.route("**/api/v1/models", (route) =>
    route.fulfill({
      contentType: "application/json",
      body: JSON.stringify({
        models: ["google", "ollama"].map((provider) => ({
          id: `${provider}/shared-model`,
          displayName: "同名模型",
          provider,
          contextTokens: 8192,
          maxOutputTokens: 2048,
          supportsStreaming: true,
          supportsUsage: true,
          supportsImages: true,
          reasoningEfforts: [],
          defaultReasoningEffort: "auto",
        })),
        providerAvailable: true,
        notice: null,
        policy: fixture.modelPolicy,
        providers: ["google", "ollama"].map((id) => ({
          id,
          available: true,
          notice: null,
        })),
      }),
    }),
  );
  await page.goto("/chat");
  await expect(
    page.getByRole("combobox", { name: "選擇模型", exact: true }),
  ).toContainText("同名模型");
  await page.getByRole("combobox", { name: "選擇模型", exact: true }).click();
  const choices = page.getByRole("listbox", { name: "選擇模型", exact: true });
  await expect(choices).not.toContainText("shared-model");
  await expect(choices).not.toContainText("Ollama");
  await choices.getByRole("option").nth(1).click();
  await page
    .getByRole("textbox", { name: "傳送訊息", exact: true })
    .fill("使用本機模型");
  await page.getByRole("button", { name: "送出訊息", exact: true }).click();
  await expect(
    page.getByRole("button", { name: "重新生成", exact: true }),
  ).toBeVisible();
  expect(fixture.lastRequest?.modelId).toBe("ollama/shared-model");
  await expect(page.locator("nx-run-timing")).toContainText("1.5 秒");
});
