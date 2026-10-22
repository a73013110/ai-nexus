import { describe, expect, it } from 'vitest';
import { attachmentName } from './download';

const fallback = { title: 'ai-nexus', extension: 'xlsx' };
const response = (disposition?: string) =>
  new Response('', disposition ? { headers: { 'Content-Disposition': disposition } } : {});

describe('attachmentName', () => {
  it('prefers the UTF-8 filename* so Chinese names survive', () => {
    expect(
      attachmentName(
        response(
          `attachment; filename="_____20261001-20261031.xlsx"; filename*=UTF-8''${encodeURIComponent('出勤紀錄_20261001-20261031.xlsx')}`,
        ),
        fallback,
      ),
    ).toEqual({ title: '出勤紀錄_20261001-20261031', extension: 'xlsx' });
  });

  it('falls back to the plain filename, then to the given name', () => {
    expect(attachmentName(response('attachment; filename=ai-nexus-audit.csv'), fallback)).toEqual({
      title: 'ai-nexus-audit',
      extension: 'csv',
    });
    // 編碼壞掉的 filename* 不拋例外，改用預設名稱。
    expect(attachmentName(response(`attachment; filename*=UTF-8''%E0%A4%A`), fallback)).toBe(
      fallback,
    );
    expect(attachmentName(response(), fallback)).toBe(fallback);
  });
});
