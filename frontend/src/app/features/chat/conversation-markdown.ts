import type { MessageDto } from '@core/api/schema';

type ExportedMessage = Pick<MessageDto, 'role' | 'content' | 'attachments'>;

/** The visible branch as a Markdown document; attachments are listed by name only. */
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
