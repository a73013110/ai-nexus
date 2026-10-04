import { test, expect } from "@playwright/test";
import { randomUUID } from "node:crypto";
import { ApiFixture, settleEntrance } from "./fixtures";
import type {
  Project,
  ProjectTemplate,
} from "../../frontend/src/app/core/api/types";

test("project settings, shared templates and private conversations work directly on desktop and mobile", async ({
  page,
}) => {
  const core = new ApiFixture();
  core.extraFeatures.push({ id: "projects", name: "專案", route: "/projects" });
  const projects: Project[] = [],
    templates: ProjectTemplate[] = [];
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
        const p: Project = {
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
  await page.screenshot({
    path: "artifacts/screenshots/projects-desktop.png",
    fullPage: true,
  });
  await page.setViewportSize({ width: 375, height: 812 });
  expect(
    await page.evaluate(
      () => document.documentElement.scrollWidth <= innerWidth,
    ),
  ).toBe(true);
  await page.getByRole("button", { name: "封存", exact: true }).click();
  await expect(page.getByRole("status")).toContainText("專案已封存");
  await expect(
    page.getByRole("button", { name: "開始對話", exact: true }),
  ).toBeDisabled();
});
