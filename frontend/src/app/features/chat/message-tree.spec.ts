import { describe, expect, it } from 'vitest';
import type { MessageDto } from '../../core/api/schema';
import { MessageTree } from './message-tree';

const message = (id: string, parentId: string | null, role = 'assistant'): MessageDto => ({
  id,
  parentId,
  role,
  content: id,
  status: 'completed',
  createdAt: '2026-10-04T00:00:00Z',
  runId: null,
  modelId: null,
  attachments: [],
  errorCode: null,
  sources: null,
  feedbackRating: 0,
  charge: null,
  webSources: null,
  webSearchCharge: null,
  timing: null,
  modelDisplayName: null,
  issueCode: null,
});

describe('Conversation branch index', () => {
  it('returns only the chosen branch in reading order', () => {
    const tree = new MessageTree([
      message('question', null, 'user'),
      message('first', 'question'),
      message('revised', 'question'),
      message('follow-up', 'revised', 'user'),
      message('latest', 'follow-up'),
    ]);
    expect(tree.branch('latest').map((x) => x.id)).toEqual([
      'question',
      'revised',
      'follow-up',
      'latest',
    ]);
    expect(tree.branch('first').map((x) => x.id)).toEqual(['question', 'first']);
    expect(tree.branch(null)).toEqual([]);
  });

  it('switches a version to that branch’s latest descendant', () => {
    const first = message('first', 'question');
    const revised = message('revised', 'question');
    const tree = new MessageTree([
      message('question', null, 'user'),
      first,
      revised,
      message('next-question', 'revised', 'user'),
      message('next-answer', 'next-question'),
    ]);
    expect(tree.versions(first).map((x) => x.id)).toEqual(['first', 'revised']);
    expect(tree.versionLeaf(first, 1)).toBe('next-answer');
    expect(tree.versionLeaf(revised, -1)).toBe('first');
    expect(tree.versionLeaf(first, -1)).toBeNull();
  });

  it('does not loop when history has a missing parent or cycle', () => {
    const tree = new MessageTree([
      message('a', 'b'),
      message('b', 'a', 'user'),
      message('orphan', 'missing'),
    ]);
    expect(tree.branch('a').map((x) => x.id)).toEqual(['b', 'a']);
    expect(tree.branch('orphan').map((x) => x.id)).toEqual(['orphan']);
    expect(tree.branch('missing')).toEqual([]);
  });
});
