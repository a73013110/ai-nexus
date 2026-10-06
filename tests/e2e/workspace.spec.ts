import { test, expect, type Page } from "@playwright/test";
import { ApiFixture, settleEntrance, chooseSelect } from "./fixtures";

const png = Buffer.from(
  "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+jJRkAAAAASUVORK5CYII=",
  "base64",
);

test("regeneration and edited branches preserve the next question draft", async ({
  page,
}) => {
  const fixture = new ApiFixture();
  await fixture.attach(page);
  await page.goto("/chat");
  await send(page, "第一個提問");
  const input = page.getByRole("textbox", { name: "傳送訊息", exact: true });
  await input.fill("稍後要送出的下一個問題");
  await upload(page, "future.txt", Buffer.from("future notes"));
  await page.getByRole("button", { name: "重新生成", exact: true }).click();
  await expect.poll(() => fixture.generated).toBe(2);
  await expect(
    page.getByRole("button", { name: "編輯提問", exact: true }),
  ).toBeEnabled();
  await expect(input).toHaveValue("稍後要送出的下一個問題");
  await expect(page.locator(".composer .attachment-card")).toHaveCount(1);
  await page.getByRole("button", { name: "編輯提問", exact: true }).click();
  await page
    .getByRole("textbox", { name: "修改你的提問", exact: true })
    .fill("修訂過的第一個提問");
  await page.getByRole("button", { name: "送出訊息" }).click();
  await expect.poll(() => fixture.generated).toBe(3);
  await expect(input).toHaveValue("稍後要送出的下一個問題");
  await expect(page.locator(".composer .attachment-card")).toHaveCount(1);
});

test("sending an existing conversation does not erase the separate new conversation draft", async ({
  page,
}) => {
  const fixture = new ApiFixture();
  await fixture.attach(page);
  await page.goto("/chat");
  await send(page, "已建立的專案");
  const conversationUrl = page.url();
  await page
    .getByRole("link", { name: "新對話", exact: false })
    .first()
    .click();
  const input = page.getByRole("textbox", { name: "傳送訊息", exact: true });
  await input.fill("另一件事情的獨立草稿");
  await expect
    .poll(() =>
      page.evaluate(() =>
        Object.values(localStorage).some((value) =>
          value.includes("另一件事情的獨立草稿"),
        ),
      ),
    )
    .toBe(true);
  await page.goto(conversationUrl);
  await send(page, "在專案中接著提問");
  await expect.poll(() => fixture.generated).toBe(2);
  await expect(input).toHaveValue("");
  await page
    .getByRole("link", { name: "新對話", exact: false })
    .first()
    .click();
  await expect(input).toHaveValue("另一件事情的獨立草稿");
});

test("returning to a new draft ignores a delayed previous conversation response", async ({
  page,
}) => {
  const fixture = new ApiFixture();
  await fixture.attach(page);
  await page.goto("/chat");
  await send(page, "原有專案");
  const id = fixture.conversations[0].id;
  const input = page.getByRole("textbox", { name: "傳送訊息", exact: true });
  const newConversation = page
    .getByRole("link", { name: "新對話", exact: false })
    .first();
  await newConversation.click();
  await input.fill("新對話中還沒送出的想法");
  await expect
    .poll(() =>
      page.evaluate(() =>
        Object.values(localStorage).some((value) =>
          value.includes("新對話中還沒送出的想法"),
        ),
      ),
    )
    .toBe(true);
  let release!: () => void;
  let started!: () => void;
  const held = new Promise<void>((resolve) => {
    release = resolve;
  });
  const requested = new Promise<void>((resolve) => {
    started = resolve;
  });
  await page.route(`**/api/v1/conversations/${id}`, async (route) => {
    started();
    await held;
    await route.fallback();
  });
  await page.getByRole("link", { name: "原有專案", exact: true }).click();
  await requested;
  await expect(input).toHaveAttribute("readonly", "");
  await newConversation.click();
  await expect(input).toHaveValue("新對話中還沒送出的想法");
  const completed = page.waitForResponse((response) =>
    response.url().endsWith(`/api/v1/conversations/${id}`),
  );
  release();
  await completed;
  await expect(page).toHaveURL(/\/chat$/);
  await expect(
    page.getByRole("heading", { name: "今天，從哪件事開始？" }),
  ).toBeVisible();
  await expect(input).toHaveValue("新對話中還沒送出的想法");
});

