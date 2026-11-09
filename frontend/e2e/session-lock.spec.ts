import { test, expect } from "@playwright/test";
import { ApiFixture } from "./fixtures";

test("an idle session locks in place and returns to the same page after the password", async ({
  page,
}) => {
  const fixture = new ApiFixture();
  fixture.lockAfterSeconds = 1;
  await fixture.attach(page);
  await page.goto("/work/chat");
  const draft = page.getByRole("textbox", { name: "傳送訊息", exact: true });
  await draft.fill("尚未送出的草稿");
  fixture.locked = true;

  const lock = page.getByRole("dialog", { name: "工作區已鎖定" });
  await expect(lock).toBeVisible();
  await expect(lock).toContainText("閒置超過 30 分鐘。");
  await page.keyboard.press("Escape");
  await expect(lock).toBeVisible();

  await lock.getByLabel("AD 密碼", { exact: true }).fill("wrong");
  await lock.getByRole("button", { name: "解鎖", exact: true }).click();
  await expect(lock).toContainText("密碼不正確");
  await expect(page).toHaveURL(/\/work\/chat/);
  // 動效階段結束後要讓位，輸錯密碼的抖動才看得到。
  await expect
    .poll(() =>
      lock
        .locator(".lock-wrap")
        .evaluate((element) => getComputedStyle(element).animationName),
    )
    .toBe("login-shake-a");

  await lock.getByLabel("AD 密碼", { exact: true }).fill("fixture-password");
  await lock.getByRole("button", { name: "解鎖", exact: true }).click();
  await expect(lock).toBeHidden();
  await expect(page).toHaveURL(/\/work\/chat/);
  await expect(draft).toHaveValue("尚未送出的草稿");
});

test("the account row shows the countdown and locks by hand", async ({
  page,
}) => {
  const fixture = new ApiFixture();
  fixture.lockAfterSeconds = 1800;
  await fixture.attach(page);
  await page.goto("/work/chat");

  await expect(page.locator(".profile-detail")).toHaveText(/\d+:\d{2} 後鎖定/);
  await page.getByRole("button", { name: "立即鎖定", exact: true }).click();
  const lock = page.getByRole("dialog", { name: "工作區已鎖定" });
  await expect(lock).toBeVisible();
  await expect(lock).toContainText("你鎖定了工作區。");
  await expect
    .poll(() =>
      lock
        .locator(".lock-ground")
        .evaluate((element) => getComputedStyle(element).backdropFilter),
    )
    .toContain("blur");

  await lock.getByRole("button", { name: "解鎖", exact: true }).click();
  await expect(lock).toContainText("請輸入密碼。");
  await lock.getByLabel("AD 密碼", { exact: true }).fill("fixture-password");
  await lock.getByRole("button", { name: "解鎖", exact: true }).click();
  await expect(lock).toBeHidden();
});

test("the top island warns near the deadline and hands over to the lock", async ({
  page,
}) => {
  const fixture = new ApiFixture();
  fixture.lockAfterSeconds = 18;
  await fixture.attach(page);
  await page.goto("/work/chat");

  const island = page.getByRole("group", { name: "工作區即將鎖定" });
  await expect(island).toBeVisible();
  await expect(island).toContainText("後鎖定");
  await expect(island.getByRole("button", { name: "繼續使用" })).toBeVisible();

  await island.getByRole("button", { name: "立即鎖定" }).click();
  await expect(
    page.getByRole("dialog", { name: "工作區已鎖定" }),
  ).toBeVisible();
  await expect(island).toBeHidden();
});

test("a page reloaded while locked waits behind the lock and loads after the password", async ({
  page,
}) => {
  const fixture = new ApiFixture();
  fixture.lockAfterSeconds = 1;
  fixture.locked = true;
  await fixture.attach(page);
  await page.goto("/work/chat");

  const lock = page.getByRole("dialog", { name: "工作區已鎖定" });
  await expect(lock).toBeVisible();
  await lock.getByLabel("AD 密碼", { exact: true }).fill("fixture-password");
  await lock.getByRole("button", { name: "解鎖", exact: true }).click();
  await expect(lock).toBeHidden();
  await expect(
    page.getByRole("textbox", { name: "傳送訊息", exact: true }),
  ).toBeVisible();
});
