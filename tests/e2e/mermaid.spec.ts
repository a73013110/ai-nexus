import { test, expect, type Page } from "@playwright/test";
import { readFile } from "node:fs/promises";
import { randomUUID } from "node:crypto";
import { permissionFlowchart } from "./mermaid-fixtures";
import {
  ApiFixture,
  expectViewportContained,
  settleEntrance,
} from "./fixtures";

const flowchart = `flowchart LR
  accTitle: 案件建立與權限分派
  accDescr: 送出案件後檢查資料，資料完整才分派審核。
  A([送出案件]) --> B{資料完整？}
  B -->|是| C[權限分派]
  B -->|否| D[補齊資料]
  D --> B
  C --> E[審核案件]
  E --> F([完成])`;
const sequence = `sequenceDiagram
  participant U as 使用者
  participant S as AI Nexus
  U->>S: 提出問題
  S-->>U: 回答與來源`;
const fence = (source: string) => ["```mermaid", source, "```"].join("\n");
const markdown = [
  "## 流程圖",
  fence(flowchart),
  "> 時序圖\n>\n" +
    fence(sequence)
      .split("\n")
      .map((line) => "> " + line)
      .join("\n"),
].join("\n\n");

async function answer(page: Page, text = markdown) {
  const core = new ApiFixture();
  core.answer = text;
  core.preferences.theme = "system";
  core.extraFeatures.push({
    id: "artifacts",
    name: "成果文件",
    route: "/artifacts",
  });
  await core.attach(page);
  await page.goto("/chat");
  await page
    .getByRole("textbox", { name: "傳送訊息", exact: true })
    .fill("請繪製流程");
  await page.getByRole("button", { name: "送出訊息", exact: true }).click();
  await expect(
    page.getByRole("button", { name: "重新生成", exact: true }),
  ).toBeEnabled();
  return core;
}

test("shared Mermaid reader renders Chinese flowcharts and nested sequences with source, zoom, export and stable themes", async ({
  page,
}) => {
  const errors: string[] = [];
  page.on("pageerror", (error) => errors.push(error.message));
  await answer(page);
  const figures = page.locator(".mermaid-frame");
  await expect(figures).toHaveCount(2);
  const flow = figures.first();
  const image = flow.getByRole("img", { name: /案件建立與權限分派/ });
  await expect(image).toBeVisible();
  await expect
    .poll(() =>
      image.evaluate(
        (img: HTMLImageElement) => img.complete && img.naturalWidth > 0,
      ),
    )
    .toBe(true);
  await expect(figures.nth(1).getByRole("img")).toBeVisible();
  await page.locator(".conversation-viewport").evaluate((el) => {
    el.scrollTop = 0;
  });
  await flow.getByRole("button", { name: "放大圖表", exact: true }).click();
  await expect(flow.locator(".mermaid-scale")).toHaveText("125%");
  await flow.getByRole("button", { name: "適合寬度", exact: true }).click();
  await expect(flow.locator(".mermaid-scale")).toHaveText("100%");
  await flow.getByRole("button", { name: "原始碼", exact: true }).click();
  await expect(flow.locator("pre code")).toHaveText(flowchart + "\n");
  await flow
    .getByRole("button", { name: "複製 Mermaid 原始碼", exact: true })
    .click();
  await expect
    .poll(() =>
      page.evaluate(() => (window as unknown as { __copied: string }).__copied),
    )
    .toBe(flowchart + "\n");
  await flow.getByRole("button", { name: "圖表", exact: true }).click();
  await settleEntrance(page);
  await page.screenshot({
    path: "artifacts/screenshots/mermaid-chat-desktop.png",
    animations: "disabled",
  });
  const previous = await image.getAttribute("src");
  await page.emulateMedia({ colorScheme: "dark" });
  await expect(page.locator("html")).toHaveAttribute("data-theme", "dark");
  await expect.poll(() => image.getAttribute("src")).not.toBe(previous);
  await expect
    .poll(() =>
      image.evaluate(
        (img: HTMLImageElement) => img.complete && img.naturalWidth > 0,
      ),
    )
    .toBe(true);
  await flow.getByRole("button", { name: "展開圖表", exact: true }).click();
  const dialog = page.getByRole("dialog", {
    name: "展開 Mermaid 圖表",
    exact: true,
  });
  await expect(dialog.getByRole("img")).toBeVisible();
  const canvas = dialog.getByRole("region", { name: /^圖表畫布/ });
  await canvas.focus();
  await canvas.press("+");
  await expect(dialog.locator(".mermaid-scale")).toHaveText("125%");
  await canvas.press("0");
  const downloaded = page.waitForEvent("download");
  await dialog
    .getByRole("button", { name: "下載 SVG 圖表", exact: true })
    .click();
  const download = await downloaded;
  const svg = await readFile((await download.path())!, "utf8");
  expect(svg).toContain("送出案件");
  expect(svg).toContain("viewBox=");
  expect(
    await page.evaluate(
      (source) =>
        new DOMParser()
          .parseFromString(source, "image/svg+xml")
          .querySelector('[data-look="neo"]') !== null,
      svg,
    ),
  ).toBe(true);
  expect(svg).not.toMatch(/<foreignObject|<script|<image/i);
  expect(svg).not.toMatch(/(?:href|onload|onclick)=/i);
  await page.screenshot({
    path: "artifacts/screenshots/mermaid-expanded-dark.png",
    animations: "disabled",
  });
  await page.keyboard.press("Escape");
  await expect(dialog).not.toBeVisible();
  await expect(
    flow.getByRole("button", { name: "展開圖表", exact: true }),
  ).toBeFocused();
  await page.setViewportSize({ width: 375, height: 812 });
  await expectViewportContained(page);
  await expect
    .poll(() => flow.evaluate((el) => el.scrollWidth <= el.clientWidth + 1))
    .toBe(true);
  await page.screenshot({
    path: "artifacts/screenshots/mermaid-chat-mobile-dark.png",
    animations: "disabled",
  });
  expect(errors).toEqual([]);
});

