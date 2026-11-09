// 從 contracts/openapi.json 的 `x-public-errors`（後端 PublicErrorCatalog 的輸出）產生
// src/app/core/errors/public-error-catalog.ts。
import { readFile } from 'node:fs/promises';
import { fileURLToPath } from 'node:url';
import { emit, fail } from './emit-generated.mjs';

const contract = new URL('../../contracts/openapi.json', import.meta.url);
const target = fileURLToPath(new URL('../src/app/core/errors/public-error-catalog.ts', import.meta.url));

const hints = JSON.parse(await readFile(contract, 'utf8'))['x-public-errors'];
if (!hints || !Object.keys(hints).length) fail('contracts/openapi.json 沒有 x-public-errors。');

await emit(
  target,
  `/**
 * 由 frontend/scripts/generate-public-errors.mjs 從 contracts/openapi.json 的 \`x-public-errors\` 產生
 * （\`npm run contracts\`），不要手改；來源是後端 \`PublicErrorCatalog\`。
 */
export const publicErrorHints: Readonly<Record<string, string>> = ${JSON.stringify(hints)};
`,
);
