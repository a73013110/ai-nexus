import { test, expect } from "@playwright/test";
import { randomUUID } from "node:crypto";
import { ApiFixture } from "./fixtures";

for (const kind of ["project", "evaluation"] as const) {
  test(`${kind} deletion requires confirmation, retains the page on a conflict and returns to the list on success`, async ({
    page,
  }) => {
    const core = new ApiFixture();
    const isProject = kind === "project";
    const section = isProject ? "projects" : "quality";
    const endpoint = isProject ? "projects" : "quality/sets";
    const title = isProject ? "保留私人資料的專案" : "保留歷史的評測題庫";
    const action = isProject ? "刪除專案" : "刪除評測集";
    core.extraFeatures.push({
      id: section,
      name: section,
      route: `/${section}`,
    });
    const resource = {
      id: randomUUID(),
      name: title,
      kind,
      canEdit: true,
      isOwner: true,
      updatedAt: new Date().toISOString(),
    };
    const value = isProject
      ? {
          resource,
          description: "",
          instructions: "",
          version: 1,
          isArchived: false,
        }
      : {
          resource,
          description: "",
          version: 1,
          cases: [
            {
              question: "測試問題",
              reference: "",
              requiredTerms: [],
              forbiddenTerms: [],
            },
          ],
        };
    let deleted = false,
      blocked = true,
      deleteCalls = 0;
    await core.attach(page);
    await page.route(`**/api/v1/${section}**`, async (route) => {
      const path = new URL(route.request().url()).pathname;
      if (route.request().method() === "DELETE") {
        ++deleteCalls;
        if (blocked)
          return route.fulfill({
            status: 409,
            contentType: "application/problem+json",
            body: JSON.stringify({
              title: "此項目仍有背景任務，請先完成或取消，再刪除。",
              code: "resource_tasks_active",
            }),
          });
        deleted = true;
        return route.fulfill({ status: 204 });
      }
      const body =
        path === `/api/v1/${endpoint}`
          ? deleted
            ? []
            : [value]
          : path === `/api/v1/${endpoint}/${resource.id}`
            ? value
            : [];
      return route.fulfill({
        contentType: "application/json",
        body: JSON.stringify(body),
      });
    });
    await page.goto(`/${section}/${resource.id}`);
    await expect(page.getByRole("heading", { name: title })).toBeVisible();
    await page.getByRole("button", { name: action, exact: true }).click();
    const confirm = page.getByRole("dialog", { name: `刪除「${title}」？` });
    await expect(confirm).toContainText(isProject ? "保留" : "歷史結果");
    await confirm.getByRole("button", { name: "取消", exact: true }).click();
    expect(deleteCalls).toBe(0);
    await page.getByRole("button", { name: action, exact: true }).click();
    await confirm.getByRole("button", { name: action, exact: true }).click();
    await expect(page.getByRole("alert")).toContainText("仍有背景任務");
    await expect(page.getByRole("heading", { name: title })).toBeVisible();
    blocked = false;
    await page.getByRole("button", { name: action, exact: true }).click();
    await confirm.getByRole("button", { name: action, exact: true }).click();
    await expect(page).toHaveURL(new RegExp(`/${section}$`));
    await expect(page.getByRole("heading", { name: title })).toHaveCount(0);
    expect(deleteCalls).toBe(2);
  });
}

test("retained project originals are discoverable through the library and explicit deletion is confirmed", async ({
  page,
}) => {
  const core = new ApiFixture();
  core.extraFeatures.push({ id: "projects", name: "專案", route: "/projects" });
  const id = randomUUID();
  core.attachments.push({
    id,
    fileName: "保留的參考文件.txt",
    contentType: "text/plain",
    size: 100,
    isImage: false,
    analysisMode: "extracted-text",
  });
  core.retainedFiles.add(id);
  await core.attach(page);
  await page.route("**/api/v1/projects", (route) =>
    route.fulfill({ contentType: "application/json", body: "[]" }),
  );
  await page.goto("/projects");
  await page.getByRole("link", { name: "檔案庫", exact: true }).last().click();
  await expect(page.locator(".file-card")).toContainText("保留的參考文件.txt");
  const search = page.getByRole("searchbox", {
    name: "搜尋檔案庫",
    exact: true,
  });
  await search.fill("不符合的文字");
  await expect(
    page.getByRole("heading", { name: "沒有符合的檔案" }),
  ).toBeVisible();
  await search.fill("參考");
  await expect(page.locator(".file-card")).toBeVisible();
  await page
    .getByRole("button", { name: "刪除 保留的參考文件.txt", exact: true })
    .click();
  const confirm = page.getByRole("dialog", { name: "刪除檔案", exact: true });
  await expect(confirm).toBeVisible();
  await page.keyboard.press("Escape");
  expect(core.retainedFiles.has(id)).toBe(true);
  await page
    .getByRole("button", { name: "刪除 保留的參考文件.txt", exact: true })
    .click();
  await confirm.getByRole("button", { name: "刪除檔案", exact: true }).click();
  await expect(page.locator(".file-card")).toHaveCount(0);
  expect(core.retainedFiles.has(id)).toBe(false);
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
