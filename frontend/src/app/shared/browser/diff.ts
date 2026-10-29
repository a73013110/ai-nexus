export type DiffStatus = 'added' | 'deleted' | 'renamed' | 'modified';

export interface DiffLine {
  kind: 'add' | 'del' | 'ctx' | 'note';
  text: string;
  old: number | null;
  new: number | null;
}

export interface DiffHunk {
  header: string;
  lines: DiffLine[];
}

export interface DiffFile {
  path: string;
  oldPath: string;
  status: DiffStatus;
  binary: boolean;
  additions: number;
  deletions: number;
  hunks: DiffHunk[];
}

const hunkHeader = /^@@ -(\d+)(?:,(\d+))? \+(\d+)(?:,(\d+))? @@ ?(.*)$/;

/** 把 git 的 unified diff 拆成檔案、hunk 與帶行號的行。 */
export function parseDiff(diff: string): DiffFile[] {
  const files: DiffFile[] = [];
  let file: DiffFile | null = null,
    hunk: DiffHunk | null = null,
    oldLine = 0,
    newLine = 0,
    // hunk 標頭承諾的行數；hunk 內的空白行是被修剪掉空格的上下文行。
    oldLeft = 0,
    newLeft = 0;
  for (const raw of diff.split('\n')) {
    const line = raw.endsWith('\r') ? raw.slice(0, -1) : raw;
    if (line.startsWith('diff --git ')) {
      const [oldPath, path] = headerPaths(line.slice(11));
      file = {
        path,
        oldPath,
        status: 'modified',
        binary: false,
        additions: 0,
        deletions: 0,
        hunks: [],
      };
      files.push(file);
      hunk = null;
      continue;
    }
    if (!file) continue;
    if (hunk && (oldLeft > 0 || newLeft > 0)) {
      const mark = line[0];
      if (mark === '+') {
        hunk.lines.push({ kind: 'add', text: line.slice(1), old: null, new: newLine++ });
        file.additions++;
        newLeft--;
        continue;
      }
      if (mark === '-') {
        hunk.lines.push({ kind: 'del', text: line.slice(1), old: oldLine++, new: null });
        file.deletions++;
        oldLeft--;
        continue;
      }
      if (mark === ' ' || line === '') {
        hunk.lines.push({ kind: 'ctx', text: line.slice(1), old: oldLine++, new: newLine++ });
        oldLeft--;
        newLeft--;
        continue;
      }
    }
    if (hunk && line.startsWith('\\')) {
      hunk.lines.push({ kind: 'note', text: line.slice(2), old: null, new: null });
      continue;
    }
    const header = hunkHeader.exec(line);
    if (header) {
      oldLine = Number(header[1]);
      oldLeft = Number(header[2] ?? 1);
      newLine = Number(header[3]);
      newLeft = Number(header[4] ?? 1);
      hunk = { header: header[5] ?? '', lines: [] };
      file.hunks.push(hunk);
      continue;
    }
    hunk = null;
    if (line.startsWith('new file mode')) file.status = 'added';
    else if (line.startsWith('deleted file mode')) file.status = 'deleted';
    else if (line.startsWith('rename from ')) {
      file.oldPath = unquote(line.slice(12));
      file.status = 'renamed';
    } else if (line.startsWith('rename to ')) file.path = unquote(line.slice(10));
    else if (line.startsWith('--- ') && line !== '--- /dev/null')
      file.oldPath = side(line.slice(4));
    else if (line.startsWith('+++ ') && line !== '+++ /dev/null') file.path = side(line.slice(4));
    else if (line.startsWith('Binary files ') || line === 'GIT binary patch') file.binary = true;
  }
  for (const each of files) {
    if (each.status === 'added') each.oldPath = each.path;
    if (each.status === 'deleted') each.path = each.oldPath;
  }
  return files;
}

function headerPaths(rest: string): [string, string] {
  if (rest.startsWith('"')) {
    const match = /^("(?:[^"\\]|\\.)*") ("(?:[^"\\]|\\.)*"|\S.*)$/.exec(rest);
    if (match) return [side(match[1]), side(match[2])];
  }
  // `a/x b/x`：同一路徑出現兩次，因此中間的部分要依長度找出，才能處理含空白的名稱。
  const half = (rest.length - 1) / 2;
  if (Number.isInteger(half) && rest.slice(2, half) === rest.slice(half + 3))
    return [rest.slice(2, half), rest.slice(half + 3)];
  const match = /^a\/(.+) b\/(.+)$/.exec(rest);
  return match ? [match[1], match[2]] : [rest, rest];
}

function side(value: string) {
  const path = unquote(value.replace(/\t.*$/, ''));
  return /^[ab]\//.test(path) ? path.slice(2) : path;
}

/** Git 會為含特殊字元的路徑加引號，並把非 ASCII 位元組寫成八進位跳脫。 */
function unquote(value: string) {
  if (!value.startsWith('"') || !value.endsWith('"')) return value;
  const bytes: number[] = [];
  const body = value.slice(1, -1);
  const escapes: Record<string, number> = {
    n: 10,
    t: 9,
    '"': 34,
    '\\': 92,
    a: 7,
    b: 8,
    f: 12,
    r: 13,
    v: 11,
  };
  for (let index = 0; index < body.length; index++) {
    const char = body[index];
    if (char !== '\\') {
      bytes.push(...new TextEncoder().encode(char));
      continue;
    }
    const octal = /^[0-7]{3}/.exec(body.slice(index + 1));
    if (octal) {
      bytes.push(parseInt(octal[0], 8));
      index += 3;
    } else {
      bytes.push(escapes[body[index + 1]] ?? body.charCodeAt(index + 1));
      index++;
    }
  }
  return new TextDecoder().decode(new Uint8Array(bytes));
}

