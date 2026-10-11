import { test, expect } from "@playwright/test";
import {
  ApiFixture,
  expectViewportContained,
  fixtureIssueCode,
} from "./fixtures";

test("global problem notices are compact, preserve safe copy and release viewport height", async ({
  page,
}) => {
  const fixture = new ApiFixture();
  await fixture.attach(page);
  await page.route("**/api/client-issues", (route) =>
    route.fulfill({ status: 503, body: "{}" }),
  );
  await page.goto("/chat");
  await page.evaluate(() => {
    setTimeout(() => {
      throw new Error("private diagnostic details must stay hidden");
    });
  });
  const notice = page.locator("nx-unhandled-issue nx-notice");
  await expect(notice).toBeVisible();
  await expect(notice).toHaveAttribute("role", "alert");
  await expect(notice).not.toContainText("private diagnostic");
  expect((await notice.boundingBox())!.height).toBeLessThanOrEqual(40);
  await notice
    .getByRole("button", { name: "複製問題查證代碼", exact: true })
    .click();
  await expect
    .poll(() =>
      page.evaluate(() => (window as unknown as { __copied: string }).__copied),
    )
    .toMatch(/^LOCAL-[A-F0-9]{16}$/);
  await page.setViewportSize({ width: 375, height: 812 });
  await expectViewportContained(page);
  expect((await notice.boundingBox())!.height).toBeLessThan(150);
  await notice.getByRole("button", { name: "關閉提示", exact: true }).click();
  await expect(notice).toHaveCount(0);
  await expect
    .poll(() =>
      page.evaluate(() =>
        document.documentElement.style.getPropertyValue(
          "--global-issue-height",
        ),
      ),
    )
    .toBe("");
  expect((await page.locator(".workbench").boundingBox())!.height).toBe(812);
});

test("unhandled client issues show a safe copyable code without overflowing the workspace", async ({
  page,
}) => {
  const fixture = new ApiFixture();
  await fixture.attach(page);
  let reports = 0;
  await page.route("**/api/v1/client-issues", (route) => {
    reports++;
    const payload = route.request().postDataJSON();
    expect(Object.keys(payload).sort()).toEqual(["fingerprint", "kind"]);
    expect(JSON.stringify(payload)).not.toContain("fixture-private");
    return route.fulfill({
      json: { accepted: true, issueCode: fixtureIssueCode },
    });
  });
  await page.goto("/chat");
  await expect(page.getByRole("textbox", { name: "傳送訊息" })).toBeVisible();
  await page.evaluate(() =>
    window.dispatchEvent(
      new ErrorEvent("error", {
        error: new TypeError("Password=fixture-private"),
        message: "fixture-private",
      }),
    ),
  );
  const banner = page.locator("nx-unhandled-issue");
  await expect(banner).toContainText(fixtureIssueCode);
  await expect(banner).not.toContainText("fixture-private");
  await expect(
    banner.getByRole("button", { name: "複製問題查證代碼" }),
  ).toBeVisible();
  await expectViewportContained(page);
  await page.setViewportSize({ width: 375, height: 812 });
  await expectViewportContained(page);
  await banner.getByRole("button", { name: "關閉提示" }).click();
  await expect(banner.getByRole("alert")).toHaveCount(0);
  await expectViewportContained(page);
  expect(reports).toBe(1);
});
