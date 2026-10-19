import { test, expect, type Page } from "@playwright/test";
import {
  ApiFixture,
  expectCompactWorkspace,
  settleEntrance,
  toasts,
} from "./fixtures";

const features = [
  { id: "projects", name: "專案", route: "/work/projects" },
  { id: "knowledge", name: "知識庫", route: "/work/knowledge" },
  { id: "artifacts", name: "成果文件", route: "/work/artifacts" },
  { id: "jobs", name: "背景任務", route: "/work/jobs" },
  { id: "quality", name: "品質評測", route: "/admin/quality" },
  { id: "sharing", name: "分享", route: "/work/sharing" },
  { id: "integrations", name: "資料來源", route: "/admin/integrations" },
];
async function gallery(page: Page) {
  const fixture = new ApiFixture();
  fixture.adminAccess = true;
  fixture.extraFeatures = features;
  await fixture.attach(page);
  await page.goto("/admin/design");
  await expect(
    page.getByRole("region", { name: "色彩", exact: true }),
  ).toBeVisible();
  return fixture;
}

test("共用資料工作區在元件頁可操作，保留局部主題與焦點", async ({ page }) => {
  await gallery(page);
  await page.getByRole("button", { name: "深色", exact: true }).click();
  const button = page.getByRole("button", {
    name: "knowledge.index.completed",
    exact: true,
  });
  await button.click();
  const drawer = page.getByRole("dialog", {
    name: "資料詳情示範",
    exact: true,
  });
  await expect(drawer).toContainText("knowledge.index.completed");
  await expect(page.locator("nx-detail-drawer")).toHaveAttribute(
    "data-theme",
    "dark",
  );
  await drawer.getByRole("tab", { name: "屬性", exact: true }).click();
  await expect(drawer.getByRole("tabpanel")).toContainText("attempts");
  await drawer
    .getByRole("button", { name: "複製示範屬性 JSON", exact: true })
    .click();
  expect(
    await page.evaluate(
      () => (window as unknown as { __copied: string }).__copied,
    ),
  ).toContain("completed");
  await page.keyboard.press("Escape");
  // The exit keeps the drawer's display; its column layout must not fall back to a row.
  await expect(page.locator(".ui-detail-drawer")).toHaveCSS(
    "flex-direction",
    "column",
  );
  await expect(drawer).toBeHidden();
  await expect(button).toBeFocused();
});

