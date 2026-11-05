import { describe, expect, it, vi } from 'vitest';
import type { AssistantQuestion, ModelsDto } from '@core/api/schema';
import { publicErrorHints } from '@core/errors/public-error-catalog';
import { ApiError } from '@core/errors/safe-errors';
import { AssistantConversation, type AssistantSource } from './assistant-conversation';

const catalog = {
  models: [
    { id: 'small', reasoningEfforts: [], defaultReasoningEffort: 'auto' },
    { id: 'large', reasoningEfforts: [], defaultReasoningEffort: 'auto' },
  ],
  policy: { allowModelSelection: true, showModelNames: true, defaultModelId: 'small' },
  notice: null,
} as unknown as ModelsDto;

const sse = (...events: [string, object][]) =>
  new Response(
    events.map(([event, data]) => `event: ${event}\ndata: ${JSON.stringify(data)}\n\n`).join(''),
  );

const answer = (text: string) =>
  sse(
    [
      'grounding',
      {
        scope: '10/01–10/31 · 2 人',
        facts: [{ label: '王小明', text: '未補足 1 天。' }],
        partial: false,
      },
    ],
    ['delta', { text: text.slice(0, 2) }],
    ['delta', { text: text.slice(2) }],
    ['done', { truncated: false }],
  );

function conversation(ask: AssistantSource['ask'], context = () => 'A') {
  return new AssistantConversation({
    ask,
    context,
    models: async () => catalog,
    preferredModelId: () => 'large',
  });
}

describe('AssistantConversation', () => {
  it('streams the grounding and the answer, asking with the preferred model', async () => {
    const ask = vi.fn<AssistantSource['ask']>(async () => answer('王小明未補足 [1]。'));
    const chat = conversation(ask);

    await chat.ask('  誰還沒補足？ ');

    const [turn] = chat.exchanges();
    expect(turn).toMatchObject({
      question: '誰還沒補足？',
      answer: '王小明未補足 [1]。',
      scope: '10/01–10/31 · 2 人',
      status: 'done',
    });
    expect(turn.facts).toHaveLength(1);
    expect(ask.mock.calls[0][0]).toEqual({ text: '誰還沒補足？', modelId: 'large', history: [] });
    expect(chat.busy()).toBe(false);
  });

  it('sends only finished turns asked under the same query as history', async () => {
    let context = 'A';
    const questions: AssistantQuestion[] = [];
    const chat = conversation(
      async (question) => {
        questions.push(question);
        return answer('回答內容');
      },
      () => context,
    );

    await chat.ask('第一問');
    context = 'B';
    await chat.ask('第二問');
    await chat.ask('第三問');

    expect(questions[1].history).toEqual([]);
    expect(questions[2].history).toEqual([{ question: '第二問', answer: '回答內容' }]);
  });

  it('keeps the partial answer when stopped and shows a safe message when the request fails', async () => {
    const chat = conversation(
      (_, signal) =>
        new Promise<Response>((_resolve, reject) =>
          signal.addEventListener('abort', () => reject(new DOMException('已停止', 'AbortError'))),
        ),
    );
    const asking = chat.ask('會被停止');
    await vi.waitFor(() => expect(chat.busy()).toBe(true));
    await chat.ask('忙碌時不送');
    chat.stop();
    await asking;
    expect(chat.exchanges().map((turn) => turn.status)).toEqual(['stopped']);

    const failing = conversation(async () => {
      throw new ApiError(400, 'assistant_question_invalid');
    });
    await failing.ask('不正確的提問');
    expect(failing.exchanges()[0]).toMatchObject({
      status: 'failed',
      error: publicErrorHints['assistant_question_invalid'],
    });
  });

  it('marks a stream that ends before done as interrupted', async () => {
    const chat = conversation(async () => sse(['delta', { text: '只到一半' }]));

    await chat.ask('中斷');

    expect(chat.exchanges()[0]).toMatchObject({ answer: '只到一半', status: 'failed' });
  });
});
