import { test, expect } from '@playwright/test';
import { ApiFixture, chooseSelect, settleEntrance } from './fixtures';

function dashboardFixture(fixture: ApiFixture) {
  const day = new Date().toLocaleDateString('sv-SE');
  return {
    scope: 'personal', counts: { conversations: 12, projects: 3, collections: 2, documents: 8, readyDocuments: 7, failedDocuments: 1, chunks: 214, activeGenerations: 0, activeJobs: 1, failedJobs: 0, staleIndexes: 1 },
    spend: { from: new Date().toISOString(), until: new Date().toISOString(), offsetMinutes: 480, requests: 42, pendingCalls: 1, legacyCalls: 2, inputTokens: 10200, outputTokens: 1400,
      totals: [{ currency: 'USD', kind: 'api', amount: .1208, knownCalls: 38, unknownCalls: 1 }, { currency: 'TWD', kind: 'internal', amount: 15, knownCalls: 1, unknownCalls: 0 }],
      daily: [{ label: day, currency: 'USD', kind: 'api', amount: .1208, requests: 40, unknownCalls: 1, inputTokens: 10000, outputTokens: 1300 }],
      models: [{ label: 'AI 助理', currency: 'USD', kind: 'api', amount: .1208, requests: 40, unknownCalls: 1, inputTokens: 10000, outputTokens: 1300 }],
      users: [{ ownerId: fixture.userId, displayName: '測試使用者', account: 'TEST\\fixture', currency: 'USD', kind: 'api', amount: .1208, requests: 40, unknownCalls: 1 }] },
    recent: [], webSearchAvailable: true, giteaAvailable: true, embeddingMode: '語意向量',
  };
}

test('dashboard keeps currencies separate, supports node inspection, and fits mobile with reduced motion', async ({ page }) => {
  const fixture = new ApiFixture(); fixture.adminAccess = true; fixture.extraFeatures = [{ id: 'dashboard', name: '總覽', route: '/dashboard' }, { id: 'knowledge', name: '知識庫', route: '/knowledge' }];
  await fixture.attach(page);
  await page.route('**/api/v1/dashboard?**', route => { const data = dashboardFixture(fixture); data.scope = new URL(route.request().url()).searchParams.get('scope') ?? 'personal'; return route.fulfill({ json: data }); });
  await page.goto('/dashboard');
  await expect(page.getByRole('heading', { name: '總覽', exact: true })).toBeVisible();
  await expect(page.locator('.metric-card').first()).toContainText('USD');
  await expect(page.locator('.metric-card').first()).not.toContainText('15.1208');
  await page.locator('.flow-node').filter({ hasText: '知識' }).click();
  await expect(page.locator('.flow-inspector')).toContainText('知識');
  const chart = page.locator('nx-trend-chart').first(); await chart.getByRole('slider').focus(); await page.keyboard.press('End');
  await expect(chart.locator('.trend-tooltip')).toBeVisible();
  await chooseSelect(page, '顯示的費用幣別與類型', 'TWD · 本機內部成本');
  await expect(page.locator('.metric-card').first()).toContainText('TWD');
  await chooseSelect(page, '總覽範圍', '整個平台');
  await expect(page.getByRole('heading', { name: '使用者區間費用' })).toBeVisible();
  const appliedFrom = await page.getByLabel('費用開始日期', { exact: true }).inputValue();
  let exportedFrom = '';
  await page.route('**/api/v1/admin/billing/export?**', route => {
    exportedFrom = new URL(route.request().url()).searchParams.get('from')!;
    return route.fulfill({ contentType: 'text/csv', body: '使用者,費用\n測試,1' });
  });
  await page.getByLabel('費用開始日期', { exact: true }).fill('2025-01-01');
  await page.getByRole('button', { name: '匯出費用', exact: true }).click();
  await expect.poll(() => exportedFrom).toBe(new Date(appliedFrom + 'T00:00:00').toISOString());
  await page.getByLabel('費用開始日期', { exact: true }).fill(appliedFrom);
  await settleEntrance(page);
  await page.locator('.feature-main').evaluate(el => el.scrollTo({ top: 0, behavior: 'instant' }));
  await page.screenshot({ animations: 'disabled', path: 'artifacts/screenshots/dashboard-light.png' });
  await page.locator('.feature-main').evaluate(el => el.scrollTo({ top: el.scrollHeight, behavior: 'instant' }));
  await page.screenshot({ animations: 'disabled', path: 'artifacts/screenshots/dashboard-reports-light.png' });
  await page.setViewportSize({ width: 375, height: 812 });
  await page.evaluate(() => { document.documentElement.dataset.theme = 'dark'; document.documentElement.dataset.reducedMotion = 'true'; });
  await expect.poll(() => page.evaluate(() => document.documentElement.scrollWidth <= innerWidth)).toBe(true);
  await expect(page.locator('.flow-pulse')).toHaveCSS('animation-name', 'none');
  await page.locator('.feature-main').evaluate(el => el.scrollTo({ top: 0, behavior: 'instant' }));
  await page.screenshot({ animations: 'disabled', path: 'artifacts/screenshots/dashboard-mobile-dark.png' });
  await page.locator('.feature-main').evaluate(el => el.scrollTo({ top: el.scrollHeight, behavior: 'instant' }));
  await page.screenshot({ animations: 'disabled', path: 'artifacts/screenshots/dashboard-reports-mobile-dark.png' });
});