test("logout clears private workspace state while drafts restore only for their original account", async ({
  page,
}) => {
  const fixture = new ApiFixture();
  fixture.ldap = true;
  fixture.authenticated = true;
  const alice = fixture.userId;
  await fixture.attach(page);
  await page.goto("/chat");
  const input = page.getByRole("textbox", { name: "傳送訊息", exact: true });
  await expect(
    page.getByRole("button", { name: "加入文件或圖片" }),
  ).toBeEnabled();
  await input.fill("Alice 的私人草稿");
  await upload(page, "private.txt", Buffer.from("private draft"));
  async function logout() {
    await page.getByRole("button", { name: "登入者選單", exact: true }).click();
    await page.getByRole("menuitem", { name: "登出工作區" }).click();
    await expect(
      page.getByRole("heading", { name: "登入工作區" }),
    ).toBeVisible();
  }
  async function login(account: string) {
    await page.getByLabel("AD 帳號", { exact: true }).fill(account);
    await page.getByLabel("AD 密碼", { exact: true }).fill("fixture-password");
    await page.getByRole("button", { name: "登入工作區", exact: true }).click();
    await expect(page).toHaveURL(/\/dashboard$/);
    await page.getByRole("link", { name: "AI 對話", exact: true }).click();
    await expect(
      page.getByRole("button", { name: "加入文件或圖片" }),
    ).toBeEnabled();
  }
  await logout();
  fixture.userId = "00000000-0000-0000-0000-000000000002";
  await login("bob");
  await expect(input).toHaveValue("");
  await expect(page.locator(".composer .attachment-card")).toHaveCount(0);
  await input.fill("Bob 的不同草稿");
  await logout();
  fixture.userId = alice;
  await login("alice");
  await expect(input).toHaveValue("Alice 的私人草稿");
  await expect(page.locator(".composer .attachment-card")).toHaveCount(1);
});

test("the composer uses the server character limit rather than a fixed local constant", async ({
  page,
}) => {
  const fixture = new ApiFixture();
  fixture.modelPolicy.maxInputCharacters = 500;
  fixture.prompts.push({
    id: "00000000-0000-0000-0000-000000000050",
    title: "長提問",
    content: "a".repeat(501),
    updatedAt: new Date().toISOString(),
  });
  await fixture.attach(page);
  await page.goto("/chat");
  const input = page.getByRole("textbox", { name: "傳送訊息", exact: true });
  await expect(input).toHaveAttribute("maxlength", "500");
  await input.fill("a".repeat(501));
  await expect(input).toHaveValue("a".repeat(500));
  await expect(page.getByRole("button", { name: "送出訊息" })).toBeEnabled();
  await page.getByRole("button", { name: "常用範本" }).click();
  await page.locator(".template-use").click();
  await expect(input).toHaveValue("a".repeat(501));
  await expect(page.getByText(/訊息超過系統允許的 500 個字元/)).toBeVisible();
  await expect(page.getByRole("button", { name: "送出訊息" })).toBeDisabled();
  await input.fill("a".repeat(500));
  await expect(page.getByRole("button", { name: "送出訊息" })).toBeEnabled();
});
const upload = async (
  page: Page,
  name: string,
  buffer: Buffer,
  mimeType = "text/plain",
) => {
  const chooser = page.waitForEvent("filechooser");
  await page
    .getByRole("button", { name: "加入文件或圖片", exact: true })
    .click();
  await (await chooser).setFiles({ name, mimeType, buffer });
};
async function send(page: Page, text = "請分析目前文件") {
  await page.getByRole("textbox", { name: "傳送訊息", exact: true }).fill(text);
  await page.getByRole("button", { name: "送出訊息" }).click();
  await expect(page.getByRole("table")).toBeVisible();
}
async function menu(page: Page, name: string) {
  await page.getByLabel("對話操作", { exact: true }).click();
  await page.getByRole("button", { name, exact: true }).click();
}

