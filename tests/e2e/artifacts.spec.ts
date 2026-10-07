import { test, expect } from "@playwright/test";
import { randomUUID } from "node:crypto";
import {
  ApiFixture,
  richAnswer,
  chooseSelect,
  settleEntrance,
} from "./fixtures";
import type { ArtifactDocument } from "../../frontend/src/app/core/api/types";

test("artifact versions preserve edits, restore older content and export a saved revision", async ({
  page,
}) => {
  const core = new ApiFixture();
  core.extraFeatures.push({
    id: "artifacts",
    name: "成果文件",
    route: "/artifacts",
  });
  const id = randomUUID();
  const versions: ArtifactDocument[] = [
    {
      resource: {
        id,
        kind: "artifact",
        name: "工作成果",
        canEdit: true,
        isOwner: true,
        updatedAt: new Date().toISOString(),
      },
      version: 1,
      currentVersion: 1,
      content: richAnswer,
      sourceMessageId: null,
      projectId: null,
    },
  ];
  let conflict = true;
  let exportedVersion = "";
  await core.attach(page);
  await page.route("**/api/v1/artifacts**", async (route) => {
    const url = new URL(route.request().url()),
      method = route.request().method();
    const json = (value: unknown, status = 200) =>
      route.fulfill({
        status,
        contentType: "application/json",
        body: JSON.stringify(value),
      });
    const latest = versions.at(-1)!;
    if (url.pathname.endsWith("/versions"))
      return json(
        versions.map((v) => ({
          version: v.version,
          author: "測試使用者",
          createdAt: new Date().toISOString(),
        })),
      );
    if (url.pathname.includes("/export/")) {
      exportedVersion = url.searchParams.get("version")!;
      return route.fulfill({
        body: Buffer.from("saved revision"),
        contentType: "application/octet-stream",
      });
    }
    if (url.pathname.endsWith("/artifacts")) return json(versions.slice(-1));
    if (method === "PUT") {
      if (conflict) {
        conflict = false;
        return json(
          { title: "Password=fixture-private", code: "artifact_version_conflict", issueCode: "NX-" + "D".repeat(32) },
          409,
        );
      }
      const body = route.request().postDataJSON();
      const v = {
        ...latest,
        resource: { ...latest.resource, name: body.title },
        content: body.content,
        version: latest.version + 1,
        currentVersion: latest.version + 1,
      };
      versions.push(v);
      return json(v);
    }
    const v =
      versions[Number(url.searchParams.get("version") || versions.length) - 1];
    return json({ ...v, currentVersion: versions.length });
  });
  await page.goto(`/artifacts/${id}`);
  await expect(page.getByRole("table")).toBeVisible();
  await page.getByRole("button", { name: "並排預覽", exact: true }).click();
  const editor = page.getByRole("textbox", {
    name: "編輯成果內容",
    exact: true,
  });
  await editor.fill("第二版內容");
  await page.getByRole("button", { name: "儲存新版本" }).click();
  await expect(page.getByRole("alert")).toContainText("此文件已被更新。你的編輯仍保留");
  await expect(page.getByRole("alert")).toContainText("查證代碼：NX-");
  await expect(page.getByRole("alert")).not.toContainText("fixture-private");
  await expect(editor).toHaveValue("第二版內容");
  await page.getByRole("button", { name: "儲存新版本" }).click();
  await expect(page.getByRole("main").getByRole("status")).toContainText(
    "版本 2 已儲存",
  );
  await page.getByRole("combobox", { name: "成果版本" }).click();
  await page.getByRole("option", { name: /^版本 1 / }).click();
  await expect(page.getByText("正在檢視舊版本")).toBeVisible();
  await expect(page.getByRole("table")).toBeVisible();
  await page.getByRole("button", { name: "將此版帶入編輯" }).click();
  await page.getByRole("button", { name: "儲存新版本" }).click();
  await expect(page.getByRole("main").getByRole("status")).toContainText(
    "版本 3 已儲存",
  );
  expect(versions[2].content).toBe(richAnswer);
  const downloaded = page.waitForEvent("download");
  await page.getByRole("button", { name: "Markdown", exact: true }).click();
  await downloaded;
  expect(exportedVersion).toBe("3");
  await editor.fill("尚未儲存");
  await page.getByRole("link", { name: "對話", exact: true }).first().click();
  await expect(page.getByRole("dialog")).toContainText("尚有未儲存的編輯");
  await page.getByRole("button", { name: "取消", exact: true }).click();
  await expect(editor).toHaveValue("尚未儲存");
  await settleEntrance(page);
  await page.screenshot({
    path: "artifacts/screenshots/artifacts-desktop.png",
    fullPage: true,
  });
  await page.setViewportSize({ width: 375, height: 812 });
  expect(
    await page.evaluate(
      () => document.documentElement.scrollWidth <= innerWidth,
    ),
  ).toBe(true);
});