test('price dialog creates immutable versions and stays within the viewport', async ({ page }) => {
  const fixture = new ApiFixture(); fixture.adminAccess = true; fixture.extraFeatures = [{ id: 'dashboard', name: '總覽', route: '/dashboard' }]; await fixture.attach(page);
  let saved: Record<string, unknown> | undefined;
  await page.route('**/api/v1/dashboard?**', route => route.fulfill({ json: dashboardFixture(fixture) }));
  await page.route('**/api/v1/admin/billing/prices', route => {
    if (route.request().method() === 'POST') { saved = route.request().postDataJSON(); return route.fulfill({ json: { ...saved, id: crypto.randomUUID() } }); }
    return route.fulfill({ json: [] });
  });
  await page.goto('/dashboard'); await page.getByRole('button', { name: '模型價格', exact: true }).click();
  const dialog = page.getByRole('dialog', { name: '模型價格版本' }); await expect(dialog).toBeVisible();
  await dialog.getByLabel('模型 ID', { exact: true }).fill('qwen3:8b');
  await chooseSelect(page, '計費供應商', 'ollama'); await chooseSelect(page, '費用類型', '本機內部成本');
  await dialog.getByLabel('每次呼叫固定費用', { exact: true }).fill('0.3');
  await dialog.getByRole('button', { name: '新增價格版本', exact: true }).click();
  await expect(dialog.getByRole('status')).toContainText('新價格版本已儲存');
  expect(saved).toMatchObject({ provider: 'ollama', modelId: 'qwen3:8b', currency: 'USD', kind: 'internal', perRequest: .3 });
  await page.screenshot({ animations: 'disabled', path: 'artifacts/screenshots/model-prices.png' });
  await page.setViewportSize({ width: 375, height: 812 });
  const bounds = await dialog.boundingBox(); expect(bounds!.x).toBeGreaterThanOrEqual(0); expect(bounds!.width).toBeLessThanOrEqual(375); expect(bounds!.y + bounds!.height).toBeLessThanOrEqual(812);
  await page.screenshot({ animations: 'disabled', path: 'artifacts/screenshots/model-prices-mobile.png' });
});

test('chat search is opt-in and cost/source panels stay accessible without clipping', async ({ page }) => {
  const fixture = new ApiFixture(); await fixture.attach(page);
  await page.route('**/api/v1/tools/web-search', route => route.fulfill({ json: { available: true, notice: '只送出本次提問。' } }));
  await page.route('**/api/v1/conversations/*/spend', route => route.fulfill({ json: { requests: 2, pendingCalls: 0, legacyCalls: 0,
    totals: [{ currency: 'USD', kind: 'api', amount: .0022, knownCalls: 2, unknownCalls: 0 }], models: [{ label: '本機測試模型', currency: 'USD', kind: 'api', amount: .0022, requests: 2, unknownCalls: 0, inputTokens: 100, outputTokens: 30 }] } }));
  await page.goto('/chat'); const search = page.getByRole('button', { name: '搜尋網路', exact: true });
  await expect(search).toHaveAttribute('aria-pressed', 'false'); await search.click();
  await page.getByRole('textbox', { name: '傳送訊息', exact: true }).fill('公開資料的最新消息');
  await page.getByRole('button', { name: '送出訊息', exact: true }).click();
  await expect.poll(() => fixture.lastRequest?.webSearch).toBe(true);
  await expect(page.getByRole('article', { name: 'AI 回覆', exact: true })).toBeVisible();
  const assistant = fixture.messages.find(x => x.role === 'assistant')!;
  assistant.charge = { state: 'metered', kind: 'api', currency: 'USD', amount: .0022, inputTokens: 100, cachedInputTokens: 40, outputTokens: 30, reasoningTokens: 5 };
  assistant.webSources = [{ number: 1, title: '可核對的網路來源', url: 'https://example.org/research', excerpt: '摘要', retrievedAt: new Date().toISOString() }];
  await page.reload();
  await expect(page.getByRole('link', { name: /可核對的網路來源/ })).toHaveAttribute('href', 'https://example.org/research');
  await page.getByRole('button', { name: '本次模型呼叫的費用與用量', exact: true }).click();
  const panel = page.getByRole('dialog', { name: '本次模型呼叫的費用與用量', exact: true }); await expect(panel).toContainText('40 個快取'); await expect(panel).toContainText('5 個思考');
  const box = await panel.boundingBox(); expect(box!.y).toBeGreaterThanOrEqual(0); expect(box!.y + box!.height).toBeLessThanOrEqual(1000);
  await page.keyboard.press('Escape'); await expect(panel).not.toBeVisible();
  await page.getByRole('button', { name: '檢視全對話費用', exact: true }).click();
  await expect(page.getByRole('dialog', { name: '檢視全對話費用', exact: true })).toContainText('全對話 · 2 次呼叫');
  await page.screenshot({ animations: 'disabled', path: 'artifacts/screenshots/chat-spend-search.png' });
  await page.setViewportSize({ width: 375, height: 812 });
  await expect.poll(() => page.evaluate(() => document.documentElement.scrollWidth <= innerWidth)).toBe(true);
  await expect(page.getByRole('button', { name: '搜尋網路', exact: true })).toBeVisible();
  const searchBox = await page.getByRole('button', { name: '搜尋網路', exact: true }).boundingBox();
  const contextBox = await page.locator('.context-trigger').boundingBox();
  expect(Math.abs(searchBox!.y - contextBox!.y)).toBeLessThanOrEqual(1);
  await page.keyboard.press('Escape');
  await page.getByRole('button', { name: '本次模型呼叫的費用與用量', exact: true }).click();
  const mobileBox = await panel.boundingBox(); expect(mobileBox!.x).toBeGreaterThanOrEqual(0); expect(mobileBox!.y + mobileBox!.height).toBeLessThanOrEqual(812);
  await page.screenshot({ animations: 'disabled', path: 'artifacts/screenshots/chat-spend-search-mobile.png' });
});