test("uploaded documents and images persist on messages and reach the run request", async ({
  page,
}) => {
  const fixture = new ApiFixture();
  await fixture.attach(page);
  await page.goto("/chat");
  await expect(
    page.getByRole("button", { name: "加入文件或圖片" }),
  ).toBeEnabled();
  await upload(page, "notes.md", Buffer.from("專案摘要"));
  await upload(page, "diagram.png", png, "image/png");
  await expect(page.locator(".composer .attachment-card")).toHaveCount(2);
  await expect(page.locator(".composer img")).toBeVisible();
  await send(page);
  expect(fixture.lastRequest?.attachmentIds).toHaveLength(2);
  await expect(page.locator(".composer .attachment-card")).toHaveCount(0);
  await expect(page.locator(".message-list .attachment-card")).toHaveCount(2);
  await page.reload();
  await expect(page.locator(".message-list .attachment-card")).toHaveCount(2);
  await page.getByRole("button", { name: "編輯提問", exact: true }).click();
  await page
    .getByRole("button", { name: "移除附件：diagram.png", exact: true })
    .click();
  await expect(page.locator(".composer .attachment-card")).toHaveCount(1);
  expect(fixture.attachments).toHaveLength(2); // The original history retains its image.
  await settleEntrance(page);
  await page.screenshot({
    path: "artifacts/screenshots/attachments-desktop.png",
    fullPage: true,
  });
});

test("clipboard image and file drop use the same validated upload flow", async ({
  page,
}) => {
  const fixture = new ApiFixture();
  await fixture.attach(page);
  await page.goto("/chat");
  await expect(
    page.getByRole("button", { name: "加入文件或圖片" }),
  ).toBeEnabled();
  const pasted = await page.evaluate(
    (bytes) => {
      const data = new DataTransfer();
      data.items.add(
        new File([new Uint8Array(bytes)], "clipboard.png", {
          type: "image/png",
        }),
      );
      const event = new ClipboardEvent("paste", {
        bubbles: true,
        cancelable: true,
        clipboardData: data,
      });
      document.querySelector("#composer")!.dispatchEvent(event);
      return event.defaultPrevented;
    },
    [...png],
  );
  expect(pasted).toBe(true);
  await expect(page.locator(".composer .attachment-card")).toHaveCount(1);
  await page.evaluate(() => {
    const data = new DataTransfer();
    data.items.add(
      new File(["meeting notes"], "drop.txt", { type: "text/plain" }),
    );
    document.querySelector("main")!.dispatchEvent(
      new DragEvent("drop", {
        bubbles: true,
        cancelable: true,
        dataTransfer: data,
      }),
    );
  });
  await expect(page.locator(".composer .attachment-card")).toHaveCount(2);
  await send(page);
  expect(fixture.lastRequest?.attachmentIds).toHaveLength(2);
});

test("attachment limits and model vision capability prevent invalid submission", async ({
  page,
}) => {
  const fixture = new ApiFixture();
  fixture.supportsImages = false;
  await fixture.attach(page);
  await page.goto("/chat");
  await expect(
    page.getByRole("button", { name: "加入文件或圖片" }),
  ).toBeEnabled();
  await upload(page, "malicious.svg", Buffer.from("<svg />"), "image/svg+xml");
  await expect(page.getByRole("alert")).toContainText("格式不支援");
  expect(fixture.attachments).toHaveLength(0);
  await upload(page, "photo.png", png, "image/png");
  await page.getByRole("textbox", { name: "傳送訊息" }).fill("describe");
  await expect(
    page.getByText("目前模型不支援圖片，請切換模型或移除圖片。"),
  ).toBeVisible();
  await expect(page.getByRole("button", { name: "送出訊息" })).toBeDisabled();
  await page.getByRole("button", { name: "移除附件：photo.png" }).click();
  await expect(page.getByRole("button", { name: "送出訊息" })).toBeEnabled();
});

