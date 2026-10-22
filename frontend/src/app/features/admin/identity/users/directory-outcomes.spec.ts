import { describe, expect, it } from 'vitest';
import type { DirectoryOutcomeDto, DirectoryPersonDto } from '@core/api/schema';
import {
  DIRECTORY_BATCH_LIMIT,
  type DirectorySelection,
  describeChange,
  outcomeName,
  outcomeNote,
  selectPeople,
  selectsAll,
  summarize,
} from './directory-outcomes';

const person = (sid: string, state = 'new', extra: Partial<DirectoryPersonDto> = {}) =>
  ({
    sid,
    account: sid.toLowerCase(),
    displayName: `人員 ${sid}`,
    email: null,
    department: null,
    title: null,
    path: '',
    disabled: false,
    state,
    userId: null,
    ...extra,
  }) satisfies DirectoryPersonDto;

const outcome = (sid: string, value: string, extra: Partial<DirectoryOutcomeDto> = {}) =>
  ({
    sid,
    userId: null,
    account: sid.toLowerCase(),
    displayName: `人員 ${sid}`,
    email: null,
    path: '',
    directoryDisabled: false,
    enabled: null,
    outcome: value,
    changes: [],
    errorCode: null,
    ...extra,
  }) satisfies DirectoryOutcomeDto;

describe('directory selection', () => {
  it('accumulates across searches and keeps what each person looked like when picked', () => {
    let selection: DirectorySelection = new Map();
    selection = selectPeople(selection, [person('A'), person('B', 'linked')], true).selection;
    selection = selectPeople(selection, [person('C', 'prebuilt'), person('A')], true).selection;
    expect([...selection.keys()]).toEqual(['A', 'B', 'C']);
    selection = selectPeople(selection, [person('B', 'linked')], false).selection;
    expect([...selection.keys()]).toEqual(['A', 'C']);
  });

  it('never picks people the server would only skip', () => {
    const results = [person('A'), person('X', 'conflict'), person('D', 'deleted')];
    const { selection } = selectPeople(new Map(), results, true);
    expect([...selection.keys()]).toEqual(['A']);
    expect(selectsAll(selection, results)).toBe(true);
    expect(selectsAll(selection, [person('X', 'conflict')])).toBe(false);
  });

  it('stops at the batch limit and reports how many were left out', () => {
    const many = Array.from({ length: DIRECTORY_BATCH_LIMIT + 3 }, (_, i) => person(`S${i}`));
    const { selection, skipped } = selectPeople(new Map(), many, true);
    expect(selection.size).toBe(DIRECTORY_BATCH_LIMIT);
    expect(skipped).toBe(3);
  });
});

describe('directory outcomes', () => {
  it('summarizes counts in a fixed order and describes each change', () => {
    expect(
      summarize([outcome('A', 'update'), outcome('B', 'create'), outcome('C', 'create')]),
    ).toBe('新建 2、更新 1');
    expect(describeChange({ field: 'displayName', before: '林可蘿', after: '王可蘿' })).toBe(
      '姓名：林可蘿 → 王可蘿',
    );
    expect(describeChange({ field: 'email', before: null, after: 'carol@example.test' })).toBe(
      '通知信箱：（無） → carol@example.test',
    );
  });

  it('names people the directory no longer has from the selection and flags what needs a decision', () => {
    const known = new Map([['A', person('A', 'new', { displayName: '林可蘿', account: 'carol' })]]);
    expect(outcomeName(outcome('A', 'not_found', { displayName: '', account: '' }), known)).toEqual(
      { displayName: '林可蘿', account: 'carol' },
    );
    expect(
      outcomeNote(outcome('A', 'unchanged', { directoryDisabled: true, enabled: true })),
    ).toContain('平台帳號仍啟用');
    expect(outcomeNote(outcome('A', 'update'))).toBe('');
  });
});