test("Mermaid keeps malformed syntax readable and blocks authored HTML, links, config overrides and image fetches", async ({
  page,
}) => {
  const requests: string[] = [];
  page.on("request", (request) => {
    if (request.url().includes("tracking.invalid"))
      requests.push(request.url());
  });
  await answer(
    page,
    [
      fence("flowchart LR\nA["),
      fence(`---
config:
  securityLevel: loose
  htmlLabels: true
  themeCSS: "@import url('https://tracking.invalid/font.css');"
---
flowchart LR
  A["<img src='https://tracking.invalid/pixel' onerror='window.__xss=1'>"] --> B[完成]
  click A "javascript:window.__xss=1"
`),
      fence(
        'flowchart LR\nA@{ img: "https://tracking.invalid/node.png", label: "圖片" }',
      ),
    ].join("\n\n"),
  );
  const figures = page.locator(".mermaid-frame");
  await expect(figures).toHaveCount(3);
  await expect(figures.first()).toContainText("無法繪製這段 Mermaid");
  await expect(figures.first().locator("pre")).toContainText("A[");
  await expect(figures.nth(1).getByRole("img")).toBeVisible();
  await expect(figures.nth(2)).toContainText("無法繪製這段 Mermaid");
  await expect(page.locator(".mermaid-measure")).toHaveCount(0);
  expect(requests).toEqual([]);
  expect(
    await page.evaluate(() => (window as unknown as { __xss?: number }).__xss),
  ).toBeUndefined();
});

