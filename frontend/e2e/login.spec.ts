import { test, expect } from "@playwright/test";
import { ApiFixture, settleEntrance, fixtureIssueCode } from "./fixtures";

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
  await page.getByLabel("AD 帳號", { exact: true }).fill("alice");
  await page.getByLabel("AD 密碼", { exact: true }).fill("fixture-password");
  await page.getByRole("button", { name: "登入工作區", exact: true }).click();
  await expect(page).toHaveURL(/\/dashboard$/);
});

for (const destination of [
  "/dashboard?scope=personal",
  "/repositories",
  "/files",
]) {
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
});

test("login copy reads at once, the wordmark follows the mark and both fit standard viewports", async ({
  page,
}) => {
  const fixture = new ApiFixture();
  fixture.ldap = true;
  await fixture.attach(page);
  await page.goto("/login");
  await expect(
    page.getByRole("heading", { name: /讓想法有個\s*專注的空間/ }),
  ).toBeVisible();
  await expect(page.locator(".login-brand")).toHaveAttribute(
    "aria-hidden",
    "true",
  );
  await expect(page.locator(".login-brand .login-reveal")).toHaveCSS(
    "opacity",
    "0",
  );
  await page.getByRole("button", { name: "跳過標誌動畫", exact: true }).click();
  await expect(page.locator(".login-intro")).toHaveClass(/intro-ready/);
  await expect(page.locator(".login-brand .login-reveal")).toHaveCSS(
    "opacity",
    "1",
  );
  for (const viewport of [
    { width: 1366, height: 768 },
    { width: 1024, height: 640 },
    { width: 375, height: 667 },
    { width: 320, height: 568 },
  ]) {
    await page.setViewportSize(viewport);
    expect(
      await page.evaluate(
        () =>
          document.documentElement.scrollHeight <= innerHeight &&
          document.documentElement.scrollWidth <= innerWidth,
      ),
    ).toBe(true);
    const submit = await page
      .getByRole("button", { name: "登入工作區", exact: true })
      .boundingBox();
    expect(submit!.y + submit!.height).toBeLessThan(viewport.height);
  }
});

test("Windows users remain at login until an explicit identity challenge succeeds", async ({
  page,
}) => {
  const core = new ApiFixture();
  await core.attach(page);
  let signedIn = false;
  const session = () => ({
    mode: "Windows",
    authenticated: signedIn,
    configured: true,
    account: signedIn ? "TEST\\fixture" : null,
    displayName: signedIn ? "測試使用者" : null,
    csrfToken: signedIn ? "browser-test-csrf" : null,
  });
  await page.route("**/api/v1/auth/session", (route) =>
    route.fulfill({
      contentType: "application/json",
      body: JSON.stringify(session()),
    }),
  );
  await page.route("**/api/v1/auth/windows", (route) => {
    signedIn = true;
    return route.fulfill({
      contentType: "application/json",
      body: JSON.stringify(session()),
    });
  });
  await page.goto("/chat");
  await expect(page).toHaveURL(/\/login\?returnUrl=%2Fchat$/);
  await expect(page.locator(".workbench")).toHaveCount(0);
  await page
    .getByRole("button", { name: "使用 Windows 身分登入", exact: true })
    .click();
  await expect(page).toHaveURL(/\/chat$/);
  await expect(
    page.getByRole("textbox", { name: "傳送訊息", exact: true }),
  ).toBeVisible();
});

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
    page.getByRole("button", { name: "使用 Windows 身分登入", exact: true }),
  ).toBeVisible();
  await expect(
    page.getByText("尚未取得 Windows 身分。", { exact: false }),
  ).toHaveCount(0);
  await expect(page.locator(".workbench")).toHaveCount(0);
  await expect(page.getByRole("textbox", { name: "傳送訊息" })).toHaveCount(0);
  expect(errors).toEqual([]);
});