test("draft text and attachment ids restore after refresh and stay per conversation", async ({
  page,
}) => {
  const fixture = new ApiFixture();
  await fixture.attach(page);
  await page.goto("/chat");
  const input = page.getByRole("textbox", { name: "傳送訊息" });
  await expect(
    page.getByRole("button", { name: "加入文件或圖片" }),
  ).toBeEnabled();
  await input.fill("尚未送出的草稿");
  await upload(page, "draft.txt", Buffer.from("draft"));
  await expect
    .poll(() =>
      page.evaluate(
        () =>
          Object.keys(localStorage).filter((key) =>
            key.startsWith("nexus.draft."),
          ).length,
      ),
    )
    .toBe(1);
  await page.reload();
  await expect(input).toHaveValue("尚未送出的草稿");
  await expect(page.locator(".composer .attachment-card")).toHaveCount(1);
  await send(page, "草稿送出");
  await input.fill("這段對話的後續草稿");
  await expect
    .poll(() =>
      page.evaluate(() =>
        Object.values(localStorage).some((value) =>
          value.includes("這段對話的後續草稿"),
        ),
      ),
    )
    .toBe(true);
  await page.getByRole("link", { name: /新對話.*開始/ }).click();
  await expect(input).toHaveValue("");
  await input.fill("另一段新對話的草稿");
  await page.locator(".history-row a").first().click();
  await expect(input).toHaveValue("這段對話的後續草稿");
  await page.getByRole("link", { name: /新對話.*開始/ }).click();
  await expect(input).toHaveValue("另一段新對話的草稿");
});

test("personal prompt library supports creating applying editing and deleting", async ({
  page,
}) => {
  const fixture = new ApiFixture();
  await fixture.attach(page);
  await page.goto("/chat");
  await page.getByRole("button", { name: "常用範本" }).click();
  await page.getByRole("textbox", { name: "範本名稱" }).fill("會議摘要");
  await page
    .getByRole("textbox", { name: "提示詞內容" })
    .fill("整理以下會議，列出決議與待辦：");
  await page.getByRole("button", { name: "儲存範本" }).click();
  await expect(page.locator(".template-use")).toContainText("會議摘要");
  await page.getByRole("button", { name: "編輯範本：會議摘要" }).click();
  await page
    .getByRole("textbox", { name: "提示詞內容" })
    .fill("整理會議並列出負責人：");
  await page.getByRole("button", { name: "儲存範本" }).click();
  await expect
    .poll(() => fixture.prompts[0]?.content)
    .toBe("整理會議並列出負責人：");
  await settleEntrance(page);
  await page.screenshot({
    path: "artifacts/screenshots/prompt-library.png",
    fullPage: true,
  });
  await page.locator(".template-use").click();
  await expect(page.getByRole("textbox", { name: "傳送訊息" })).toHaveValue(
    "整理會議並列出負責人：",
  );
  await page.keyboard.press("Control+Shift+L");
  await page.getByRole("button", { name: "刪除範本：會議摘要" }).click();
  await expect(page.locator(".template-row")).toHaveCount(0);
  expect(fixture.prompts).toHaveLength(0);
});

test("conversation instruction and tags persist and allow label filtering", async ({
  page,
}) => {
  const fixture = new ApiFixture();
  await fixture.attach(page);
  await page.goto("/chat");
  await send(page);
  await menu(page, "對話指令與標籤");
  await page
    .getByRole("textbox", { name: "對話指令", exact: true })
    .fill("先提供摘要，再列出待辦");
  await page
    .getByRole("textbox", { name: "標籤", exact: true })
    .fill("企劃, 工作");
  await page.getByRole("button", { name: "儲存設定" }).click();
  await expect
    .poll(() => fixture.conversations[0].systemInstruction)
    .toBe("先提供摘要，再列出待辦");
  await chooseSelect(page, "依標籤篩選", "企劃");
  await expect(page.locator(".history-row")).toHaveCount(1);
  await page.reload();
  await menu(page, "對話指令與標籤");
  await expect(
    page.getByRole("textbox", { name: "對話指令", exact: true }),
  ).toHaveValue("先提供摘要，再列出待辦");
  await expect(
    page.getByRole("textbox", { name: "標籤", exact: true }),
  ).toHaveValue("企劃, 工作");
});