test("selection actions share icons and Explain, with one-click language segments and mobile containment", async ({
  page,
}) => {
  await answer(page, "這裡說明 **API 與 RPA 的關係**，以及資料交換的方式。");
  await expect(page.locator("nx-text-tools nx-markdown-editor")).toHaveCount(0);
  const transforms: {
    action: string;
    text: string;
    language: string;
    modelId: string | null;
  }[] = [];
  await page.route("**/api/v1/text/transform", async (route) => {
    const request = route.request().postDataJSON();
    transforms.push(request);
    await route.fulfill({
      json: {
        text:
          request.action === "explain"
            ? "## 概念解釋\n\nAPI 是軟體交換資訊的介面；RPA 會透過介面處理重複工作。\n\n- **API**：交換資料\n- **RPA**：執行流程\n\n" +
              "這是補充說明，協助理解系統流程。\n\n".repeat(50) +
              "### 說明結束\n\n請核對實際的資料流程。"
            : "API and RPA",
        truncated: false,
      },
    });
  });
  const select = async () => {
    const text = page.locator(".message:not(.user) .markdown strong");
    await text.scrollIntoViewIfNeeded();
    await text.evaluate((element) => {
      const range = document.createRange();
      range.selectNodeContents(element);
      window.getSelection()!.removeAllRanges();
      window.getSelection()!.addRange(range);
      element.dispatchEvent(new PointerEvent("pointerup", { bubbles: true }));
    });
  };
  await select();
  const toolbar = page.getByRole("toolbar", {
    name: "選取文字操作",
    exact: true,
  });
  for (const label of ["改寫", "摘要", "解釋", "翻譯", "儲存成果"]) {
    await expect(
      toolbar.getByRole("button", { name: label, exact: true }).locator("svg"),
    ).toHaveCount(1);
  }
  await toolbar.getByRole("button", { name: "解釋", exact: true }).click();
  const tools = page.getByRole("dialog", { name: "解釋段落", exact: true });
  await expect(
    tools.getByRole("region", { name: "段落處理結果預覽" }),
  ).toContainText(/API 是/);
  await expect(tools.getByRole("heading", { name: "概念解釋" })).toBeVisible();
  await expect(tools.locator(".markdown ul li")).toHaveCount(2);
  const preview = tools.getByRole("region", { name: "段落處理結果預覽" });
  expect(
    await preview.evaluate((el) => el.scrollHeight > el.clientHeight),
  ).toBe(true);
  const originalSize = (await tools.boundingBox())!;
  await page.screenshot({
    path: "artifacts/screenshots/text-tools-markdown-reading.png",
    animations: "disabled",
  });
  await tools.getByRole("button", { name: "展開段落工具" }).click();
  await settleEntrance(page);
  expect((await tools.boundingBox())!.width).toBeGreaterThan(
    originalSize.width,
  );
  await tools.getByRole("button", { name: "編輯", exact: true }).click();
  await tools
    .getByRole("textbox", { name: "段落處理結果", exact: true })
    .fill("## 已編輯的解釋\n\n**重點**：保留修改後的內容。");
  await tools.getByRole("button", { name: "閱讀", exact: true }).click();
  await expect(
    tools.getByRole("heading", { name: "已編輯的解釋" }),
  ).toBeVisible();
  await tools.getByRole("button", { name: "複製結果", exact: true }).click();
  await expect
    .poll(() => page.evaluate(() => (window as any).__copied))
    .toContain("## 已編輯的解釋");
  expect(transforms[0]).toMatchObject({
    action: "explain",
    text: "API 與 RPA 的關係",
    modelId: "fixture:8b",
  });
  await tools.getByRole("button", { name: "關閉段落工具" }).click();
  await expect(page.locator("nx-text-tools nx-markdown-editor")).toHaveCount(0);
  await select();
  await toolbar.getByRole("button", { name: "翻譯", exact: true }).click();
  const translated = page.getByRole("dialog", {
    name: "翻譯段落",
    exact: true,
  });
  await expect(translated.getByRole("combobox")).toHaveCount(0);
  const languages = translated.getByRole("group", { name: "翻譯語言" });
  await expect(languages.getByRole("button")).toHaveCount(4);
  await languages.getByRole("button", { name: "日文", exact: true }).click();
  await expect(
    languages.getByRole("button", { name: "日文", exact: true }),
  ).toHaveAttribute("aria-pressed", "true");
  await translated
    .getByRole("button", { name: "開始處理", exact: true })
    .click();
  await expect(
    translated.getByRole("region", { name: "段落處理結果預覽" }),
  ).toContainText("API and RPA");
  expect(transforms[1]).toMatchObject({
    action: "translate",
    language: "日本語",
  });
  await page.screenshot({
    path: "artifacts/screenshots/text-tools-language-segments.png",
    animations: "disabled",
  });
  await translated.getByRole("button", { name: "關閉段落工具" }).click();
  await page.setViewportSize({ width: 375, height: 812 });
  await select();
  await expect(toolbar).toBeVisible();
  await expect
    .poll(async () => {
      const box = (await toolbar.boundingBox())!;
      return (
        box.x >= 11 &&
        box.y >= 11 &&
        box.x + box.width <= 364 &&
        box.y + box.height <= 801
      );
    })
    .toBe(true);
  await expectViewportContained(page);
  await page.screenshot({
    path: "artifacts/screenshots/text-selection-mobile.png",
    animations: "disabled",
  });
  await toolbar.getByRole("button", { name: "改寫", exact: true }).focus();
  await page.keyboard.press("ArrowRight");
  await expect(
    toolbar.getByRole("button", { name: "摘要", exact: true }),
  ).toBeFocused();
  await page.keyboard.press("Escape");
  await expect(toolbar).not.toBeVisible();
});

