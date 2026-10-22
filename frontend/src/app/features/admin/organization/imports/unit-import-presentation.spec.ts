import { describe, expect, it } from 'vitest';
import type { UnitImportChangeDto, UnitImportPreviewDto } from '@core/api/schema';
import { changeNote, hasChanges, importCounts, resultSummary } from './unit-import-presentation';

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

describe('unit import presentation', () => {
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
