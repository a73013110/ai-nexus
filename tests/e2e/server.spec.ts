import { test, expect } from "@playwright/test";

test("real local server serves CSP-compatible assets and explicit unconfigured storage", async ({
  page,
  request,
}) => {
  const response = await request.get("/api/v1/me");
  expect(response.status()).toBe(503);
  expect((await response.json()).code).toBe("storage_not_configured");
  expect(response.headers()["www-authenticate"]).toBeUndefined();
  const errors: string[] = [];
  page.on("pageerror", (error) => errors.push(error.message));
  const document = await page.goto("/chat");
  expect(document?.headers()["content-security-policy"]).toContain(
    "script-src 'self'",
  );
  await expect(page.getByRole("alert")).toContainText("資料庫設定");
  await expect(page.locator(".workbench")).toHaveCSS("display", "grid");
  await expect(page.locator(".sidebar")).toHaveCSS("width", "264px");
  await page.getByRole("textbox", { name: "傳送訊息" }).fill("本機草稿");
  await expect(page.getByRole("button", { name: "送出訊息" })).toBeDisabled();
  await expect(
    page.getByRole("button", { name: "登入者選單", exact: true }),
  ).toBeVisible();
  expect(errors).toEqual([]);
  await page.screenshot({
    path: "artifacts/screenshots/local-unconfigured.png",
    fullPage: true,
  });
});
