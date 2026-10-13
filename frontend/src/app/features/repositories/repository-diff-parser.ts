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

/** Splits a git unified diff into files, hunks and numbered lines. */
export function parseDiff(diff: string): DiffFile[] {
  const files: DiffFile[] = [];
  let file: DiffFile | null = null,
    hunk: DiffHunk | null = null,
    oldLine = 0,
    newLine = 0,
    // Lines the hunk header promised; a blank line inside a hunk is context whose space was trimmed.
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
  // `a/x b/x`: the same path twice, so the middle is found by length when names contain spaces.
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

/** Git quotes paths with special characters and writes non-ASCII bytes as octal escapes. */
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
  for (let i = 0; i < body.length; i++) {
    const char = body[i];
    if (char !== '\\') {
      bytes.push(...new TextEncoder().encode(char));
      continue;
    }
    const octal = /^[0-7]{3}/.exec(body.slice(i + 1));
    if (octal) {
      bytes.push(parseInt(octal[0], 8));
      i += 3;
    } else {
      bytes.push(escapes[body[i + 1]] ?? body.charCodeAt(i + 1));
      i++;
    }
  }
  return new TextDecoder().decode(new Uint8Array(bytes));
}