for (const target of ["/", "/chat", "/projects", "/admin", "/quality"]) {
  test(`an unavailable session never exposes the workspace at ${target}`, async ({
    page,
  }) => {
    await page.route("**/api/v1/auth/session", (route) =>
      route.fulfill({
        status: 503,
        contentType: "application/problem+json",
        body: JSON.stringify({
          title: "Password=fixture-private; internal endpoint",
          code: "service_unavailable",
          issueCode: fixtureIssueCode,
        }),
      }),
    );
    await page.goto(target);
    await expect(page).toHaveURL(/\/login\?/);
    await expect(page.getByRole("alert")).toContainText(
      "操作未完成，請聯絡管理員。查證代碼：" + fixtureIssueCode,
    );
    await expect(page.getByRole("alert")).not.toContainText("fixture-private");
    await expect(
      page.getByRole("button", { name: "重新確認登入服務" }),
    ).toBeVisible();
    await expect(page.locator(".workbench")).toHaveCount(0);
  });
}

test("login focuses the account, reveals the password on request and warns about CapsLock", async ({
  page,
}) => {
  const fixture = new ApiFixture();
  fixture.ldap = true;
  await fixture.attach(page);
  await page.goto("/login");
  const account = page.getByLabel("AD 帳號", { exact: true });
  const password = page.getByLabel("AD 密碼", { exact: true });
  await expect(account).toBeFocused();
  await account.fill("alice");
  await password.fill("fixture-password");
  await expect(password).toHaveAttribute("type", "password");
  await page.getByRole("button", { name: "顯示密碼", exact: true }).click();
  await expect(password).toHaveAttribute("type", "text");
  await expect(
    page.getByRole("button", { name: "隱藏密碼", exact: true }),
  ).toHaveAttribute("aria-pressed", "true");
  await password.dispatchEvent("keydown", { key: "A", modifierCapsLock: true });
  await expect(password).toHaveAccessibleDescription("大寫鎖定已開啟");
  await password.dispatchEvent("keyup", { key: "a", modifierCapsLock: false });
  await expect(page.getByText("大寫鎖定已開啟")).toHaveCount(0);
  // A slow sign-in shows the loading state, then the button docks into its check.
  await page.route("**/api/v1/auth/login", async (route) => {
    await new Promise((resolve) => setTimeout(resolve, 400));
    await route.fallback();
  });
  await page.getByRole("button", { name: "登入工作區", exact: true }).click();
  await expect(password).toHaveAttribute("type", "password");
  await expect(page.locator(".login-submit")).toHaveAttribute(
    "data-phase",
    "loading",
  );
  await expect(page.getByRole("button", { name: "登入成功" })).toBeVisible();
  await expect(page).toHaveURL(/\/dashboard$/);
});

test("the Windows hint waits for a refused challenge and the mark plays once per tab", async ({
  page,
}) => {
  const core = new ApiFixture();
  await core.attach(page);
  await page.route("**/api/v1/auth/session", (route) =>
    route.fulfill({
      contentType: "application/json",
      body: JSON.stringify({
        mode: "Windows",
        authenticated: false,
        configured: true,
        account: null,
        displayName: null,
        csrfToken: "browser-test-csrf",
        methods: ["windows", "local"],
      }),
    }),
  );
  await page.route("**/api/v1/auth/windows", (route) =>
    route.fulfill({
      status: 401,
      contentType: "application/problem+json",
      body: JSON.stringify({ code: "authentication_required" }),
    }),
  );
  await page.goto("/login");
  await expect(
    page.getByRole("button", { name: "跳過標誌動畫", exact: true }),
  ).toBeVisible();
  const hint = page.getByText("尚未取得 Windows 身分。", { exact: false });
  await expect(hint).toHaveCount(0);
  await page
    .getByRole("button", { name: "使用 Windows 身分登入", exact: true })
    .click();
  await expect(hint).toBeVisible();
  await expect(page.getByRole("alert")).toHaveCount(0);
  await page.getByRole("button", { name: "本地帳號", exact: true }).click();
  await expect(hint).toHaveCount(0);
  await page.reload();
  await expect(page.locator(".login-intro")).toHaveClass(/intro-ready/);
  await expect(
    page.getByRole("button", { name: "重播標誌動畫", exact: true }),
  ).toBeVisible();
});
