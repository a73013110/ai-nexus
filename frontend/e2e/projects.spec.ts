import { test, expect } from "@playwright/test";
import { randomUUID } from "node:crypto";
import type {
  ProjectDto,
  ProjectTemplateDto,
} from "../src/app/core/api/schema";
import { expectCompactWorkspace, ApiFixture, settleEntrance } from "./fixtures";

test("project settings, shared templates and private conversations work directly on desktop and mobile", async ({
  page,
}) => {
  const core = new ApiFixture();
  core.extraFeatures.push({ id: "projects", name: "專案", route: "/projects" });
  const projects: ProjectDto[] = [],
    templates: ProjectTemplateDto[] = [];
  await core.attach(page);
  await page.route("**/api/v1/projects**", async (route) => {
    const url = new URL(route.request().url()),
      path = url.pathname,
      method = route.request().method();
    const json = (body: unknown) =>
      route.fulfill({
        contentType: "application/json",
        body: JSON.stringify(body),
      });
    if (path.endsWith("/projects")) {
      if (method === "POST") {
        const body = route.request().postDataJSON();
        const p: ProjectDto = {
          resource: {
            id: randomUUID(),
            name: body.name,
            kind: "project",
            isOwner: true,
            canEdit: true,
            updatedAt: new Date().toISOString(),
          },
          description: body.description,
          instructions: body.instructions,
          version: 1,
          isArchived: false,
        };
        projects.push(p);
        return json(p);
      }
      return json(projects);
    }
    if (path.endsWith("/files") || path.endsWith("/conversations"))
      return json([]);
    if (path.endsWith("/templates")) {
      if (method === "POST") {
        const body = route.request().postDataJSON();
        const t = {
          id: randomUUID(),
          title: body.title,
          content: body.content,
        };
        templates.push(t);
        return json(t);
      }
      return json(templates);
    }
    if (method === "PUT") {
      Object.assign(projects[0], route.request().postDataJSON(), {
        version: projects[0].version + 1,
      });
      return json(projects[0]);
    }
    return json(projects[0]);
  });
  await page.goto("/projects");
  await page.getByRole("button", { name: "新增專案", exact: true }).click();
  const dialog = page.getByRole("dialog");
  await dialog.getByRole("textbox", { name: "項目名稱" }).fill("公司文件研究");
  await dialog
    .getByRole("textbox", { name: "專案說明" })
    .fill("共用背景与參考文件");
  await dialog
    .getByRole("textbox", { name: "專案共用指示" })
    .fill("先提供結論，再提供依據。");
  await dialog.getByRole("button", { name: "儲存", exact: true }).click();
  await expect(
    page.getByRole("heading", { name: "公司文件研究" }),
  ).toBeVisible();
  await page.getByRole("button", { name: "新增範本", exact: true }).click();
  await dialog.getByRole("textbox", { name: "項目名稱" }).fill("公文重點摘要");
  await dialog
    .getByRole("textbox", { name: "範本提問內容" })
    .fill("請列出本公文的重點、期限與待辦事項。");
  await dialog.getByRole("button", { name: "儲存", exact: true }).click();
  await expect(
    page.getByRole("button", { name: /公文重點摘要.*使用範本開始對話/ }),
  ).toBeVisible();
  await expect(page.getByText("這裡顯示你的提問紀錄。")).toBeVisible();
  await settleEntrance(page);
  await expectCompactWorkspace(page);
  await page.setViewportSize({ width: 375, height: 812 });
  expect(
    await page.evaluate(
      () => document.documentElement.scrollWidth <= innerWidth,
    ),
  ).toBe(true);
  await page.getByRole("button", { name: "封存", exact: true }).click();
  await expect(page.getByRole("main").getByRole("status")).toContainText(
    "專案已封存",
  );
  await expect(
    page.getByRole("button", { name: "開始對話", exact: true }),
  ).toBeDisabled();
});

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
