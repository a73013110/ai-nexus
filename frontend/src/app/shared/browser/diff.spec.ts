import { describe, expect, it } from 'vitest';
import { diffLines, parseDiff, sideBySide } from './diff';

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
    expect(file.hunks[0].lines.map((item2) => [item2.kind, item2.old, item2.new])).toEqual([
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
    expect(files.map((item3) => [item3.status, item3.oldPath, item3.path, item3.binary])).toEqual([
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
    expect(file.hunks[0].lines.map((item) => item.kind)).toEqual(['del', 'add']);
  });

  it('decodes quoted non-ASCII paths', () => {
    const [file] = parseDiff(
      'diff --git "a/\\346\\226\\207\\344\\273\\266.md" "b/\\346\\226\\207\\344\\273\\266.md"\n',
    );
    expect(file.path).toBe('文件.md');
  });
});

describe('diffLines', () => {
  const kinds = (before: string, after: string) =>
    diffLines(before, after).map(
      (line) => `${line.kind} ${line.old ?? '-'} ${line.new ?? '-'} ${line.text}`,
    );

  it('keeps shared lines as context and numbers both sides', () => {
    expect(kinds('a\nb\nc\nd', 'a\nB\nc\nd\ne')).toEqual([
      'ctx 1 1 a',
      'del 2 - b',
      'add - 2 B',
      'ctx 3 3 c',
      'ctx 4 4 d',
      'add - 5 e',
    ]);
  });

  it('ignores carriage returns and a trailing newline', () => {
    expect(diffLines('a\r\nb\r\n', 'a\nb').every((line) => line.kind === 'ctx')).toBe(true);
  });

  it('treats a missing side as entirely added or deleted', () => {
    expect(kinds('', 'x\ny')).toEqual(['add - 1 x', 'add - 2 y']);
    expect(kinds('x', '')).toEqual(['del 1 - x']);
  });

  it('finds the shortest edit inside the changed middle', () => {
    const before = ['select', 'from t', 'where a = 1', 'and b = 2', 'order by c'].join('\n');
    const after = ['select', 'from t', '-- fix', 'where a = 1', 'order by c'].join('\n');
    expect(diffLines(before, after).filter((line) => line.kind !== 'ctx')).toEqual([
      { kind: 'add', text: '-- fix', old: null, new: 3 },
      { kind: 'del', text: 'and b = 2', old: 4, new: null },
    ]);
  });
});

describe('sideBySide', () => {
  const show = (rows: ReturnType<typeof sideBySide>) =>
    rows.map((row) =>
      row.skipped !== undefined
        ? `… ${row.skipped}`
        : `${row.left?.text ?? '_'} | ${row.right?.text ?? '_'}`,
    );

  it('pairs a run of deletions with the additions that replace it', () => {
    expect(show(sideBySide(diffLines('a\nb\nc\nz', 'a\nB\nC\nD\nz')))).toEqual([
      'a | a',
      'b | B',
      'c | C',
      '_ | D',
      'z | z',
    ]);
  });

  it('folds unchanged lines far from any change', () => {
    const before = ['1', '2', '3', '4', '5', '6', '7', '8'].join('\n');
    const after = before.replace('7', 'seven');
    expect(show(sideBySide(diffLines(before, after), 2))).toEqual([
      '… 4',
      '5 | 5',
      '6 | 6',
      '7 | seven',
      '8 | 8',
    ]);
  });
});
