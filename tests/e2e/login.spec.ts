import { test, expect } from "@playwright/test";
import { ApiFixture } from "./fixtures";

test("LDAP login rejects passwords, clears secrets and returns to chat", async ({
  page,
}) => {
  const fixture = new ApiFixture();
  fixture.ldap = true;
  await fixture.attach(page);
  await page.goto("/chat");
  await expect(page.getByRole("heading", { name: "登入工作台" })).toBeVisible();
  const submit = page.getByRole("button", { name: "登入工作台", exact: true });
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
  await page.locator(".profile-menu summary").click();
  await page.getByRole("button", { name: "登出工作台" }).click();
  await expect(page.getByRole("heading", { name: "登入工作台" })).toBeVisible();
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
  await expect(page.getByRole("heading", { name: "登入工作台" })).toBeVisible();
  expect(
    await page.evaluate(
      () => document.documentElement.scrollWidth <= innerWidth,
    ),
  ).toBe(true);
  await page.screenshot({
    path: "artifacts/screenshots/login-mobile.png",
    fullPage: true,
  });
  await page.getByLabel("AD 帳號", { exact: true }).fill("alice");
  await page.getByLabel("AD 密碼", { exact: true }).fill("fixture-password");
  await page.getByRole("button", { name: "登入工作台", exact: true }).click();
  await expect(page).toHaveURL(/\/chat$/);
});

test("desktop login keeps the workspace visual language", async ({ page }) => {
  const fixture = new ApiFixture();
  fixture.ldap = true;
  await fixture.attach(page);
  await page.goto("/login");
  await expect(page.getByRole("heading", { name: "登入工作台" })).toBeVisible();
  await expect(page.locator(".login-workspace")).toHaveCSS("display", "grid");
  await page.screenshot({
    path: "artifacts/screenshots/login-desktop.png",
    fullPage: true,
  });
});
