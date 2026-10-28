import { describe, expect, it } from 'vitest';
import type { UnitImportChangeDto, UnitImportPreviewDto } from '@core/api/schema';
import {
  changeNote,
  hasChanges,
  importChoices,
  importCounts,
  importDepartments,
  resultSummary,
} from './unit-import-presentation';

const change = (overrides: Partial<UnitImportChangeDto>): UnitImportChangeDto => ({
  gdDepNo: 2,
  unitId: 'sales',
  action: 'Update',
  path: ['總公司', '業務一部'],
  previousPath: null,
  enabled: true,
  fields: [],
  ...overrides,
});

const preview = (overrides: Partial<UnitImportPreviewDto> = {}): UnitImportPreviewDto => ({
  fingerprint: 'f',
  sourceUnits: 3,
  units: [],
  managers: [],
  issues: [],
  ...overrides,
});

const manager = {
  gdDepNo: 2,
  unitId: 'sales',
  path: ['總公司', '業務一部'],
  userId: 'bob',
  account: 'bob',
  displayName: '王小明',
  added: true,
  deputy: false,
  joinsUnit: true,
};

describe('unit import presentation', () => {
  it('lists one choice per department and leaves re-enabling a locally disabled unit unchecked', () => {
    const departments = importDepartments(
      preview({
        units: [
          change({ gdDepNo: 3, action: 'Disable', path: ['總公司', '倉儲部'], enabled: false }),
          change({ gdDepNo: 2, fields: ['Enabled'] }),
          change({ gdDepNo: 4, action: 'Create', path: ['總公司', '研發部'] }),
        ],
        managers: [
          manager,
          { ...manager, gdDepNo: 1, unitId: 'hq', path: ['總公司'], added: false },
        ],
      }),
    );
    expect(departments.map((item) => [item.id, item.group, item.reenables])).toEqual([
      ['4', 'Create', false],
      ['2', 'Update', true],
      ['3', 'Disable', false],
      ['1', 'Managers', false],
    ]);
    expect(departments[1].description).toBe(
      '這裡已停用，套用會重新啟用；啟用狀態依公文系統更新；王小明 設為主管並加入為成員',
    );
    expect(departments[3].description).toBe('王小明 取消主管（保留成員）');
    expect(importChoices(departments).map((group) => [group.name, group.options.length])).toEqual([
      ['新增', 1],
      ['綁定手建單位', 0],
      ['更新', 1],
      ['停用', 1],
      ['只有主管變動', 1],
    ]);
  });

  it('counts each kind of change for the summary', () => {
    const counts = importCounts(
      preview({
        units: [
          change({ action: 'Create' }),
          change({ action: 'Update' }),
          change({ action: 'Disable' }),
        ],
        managers: [
          {
            gdDepNo: 2,
            unitId: 'sales',
            path: ['總公司'],
            userId: 'bob',
            account: 'bob',
            displayName: 'Bob',
            added: false,
            deputy: false,
            joinsUnit: false,
          },
        ],
        issues: [{ gdDepNo: 5, name: null, code: 'NameMissing', gdUserId: null }],
      }),
    );
    expect(counts).toEqual({
      create: 1,
      adopt: 0,
      update: 1,
      disable: 1,
      managersAdded: 0,
      managersRemoved: 1,
      issues: 1,
    });
  });

  it('needs applying only when units or managers change', () => {
    expect(
      hasChanges(preview({ issues: [{ gdDepNo: 5, name: null, code: 'Cycle', gdUserId: null }] })),
    ).toBe(false);
    expect(hasChanges(preview({ units: [change({})] }))).toBe(true);
  });

  it('explains the changed fields and where a moved unit was', () => {
    expect(
      changeNote(change({ fields: ['Name', 'Parent'], previousPath: ['總公司', '業務部'] })),
    ).toBe('名稱、上層依公文系統更新；原為 總公司 › 業務部');
    expect(changeNote(change({ action: 'Disable', fields: ['Enabled'], enabled: false }))).toBe(
      '公文系統已沒有此部門，保留成員與歷史',
    );
    expect(changeNote(change({ action: 'Create' }))).toBe('');
    // 首長單位層級不是公文系統的欄位：搬移時清除，另外說明。
    expect(changeNote(change({ fields: ['Parent', 'HeadRank'] }))).toBe(
      '上層依公文系統更新；搬到別的上層，取消首長單位',
    );
  });

  it('summarises the applied result and says when nothing changed', () => {
    expect(
      resultSummary({
        created: 3,
        adopted: 0,
        updated: 1,
        disabled: 0,
        managersAdded: 2,
        managersRemoved: 1,
        issues: 4,
      }),
    ).toBe('已從公文系統匯入：單位新增 3、更新 1 個，主管設定 2、取消 1 位');
    expect(
      resultSummary({
        created: 0,
        adopted: 0,
        updated: 0,
        disabled: 0,
        managersAdded: 0,
        managersRemoved: 0,
        issues: 2,
      }),
    ).toBe('組織已與公文系統一致');
  });
});