test("共用密度、檢視鍵盤與選填日期保留桌面和手機可用性", async ({ page }) => {
  await gallery(page);
  await expectCompactWorkspace(page);
  await expect(page.locator(".design-reading")).toHaveCSS("font-size", "15px");
  const field = page.getByRole("textbox", {
    name: "示範資料篩選",
    exact: true,
  });
  await expect(field).toHaveCSS("font-size", "13px");
  expect((await field.boundingBox())!.height).toBe(34);
  const views = page.getByRole("group", { name: "資料範例檢視", exact: true });
  const all = views.getByRole("button", { name: "全部狀態", exact: true });
  const completed = views.getByRole("button", { name: "已完成", exact: true });
  await all.focus();
  await all.press("ArrowRight");
  await expect(completed).toBeFocused();
  await expect(completed).toHaveAttribute("aria-pressed", "true");
  await expect(
    page.locator(".design-data-workspace nx-data-table tbody tr"),
  ).toHaveCount(1);
  await completed.press("Home");
  await expect(all).toBeFocused();
  await expect(
    page.locator(".design-data-workspace nx-data-table tbody tr"),
  ).toHaveCount(3);
  // Row states: a click selects the row without a ring, closing returns focus to the row without
  // one either, and keyboard focus rings the row once (not its action as well).
  const rows = page.locator(".design-data-workspace nx-data-table tbody tr");
  const actions = rows.locator("[data-row-action]");
  await rows.nth(1).locator("td").first().click();
  await expect(rows.nth(1)).toHaveClass(/is-selected/);
  await page
    .getByRole("button", { name: "關閉資料詳情示範", exact: true })
    .click();
  await expect(rows.nth(1)).not.toHaveClass(/is-selected/);
  await expect(actions.nth(1)).toBeFocused();
  await expect(rows.nth(1)).toHaveCSS("outline-style", "none");
  await expect(actions.nth(1)).toHaveCSS("outline-style", "none");
  // Closing with Escape counts as the keyboard to the browser; a clicked row still gets no ring.
  await rows.nth(1).locator("td").first().click();
  await expect(rows.nth(1)).toHaveClass(/is-selected/);
  await page.keyboard.press("Escape");
  await expect(rows.nth(1)).not.toHaveClass(/is-selected/);
  await expect(actions.nth(1)).toBeFocused();
  await expect(rows.nth(1)).toHaveCSS("outline-style", "none");
  await actions.nth(1).press("ArrowDown");
  await expect(actions.nth(2)).toBeFocused();
  await expect(rows.nth(2)).toHaveCSS("outline-style", "solid");
  await expect(actions.nth(2)).toHaveCSS("outline-style", "none");
  // The last row meets the frame's rounded corners, so its ring takes the inner radius; a row
  // opened from the keyboard keeps its ring when focus comes back.
  await expect(rows.nth(2)).toHaveCSS("border-bottom-left-radius", "11px");
  await expect(rows.nth(0)).toHaveCSS("border-bottom-left-radius", "0px");
  await actions.nth(2).press("Enter");
  await expect(rows.nth(2)).toHaveClass(/is-selected/);
  await page.keyboard.press("Escape");
  await expect(actions.nth(2)).toBeFocused();
  await expect(rows.nth(2)).toHaveCSS("outline-style", "solid");
  // No gutter is kept for a scrollbar the rows do not need: header and row lines reach the frame.
  expect(
    await page
      .locator(".design-data-workspace .ui-table-scroll")
      .evaluate((el) => (el as HTMLElement).offsetWidth - el.clientWidth),
  ).toBe(0);
  const optional = page.getByRole("textbox", { name: "選填日期", exact: true });
  await optional.fill("2026/02/30");
  await optional.press("Tab");
  await expect(optional).toHaveAttribute("aria-invalid", "true");
  await optional.fill("");
  await optional.press("Tab");
  await expect(optional).not.toHaveAttribute("aria-invalid", "true");
  await expect(page.locator(".ui-date-error")).toHaveCount(0);
  await page.setViewportSize({ width: 390, height: 844 });
  await expectCompactWorkspace(page);
  // A stacked table turns its rows into cards instead of scrolling sideways.
  const example = page.locator(".design-data-workspace nx-data-table");
  await expect(example.locator("tbody tr").first()).toHaveCSS(
    "display",
    "grid",
  );
  await expect(example.locator('td[data-stack="end"]').first()).toHaveCSS(
    "grid-column-start",
    "2",
  );
  expect(
    await example
      .locator(".ui-table-scroll")
      .evaluate((el) => el.scrollWidth <= el.clientWidth),
  ).toBe(true);
  expect((await field.boundingBox())!.height).toBeGreaterThanOrEqual(44);
  await expect(field).toHaveCSS("font-size", "16px");
  await expect(page.locator(".design-reading")).toHaveCSS("font-size", "15px");
});

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
        ...Object.fromEntries(
          [...element.querySelectorAll("nx-count-badge:not(.is-dot)")].map(
            (badge, index) => {
              const colors = getComputedStyle(badge);
              return [
                "badge-" + index,
                ratio(colors.color, colors.backgroundColor),
              ];
            },
          ),
        ),
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
  }
  await page.getByRole("button", { name: "匯出目前 tokens" }).click();
  // The workbench index fills the sidebar, so “全部功能” starts collapsed and lists every
  // feature, pinned ones included, once opened.
  const nav = page.getByRole("navigation", { name: "工作區功能" });
  const launcher = nav.getByRole("button", { name: "全部功能", exact: true });
  await expect(launcher).toHaveCSS("box-shadow", "none");
  await launcher.click();
  // Open replaces the page's panel, so the toggle raises the selected chip.
  await expect(launcher).not.toHaveCSS("box-shadow", "none");
  // 管理員的存取管理與平台營運各拆成多頁（使用者、角色…、用量、價格、檢索），連結比拆分前多 5 個。
  await expect(nav.getByRole("link")).toHaveCount(20);
  await expect(
    nav.getByRole("link", { name: "檔案庫", exact: true }).last(),
  ).toBeVisible();
  await nav.getByRole("button", { name: "全部功能", exact: true }).click();
  const index = page.getByRole("navigation", { name: "元件索引", exact: true });
  // A short section followed by another short one stays current after the jump.
  await index.getByRole("link", { name: "間距與圓角", exact: true }).click();
  await page.evaluate(() => new Promise(requestAnimationFrame));
  await expect(index.locator('[aria-current="true"]')).toHaveText("間距與圓角");
  await index.getByRole("link", { name: "動態島", exact: true }).click();
  await expect(
    index.getByRole("link", { name: "動態島", exact: true }),
  ).toHaveAttribute("aria-current", "true");
  await expect(
    page.getByRole("heading", { name: "動態島", exact: true }),
  ).toBeFocused();
  await page.setViewportSize({ width: 390, height: 844 });
  await page.locator(".feature-main").evaluate((el) => (el.scrollTop = 0));
  await settleEntrance(page);
  expect(
    await page.evaluate(
      () => document.documentElement.scrollWidth <= innerWidth,
    ),
  ).toBe(true);
  for (const label of ["主要操作", "重設示範"]) {
    const size = await page
      .getByRole("button", { name: label, exact: true })
      .boundingBox();
    expect(size!.width).toBeGreaterThanOrEqual(44);
    expect(size!.height).toBeGreaterThanOrEqual(44);
  }
  await page.locator(".design-markdown").scrollIntoViewIfNeeded();
  await settleEntrance(page);
  expect(errors).toEqual([]);
});

