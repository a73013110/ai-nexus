import { describe, expect, it } from 'vitest';
import type { DirectoryOutcomeDto, DirectoryPersonDto } from '@core/api/schema';
import {
  DIRECTORY_BATCH_LIMIT,
  type DirectorySelection,
  changedFields,
  describeChange,
  fieldChanges,
  groupOf,
  outcomeName,
  outcomeNote,
  selectPeople,
  selectable,
  selectionState,
  stateLabel,
  summarize,
  tally,
  withinDn,
} from './directory-outcomes';

const person = (sid: string, state = 'new', extra: Partial<DirectoryPersonDto> = {}) =>
  ({
    sid,
    dn: `CN=${sid},OU=研發處,DC=fixture,DC=internal`,
    account: sid.toLowerCase(),
    upn: `${sid.toLowerCase()}@fixture.internal`,
    displayName: `人員 ${sid}`,
    email: null,
    department: null,
    title: null,
    path: '',
    disabled: false,
    whenChanged: null,
    lastLogon: null,
    state,
    userId: null,
    changes: [],
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
  it('accumulates across nodes and keeps what each person looked like when picked', () => {
    let selection: DirectorySelection = new Map();
    selection = selectPeople(selection, [person('A'), person('B', 'synced')], true).selection;
    selection = selectPeople(selection, [person('C', 'prebuilt'), person('A')], true).selection;
    expect([...selection.keys()]).toEqual(['A', 'B', 'C']);
    selection = selectPeople(selection, [person('B', 'synced')], false).selection;
    expect([...selection.keys()]).toEqual(['A', 'C']);
  });

  it('never picks people the server would only skip', () => {
    const blocked = ['conflict', 'invalid_account', 'deleted'];
    for (const state of blocked) expect(selectable(person('X', state)), state).toBe(false);
    for (const state of ['new', 'prebuilt', 'changed', 'synced'])
      expect(selectable(person('X', state)), state).toBe(true);

    const results = [person('A'), person('X', 'conflict'), person('D', 'deleted')];
    const { selection } = selectPeople(new Map(), results, true);
    expect([...selection.keys()]).toEqual(['A']);
  });

  it('reports whether the selectable people in a list are all, some or none picked', () => {
    const list = [person('A'), person('B', 'changed'), person('X', 'conflict')];
    expect(selectionState(new Map(), list)).toBe('none');
    const one = selectPeople(new Map(), [list[0]], true).selection;
    expect(selectionState(one, list)).toBe('some');
    // 無法勾選的人不算在分母，否則表頭永遠到不了「全部」。
    const both = selectPeople(one, list, true).selection;
    expect(selectionState(both, list)).toBe('all');
    expect(selectionState(both, [person('X', 'conflict')])).toBe('none');
    expect(selectionState(both, [])).toBe('none');
  });

  it('stops at the batch limit and reports how many were left out', () => {
    const many = Array.from({ length: DIRECTORY_BATCH_LIMIT + 3 }, (_, i) => person(`S${i}`));
    const { selection, skipped } = selectPeople(new Map(), many, true);
    expect(selection.size).toBe(DIRECTORY_BATCH_LIMIT);
    expect(skipped).toBe(3);
    // 已在選取中的人重勾不算超額。
    expect(selectPeople(selection, [many[0]], true).skipped).toBe(0);
  });
});

describe('directory person state', () => {
  it('groups people the way the filter and the basket do', () => {
    expect(groupOf(person('A', 'new'))).toBe('new');
    expect(groupOf(person('A', 'prebuilt'))).toBe('new');
    expect(groupOf(person('A', 'changed'))).toBe('changed');
    expect(groupOf(person('A', 'synced'))).toBe('synced');
    for (const state of ['conflict', 'invalid_account', 'deleted'])
      expect(groupOf(person('A', state)), state).toBe('blocked');
  });

  it('labels a changed person by how many fields differ', () => {
    const changes = [
      { field: 'displayName', before: 'Carol', after: '林可蘿' },
      { field: 'email', before: null, after: 'carol@example.test' },
    ];
    expect(stateLabel(person('A', 'changed', { changes }))).toBe('2 欄異動');
    expect(stateLabel(person('A', 'changed'))).toBe('有異動');
    expect(stateLabel(person('A', 'synced'))).toBe('已同步');
    expect(stateLabel(person('A', 'something_new'))).toBe('something_new');
    expect(changedFields(changes)).toBe('姓名、通知信箱');
  });

  it('matches a DN against an ancestor regardless of case', () => {
    const dn = 'CN=林可蘿,OU=平台部,OU=研發處,DC=fixture,DC=internal';
    expect(withinDn(dn, 'ou=研發處,dc=FIXTURE,dc=internal')).toBe(true);
    expect(withinDn(dn, dn.toUpperCase())).toBe(true);
    expect(withinDn(dn, 'OU=業務處,DC=fixture,DC=internal')).toBe(false);
    // 只比對完整 RDN：名稱結尾相同的兄弟 OU 不算祖先。
    expect(
      withinDn('CN=吳大衛,OU=新研發處,DC=fixture,DC=internal', 'OU=研發處,DC=fixture,DC=internal'),
    ).toBe(false);
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

  it('counts binding as created and everything that does not write as skipped', () => {
    expect(
      tally([
        outcome('A', 'create'),
        outcome('B', 'bind'),
        outcome('C', 'update'),
        outcome('D', 'unchanged'),
        outcome('E', 'disabled'),
        outcome('F', 'failed'),
      ]),
    ).toEqual({ created: 2, updated: 1, skipped: 3 });
    expect(tally([])).toEqual({ created: 0, updated: 0, skipped: 0 });
  });

  it('turns server changes into labelled rows for the change list', () => {
    expect(
      fieldChanges([
        { field: 'displayName', before: 'Carol', after: '林可蘿' },
        { field: 'adAccount', before: null, after: 'carol.lin' },
        { field: 'mystery', before: 'a', after: null },
      ]),
    ).toEqual([
      { label: '姓名', before: 'Carol', after: '林可蘿' },
      { label: 'AD 帳號', before: null, after: 'carol.lin' },
      { label: 'mystery', before: 'a', after: null },
    ]);
  });

  it('names people the directory no longer has from the selection and flags what needs a decision', () => {
    const known = new Map([['A', person('A', 'new', { displayName: '林可蘿', account: 'carol' })]]);
    expect(outcomeName(outcome('A', 'not_found', { displayName: '', account: '' }), known)).toEqual(
      { displayName: '林可蘿', account: 'carol' },
    );
    expect(outcomeName(outcome('Z', 'not_found', { displayName: '', account: '' }))).toEqual({
      displayName: 'Z',
      account: '',
    });
    expect(
      outcomeNote(outcome('A', 'unchanged', { directoryDisabled: true, enabled: true })),
    ).toContain('平台帳號仍啟用');
    expect(outcomeNote(outcome('A', 'failed'))).toContain('重新預覽');
    expect(outcomeNote(outcome('A', 'update'))).toBe('');
  });
});