test("long permission flowcharts retain all SVG bounds and scroll to the right and bottom at every zoom", async ({
  page,
}) => {
  await answer(page, fence(permissionFlowchart));
  const figure = page.locator(".mermaid-frame"),
    image = figure.getByRole("img");
  await expect(image).toBeVisible();
  const downloadEvent = page.waitForEvent("download");
  await figure
    .getByRole("button", { name: "下載 SVG 圖表", exact: true })
    .click();
  const svg = await readFile((await (await downloadEvent).path())!, "utf8");
  expect(svg).toContain("看不到案件");
  const bounds = await page.evaluate((source) => {
    const parent = document.createElement("div");
    parent.style.cssText = "position:fixed;visibility:hidden";
    parent.innerHTML = source;
    document.body.append(parent);
    const svg = parent.querySelector<SVGSVGElement>("svg")!,
      view = svg.viewBox.baseVal,
      box = svg.getBBox();
    const result = {
      left: box.x - view.x,
      top: box.y - view.y,
      right: view.x + view.width - box.x - box.width,
      bottom: view.y + view.height - box.y - box.height,
    };
    parent.remove();
    return result;
  }, svg);
  for (const space of Object.values(bounds))
    expect(space).toBeGreaterThanOrEqual(15);
  const canvas = figure.getByRole("region", { name: /^圖表畫布/ });
  for (const width of [1440, 375]) {
    await page.setViewportSize({ width, height: 1000 });
    await figure.getByRole("button", { name: "適合寬度", exact: true }).click();
    await canvas.focus();
    await canvas.press("+");
    await canvas.press("+");
    await expect
      .poll(() =>
        canvas.evaluate((el) => {
          el.scrollTop = el.scrollHeight;
          el.scrollLeft = el.scrollWidth;
          const box = el.getBoundingClientRect(),
            img = el.querySelector("img")!.getBoundingClientRect();
          return (
            img.bottom <= box.top + el.clientHeight - 15 &&
            img.right <= box.left + el.clientWidth - 15
          );
        }),
      )
      .toBe(true);
    await page.screenshot({
      path: `artifacts/screenshots/mermaid-permission-bottom-${width}.png`,
      animations: "disabled",
    });
    await figure
      .getByRole("button", { name: "顯示完整圖表", exact: true })
      .click();
    await expect
      .poll(() =>
        canvas.evaluate(
          (el) =>
            el.scrollHeight <= el.clientHeight + 2 &&
            el.scrollWidth <= el.clientWidth + 2,
        ),
      )
      .toBe(true);
    await expectViewportContained(page);
  }
  await figure.getByRole("button", { name: "展開圖表", exact: true }).click();
  const dialog = page.getByRole("dialog", { name: "展開 Mermaid 圖表" });
  await expect(dialog).toBeVisible();
  await dialog
    .getByRole("button", { name: "顯示完整圖表", exact: true })
    .click();
  await expect
    .poll(() =>
      dialog
        .locator(".mermaid-canvas")
        .evaluate((el) => el.scrollHeight <= el.clientHeight + 2),
    )
    .toBe(true);
});

