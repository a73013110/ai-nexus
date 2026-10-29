import { describe, expect, it } from 'vitest';
import type { ReleaseStatusDto } from '@core/api/schema';
import { changeTone, compatibility } from './releases-format';

const status = (patch: Partial<ReleaseStatusDto>): ReleaseStatusDto => ({
  appVersion: '2026.11.4',
  appRevision: null,
  appBuiltAt: null,
  databaseVersion: '2026.11.3',
  databaseDeployedAt: null,
  minimumDatabaseVersion: '2026.11.3',
  compatibility: 'compatible',
  hasSnapshot: true,
  ...patch,
});

describe('compatibility', () => {
  it('names the versions when the database is behind the program', () => {
    expect(
      compatibility(status({ compatibility: 'incompatible', databaseVersion: '2026.11.2' })),
    ).toEqual({
      label: '不相容',
      tone: 'danger',
      hint: '資料庫 v2026.11.2 低於程式需要的 v2026.11.3',
    });
  });

  it('treats a database without any recorded version as incompatible', () => {
    expect(
      compatibility(status({ compatibility: 'incompatible', databaseVersion: null })).hint,
    ).toBe('資料庫沒有版本紀錄');
  });

  it('does not judge development builds', () => {
    expect(compatibility(status({ compatibility: 'unchecked' })).tone).toBe('neutral');
  });
});

describe('changeTone', () => {
  it('ranks a removed object above a modified one', () => {
    expect(['removed', 'modified', 'added'].map(changeTone)).toEqual(['danger', 'warning', 'info']);
  });
});
