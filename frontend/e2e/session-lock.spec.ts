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
  // 小鎖掛在島下打哈欠。
  const hanging = island.locator("nx-lock-pet-art");
  await expect(hanging).toBeVisible();
  await expect(hanging).toHaveAttribute("data-mood", "yawn");

  await island.getByRole("button", { name: "立即鎖定" }).click();
  const lock = page.getByRole("dialog", { name: "工作區已鎖定" });
  await expect(lock).toBeVisible();
  await expect(island).toBeHidden();
  // 島撐開成卡片時，小鎖從島上掉下來站到卡片頂緣。
  await expect
    .poll(async () => {
      const [cardBox, petBox] = await Promise.all([
        lock.locator(".lock-wrap").boundingBox(),
        lock.locator(".pet").boundingBox(),
      ]);
      return Math.round(petBox!.y + petBox!.height - cardBox!.y);
    })
    .toBe(0);
});

test("the lock pet plays on the card without getting in the way of the password", async ({
  page,
}) => {
  const fixture = new ApiFixture();
  fixture.lockAfterSeconds = 1800;
  await fixture.attach(page);
  await page.goto("/work/chat");
  await page.getByRole("button", { name: "立即鎖定", exact: true }).click();
  const lock = page.getByRole("dialog", { name: "工作區已鎖定" });
  const pet = lock.locator(".pet");
  const card = lock.locator(".lock-wrap");

  // 卡片撐開後才落下，站在頂緣上、不蓋住卡片內容。
  await expect
    .poll(async () => {
      const [cardBox, petBox] = await Promise.all([
        card.boundingBox(),
        pet.boundingBox(),
      ]);
      return Math.round(petBox!.y + petBox!.height - cardBox!.y);
    })
    .toBe(0);

  const field = lock.getByLabel("AD 密碼", { exact: true });
  await field.fill("密碼還沒打完");
  await expect(pet).toHaveAttribute("data-mood", "shy");

  // 拎起來往右上甩：落地報距離，焦點一直留在密碼欄。
  const box = (await pet.boundingBox())!;
  await page.mouse.move(box.x + box.width / 2, box.y + box.height / 2);
  await page.mouse.down();
  await page.mouse.move(box.x + 160, box.y - 40, { steps: 3 });
  await page.mouse.up();
  await expect(field).toBeFocused();
  await expect(lock.locator(".pet-say")).toContainText("公尺");

  // 顯示密碼時轉身背對（摔到地上的會先跳回卡片）。
  await lock.getByRole("button", { name: "顯示密碼", exact: true }).click();
  await expect(pet).toHaveAttribute("data-mood", "turned");

  await field.fill("fixture-password");
  await lock.getByRole("button", { name: "解鎖", exact: true }).click();
  await expect(lock).toBeHidden();
  await expect(pet).toHaveCount(0);
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
