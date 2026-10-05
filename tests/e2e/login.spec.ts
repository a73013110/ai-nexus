import { test, expect } from "@playwright/test";
import { ApiFixture, settleEntrance } from "./fixtures";

test("an authenticated Windows user can switch to local login and return to Windows", async ({
  page,
}) => {
  const fixture = new ApiFixture();
  await fixture.attach(page);
  const transitions: string[] = [];
  page.on("request", (request) => {
    const path = new URL(request.url()).pathname;
    if (path === "/api/v1/auth/logout" || path === "/api/v1/auth/windows")
      transitions.push(path);
  });
  await page.goto("/chat");
  await page.getByRole("button", { name: "登入者選單", exact: true }).click();
  await page.getByRole("menuitem", { name: "切換登入方式" }).click();
  await expect(page).toHaveURL(/\/login\?method=local$/);
  await expect(page.getByRole("heading", { name: "登入工作區" })).toBeVisible();
  await page.getByLabel("本地帳號", { exact: true }).fill("tester");
  await page.getByLabel("本地密碼", { exact: true }).fill("fixture-password");
  await page.getByRole("button", { name: "登入工作區", exact: true }).click();
  await expect(page).toHaveURL(/\/dashboard$/);
  expect(fixture.loginMethod).toBe("local");
  await page.goto("/chat");
  await page.getByRole("button", { name: "登入者選單", exact: true }).click();
  await page.getByRole("menuitem", { name: "切換登入方式" }).click();
  await page.getByRole("button", { name: "Windows 身分", exact: true }).click();
  await page.getByRole("button", { name: "使用 Windows 身分登入" }).click();
  await expect(page).toHaveURL(/\/dashboard$/);
  expect(transitions).toEqual(["/api/v1/auth/logout", "/api/v1/auth/windows"]);
  expect(fixture.loginMethod).toBe("windows");
});

test("local and AD sign-in share a form, clear passwords on method change and keep secrets out of storage", async ({
  page,
}) => {
  const fixture = new ApiFixture();
  fixture.ldap = true;
  await fixture.attach(page);
  await page.goto("/login");
  await page
    .getByLabel("AD 密碼", { exact: true })
    .fill("old directory secret");
  await page.getByRole("button", { name: "本地帳號", exact: true }).click();
  await expect(page.getByLabel("本地密碼", { exact: true })).toHaveValue("");
  await page.getByLabel("本地帳號", { exact: true }).fill("local-tester");
  await page.getByLabel("本地密碼", { exact: true }).fill("fixture-password");
  await page.getByRole("button", { name: "登入工作區", exact: true }).click();
  await expect(page).toHaveURL(/\/dashboard$/);
  expect(fixture.loginMethod).toBe("local");
  expect(await page.evaluate(() => JSON.stringify(localStorage))).not.toContain(
    "fixture-password",
  );
});

test("LDAP login rejects passwords, clears secrets and returns to chat", async ({
  page,
}) => {
  const fixture = new ApiFixture();
  fixture.ldap = true;
  await fixture.attach(page);
  await page.goto("/chat");
  await expect(page.getByRole("heading", { name: "登入工作區" })).toBeVisible();
  const submit = page.getByRole("button", { name: "登入工作區", exact: true });
  await expect(submit).toBeDisabled();
  await page.getByLabel("AD 帳號", { exact: true }).fill("alice");
  await page.getByLabel("AD 密碼", { exact: true }).fill("wrong");
  await submit.click();
  await expect(page.getByRole("alert")).toContainText("AD 帳號或密碼不正確");
  await expect(page.getByLabel("AD 密碼", { exact: true })).toHaveValue("");
  await page.getByLabel("AD 密碼", { exact: true }).fill("fixture-password");
  await submit.click();
  await expect(
    page.getByRole("heading", { name: "今天，從哪件事開始？" }),
  ).toBeVisible();
  await page.getByRole("button", { name: "登入者選單", exact: true }).click();
  await page.getByRole("menuitem", { name: "登出工作區" }).click();
  await expect(page.getByRole("heading", { name: "登入工作區" })).toBeVisible();
  expect(await page.evaluate(() => JSON.stringify(localStorage))).not.toContain(
    "fixture-password",
  );
});

test("login works at 375px and cannot redirect to an external origin", async ({
  page,
}) => {
  await page.setViewportSize({ width: 375, height: 812 });
  const fixture = new ApiFixture();
  fixture.ldap = true;
  await fixture.attach(page);
  await page.goto("/login?returnUrl=https://evil.invalid");
  await expect(page.getByRole("heading", { name: "登入工作區" })).toBeVisible();
  expect(
    await page.evaluate(
      () => document.documentElement.scrollWidth <= innerWidth,
    ),
  ).toBe(true);
  await settleEntrance(page);
  await page.screenshot({
    path: "artifacts/screenshots/login-mobile.png",
    fullPage: true,
  });
  await page.getByLabel("AD 帳號", { exact: true }).fill("alice");
  await page.getByLabel("AD 密碼", { exact: true }).fill("fixture-password");
  await page.getByRole("button", { name: "登入工作區", exact: true }).click();
  await expect(page).toHaveURL(/\/dashboard$/);
});

for (const destination of ["/dashboard?scope=personal", "/repositories"]) {
  test(`sign-in preserves the requested ${destination} route without loading chat data`, async ({
    page,
  }) => {
    const fixture = new ApiFixture();
    fixture.ldap = true;
    fixture.extraFeatures = [
      { id: "dashboard", name: "總覽", route: "/dashboard" },
    ];
    const chatRequests: string[] = [];
    page.on("request", (request) => {
      if (
        /\/api\/v1\/(?:models|conversations|attachments)(?:\?|$)/.test(
          new URL(request.url()).pathname,
        )
      )
        chatRequests.push(request.url());
    });
    await fixture.attach(page);
    await page.goto(`/login?returnUrl=${encodeURIComponent(destination)}`);
    await page.getByLabel("AD 帳號", { exact: true }).fill("alice");
    await page.getByLabel("AD 密碼", { exact: true }).fill("fixture-password");
    await page.getByRole("button", { name: "登入工作區", exact: true }).click();
    await expect(page).toHaveURL(
      new RegExp(destination.replace("?", "\\?") + "$"),
    );
    await expect(page.locator(".feature-header")).toBeVisible();
    expect(chatRequests).toEqual([]);
  });
}

test("desktop login keeps the workspace visual language", async ({ page }) => {
  const fixture = new ApiFixture();
  fixture.ldap = true;
  await fixture.attach(page);
  await page.goto("/login");
  await expect(page.getByRole("heading", { name: "登入工作區" })).toBeVisible();
  await expect(page.locator(".login-workspace")).toHaveCSS("display", "grid");
  await settleEntrance(page);
  await page.screenshot({
    path: "artifacts/screenshots/login-desktop.png",
    fullPage: true,
  });
});