test("favorite and archive views retain the ability to restore conversations", async ({
  page,
}) => {
  const fixture = new ApiFixture();
  await fixture.attach(page);
  await page.goto("/chat");
  await send(page, "需要保留的對話");
  await page.getByRole("button", { name: "收藏目前對話" }).click();
  await expect(
    page.getByRole("button", { name: "取消收藏目前對話" }),
  ).toHaveAttribute("aria-pressed", "true");
  await page.getByRole("button", { name: "收藏", exact: true }).click();
  await expect(page.locator(".history-row")).toHaveCount(1);
  await menu(page, "封存對話");
  await expect(page.locator(".history-row")).toHaveCount(0);
  await expect(page.getByRole("button", { name: "送出訊息" })).toBeDisabled();
  await page.getByRole("button", { name: "封存", exact: true }).click();
  await expect(page.locator(".history-row")).toHaveCount(1);
  await page.getByRole("button", { name: "還原對話", exact: true }).click();
  await expect(page.locator(".archive-notice")).toHaveCount(0);
  await page.getByRole("button", { name: "近期", exact: true }).click();
  await expect(page.locator(".history-row")).toHaveCount(1);
});

test("history search finds message content independently of its title", async ({
  page,
}) => {
  const fixture = new ApiFixture();
  fixture.answer = "內容關鍵字：ROADMAP";
  await fixture.attach(page);
  await page.goto("/chat");
  await page.getByRole("textbox", { name: "傳送訊息" }).fill("獨立標題");
  await page.getByRole("button", { name: "送出訊息" }).click();
  await expect(
    page.getByText("內容關鍵字：ROADMAP", { exact: true }),
  ).toBeVisible();
  await page
    .getByRole("searchbox", { name: "搜尋對話標題與內容" })
    .fill("ROADMAP");
  await expect(page.locator(".history-row")).toHaveCount(1);
  await page
    .getByRole("searchbox", { name: "搜尋對話標題與內容" })
    .fill("不存在");
  await expect(page.locator(".history-row")).toHaveCount(0);
});

test("duplicate and JSON text backup preserve the tree in a separate conversation", async ({
  page,
}) => {
  const fixture = new ApiFixture();
  await fixture.attach(page);
  await page.goto("/chat");
  await send(page, "可複製的內容");
  await menu(page, "建立對話副本");
  await expect(
    page.getByRole("heading", { name: "可複製的內容 · 副本" }),
  ).toBeVisible();
  expect(fixture.conversations).toHaveLength(2);
  await expect(page.getByRole("table")).toBeVisible();
  await page.getByLabel("對話操作", { exact: true }).click();
  const downloaded = page.waitForEvent("download");
  await page.getByRole("button", { name: "匯出 JSON 文字備份" }).click();
  const file = await downloaded;
  let content = "";
  for await (const chunk of (await file.createReadStream())!)
    content += chunk.toString();
  const backup = JSON.parse(content);
  expect(backup.messages).toHaveLength(2);
  expect(backup.version).toBe(1);
  await page.locator("nx-chat-sidebar input[type=file]").setInputFiles({
    name: "backup.json",
    mimeType: "application/json",
    buffer: Buffer.from(content),
  });
  await expect.poll(() => fixture.conversations.length).toBe(3);
  await expect(page.getByRole("table")).toBeVisible();
});

