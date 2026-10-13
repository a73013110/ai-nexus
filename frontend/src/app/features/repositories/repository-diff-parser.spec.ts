import { describe, expect, it } from 'vitest';
import { parseDiff } from './repository-diff-parser';

describe('parseDiff', () => {
  it('numbers old and new lines across a hunk', () => {
    const [file] = parseDiff(
      [
        'diff --git a/src/app.ts b/src/app.ts',
        'index 1111111..2222222 100644',
        '--- a/src/app.ts',
        '+++ b/src/app.ts',
        '@@ -3,4 +3,4 @@ export class App {',
        ' keep',
        '-old',
        '+new',
        '',
        ' tail',
        '',
      ].join('\n'),
    );
    expect(file).toMatchObject({
      path: 'src/app.ts',
      status: 'modified',
      additions: 1,
      deletions: 1,
    });
    expect(file.hunks[0].header).toBe('export class App {');
    expect(file.hunks[0].lines.map((x) => [x.kind, x.old, x.new])).toEqual([
      ['ctx', 3, 3],
      ['del', 4, null],
      ['add', null, 4],
      ['ctx', 5, 5],
      ['ctx', 6, 6],
    ]);
  });

  it('reads added, deleted, renamed and binary files', () => {
    const files = parseDiff(
      [
        'diff --git a/new.md b/new.md',
        'new file mode 100644',
        '--- /dev/null',
        '+++ b/new.md',
        '@@ -0,0 +1 @@',
        '+hello',
        '\\ No newline at end of file',
        'diff --git a/gone.txt b/gone.txt',
        'deleted file mode 100644',
        '--- a/gone.txt',
        '+++ /dev/null',
        '@@ -1 +0,0 @@',
        '-bye',
        'diff --git a/old name.txt b/new name.txt',
        'similarity index 100%',
        'rename from old name.txt',
        'rename to new name.txt',
        'diff --git a/logo.png b/logo.png',
        'Binary files a/logo.png and b/logo.png differ',
      ].join('\n'),
    );
    expect(files.map((x) => [x.status, x.oldPath, x.path, x.binary])).toEqual([
      ['added', 'new.md', 'new.md', false],
      ['deleted', 'gone.txt', 'gone.txt', false],
      ['renamed', 'old name.txt', 'new name.txt', false],
      ['modified', 'logo.png', 'logo.png', true],
    ]);
    expect(files[0].hunks[0].lines.at(-1)).toMatchObject({ kind: 'note' });
  });

  it('keeps lines that look like headers inside a hunk', () => {
    const [file] = parseDiff(
      [
        'diff --git a/a.md b/a.md',
        '--- a/a.md',
        '+++ b/a.md',
        '@@ -1,1 +1,1 @@',
        '--- x',
        '+++ y',
      ].join('\n'),
    );
    expect(file.path).toBe('a.md');
    expect(file.hunks[0].lines.map((x) => x.kind)).toEqual(['del', 'add']);
  });

  it('decodes quoted non-ASCII paths', () => {
    const [file] = parseDiff(
      'diff --git "a/\\346\\226\\207\\344\\273\\266.md" "b/\\346\\226\\207\\344\\273\\266.md"\n',
    );
    expect(file.path).toBe('文件.md');
  });
});