test("artifact preview uses the same Mermaid reader as chat", async ({
  page,
}) => {
  const core = new ApiFixture();
  core.extraFeatures.push({
    id: "artifacts",
    name: "成果文件",
    route: "/artifacts",
  });
  await core.attach(page);
  const id = randomUUID();
  const artifact = {
    resource: {
      id,
      kind: "artifact",
      name: "案件流程",
      canEdit: false,
      isOwner: true,
      updatedAt: new Date().toISOString(),
    },
    version: 1,
    currentVersion: 1,
    content: markdown,
    sourceMessageId: null,
    projectId: null,
  };
  await page.route("**/api/v1/artifacts**", async (route) => {
    const path = new URL(route.request().url()).pathname;
    await route.fulfill({
      json: path.endsWith("/versions")
        ? []
        : path.endsWith("/artifacts")
          ? [artifact]
          : artifact,
    });
  });
  await page.goto(`/artifacts/${id}`);
  await expect(page.locator(".artifact-preview .mermaid-frame")).toHaveCount(2);
  await expect(
    page.locator(".artifact-preview .mermaid-frame").first().getByRole("img"),
  ).toBeVisible();
  await expectViewportContained(page);
});

test("semantic nodes use the workspace palette and keep readable labels in dark and light themes", async ({
  page,
}) => {
  await page.emulateMedia({ colorScheme: "dark" });
  await answer(
    page,
    fence(
      [
        "flowchart LR",
        "classDef process fill:#e1f5fe,stroke:#01579b,stroke-width:2px;",
        "classDef result fill:#182b35,stroke:#01579b,stroke-width:2px;",
        "A[API 檢查]:::process --> B[權限分派]:::result",
      ].join("\n"),
    ),
  );
  const figure = page.locator(".mermaid-frame");
  const image = figure.getByRole("img");
  await expect(image).toBeVisible();
  for (const colorScheme of ["dark", "light"] as const) {
    if (colorScheme === "light") {
      const previous = await image.getAttribute("src");
      await page.emulateMedia({ colorScheme });
      await expect.poll(() => image.getAttribute("src")).not.toBe(previous);
    }
    const downloaded = page.waitForEvent("download");
    await figure
      .getByRole("button", { name: "下載 SVG 圖表", exact: true })
      .click();
    const svg = await readFile((await (await downloaded).path())!, "utf8");
    const colors = await page.evaluate((source) => {
      const document = new DOMParser().parseFromString(source, "image/svg+xml");
      return [...document.querySelectorAll<SVGGElement>(".node")].map(
        (node) => ({
          fill: node.querySelector<SVGElement>(".label-container")!.style.fill,
          text: node.querySelector<SVGElement>(".label text")!.style.fill,
        }),
      );
    }, svg);
    expect(colors.map((color) => color.fill)).not.toEqual([
      "rgb(225, 245, 254)",
      "rgb(24, 43, 53)",
    ]);
    expect(svg).toContain('rx="7"');
    expect(svg).not.toMatch(/filter:\s*drop-shadow/i);
    for (const color of colors) {
      const ratio = await page.evaluate(({ fill, text }) => {
        const canvas = document.createElement("canvas");
        canvas.width = canvas.height = 1;
        const context = canvas.getContext("2d")!;
        const light = (value: string) => {
          context.fillStyle = value;
          context.fillRect(0, 0, 1, 1);
          const rgb = [...context.getImageData(0, 0, 1, 1).data]
            .slice(0, 3)
            .map((channel) => {
              const value = channel / 255;
              return value <= 0.04045
                ? value / 12.92
                : ((value + 0.055) / 1.055) ** 2.4;
            });
          return rgb[0] * 0.2126 + rgb[1] * 0.7152 + rgb[2] * 0.0722;
        };
        const foreground = light(text),
          background = light(fill);
        return (
          (Math.max(foreground, background) + 0.05) /
          (Math.min(foreground, background) + 0.05)
        );
      }, color);
      expect(ratio).toBeGreaterThanOrEqual(4.5);
    }
    await page.screenshot({
      path: `artifacts/screenshots/mermaid-authored-${colorScheme}.png`,
      animations: "disabled",
    });
  }
});

