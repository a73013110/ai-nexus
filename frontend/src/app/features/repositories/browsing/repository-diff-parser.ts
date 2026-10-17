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