test("component workbench needs its own grant", async ({ page }) => {
  const fixture = new ApiFixture();
  await fixture.attach(page);
  await page.goto("/admin/design");
  await expect(page.getByRole("alert")).toContainText("需要「介面元件」權限");
  await expect(page.locator(".design-preview")).toHaveCount(0);
});

test("island follows the preview theme instead of a fixed dark zone", async ({
  page,
}) => {
  await gallery(page);
  await page.getByRole("button", { name: "生成狀態", exact: true }).click();
  const island = page.locator("#design-island nx-island .island");
  await expect(island).toBeVisible();
  const lightness = () =>
    island.evaluate((element) => {
      // color-mix() may serialise as color(srgb r g b / a) with 0–1 channels.
      const color = getComputedStyle(element).backgroundColor;
      const scale = color.startsWith("color(") ? 255 : 1;
      const [r, g, b] = color.match(/[\d.]+/g)!.map(Number);
      return ((r + g + b) / 3) * scale;
    });
  expect(await lightness()).toBeGreaterThan(200);
  await page.getByRole("button", { name: "深色", exact: true }).click();
  await expect.poll(lightness).toBeLessThan(80);
});

test("missed toasts stay in the notification center's session list, opened and left by keyboard", async ({
  page,
}) => {
  await gallery(page);
  await page.getByRole("button", { name: "附帶復原", exact: true }).click();
  await page.getByRole("button", { name: "成功", exact: true }).click();
  await expect(toasts(page)).toHaveCount(2);
  // Cleared before anyone read them: the stack empties, the session record stays.
  await page.getByRole("button", { name: "全部清除", exact: true }).click();
  await expect(toasts(page)).toHaveCount(0);
  const sample = page.locator("#design-toasts nx-toast-history li");
  await expect(sample).toHaveCount(2);
  await expect(sample.first()).toContainText("變更已儲存");

  const bell = page.getByRole("button", { name: "通知", exact: true });
  await bell.focus();
  await page.keyboard.press("Enter");
  const dialog = page.getByRole("dialog", { name: "通知", exact: true });
  await expect(dialog).toBeVisible();
  await expect(
    dialog.getByRole("tab", { name: "通知匣", exact: true }),
  ).toHaveAttribute("aria-selected", "true");
  const session = dialog.getByRole("tab", {
    name: "本次工作階段，2 則",
    exact: true,
  });
  await expect(session.locator("nx-count-badge")).toHaveText("2");
  await page.keyboard.press("Tab");
  await page.keyboard.press("ArrowRight");
  await expect(session).toBeFocused();
  await expect(session).toHaveAttribute("aria-selected", "true");
  const panel = dialog.getByRole("tabpanel", { name: /^本次工作階段/ });
  const rows = panel.locator("li");
  await expect(rows).toHaveCount(2);
  await expect(rows.nth(1)).toContainText("已封存 3 則示範對話");
  await expect(rows.first().locator("time")).toHaveText(/^\d{2}:\d{2}$/);

  await page.keyboard.press("Tab");
  const undo = panel.getByRole("button", {
    name: "復原：已封存 3 則示範對話",
    exact: true,
  });
  await expect(undo).toBeFocused();
  await page.keyboard.press("Enter");
  await expect(dialog).not.toBeVisible();
  await expect(bell).toBeFocused();
  await expect(toasts(page).filter({ hasText: "已復原封存" })).toBeVisible();

  // The action ran once; reopening returns to the same tab without it.
  await page.keyboard.press("Enter");
  await expect(dialog).toBeVisible();
  await expect(
    dialog.getByRole("tab", { name: "本次工作階段，3 則", exact: true }),
  ).toHaveAttribute("aria-selected", "true");
  await expect(rows).toHaveCount(3);
  await expect(rows.first()).toContainText("已復原封存");
  await expect(panel.getByRole("button", { name: /^復原/ })).toHaveCount(0);
  await page.keyboard.press("Escape");
  await expect(dialog).not.toBeVisible();
  await expect(bell).toBeFocused();
});

