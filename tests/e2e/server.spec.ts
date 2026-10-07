import { test, expect } from "@playwright/test";
import { ApiFixture, expectViewportContained, fixtureIssueCode } from "./fixtures";

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
        body: JSON.stringify({ title: "Password=fixture-private; internal endpoint", code: "service_unavailable", issueCode: fixtureIssueCode }),
      }),
    );
    await page.goto(target);
    await expect(page).toHaveURL(/\/login\?/);
    await expect(page.getByRole("alert")).toContainText("操作未完成，請聯絡管理員。查證代碼：" + fixtureIssueCode);
    await expect(page.getByRole("alert")).not.toContainText("fixture-private");
    await expect(
      page.getByRole("button", { name: "重新確認登入服務" }),
    ).toBeVisible();
    await expect(page.locator(".workbench")).toHaveCount(0);
  });
}

test("unhandled client issues show a safe copyable code without overflowing the workspace", async ({ page }) => {
  const fixture = new ApiFixture();
  await fixture.attach(page);
  let reports = 0;
  await page.route("**/api/v1/client-issues", (route) => {
    reports++;
    const payload = route.request().postDataJSON();
    expect(Object.keys(payload).sort()).toEqual(["fingerprint", "kind"]);
    expect(JSON.stringify(payload)).not.toContain("fixture-private");
    return route.fulfill({ json: { accepted: true, issueCode: fixtureIssueCode } });
  });
  await page.goto("/chat");
  await expect(page.getByRole("textbox", { name: "傳送訊息" })).toBeVisible();
  await page.evaluate(() => window.dispatchEvent(new ErrorEvent("error", { error: new TypeError("Password=fixture-private"), message: "fixture-private" })));
  const banner = page.locator("nx-unhandled-issue");
  await expect(banner).toContainText(fixtureIssueCode);
  await expect(banner).not.toContainText("fixture-private");
  await expect(banner.getByRole("button", { name: "複製問題查證代碼" })).toBeVisible();
  await expectViewportContained(page);
  await page.setViewportSize({ width: 375, height: 812 });
  await expectViewportContained(page);
  await banner.getByRole("button", { name: "關閉提示" }).click();
  await expect(banner.getByRole("alert")).toHaveCount(0);
  await expectViewportContained(page);
  expect(reports).toBe(1);
});
