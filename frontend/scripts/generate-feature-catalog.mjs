// 從 contracts/openapi.json 的 `x-feature-catalog`（後端 FeatureCatalog 的輸出）產生
// src/app/core/api/feature-catalog.ts。`--check` 只比對、不寫檔，不一致時以非零結束，給 contracts:check 用。
// 輸出先經 prettier（讀專案的 .prettierrc）排版，所以產生物本身就通過 format:check，不必列入 .prettierignore。
import { readFile, writeFile } from 'node:fs/promises';
import { fileURLToPath } from 'node:url';
import prettier from 'prettier';

const root = fileURLToPath(new URL('..', import.meta.url));
const contract = new URL('../../contracts/openapi.json', import.meta.url);
const target = fileURLToPath(new URL('../src/app/core/api/feature-catalog.ts', import.meta.url));
const check = process.argv.includes('--check');

const document = JSON.parse(await readFile(contract, 'utf8'));
const catalog = document['x-feature-catalog'];
if (!catalog?.areas?.length || !catalog?.modules?.length || !catalog?.features?.length)
  fail('contracts/openapi.json 沒有 x-feature-catalog（areas、modules、features）。');

if (!catalog.scopes?.length) fail('contracts/openapi.json 的 x-feature-catalog 沒有 scopes（資料範圍）。');
const kinds = new Set(['page', 'action', 'reader']);
const scopes = new Set(catalog.scopes.map((scope) => scope.id));
const areas = new Set(catalog.areas.map((area) => area.id));
const modules = new Set(catalog.modules.map((module) => module.id));
for (const module of catalog.modules)
  if (!areas.has(module.area)) fail(`模組 ${module.id} 的區 ${module.area} 不在目錄中。`);
for (const feature of catalog.features) {
  if (!kinds.has(feature.kind)) fail(`功能 ${feature.id} 的 kind ${feature.kind} 不認得。`);
  if (!modules.has(feature.module)) fail(`功能 ${feature.id} 的模組 ${feature.module} 不在目錄中。`);
  if ((feature.kind === 'action') !== (feature.route == null))
    fail(`功能 ${feature.id}：只有 action 沒有網址。`);
  for (const scope of feature.scopes ?? [])
    if (!scopes.has(scope)) fail(`功能 ${feature.id} 的範圍 ${scope} 不在目錄中。`);
  if (feature.managerScope != null && !(feature.scopes ?? []).includes(feature.managerScope))
    fail(`功能 ${feature.id} 的主管範圍 ${feature.managerScope} 不在它支援的範圍內。`);
}

const pick = (items, keys) => items.map((item) => Object.fromEntries(keys.map((key) => [key, item[key] ?? null])));
const source = `/**
 * 由 frontend/scripts/generate-feature-catalog.mjs 從 contracts/openapi.json 的 \`x-feature-catalog\` 產生
 * （\`npm run contracts\`），不要手改；來源是後端 \`FeatureCatalog\`。
 */

export const CATALOG_AREAS = ${JSON.stringify(pick(catalog.areas, ['id', 'name']))} as const;

export const CATALOG_MODULES = ${JSON.stringify(pick(catalog.modules, ['id', 'area', 'name']))} as const;

export const CATALOG_FEATURES = ${JSON.stringify(
  pick(catalog.features, ['id', 'kind', 'module', 'name', 'icon', 'route', 'scopes', 'managerScope']),
)} as const;

/** 資料範圍（ADR 0017），由窄到寬；功能的 \`scopes\` 是它支援的子集，空的表示授權不帶範圍；\`managerScope\` 是主管自動取得的範圍（null＝沒有）。 */
export const CATALOG_SCOPES = ${JSON.stringify(pick(catalog.scopes, ['id', 'name']))} as const;

export type CatalogArea = (typeof CATALOG_AREAS)[number];
export type AreaId = CatalogArea['id'];
export type CatalogModule = (typeof CATALOG_MODULES)[number];
export type ModuleId = CatalogModule['id'];
export type CatalogFeature = (typeof CATALOG_FEATURES)[number];
export type CatalogId = CatalogFeature['id'];
export type CatalogKind = CatalogFeature['kind'];
export type CatalogScope = (typeof CATALOG_SCOPES)[number];
export type DataScopeId = CatalogScope['id'];
`;

const options = { ...(await prettier.resolveConfig(target)), filepath: target };
const formatted = await prettier.format(source, options);

if (check) {
  let current = '';
  try {
    current = await readFile(target, 'utf8');
  } catch {
    /* 沒有檔案就是不一致。 */
  }
  if (current.replace(/\r\n/g, '\n') !== formatted)
    fail('feature-catalog.ts 與 contracts/openapi.json 不一致，請執行 npm --prefix frontend run contracts。');
  console.log('feature-catalog.ts 與 contracts/openapi.json 一致。');
} else {
  await writeFile(target, formatted);
  console.log(`已產生 ${target.slice(root.length)}`);
}

function fail(message) {
  console.error(message);
  process.exit(1);
}
