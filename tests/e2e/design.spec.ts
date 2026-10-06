import { test, expect, type Page } from "@playwright/test";
import { ApiFixture, settleEntrance } from "./fixtures";

const features = [
  { id: "projects", name: "專案", route: "/projects" },
  { id: "knowledge", name: "知識庫", route: "/knowledge" },
  { id: "artifacts", name: "成果文件", route: "/artifacts" },
  { id: "tasks", name: "背景任務", route: "/tasks" },
  { id: "quality", name: "品質評測", route: "/quality" },
  { id: "shared", name: "分享", route: "/shared" },
  { id: "integrations", name: "資料來源", route: "/integrations" },
];
async function gallery(page: Page) {
  const fixture = new ApiFixture();
  fixture.adminAccess = true;
  fixture.extraFeatures = features;
  await fixture.attach(page);
  await page.goto("/design");
  await expect(
    page.getByRole("heading", { name: "從 tokens 到每一個細節" }),
  ).toBeVisible();
  return fixture;
}

test("production controls support keyboard selection, disabled options and dialog focus return", async ({
  page,
}) => {
  const fixture = await gallery(page);
  const select = page.getByRole("combobox", { name: "示範閱讀模式" });
  await select.focus();
  await select.press("ArrowDown");
  await select.press("Enter");
  await expect(select).toHaveText("寬幅閱讀");
  await select.press("ArrowDown");
  await select.press("End");
  await expect(select).toHaveAttribute("aria-activedescendant", /-1$/);
  await select.press("Escape");
  await expect(select).toBeFocused();
  await expect(select).toHaveAttribute("aria-expanded", "false");
  const menu = page.getByRole("button", { name: "示範更多操作" });
  await menu.focus();
  await menu.press("ArrowDown");
  await expect(
    page.getByRole("menuitem", { name: "修改示範標題" }),
  ).toBeFocused();
  await page.keyboard.press("ArrowDown");
  await expect(page.getByRole("menuitem", { name: "重設示範" })).toBeFocused();
  await page.keyboard.press("Escape");
  await expect(menu).toBeFocused();
  const reset = page.getByRole("button", { name: "重設示範", exact: true });
  await reset.click();
  const dialog = page.getByRole("dialog", { name: "重設介面示範？" });
  await expect(
    dialog.getByRole("button", { name: "取消", exact: true }),
  ).toBeFocused();
  await page.keyboard.press("Escape");
  await expect(dialog).not.toBeVisible();
  await expect(reset).toBeFocused();
  await page
    .getByRole("heading", { name: "把想法，整理成可用的成果" })
    .press("F2");
  const input = page.getByRole("textbox", { name: "目前對話名稱" });
  await input.fill("直接修改就很方便");
  await input.press("Enter");
  await expect(
    page.getByRole("heading", { name: "直接修改就很方便" }),
  ).toBeVisible();
  await page.emulateMedia({ reducedMotion: "reduce" });
  await page.getByRole("button", { name: "播放狀態示範" }).click();
  expect(
    await page
      .locator(".signal-flow")
      .first()
      .evaluate((el) => getComputedStyle(el).animationName),
  ).toBe("none");
  await expect(page.locator("nx-job-progress")).toContainText("處理完成");
  expect(fixture.posts).toBe(0);
});

test("theme previews have readable contrast, full navigation and usable narrow layouts", async ({
  page,
}) => {
  const errors: string[] = [];
  page.on("pageerror", (e) => errors.push(e.message));
  await gallery(page);
  const contrast = () =>
    page.locator(".design-preview").evaluate((element) => {
      const style = getComputedStyle(element);
      const luminance = (color: string) => {
        const rgb = color
          .match(/[\d.]+/g)!
          .slice(0, 3)
          .map(Number)
          .map((x) => x / 255)
          .map((x) =>
            x <= 0.04045 ? x / 12.92 : ((x + 0.055) / 1.055) ** 2.4,
          );
        return 0.2126 * rgb[0] + 0.7152 * rgb[1] + 0.0722 * rgb[2];
      };
      const ratio = (a: string, b: string) => {
        const [light, dark] = [luminance(a), luminance(b)].sort(
          (x, y) => y - x,
        );
        return (light + 0.05) / (dark + 0.05);
      };
      const resolved = (name: string) => {
        const el = document.createElement("span");
        el.style.color = `var(${name})`;
        element.append(el);
        const value = getComputedStyle(el).color;
        el.remove();
        return value;
      };
      return {
        ink: ratio(resolved("--ink"), resolved("--canvas")),
        secondary: ratio(resolved("--secondary"), resolved("--surface")),
        muted: ratio(resolved("--muted"), resolved("--canvas")),
        primary: ratio(resolved("--accent-text"), resolved("--accent")),
        danger: ratio(resolved("--danger-text"), resolved("--danger-surface")),
        caption: parseFloat(style.getPropertyValue("--text-caption")),
      };
    });
  for (const theme of ["淺色", "深色"]) {
    await page.getByRole("button", { name: theme, exact: true }).click();
    for (const [name, value] of Object.entries(await contrast()))
      if (name !== "caption")
        expect(value, `${theme}: ${name}`).toBeGreaterThanOrEqual(4.5);
    await page.locator(".feature-main").evaluate((el) => (el.scrollTop = 0));
    await settleEntrance(page);
    await page.screenshot({
      path: `artifacts/screenshots/design-${theme === "淺色" ? "light" : "dark"}.png`,
      fullPage: true,
    });
  }
  await page.getByRole("button", { name: "匯出目前 tokens" }).click();
  const nav = page.getByRole("navigation", { name: "工作區功能" });
  await expect(nav.getByRole("link")).toHaveCount(10);
  await expect(
    nav.getByRole("link", { name: "檔案庫", exact: true }),
  ).toBeVisible();
  await page.setViewportSize({ width: 390, height: 844 });
  await page.locator(".feature-main").evaluate((el) => (el.scrollTop = 0));
  await settleEntrance(page);
  expect(
    await page.evaluate(
      () => document.documentElement.scrollWidth <= innerWidth,
    ),
  ).toBe(true);
  await page.screenshot({
    path: "artifacts/screenshots/design-mobile.png",
    fullPage: true,
  });
  for (const label of ["主要操作", "重設示範"]) {
    const size = await page
      .getByRole("button", { name: label, exact: true })
      .boundingBox();
    expect(size!.width).toBeGreaterThanOrEqual(44);
    expect(size!.height).toBeGreaterThanOrEqual(44);
  }
  await page.locator(".design-markdown").scrollIntoViewIfNeeded();
  await settleEntrance(page);
  await page.screenshot({
    path: "artifacts/screenshots/design-reading-mobile.png",
    fullPage: true,
  });
  expect(errors).toEqual([]);
});

test("component workbench is scoped to administrators", async ({ page }) => {
  const fixture = new ApiFixture();
  await fixture.attach(page);
  await page.goto("/design");
  await expect(page.getByRole("alert")).toContainText("僅提供平台管理員");
  await expect(page.locator(".design-preview")).toHaveCount(0);
});