/** 差異超過這麼多行時不再找最短編輯，整段當成刪除再新增：Myers 的記憶體隨差異行數平方成長。 */
const maxEdits = 2000;

/**
 * 兩段全文逐行比對（Myers），輸出與 `parseDiff` 同樣的行：前後相同的部分先切掉，只比中間。
 * 行尾的 `\r` 不算差異，避免同一份定義只因換行字元不同而整份標成修改。
 */
export function diffLines(before: string, after: string): DiffLine[] {
  const a = splitLines(before),
    b = splitLines(after);
  let start = 0;
  while (start < a.length && start < b.length && a[start] === b[start]) start++;
  let endA = a.length,
    endB = b.length;
  while (endA > start && endB > start && a[endA - 1] === b[endB - 1]) {
    endA--;
    endB--;
  }
  const lines: DiffLine[] = [];
  for (let index = 0; index < start; index++)
    lines.push({ kind: 'ctx', text: a[index], old: index + 1, new: index + 1 });
  for (const step of middle(a.slice(start, endA), b.slice(start, endB))) {
    const old = step.old === null ? null : step.old + start + 1;
    const next = step.new === null ? null : step.new + start + 1;
    lines.push({ kind: step.kind, text: step.text, old, new: next });
  }
  for (let offset = 0; offset < a.length - endA; offset++)
    lines.push({
      kind: 'ctx',
      text: a[endA + offset],
      old: endA + offset + 1,
      new: endB + offset + 1,
    });
  return lines;
}

function splitLines(text: string) {
  if (!text) return [];
  const lines = text.split('\n').map((line) => (line.endsWith('\r') ? line.slice(0, -1) : line));
  if (lines.at(-1) === '') lines.pop();
  return lines;
}

/** 回傳的行號從 0 起算、相對於傳入的片段。 */
function middle(a: string[], b: string[]): DiffLine[] {
  const replace = (): DiffLine[] => [
    ...a.map((text, index) => ({ kind: 'del' as const, text, old: index, new: null })),
    ...b.map((text, index) => ({ kind: 'add' as const, text, old: null, new: index })),
  ];
  if (!a.length || !b.length) return replace();
  const max = a.length + b.length,
    offset = max + 1,
    limit = Math.min(max, maxEdits);
  const v = new Int32Array(2 * max + 3);
  const trace: Int32Array[] = [];
  let found = -1;
  for (let d = 0; d <= limit && found < 0; d++) {
    trace.push(v.slice());
    for (let k = -d; k <= d; k += 2) {
      let x =
        k === -d || (k !== d && v[offset + k - 1] < v[offset + k + 1])
          ? v[offset + k + 1]
          : v[offset + k - 1] + 1;
      let y = x - k;
      while (x < a.length && y < b.length && a[x] === b[y]) {
        x++;
        y++;
      }
      v[offset + k] = x;
      if (x >= a.length && y >= b.length) {
        found = d;
        break;
      }
    }
  }
  if (found < 0) return replace();
  // 從終點沿著每一步的 V 往回走，得到反向的編輯序列。
  const steps: DiffLine[] = [];
  let x = a.length,
    y = b.length;
  for (let d = found; d > 0; d--) {
    const previous = trace[d];
    const k = x - y;
    const down = k === -d || (k !== d && previous[offset + k - 1] < previous[offset + k + 1]);
    const prevK = down ? k + 1 : k - 1;
    const prevX = previous[offset + prevK],
      prevY = prevX - prevK;
    while (x > prevX && y > prevY) {
      x--;
      y--;
      steps.push({ kind: 'ctx', text: a[x], old: x, new: y });
    }
    if (down) {
      y--;
      steps.push({ kind: 'add', text: b[y], old: null, new: y });
    } else {
      x--;
      steps.push({ kind: 'del', text: a[x], old: x, new: null });
    }
  }
  while (x > 0 && y > 0) {
    x--;
    y--;
    steps.push({ kind: 'ctx', text: a[x], old: x, new: y });
  }
  return steps.reverse();
}

/** 並排差異的一列：左邊是舊的、右邊是新的，只有一邊有內容時另一邊為 null；`skipped` 是收起的相同行數。 */
export type DiffRow =
  { left: DiffLine | null; right: DiffLine | null; skipped?: undefined } | { skipped: number };

/**
 * 把 `diffLines` 的結果排成並排的列：相連的刪除與新增一對一配在同一列，多的那邊留白。
 * 離變更超過 `context` 行的相同內容收成一列 `skipped`，長的 procedure 才不必捲過整份沒變的本文。
 */
export function sideBySide(lines: readonly DiffLine[], context = Infinity): DiffRow[] {
  const changed = lines.map((line) => line.kind === 'add' || line.kind === 'del');
  const near = new Uint8Array(lines.length);
  changed.forEach((isChanged, index) => {
    if (!isChanged) return;
    const from = Math.max(0, index - context),
      to = Math.min(lines.length - 1, index + context);
    for (let at = from; at <= to; at++) near[at] = 1;
  });
  const rows: DiffRow[] = [];
  let deleted: DiffLine[] = [],
    added: DiffLine[] = [];
  const flush = () => {
    for (let index = 0; index < Math.max(deleted.length, added.length); index++)
      rows.push({ left: deleted[index] ?? null, right: added[index] ?? null });
    deleted = [];
    added = [];
  };
  lines.forEach((line, index) => {
    if (line.kind === 'del') deleted.push(line);
    else if (line.kind === 'add') added.push(line);
    else {
      flush();
      const last = rows.at(-1);
      if (context === Infinity || near[index]) rows.push({ left: line, right: line });
      else if (last?.skipped !== undefined) last.skipped++;
      else rows.push({ skipped: 1 });
    }
  });
  flush();
  return rows;
}