test("toasts stack, fan out, update in place and leave by keyboard or swipe", async ({
  page,
}) => {
  await gallery(page);
  const stack = toasts(page);
  const gap = async (a: string, b: string) =>
    (await stack.filter({ hasText: b }).boundingBox())!.y -
    (await stack.filter({ hasText: a }).boundingBox())!.y;
  await page.getByRole("button", { name: "連續三則", exact: true }).click();
  await expect(stack).toHaveCount(3);
  await expect(stack.first()).toContainText("知識庫已儲存");
  // Collapsed, the older toast peeks above the front one; hovering fans them apart.
  await expect
    .poll(() => gap("已安排重新索引", "知識庫已儲存"))
    .toBeLessThan(20);
  await stack.first().hover();
  await expect
    .poll(() => gap("已安排重新索引", "知識庫已儲存"))
    .toBeGreaterThan(30);
  await stack
    .first()
    .getByRole("button", { name: "關閉通知", exact: true })
    .focus();
  await page.keyboard.press("Escape");
  await expect(stack.filter({ hasText: "知識庫已儲存" })).toHaveCount(0);
  await expect(
    stack
      .filter({ hasText: "已安排重新索引" })
      .getByRole("button", { name: "關閉通知", exact: true }),
  ).toBeFocused();
  await page.getByRole("button", { name: "全部清除", exact: true }).click();
  await expect(stack).toHaveCount(0);

  await page.getByRole("button", { name: "上傳到完成", exact: true }).click();
  const upload = stack.filter({ hasText: /上傳/ });
  await expect(upload).toContainText("正在上傳");
  await expect(upload).toContainText("文件已上傳");
  await expect(upload).toHaveCount(1);
  await expect(page.locator("nx-toaster [aria-live='polite']")).toContainText(
    "文件已上傳",
  );
  await page.mouse.move(0, 0);
  const box = (await upload.boundingBox())!;
  await page.mouse.move(box.x + 24, box.y + box.height / 2);
  await page.mouse.down();
  await page.mouse.move(box.x + 160, box.y + box.height / 2, { steps: 6 });
  await page.mouse.up();
  await expect(upload).toHaveCount(0);

  // A toast raised while a modal is open stays above it.
  await page.getByRole("button", { name: "命名對話框", exact: true }).click();
  const dialog = page.getByRole("dialog", { name: "重新命名示範" });
  await dialog.getByRole("textbox").fill("新的示範名稱");
  await dialog.getByRole("button", { name: "儲存名稱", exact: true }).click();
  await expect(dialog).not.toBeVisible();
  await expect(stack.filter({ hasText: "名稱已更新" })).toBeVisible();
  await expect(
    page.getByRole("heading", { name: "新的示範名稱", exact: true }),
  ).toBeVisible();
});
