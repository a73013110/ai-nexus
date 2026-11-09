// 合約產生物共用的輸出：先經 prettier（讀專案的 .prettierrc）排版，所以產生物本身就通過 format:check，不必列入 .prettierignore。
// `--check` 只比對、不寫檔，不一致時以非零結束，給 contracts:check 用。
import { readFile, writeFile } from 'node:fs/promises';
import prettier from 'prettier';

export const fail = (message) => {
  console.error(message);
  process.exit(1);
};

export async function emit(target, source) {
  const name = target.split(/[\\/]/).pop();
  const options = { ...(await prettier.resolveConfig(target)), filepath: target };
  const formatted = await prettier.format(source, options);
  if (!process.argv.includes('--check')) {
    await writeFile(target, formatted);
    console.log(`已產生 ${name}`);
    return;
  }
  let current = '';
  try {
    current = await readFile(target, 'utf8');
  } catch {
    /* 沒有檔案就是不一致。 */
  }
  if (current.replace(/\r\n/g, '\n') !== formatted)
    fail(`${name} 與 contracts/openapi.json 不一致，請執行 npm --prefix frontend run contracts。`);
  console.log(`${name} 與 contracts/openapi.json 一致。`);
}