test("message finder highlights matches and moves without auto-scrolling back", async ({
  page,
}) => {
  const fixture = new ApiFixture();
  await fixture.attach(page);
  await page.goto("/chat");
  await send(page, "工作重點與待辦");
  await page.getByRole("button", { name: "搜尋目前對話訊息" }).click();
  await page.getByRole("searchbox", { name: "尋找訊息" }).fill("工作");
  await expect(page.locator(".find-count")).toHaveText("1 / 2");
  await expect(page.locator(".message.search-match")).toHaveCount(2);
  await page.getByRole("button", { name: "下一個符合訊息" }).click();
  await expect(page.locator(".find-count")).toHaveText("2 / 2");
  await page.getByRole("searchbox", { name: "尋找訊息" }).press("Escape");
  await expect(page.locator(".conversation-find")).toHaveCount(0);
});

test("command palette supports keyboard selection and focus restoration", async ({
  page,
}) => {
  const fixture = new ApiFixture();
  await fixture.attach(page);
  await page.goto("/chat");
  const input = page.getByRole("textbox", { name: "傳送訊息" });
  await input.focus();
  await page.keyboard.press("Control+k");
  const search = page.getByRole("combobox", { name: "快捷指令" });
  await expect(search).toBeFocused();
  await search.fill("深色");
  await search.press("Enter");
  await expect(page.locator("html")).toHaveAttribute("data-theme", "dark");
  await expect(input).toBeFocused();
  await page.keyboard.press("Control+k");
  await search.fill("提示詞");
  await search.press("Enter");
  await expect(page.getByRole("heading", { name: "常用提示詞" })).toBeVisible();
});

test("shared disclosures close outside and permanent authorization errors do not retry SSE", async ({
  page,
}) => {
  const fixture = new ApiFixture();
  fixture.eventsStatus = 403;
  await fixture.attach(page);
  await page.goto("/chat");
  await page.getByLabel(/上下文用量：/).click();
  await expect(
    page.getByRole("group", { name: "上下文用量", exact: true }),
  ).toHaveAttribute("open");
  await page.getByRole("textbox", { name: "傳送訊息" }).click();
  await expect(
    page.getByRole("group", { name: "上下文用量", exact: true }),
  ).not.toHaveAttribute("open");
  await page.getByRole("textbox", { name: "傳送訊息" }).fill("權限撤銷");
  await page.getByRole("button", { name: "送出訊息" }).click();
  await expect(page.getByRole("alert")).toContainText("沒有 AI 對話權限");
  await page.waitForTimeout(1000);
  expect(fixture.eventReads).toBe(1);
  expect(fixture.posts).toBe(1);
});

test("mobile uploads settings and palette remain usable with reduced motion", async ({
  page,
}) => {
  await page.setViewportSize({ width: 375, height: 812 });
  const fixture = new ApiFixture();
  fixture.preferences.theme = "dark";
  fixture.preferences.reducedMotion = true;
  await fixture.attach(page);
  await page.goto("/chat");
  await expect(
    page.getByRole("button", { name: "加入文件或圖片" }),
  ).toBeEnabled();
  await upload(page, "mobile.png", png, "image/png");
  await expect(page.locator(".composer .attachment-card")).toBeVisible();
  await expect
    .poll(() =>
      page.evaluate(() => document.documentElement.scrollWidth <= innerWidth),
    )
    .toBe(true);
  await settleEntrance(page);
  await page.screenshot({
    path: "artifacts/screenshots/mobile-attachments.png",
    fullPage: true,
  });
  await send(page);
  await menu(page, "對話指令與標籤");
  await expect(
    page.getByRole("textbox", { name: "對話指令", exact: true }),
  ).toBeVisible();
  await settleEntrance(page);
  await page.screenshot({
    path: "artifacts/screenshots/mobile-settings.png",
    fullPage: true,
  });
  await page.getByRole("button", { name: "關閉對話設定" }).click();
  await page.getByRole("button", { name: "開啟側欄" }).click();
  await page.getByRole("button", { name: "開啟快捷指令" }).click();
  await expect(page.getByRole("combobox", { name: "快捷指令" })).toBeFocused();
});
