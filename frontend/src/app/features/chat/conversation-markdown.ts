import type { MessageDto } from '@core/api/schema';

type ExportedMessage = Pick<MessageDto, 'role' | 'content' | 'attachments'>;

/** 可見分支的 Markdown 文件；附件只列出名稱。 */
export function conversationMarkdown(title: string, messages: readonly ExportedMessage[]) {
  return [
    '# ' + title,
    ...messages.map((message) => {
      const files = message.attachments?.map((file) => file.fileName).join('、');
      return (
        `## ${message.role === 'user' ? '你' : 'AI Nexus'}\n\n${message.content}` +
        (files ? '\n\n附件：' + files : '')
      );
    }),
  ].join('\n\n');
}
