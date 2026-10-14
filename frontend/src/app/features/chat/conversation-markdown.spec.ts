import { describe, expect, it } from 'vitest';
import type { AttachmentDto } from '@core/api/schema';
import { conversationMarkdown } from './conversation-markdown';

describe('conversationMarkdown', () => {
  it('writes each turn under its speaker and lists attachments by name', () => {
    const files = [{ fileName: 'a.pdf' }, { fileName: 'b.png' }] as AttachmentDto[];
    expect(
      conversationMarkdown('週報', [
        { role: 'user', content: '整理重點', attachments: files },
        { role: 'assistant', content: '- 第一點', attachments: [] },
      ]),
    ).toBe('# 週報\n\n## 你\n\n整理重點\n\n附件：a.pdf、b.png\n\n## AI Nexus\n\n- 第一點');
  });
});
