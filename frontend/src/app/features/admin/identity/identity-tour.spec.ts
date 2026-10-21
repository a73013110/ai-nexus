import { describe, expect, it } from 'vitest';
import { tourSteps } from '@shared/ui/product-tour';
import { userInspectorTour, usersTour } from './identity-tour';

const titles = (definition: Parameters<typeof tourSteps>[0]) =>
  tourSteps(definition).map((step) => step.title);

describe('usersTour', () => {
  it('covers creating and row actions for administrators', () => {
    expect(titles(usersTour({ testing: false }))).toEqual([
      '管理身分與 AI 政策',
      '找到使用者',
      '新增使用者',
      '最近 30 天用量',
      '開啟使用者詳情',
      '測試身分與刪除',
    ]);
  });

  it('drops the disabled actions and explains read-only while testing an identity', () => {
    const steps = tourSteps(usersTour({ testing: true }));
    expect(steps.map((step) => step.title)).toEqual([
      '管理身分與 AI 政策',
      '找到使用者',
      '最近 30 天用量',
      '開啟使用者詳情',
    ]);
    expect(steps.at(-1)?.description).toContain('唯讀');
  });

  it('scopes every target to the page', () => {
    for (const step of tourSteps(usersTour({ testing: false })))
      expect(step.target).toMatch(/^nx-users-page /);
  });
});

describe('userInspectorTour', () => {
  it('introduces the summary, tabs and pager outside the conversations tab', () => {
    expect(
      titles(userInspectorTour({ audit: true, testing: false, conversations: false })),
    ).toEqual(['這位使用者的概況', '反查活動稽核', '依頁籤管理', '上一位、下一位']);
  });

  it('omits the audit lookup without the audit grant', () => {
    expect(
      titles(userInspectorTour({ audit: false, testing: false, conversations: false })),
    ).not.toContain('反查活動稽核');
    expect(
      titles(userInspectorTour({ audit: false, testing: false, conversations: true })),
    ).not.toContain('反查活動稽核');
  });

  it('switches to the audited read-only view on the conversations tab', () => {
    expect(titles(userInspectorTour({ audit: true, testing: false, conversations: true }))).toEqual(
      [
        '受稽核的唯讀檢視',
        '搜尋對話',
        '包含已刪除對話',
        '切換對話',
        '完整的對話內容',
        '反查活動稽核',
      ],
    );
  });

  it('says the tabs are read-only while testing an identity', () => {
    const tabs = tourSteps(
      userInspectorTour({ audit: false, testing: true, conversations: false }),
    ).find((step) => step.title === '依頁籤管理');
    expect(tabs?.description).toContain('唯讀');
  });

  it('scopes every target to the inspector', () => {
    for (const conversations of [false, true])
      for (const step of tourSteps(
        userInspectorTour({ audit: true, testing: false, conversations }),
      ))
        expect(step.target).toMatch(/^nx-admin-user-inspector /);
  });
});