test("Mermaid math labels preserve sanitized native MathML", async ({
  page,
}) => {
  await answer(
    page,
    fence("sequenceDiagram\n使用者->>系統: $$x^2 + y^2 = z^2$$"),
  );
  const figure = page.locator(".mermaid-frame");
  const image = figure.getByRole("img");
  await expect(image).toBeVisible();
  const downloaded = page.waitForEvent("download");
  await figure
    .getByRole("button", { name: "下載 SVG 圖表", exact: true })
    .click();
  const svg = await readFile((await (await downloaded).path())!, "utf8");
  expect(svg).toContain("<math");
  expect(svg).toContain("msup");
  expect(svg).not.toMatch(/<script|<img|<image|(?:href|onclick|onload)=/i);
  await expect
    .poll(() =>
      image.evaluate(
        (img: HTMLImageElement) => img.complete && img.naturalWidth > 0,
      ),
    )
    .toBe(true);
  await page.screenshot({
    path: "artifacts/screenshots/mermaid-math-labels.png",
    animations: "disabled",
  });
});

test("streaming Mermaid stays source until committed and releases diagrams on completion and navigation", async ({
  page,
}) => {
  const core = new ApiFixture();
  core.hold = true;
  core.partialAnswer = "```mermaid\nflowchart LR\nA[等待] -->";
  await core.attach(page);
  await page.addInitScript(() => {
    const revoked: string[] = [];
    (window as unknown as { __revoked: string[] }).__revoked = revoked;
    const revoke = URL.revokeObjectURL.bind(URL);
    URL.revokeObjectURL = (url: string) => {
      revoked.push(url);
      revoke(url);
    };
  });
  await page.goto("/chat");
  await page
    .getByRole("textbox", { name: "傳送訊息", exact: true })
    .fill("逐步繪製");
  await page.getByRole("button", { name: "送出訊息", exact: true }).click();
  const stream = page.locator("nx-streaming-answer");
  await expect(stream.locator("code")).toContainText("A[等待] -->");
  await expect(stream.locator(".mermaid-frame")).toHaveCount(0);
  const run = core.runs[0],
    assistant = core.messages.find(
      (message) => message.id === run.assistantMessageId,
    )!;
  const delta = "B[完成]\n```\n\n已完成圖表。";
  run.content += delta;
  assistant.content = run.content;
  core.events.get(run.id)!.push({
    version: 1,
    sequence: ++run.lastSequence,
    runId: run.id,
    type: "delta",
    status: "running",
    delta,
    errorCode: null,
  });
  await expect(stream.locator(".mermaid-frame").getByRole("img")).toBeVisible();
  const liveUrl = (await stream
    .locator(".mermaid-frame img")
    .getAttribute("src"))!;
  run.status = assistant.status = "completed";
  run.finishedAt = new Date().toISOString();
  core.events.get(run.id)!.push({
    version: 1,
    sequence: ++run.lastSequence,
    runId: run.id,
    type: "status",
    status: "completed",
    delta: null,
    errorCode: null,
  });
  await expect(stream).toHaveCount(0);
  await expect(page.locator(".mermaid-frame").getByRole("img")).toBeVisible();
  await expect
    .poll(() =>
      page.evaluate(
        () => (window as unknown as { __revoked: string[] }).__revoked,
      ),
    )
    .toContain(liveUrl);
  const finalUrl = (await page
    .locator(".mermaid-frame img")
    .getAttribute("src"))!;
  await page.getByRole("link", { name: /^新對話/ }).click();
  await expect(page.locator(".mermaid-frame")).toHaveCount(0);
  await expect
    .poll(() =>
      page.evaluate(
        () => (window as unknown as { __revoked: string[] }).__revoked,
      ),
    )
    .toContain(finalUrl);
  await expect(page.locator(".mermaid-measure")).toHaveCount(0);
});
