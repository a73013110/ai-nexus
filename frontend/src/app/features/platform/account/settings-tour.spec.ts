import { describe, expect, it } from 'vitest';
import { tourSteps } from '@shared/ui/product-tour';
import { SETTINGS_TABS } from './settings-tabs';
import { settingsTour, type SettingsTourOptions } from './settings-tour';

const steps = (options: Partial<SettingsTourOptions>) =>
  tourSteps(settingsTour({ tab: 'appearance', saveBar: true, testing: false, ...options }));
const titles = (options: Partial<SettingsTourOptions>) => steps(options).map((step) => step.title);

describe('settingsTour', () => {
  it('opens with the categories and ends with saving on saved tabs', () => {
    expect(titles({ tab: 'appearance' })).toEqual([
      '切換設定分類',
      '主題與動態效果',
      '調整閱讀版面',
      '即時預覽',
      '預覽後再儲存',
    ]);
  });

  it('gives every tab its own steps after the shared opening', () => {
    const seen = new Set<string>();
    for (const tab of SETTINGS_TABS) {
      const [first, ...rest] = titles({ tab: tab.id, saveBar: false });
      expect(first).toBe('切換設定分類');
      expect(rest.length, tab.id).toBeGreaterThan(0);
      for (const title of rest) {
        expect(seen.has(title), title).toBe(false);
        seen.add(title);
      }
    }
  });

  it('skips the save step on read-only tabs without unsaved changes', () => {
    expect(titles({ tab: 'usage', saveBar: false })).toEqual([
      '切換設定分類',
      '今日 token 預算',
      '最近 30 天用量',
      '原檔容量',
    ]);
    expect(titles({ tab: 'shortcuts', saveBar: true }).at(-1)).toBe('預覽後再儲存');
  });

  it('explains the account tab is read-only while testing an identity', () => {
    const normal = steps({ tab: 'account', saveBar: false });
    const testing = steps({ tab: 'account', saveBar: false, testing: true });
    expect(testing.map((step) => step.title)).toEqual(normal.map((step) => step.title));
    expect(normal[1].description).toContain('驗證碼');
    expect(testing[1].description).toContain('只能查看');
    expect(testing[2].description).toContain('登入者選單');
  });

  it('scopes every target to the settings page', () => {
    for (const tab of SETTINGS_TABS)
      for (const step of steps({ tab: tab.id })) expect(step.target).toMatch(/^nx-settings-page /);
  });
});
