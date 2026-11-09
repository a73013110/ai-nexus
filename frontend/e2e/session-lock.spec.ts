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
