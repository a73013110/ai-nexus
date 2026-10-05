import { test, expect } from "@playwright/test";

test("real anonymous startup shows login before constructing the private workspace", async ({
  page,
  request,
}) => {
  const response = await request.get("/api/v1/me");
  expect(response.status()).toBe(401);
  const errors: string[] = [];
  page.on("pageerror", (error) => errors.push(error.message));
  const document = await page.goto("/chat");
  expect(document?.headers()["content-security-policy"]).toContain(
    "script-src 'self'",
  );
  await expect(page).toHaveURL(/\/login\?returnUrl=%2Fchat$/);
  await expect(page.getByRole("heading", { name: "登入工作區" })).toBeVisible();
  await expect(
    page.getByText("尚未取得 Windows 身分。", { exact: false }),
  ).toBeVisible();
  await expect(page.locator(".workbench")).toHaveCount(0);
  await expect(page.getByRole("textbox", { name: "傳送訊息" })).toHaveCount(0);
  expect(errors).toEqual([]);
  await page.screenshot({
    path: "artifacts/screenshots/anonymous-login.png",
    fullPage: true,
  });
});

for (const target of ["/", "/chat", "/projects", "/admin", "/quality"]) {
  test(`an unavailable session never exposes the workspace at ${target}`, async ({
    page,
  }) => {
    await page.route("**/api/v1/auth/session", (route) =>
      route.fulfill({
        status: 503,
        contentType: "application/problem+json",
        body: JSON.stringify({ title: "登入服務暫時無法使用。" }),
      }),
    );
    await page.goto(target);
    await expect(page).toHaveURL(/\/login\?/);
    await expect(page.getByRole("alert")).toContainText("登入服務暫時無法使用");
    await expect(
      page.getByRole("button", { name: "重新確認登入服務" }),
    ).toBeVisible();
    await expect(page.locator(".workbench")).toHaveCount(0);
  });
}
