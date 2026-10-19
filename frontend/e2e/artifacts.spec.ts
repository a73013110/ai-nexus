import { test, expect } from "@playwright/test";
import { randomUUID } from "node:crypto";
import {
  expectCompactWorkspace,
  ApiFixture,
  richAnswer,
  settleEntrance,
  toasts,
} from "./fixtures";
import type { ArtifactDto } from "../src/app/core/api/schema";

test("artifact versions preserve edits, restore older content and export a saved revision", async ({
  page,
}) => {
  const core = new ApiFixture();
  core.extraFeatures.push({
    id: "artifacts",
    name: "成果文件",
    route: "/work/artifacts",
  });
  const id = randomUUID();
  const versions: ArtifactDto[] = [
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
          {
            title: "Password=fixture-private",
            code: "artifact_version_conflict",
            issueCode: "NX-" + "D".repeat(32),
          },
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
  await page.goto(`/work/artifacts/${id}`);
  await expect(page.getByRole("table")).toBeVisible();
  await expectCompactWorkspace(page);
  const reading = page.getByRole("button", { name: "閱讀", exact: true });
  const split = page.getByRole("button", { name: "並排預覽", exact: true });
  await reading.focus();
  await reading.press("End");
  await expect(split).toBeFocused();
  await expect(split).toHaveAttribute("aria-pressed", "true");
  const editor = page.getByRole("textbox", {
    name: "編輯成果內容",
    exact: true,
  });
  await expect(editor).toHaveCSS("font-size", "15px");
  expect((await editor.boundingBox())!.height).toBeGreaterThanOrEqual(320);
  await editor.fill("第二版內容");
  await page.getByRole("button", { name: "儲存新版本" }).click();
  await expect(page.getByRole("alert")).toContainText(
    "此文件已被更新。你的編輯仍保留",
  );
  await expect(page.getByRole("alert")).toContainText("查證代碼：NX-");
  await expect(page.getByRole("alert")).not.toContainText("fixture-private");
  await expect(editor).toHaveValue("第二版內容");
  await page.getByRole("button", { name: "儲存新版本" }).click();
  await expect(toasts(page).filter({ hasText: "版本 2 已儲存" })).toBeVisible();
  await page.getByRole("combobox", { name: "成果版本" }).click();
  await page.getByRole("option", { name: /^版本 1 / }).click();
  await expect(page.getByText("正在檢視舊版本")).toBeVisible();
  await expect(
    page.getByRole("button", { name: "編輯", exact: true }),
  ).toHaveCount(0);
  await expect(page.getByRole("table")).toBeVisible();
  await page.getByRole("button", { name: "將此版帶入編輯" }).click();
  await page.getByRole("button", { name: "儲存新版本" }).click();
  await expect(toasts(page).filter({ hasText: "版本 3 已儲存" })).toBeVisible();
  expect(versions[2].content).toBe(richAnswer);
  const downloaded = page.waitForEvent("download");
  await page.getByRole("button", { name: "匯出成果", exact: true }).click();
  await page.getByRole("menuitem", { name: "Markdown", exact: true }).click();
  await downloaded;
  expect(exportedVersion).toBe("3");
  await editor.fill("尚未儲存");
  await page.getByRole("link", { name: "對話", exact: true }).first().click();
  await expect(page.getByRole("dialog")).toContainText("尚有未儲存的編輯");
  await page.getByRole("button", { name: "取消", exact: true }).click();
  await expect(editor).toHaveValue("尚未儲存");
  await settleEntrance(page);
  await expectCompactWorkspace(page);
  await page.setViewportSize({ width: 375, height: 812 });
  expect((await reading.boundingBox())!.height).toBeGreaterThanOrEqual(44);
  expect(
    await page.evaluate(
      () => document.documentElement.scrollWidth <= innerWidth,
    ),
  ).toBe(true);
});

for (const width of [1440, 375]) {
  test(`artifacts tours walk the list and what the viewer can do with a document at ${width}px`, async ({
    page,
  }) => {
    const errors: string[] = [];
    page.on("pageerror", (error) => errors.push(error.message));
    const core = new ApiFixture();
    core.extraFeatures.push({
      id: "artifacts",
      name: "成果文件",
      route: "/work/artifacts",
    });
    // A reader of a single version has no version picker, editor or document menu.
    const reader = width === 375;
    const versions = reader ? 1 : 2;
    const artifact: ArtifactDto = {
      resource: {
        id: randomUUID(),
        kind: "artifact",
        name: "工作成果",
        canEdit: !reader,
        isOwner: !reader,
        updatedAt: new Date().toISOString(),
      },
      version: versions,
      currentVersion: versions,
      content: "# 工作成果\n\n第一段內容。",
      sourceMessageId: null,
      projectId: null,
    };
    await core.attach(page);
    await page.route("**/api/v1/artifacts**", (route) => {
      const path = new URL(route.request().url()).pathname;
      const body = path.endsWith("/versions")
        ? Array.from({ length: versions }, (_, index) => ({
            version: index + 1,
            author: "測試使用者",
            createdAt: new Date().toISOString(),
          }))
        : path === "/api/v1/artifacts"
          ? [artifact]
          : artifact;
      return route.fulfill({
        contentType: "application/json",
        body: JSON.stringify(body),
      });
    });
    const height = width === 375 ? 812 : 1000;
    await page.setViewportSize({ width, height });
    const launcher = page
      .locator(".feature-header")
      .getByRole("button", { name: "操作導覽", exact: true });
    const tour = page.locator(".driver-popover.nx-tour");
    const title = tour.locator(".driver-popover-title");
    const progress = tour.locator(".driver-popover-progress-text");
    // Walks to the end and returns each step's title and the last description.
    const walk = async () => {
      await launcher.click();
      await expect(title).toHaveText(/./);
      const total = Number((await progress.innerText()).split("/")[1]);
      const titles = [await title.innerText()];
      for (let step = 2; step <= total; step++) {
        await tour.getByRole("button", { name: "下一步", exact: true }).click();
        await expect(progress).toHaveText(`${step} / ${total}`);
        titles.push(await title.innerText());
        await settleEntrance(page);
        const bounds = (await tour.boundingBox())!;
        expect(bounds.x).toBeGreaterThanOrEqual(0);
        expect(bounds.x + bounds.width).toBeLessThanOrEqual(width);
        expect(bounds.y + bounds.height).toBeLessThanOrEqual(height);
      }
      const last = await tour
        .locator(".driver-popover-description")
        .innerText();
      await tour.getByRole("button", { name: "開始使用", exact: true }).click();
      await expect(tour).toHaveCount(0);
      await expect(launcher).toBeFocused();
      await expect(page.locator(".driver-overlay")).toHaveCount(0);
      return { titles, last };
    };
    await page.goto("/work/artifacts");
    await expect(page.locator(".resource-card")).toBeVisible();
    const list = await walk();
    expect(list.titles).toEqual([
      "把回答整理成文件",
      "我擁有與共用給我",
      "用名稱尋找成果",
      "打開成果",
      "新增成果",
    ]);
    expect(list.last).toContain("重播導覽");
    await page.locator(".resource-card").click();
    await expect(page.locator(".artifact-paper")).toContainText("第一段內容");
    // An editor switches to 編輯 first, so the tour adds the selection and save steps.
    if (!reader) {
      await page.getByRole("button", { name: "編輯", exact: true }).click();
      await expect(
        page.getByRole("textbox", { name: "編輯成果內容", exact: true }),
      ).toBeVisible();
    }
    const detail = await walk();
    expect(detail.titles).toEqual(
      reader
        ? ["成果的狀態", "閱讀成果", "匯出已儲存的版本"]
        : [
            "成果的狀態",
            "切換版本",
            "閱讀、編輯或並排預覽",
            "選取段落交給 AI",
            "儲存新版本",
            "匯出已儲存的版本",
            "分享與權限",
          ],
    );
    expect(detail.last).toContain("重播導覽");
    expect(errors).toEqual([]);
  });
}
