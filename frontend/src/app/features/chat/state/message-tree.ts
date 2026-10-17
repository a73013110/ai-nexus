import type { MessageDto } from '@core/api/schema';

/** 每個歷史快照一個索引，而不是為每個可見列掃描所有訊息。 */
export class MessageTree {
  private readonly byId = new Map<string, MessageDto>();
  private readonly children = new Map<string | null, MessageDto[]>();
  constructor(messages: MessageDto[]) {
    for (const message of messages) {
      this.byId.set(message.id, message);
      const siblings = this.children.get(message.parentId) ?? [];
      siblings.push(message);
      this.children.set(message.parentId, siblings);
    }
  }
  branch(leaf: string | null | undefined): MessageDto[] {
    const result: MessageDto[] = [],
      seen = new Set<string>();
    while (leaf && !seen.has(leaf)) {
      seen.add(leaf);
      const message = this.byId.get(leaf);
      if (!message) break;
      result.push(message);
      leaf = message.parentId;
    }
    return result.reverse();
  }
  versions(message: MessageDto) {
    return (this.children.get(message.parentId) ?? []).filter((item) => item.role === message.role);
  }
  versionLeaf(message: MessageDto, direction: number): string | null {
    const siblings = this.versions(message);
    let next = siblings[siblings.findIndex((item) => item.id === message.id) + direction];
    if (!next) return null;
    const seen = new Set<string>();
    while (!seen.has(next.id)) {
      seen.add(next.id);
      const descendants = this.children.get(next.id);
      if (!descendants?.length) break;
      next = descendants[descendants.length - 1];
    }
    return next.id;
  }
}