test('Gitea token is cleared after connecting, pinned files become drafts without sending automatically', async ({ page }) => {
  const fixture = new ApiFixture(); fixture.extraFeatures = [{ id: 'repositories', name: '程式庫', route: '/repositories' }, { id: 'knowledge', name: '知識庫', route: '/knowledge' }]; await fixture.attach(page);
  let connected = false; let tokenReceived = ''; const commit = 'a'.repeat(40);
  await page.route('**/api/v1/repositories**', route => {
    const url = new URL(route.request().url()); const path = url.pathname;
    if (path.endsWith('/connection')) { if (route.request().method() === 'POST') { tokenReceived = route.request().postDataJSON().token; connected = true; } return route.fulfill({ json: { available: true, connected, baseUrl: 'https://gitea.fixture/', login: connected ? 'fixture-user' : null, notice: '使用你的唯讀權限。' } }); }
    if (path.endsWith('/tree')) return route.fulfill({ json: { repository: 'hanglong/nexus', commit, path: '', entries: [{ name: 'README.md', path: 'README.md', kind: 'file', size: 20 }] } });
    if (path.endsWith('/file')) { expect(url.searchParams.get('commit')).toBe(commit); return route.fulfill({ json: { repository: 'hanglong/nexus', commit, path: 'README.md', text: '# 內部技術文件', url: `https://gitea.fixture/hanglong/nexus/src/commit/${commit}/README.md` } }); }
    return route.fulfill({ json: { items: [{ fullName: 'hanglong/nexus', description: '唯讀文件', private: true, defaultBranch: 'main', url: 'https://gitea.fixture/hanglong/nexus' }], page: 1, hasMore: false } });
  });
  await page.route('**/api/v1/knowledge/collections', route => route.fulfill({ json: [] }));
  await page.goto('/repositories'); await page.getByLabel('個人存取 token', { exact: true }).fill('fixtureReadOnlyToken00000000');
  await page.getByRole('button', { name: '連線 Gitea', exact: true }).click();
  await expect(page.locator('.repository-login')).toContainText('fixture-user'); expect(tokenReceived).toBe('fixtureReadOnlyToken00000000');
  await expect(page.locator('#gitea-token')).toHaveCount(0);
  await page.getByRole('button', { name: /hanglong\/nexus/ }).click(); await page.getByRole('button', { name: /README.md/ }).click();
  await expect(page.locator('.repository-file-text')).toContainText('內部技術文件');
  await settleEntrance(page); await page.screenshot({ animations: 'disabled', path: 'artifacts/screenshots/gitea-readonly.png' });
  await page.getByRole('button', { name: '帶入對話草稿', exact: true }).click();
  await expect(page.getByRole('textbox', { name: '傳送訊息', exact: true })).toHaveValue(/內部技術文件/);
  expect(fixture.posts).toBe(0); expect(fixture.conversations).toHaveLength(1);
});
